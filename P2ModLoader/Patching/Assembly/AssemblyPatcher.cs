using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.TypeSystem;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Emit;
using Mono.Cecil;
using P2ModLoader.Helper;
using P2ModLoader.Logging;
using P2ModLoader.Patching.Assembly.ILCloning;
using LanguageVersion = Microsoft.CodeAnalysis.CSharp.LanguageVersion;

namespace P2ModLoader.Patching.Assembly;

public static class AssemblyPatcher {
    public static bool PatchAssembly(string dllPath, string updatedSourcePath) { 	
        var dllDirectory = Path.GetDirectoryName(Path.GetFullPath(dllPath))!;
        var fileCopy = Path.Combine(dllDirectory, $"{Path.GetFileNameWithoutExtension(dllPath)}Temp.dll");
        File.Copy(dllPath, fileCopy, true);
        var backupPath = dllPath + ".backup";
        
        try {
            var references = ReferenceCollector.CollectReferences(dllDirectory!, dllPath);

            var updatedSource = File.ReadAllText(updatedSourcePath);
            var updatedTree = CSharpSyntaxTree.ParseText(updatedSource, new CSharpParseOptions(LanguageVersion.Latest));

            var updatedRoot = updatedTree.GetRoot();
            var classDeclarations = updatedRoot.DescendantNodes().OfType<ClassDeclarationSyntax>().ToList();
            var enumDeclarations = updatedRoot.DescendantNodes().OfType<EnumDeclarationSyntax>().ToList();
            var interfaceDeclarations = updatedRoot.DescendantNodes().OfType<InterfaceDeclarationSyntax>().ToList();
            
            var namespaceDecl = updatedRoot.DescendantNodes().OfType<NamespaceDeclarationSyntax>().FirstOrDefault();
            var namespaceName = namespaceDecl?.Name.ToString() ?? string.Empty;
            
            var hasClass = classDeclarations.Count != 0;
            var hasEnum = enumDeclarations.Count != 0;
            var hasInterface = interfaceDeclarations.Count != 0;

            if (!hasClass && !hasEnum && !hasInterface) {
                ErrorHandler.Handle($"No classes, enums or interfaces found in the source file {updatedSourcePath}", null);
                return false;
            }

            if (classDeclarations.Concat<MemberDeclarationSyntax>(enumDeclarations).Concat(interfaceDeclarations).Count() > 1) {
                ErrorHandler.Handle($"The file {updatedSourcePath} contains multiple member definitions.", null);
                return false;
            }
            
            var methods = updatedRoot.DescendantNodes().OfType<MethodDeclarationSyntax>().ToList();
            var properties = updatedRoot.DescendantNodes().OfType<PropertyDeclarationSyntax>().ToList();
            var fields = updatedRoot.DescendantNodes().OfType<FieldDeclarationSyntax>().ToList();
            var members = methods.Concat<MemberDeclarationSyntax>(properties).Concat(fields).ToList();

            var decompiler = new CSharpDecompiler(dllPath, new DecompilerSettings {
                ShowXmlDocumentation = false,
                RemoveDeadCode = false,
                RemoveDeadStores = false
            });

            var resolver = new DefaultAssemblyResolver();
            resolver.AddSearchDirectory(dllDirectory);

            var readerParams = new ReaderParameters { AssemblyResolver = resolver, ReadWrite = true };
            
            File.Copy(dllPath, backupPath, true);

            using var originalAssembly = AssemblyDefinition.ReadAssembly(backupPath, readerParams);

            if (hasEnum) {
                var enumDecl = enumDeclarations.First();
                var fullTypeName = string.IsNullOrEmpty(namespaceName)
                    ? enumDecl.Identifier.Text
                    : $"{namespaceName}.{enumDecl.Identifier.Text}";

                var originalType = originalAssembly.MainModule.GetType(fullTypeName);
                if (originalType == null) {
                    if (!TryAddNewType(references, namespaceDecl, enumDecl, originalAssembly, readerParams))
                        return false;

                    Logger.Log(LogLevel.Info, $"Added new enum {fullTypeName}.");
                } else {
                    if (!EnumPatcher.UpdateEnum(originalType, enumDecl, originalAssembly))
                        return false;

                    Logger.Log(LogLevel.Info, $"Updated enum {fullTypeName} with new members.");
                }
            }

            if (hasInterface) {
                var interfaceDecl  = interfaceDeclarations.First();
                var fullTypeName = string.IsNullOrEmpty(namespaceName) ? interfaceDecl.Identifier.Text
                    : $"{namespaceName}.{interfaceDecl.Identifier.Text}";

                var originalType = originalAssembly.MainModule.GetType(fullTypeName);
                if (originalType == null) {
                    Logger.Log(LogLevel.Info, $"Adding new interface {fullTypeName}.");
                    if (!TryAddNewType(references, namespaceDecl, interfaceDecl, originalAssembly, readerParams))
                        return false;
                } else {
                    Logger.Log(LogLevel.Error, $"Interface {fullTypeName} already exists; can't patch existing interfaces.");
                }
            }
            
            if (hasClass) {
                var classDecl = classDeclarations.First();

                var baseName = classDecl.Identifier.Text;
                var arity = classDecl.TypeParameterList?.Parameters.Count ?? 0;
                var fullType = arity > 0 ? $"{baseName}`{arity}" : baseName;
                var fullTypeName = string.IsNullOrEmpty(namespaceName) ? fullType : $"{namespaceName}.{fullType}";

                var originalType = originalAssembly.MainModule.GetType(fullTypeName);

                if (originalType == null) {
                    Logger.Log(LogLevel.Info, $"Adding new type {fullTypeName}.");
                    if (!TryAddNewType(references, namespaceDecl, classDecl, originalAssembly, readerParams))
                        return false;
                } else {
                    if (members.Count != 0) {
                        Logger.Log(LogLevel.Info, $"Updating class {fullTypeName} with new/changed members.");
                        if (!TryUpdateClassTypeMembers(decompiler, originalAssembly, fullTypeName, namespaceDecl,
                                classDecl, methods, properties, fields, updatedRoot, references, readerParams))
                            return false;
                    } else {
                        Logger.Log(LogLevel.Info, $"Class {fullTypeName} found but no members to add/replace.");
                    }
                }
            }

            originalAssembly.Write(dllPath);
            Logger.Log(LogLevel.Info, $"Successfully patched assembly at {dllPath}");
            return true;
        } catch (Exception ex) {
            ErrorHandler.Handle("Error patching assembly", ex);
            return false;
        } finally {
            File.Delete(fileCopy);
            File.Delete(backupPath);
        }
    }

    private static bool TryAddNewType(List<MetadataReference> references, NamespaceDeclarationSyntax? namespaceDecl,
        MemberDeclarationSyntax typeDecl, AssemblyDefinition originalAssembly, ReaderParameters readerParams) { 	
        var compilationUnit = SyntaxFactory.CompilationUnit();
        var updatedRoot = typeDecl.SyntaxTree.GetRoot();
        var usings = ReferenceCollector.CollectAllUsings(updatedRoot);
        compilationUnit = compilationUnit.WithUsings(usings);

        if (namespaceDecl != null) {
            MemberDeclarationSyntax namespaceSyntax = SyntaxFactory.NamespaceDeclaration(namespaceDecl.Name)
                .WithMembers(SyntaxFactory.SingletonList(typeDecl));
            compilationUnit = compilationUnit.WithMembers(SyntaxFactory.SingletonList(namespaceSyntax));
        } else {
            compilationUnit = compilationUnit.WithMembers(SyntaxFactory.SingletonList(typeDecl));
        }

        var syntaxTree = compilationUnit.SyntaxTree;

        var tempAsmName = "WorkingCopy";
        var compilation = CSharpCompilation.Create(
            tempAsmName,
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithOptimizationLevel(OptimizationLevel.Release)
                .WithPlatform(Platform.AnyCpu).WithAllowUnsafe(true)
        );

        using var ms = new MemoryStream();
        var result = compilation.Emit(ms);

        if (!result.Success) {
            PrintCompilationFailure(result, syntaxTree);
            return false;
        }

        ms.Seek(0, SeekOrigin.Begin);

        using var newAssembly = AssemblyDefinition.ReadAssembly(new MemoryStream(ms.ToArray()), readerParams);

        var fullTypeName = GetFullTypeName(namespaceDecl, typeDecl);

        var newType = newAssembly.MainModule.GetType(fullTypeName);
        if (newType == null) {
            ErrorHandler.Handle($"Could not find type {fullTypeName} in the compiled assembly", null);
            return false;
        }

        var importedType = TypeCloner.CloneType(newType, originalAssembly.MainModule);
        originalAssembly.MainModule.Types.Add(importedType);
        var orig = originalAssembly.MainModule.GetType(fullTypeName);
        foreach (var clone in newType.NestedTypes.Select(n => TypeCloner.CloneType(n, originalAssembly.MainModule))) {
            orig.NestedTypes.Add(clone);
            PostPatchReferenceFixer.FixReferencesForPatchedType(clone, tempAsmName, originalAssembly.MainModule);
        }
        PostPatchReferenceFixer.FixReferencesForPatchedType(importedType, tempAsmName, originalAssembly.MainModule);
        return true;
    }

    private static string GetFullTypeName(NamespaceDeclarationSyntax? nsDecl, MemberDeclarationSyntax typeDecl) { 	
        var namespaceName = nsDecl?.Name.ToString() ?? "";
        var typeName = typeDecl switch {
            ClassDeclarationSyntax c => c.Identifier.Text,
            EnumDeclarationSyntax e => e.Identifier.Text,
            InterfaceDeclarationSyntax i => i.Identifier.Text,
            _ => ""
        };

        return string.IsNullOrEmpty(namespaceName) ? typeName : $"{namespaceName}.{typeName}";
    }

    private static bool TryUpdateClassTypeMembers(
        CSharpDecompiler decompiler,
        AssemblyDefinition originalAssembly,
        string fullTypeName,
        NamespaceDeclarationSyntax? namespaceDecl,
        ClassDeclarationSyntax classDecl,
        List<MethodDeclarationSyntax> methodGroup,
        List<PropertyDeclarationSyntax> propertyGroup,
        List<FieldDeclarationSyntax> fieldGroup,
        SyntaxNode updatedRoot,
        List<MetadataReference> references,
        ReaderParameters readerParams) { 	
        string decompiledSource;
        try {
            decompiledSource = decompiler.DecompileTypeAsString(new FullTypeName(fullTypeName));
        } catch (Exception ex) {
            ErrorHandler.Handle($"Failed to decompile type {fullTypeName}", ex);
            return false;
        }

        var decompTree = CSharpSyntaxTree.ParseText(decompiledSource);

        if (decompTree.GetRoot() is not CompilationUnitSyntax decompRoot) {
            ErrorHandler.Handle("Failed to parse decompiled source.", null);
            return false;
        }

        var decompClass = FindTypeDeclaration<ClassDeclarationSyntax>(decompRoot, classDecl.Identifier.Text);

        if (decompClass == null) {
            ErrorHandler.Handle($"Failed to find class {classDecl.Identifier.Text} in source.", null);
            return false;
        }

        var methodReplacements = methodGroup.Select(m => new MethodReplacement(
            m.Identifier.Text,
            m.ParameterList.Parameters.Select(p => p.Type.ToString()).ToList(),
            m
        )).ToList();


        var duplicateMethods = methodReplacements.GroupBy(m => new { m.Name, Types = string.Join(",", m.Types) })
            .Where(g => g.Count() > 1).Select(g => g.Key).ToList();

        if (duplicateMethods.Count != 0) {
            foreach (var dup in duplicateMethods) {
                ErrorHandler.Handle(
                    $"Duplicate method definition detected in updated source: {dup.Name} with parameters ({dup.Types})",
                    null);
            }

            return false;
        }

        var methodRewriter = new MethodReplacer(methodReplacements);
        var modifiedClass = (ClassDeclarationSyntax?)methodRewriter.Visit(decompClass)
                            ?? decompClass;

        var existingPropNames = decompClass.Members.OfType<PropertyDeclarationSyntax>().Select(p => p.Identifier.Text).ToHashSet();
        var newProps = propertyGroup.Where(p => !existingPropNames.Contains(p.Identifier.Text)).ToArray();

        var existingFieldNames = decompClass.Members.OfType<FieldDeclarationSyntax>().SelectMany(f => f.Declaration.Variables).Select(v => v.Identifier.Text).ToHashSet();
        var newFields = fieldGroup.Where(f => f.Declaration.Variables.Any(v => !existingFieldNames.Contains(v.Identifier.Text))).ToArray();

        if (newProps.Length > 0)
            modifiedClass = modifiedClass.AddMembers(newProps);
        if (newFields.Length > 0)
            modifiedClass = modifiedClass.AddMembers(newFields);

        CompilationUnitSyntax mergedRoot;
        if (namespaceDecl != null) {
            var mergedNs = SyntaxFactory.NamespaceDeclaration(namespaceDecl.Name)
                .WithMembers(SyntaxFactory.SingletonList<MemberDeclarationSyntax>(modifiedClass));
            mergedRoot = SyntaxFactory.CompilationUnit()
                .WithUsings(ReferenceCollector.MergeUsings(decompRoot, updatedRoot))
                .WithMembers(SyntaxFactory.SingletonList<MemberDeclarationSyntax>(mergedNs));
        } else {
            mergedRoot = SyntaxFactory.CompilationUnit()
                .WithUsings(ReferenceCollector.MergeUsings(decompRoot, updatedRoot))
                .WithMembers(SyntaxFactory.SingletonList<MemberDeclarationSyntax>(modifiedClass));
        }

        var mergedSource = mergedRoot.NormalizeWhitespace().ToFullString();
        var mergedTree = CSharpSyntaxTree.ParseText(mergedSource);

        var tempAsmName = Path.GetRandomFileName();
        var compilation = CSharpCompilation.Create(
            tempAsmName,
            [mergedTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithOptimizationLevel(OptimizationLevel.Release)
                .WithPlatform(Platform.AnyCpu).WithAllowUnsafe(true)
        );

        using var ms = new MemoryStream();
        var result = compilation.Emit(ms);

        if (!result.Success) {
            PrintCompilationFailure(result, mergedTree);
            return false;
        }

        ms.Seek(0, SeekOrigin.Begin);

        using var newAssembly = AssemblyDefinition.ReadAssembly(new MemoryStream(ms.ToArray()), readerParams);
        var newType = newAssembly.MainModule.GetType(fullTypeName);

        if (newType == null) {
            ErrorHandler.Handle($"Could not find type {fullTypeName} in the compiled assembly", null);
            return false;
        }

        var originalType = originalAssembly.MainModule.GetType(fullTypeName);
        if (originalType == null) {
            ErrorHandler.Handle($"Original type {fullTypeName} not found.", null);
            return false;
        }
        
        foreach (var clone in newType.NestedTypes.Select(n => TypeCloner.CloneType(n, originalAssembly.MainModule))) {
            originalType.NestedTypes.Add(clone);
            PostPatchReferenceFixer.FixReferencesForPatchedType(clone, tempAsmName, originalAssembly.MainModule);
        }

        foreach (var methodName in methodGroup.Select(m => m.Identifier.Text)) {
            var newMethod = newType.Methods.FirstOrDefault(m => m.Name == methodName);
            var originalMethod = originalType.Methods.FirstOrDefault(m => m.Name == methodName);

            if (newMethod == null) {
                Logger.Log(LogLevel.Warning, $"Could not find method {methodName} in the compiled assembly");
                continue;
            }

            if (originalMethod == null) {
                Logger.Log(LogLevel.Info, $"Adding new method {methodName} to type {fullTypeName}");
                var importedMethod = MethodCloner.CloneMethod(newMethod, originalAssembly.MainModule);
                originalType.Methods.Add(importedMethod);
                Logger.Log(LogLevel.Info, $"Added new method {methodName}");
            } else {
                MethodCloner.ReplaceMethodBody(originalMethod, newMethod, originalAssembly.MainModule);
                Logger.Log(LogLevel.Info, $"Replaced method {methodName} in type {fullTypeName}");
            }
        }


        foreach (var fieldDecl in fieldGroup) {
            foreach (var variable in fieldDecl.Declaration.Variables) {
                var fieldName = variable.Identifier.Text;
                if (originalType.Fields.Any(f => f.Name == fieldName)) continue;
                originalType.Fields.Add(FieldCloner.CloneField(newType.Fields.First(f => f.Name == fieldName), originalAssembly.MainModule));
                Logger.Log(LogLevel.Info, $"Added new field {fieldName}");
            }
        }

        foreach (var propDecl in propertyGroup) {
            var propName = propDecl.Identifier.Text;
            var newPropDef = newType.Properties.First(p => p.Name == propName);
            var existingPropDef = originalType.Properties.FirstOrDefault(p => p.Name == propName);

            if (existingPropDef != null) {
                PropertyCloner.UpdateProperty(existingPropDef, newPropDef, originalAssembly.MainModule, originalType);
                Logger.Log(LogLevel.Info, $"Updated property {propName}");
            } else {
                originalType.Properties.Add(PropertyCloner.CloneProperty(newPropDef, originalAssembly.MainModule, originalType));
                Logger.Log(LogLevel.Info, $"Added new property {propName}");
            }
        }

        PostPatchReferenceFixer.FixReferencesForPatchedType(originalType, tempAsmName, originalAssembly.MainModule);
        return true;
    }

    private static T? FindTypeDeclaration<T>(SyntaxNode node, string name) where T : TypeDeclarationSyntax { 	
        foreach (var child in node.ChildNodes()) {
            if (child is T typed && typed.Identifier.Text == name)
                return typed;

            var result = FindTypeDeclaration<T>(child, name);
            if (result != null)
                return result;
        }

        return null;
    }

    private static void PrintCompilationFailure(EmitResult result, SyntaxTree tree) { 	
        Logger.Log(LogLevel.Error, $"Compilation failed!");
        foreach (var diagnostic in result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)) {
            var lineSpan = diagnostic.Location.GetLineSpan();
            var sourceText = tree.GetText();
            var errorLine = lineSpan.StartLinePosition.Line < sourceText.Lines.Count
                ? sourceText.Lines[lineSpan.StartLinePosition.Line].ToString()
                : "<unknown>";

            Logger.Log(LogLevel.Info, $"{diagnostic.Id}: {diagnostic.GetMessage()}");
            Logger.Log(LogLevel.Info, $"Location: {lineSpan}");
            Logger.Log(LogLevel.Info, $"Source line: {errorLine}");
            Logger.LogLineBreak(LogLevel.Info);
        }
    }
}