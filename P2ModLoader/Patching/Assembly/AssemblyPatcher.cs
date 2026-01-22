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
using P2ModLoader.Patching.Assembly.Rewriters;
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
            var modTree = CSharpSyntaxTree.ParseText(updatedSource, new CSharpParseOptions(LanguageVersion.Latest));

            var modRoot = modTree.GetRoot();
            var classes = modRoot.DescendantNodes().OfType<ClassDeclarationSyntax>().ToList();
            var enums = modRoot.DescendantNodes().OfType<EnumDeclarationSyntax>().ToList();
            var interfaces = modRoot.DescendantNodes().OfType<InterfaceDeclarationSyntax>().ToList();
            
            var @namespace = modRoot.DescendantNodes().OfType<NamespaceDeclarationSyntax>().FirstOrDefault();
            var namespaceName = @namespace?.Name.ToString() ?? string.Empty;
            
            var hasClass = classes.Count != 0;
            var hasEnum = enums.Count != 0;
            var hasInterface = interfaces.Count != 0;

            if (!hasClass && !hasEnum && !hasInterface) {
                ErrorHandler.Handle($"No classes, enums or interfaces found in the source file {updatedSourcePath}", null);
                return false;
            }

            if (classes.Concat<MemberDeclarationSyntax>(enums).Concat(interfaces).Count() > 1) {
                ErrorHandler.Handle($"The file {updatedSourcePath} contains multiple member definitions.", null);
                return false;
            }
            
            var methods = modRoot.DescendantNodes().OfType<MethodDeclarationSyntax>().ToList();
            var properties = modRoot.DescendantNodes().OfType<PropertyDeclarationSyntax>().ToList();
            var fields = modRoot.DescendantNodes().OfType<FieldDeclarationSyntax>().ToList();
            var events = modRoot.DescendantNodes().OfType<EventDeclarationSyntax>().ToList();
            var eventFields = modRoot.DescendantNodes().OfType<EventFieldDeclarationSyntax>().ToList();

            var members = methods.Concat<MemberDeclarationSyntax>(properties).Concat(fields).Concat(events).Concat(eventFields).ToList();

            var decompiler = new CSharpDecompiler(dllPath, new DecompilerSettings { ShowXmlDocumentation = false });

            using var resolver = new DefaultAssemblyResolver();
            resolver.AddSearchDirectory(dllDirectory);

            var readerParams = new ReaderParameters { AssemblyResolver = resolver, ReadWrite = true };
            
            File.Copy(dllPath, backupPath, true);

            using var originalAssembly = AssemblyDefinition.ReadAssembly(backupPath, readerParams);

            if (hasEnum) {
                var enumDecl = enums.First();
                var fullTypeName = string.IsNullOrEmpty(namespaceName)
                    ? enumDecl.Identifier.Text
                    : $"{namespaceName}.{enumDecl.Identifier.Text}";

                var originalType = originalAssembly.MainModule.GetType(fullTypeName);
                if (originalType == null) {
                    if (!TryAddNewType(references, @namespace, enumDecl, originalAssembly, readerParams))
                        return false;

                    Logger.Log(LogLevel.Info, $"Added new enum {fullTypeName}.");
                } else {
                    if (!EnumPatcher.UpdateEnum(originalType, enumDecl, originalAssembly))
                        return false;

                    Logger.Log(LogLevel.Info, $"Updated enum {fullTypeName} with new members.");
                }
            }

            if (hasInterface) {
                var interfaceDecl  = interfaces.First();
                var fullTypeName = string.IsNullOrEmpty(namespaceName) ? interfaceDecl.Identifier.Text
                    : $"{namespaceName}.{interfaceDecl.Identifier.Text}";

                var originalType = originalAssembly.MainModule.GetType(fullTypeName);
                if (originalType == null) {
                    Logger.Log(LogLevel.Info, $"Adding new interface {fullTypeName}.");
                    if (!TryAddNewType(references, @namespace, interfaceDecl, originalAssembly, readerParams))
                        return false;
                } else {
                    Logger.Log(LogLevel.Error, $"Interface {fullTypeName} already exists; can't patch existing interfaces.");
                }
            }
            
            if (hasClass) {
                var classDecl = classes.First();

                var baseName = classDecl.Identifier.Text;
                var arity = classDecl.TypeParameterList?.Parameters.Count ?? 0;
                var fullType = arity > 0 ? $"{baseName}`{arity}" : baseName;
                var fullTypeName = string.IsNullOrEmpty(namespaceName) ? fullType : $"{namespaceName}.{fullType}";

                var originalType = originalAssembly.MainModule.GetType(fullTypeName);

                if (originalType == null) {
                    Logger.Log(LogLevel.Info, $"Adding new type {fullTypeName}.");
                    if (!TryAddNewType(references, @namespace, classDecl, originalAssembly, readerParams))
                        return false;
                } else {
                    if (members.Count != 0) {
                         	
                        string decompiledSource;
                        try {
                            decompiledSource = decompiler.DecompileTypeAsString(new FullTypeName(fullTypeName));
                        } catch (Exception ex) {
                            ErrorHandler.Handle($"Failed to decompile type {fullTypeName}", ex);
                            return false;
                        }
                        
                        Logger.Log(LogLevel.Info, $"Updating class {fullTypeName} with new/changed members.");
                        if (!TryUpdateClassTypeMembers(originalAssembly, fullTypeName, @namespace, classDecl, methods,
                                properties, fields, events, eventFields, modRoot, references, readerParams, decompiledSource))
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
        AssemblyDefinition originalAssembly,
        string fullTypeName,
        NamespaceDeclarationSyntax? @namespace,
        ClassDeclarationSyntax @class,
        List<MethodDeclarationSyntax> methods,
        List<PropertyDeclarationSyntax> properties,
        List<FieldDeclarationSyntax> fields,
        List<EventDeclarationSyntax> events,
        List<EventFieldDeclarationSyntax> eventFields,
        SyntaxNode updatedRoot,
        List<MetadataReference> references,
        ReaderParameters readerParams,
        string decompiledSource) {

        var decompTree = CSharpSyntaxTree.ParseText(decompiledSource);

        if (decompTree.GetRoot() is not CompilationUnitSyntax decompRoot) {
            ErrorHandler.Handle("Failed to parse decompiled source.", null);
            return false;
        }

        var decompClass = FindTypeDeclaration<ClassDeclarationSyntax>(decompRoot, @class.Identifier.Text);

        if (decompClass == null) {
            ErrorHandler.Handle($"Failed to find class {@class.Identifier.Text} in source.", null);
            return false;
        }

        var methodReplacements = methods.Select(m => new MethodReplacement(
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
        var modifiedClass = (ClassDeclarationSyntax?)methodRewriter.Visit(decompClass) ?? decompClass;

        PropertyReplacer propRewriter = new(properties.Select(p => new PropertyReplacement(p.Identifier.Text, p)).ToList());
        modifiedClass = (ClassDeclarationSyntax)propRewriter.Visit(modifiedClass);

        var existingPropNames = decompClass.Members.OfType<PropertyDeclarationSyntax>().Select(p => p.Identifier.Text).ToHashSet();
        var newProps = properties.Where(p => !existingPropNames.Contains(p.Identifier.Text)).ToArray();

        var existingFieldNames = decompClass.Members.OfType<FieldDeclarationSyntax>().SelectMany(f => f.Declaration.Variables).Select(v => v.Identifier.Text).ToHashSet();
        var newFields = fields.Where(f => f.Declaration.Variables.Any(v => !existingFieldNames.Contains(v.Identifier.Text))).ToArray();

        EventReplacer evtRewriter = new(events.Select(e => new EventReplacement(e.Identifier.Text, e)).ToList());
        modifiedClass = (ClassDeclarationSyntax)evtRewriter.Visit(modifiedClass);

        var existingEventNames = decompClass.Members.OfType<EventDeclarationSyntax>().Select(e => e.Identifier.Text).ToHashSet();
        var newEvents = events.Where(e => !existingEventNames.Contains(e.Identifier.Text)).ToArray();
        

        var existingEventFieldNames = decompClass.Members.OfType<EventFieldDeclarationSyntax>()
            .SelectMany(fld => fld.Declaration.Variables).Select(v => v.Identifier.Text).ToHashSet();

        var newFieldEvents = eventFields.SelectMany(fld => fld.Declaration.Variables.Select(v => (fld, v)))
            .Where(pair => !existingEventFieldNames.Contains(pair.v.Identifier.Text))
            .Select(pair => SyntaxFactory.EventFieldDeclaration(
                    SyntaxFactory.VariableDeclaration(pair.fld.Declaration.Type)
                        .WithVariables(SyntaxFactory.SingletonSeparatedList(pair.v)))
                .WithModifiers(pair.fld.Modifiers)).ToArray();
        
        if (newProps.Length > 0) modifiedClass = modifiedClass.AddMembers(newProps);
        if (newFields.Length > 0) modifiedClass = modifiedClass.AddMembers(newFields);
        if (newEvents.Length > 0) modifiedClass = modifiedClass.AddMembers(newEvents);
        if (newFieldEvents.Length > 0) modifiedClass = modifiedClass.AddMembers(newFieldEvents);
        
        CompilationUnitSyntax mergedRoot;
        if (@namespace != null) {
            var mergedNs = SyntaxFactory.NamespaceDeclaration(@namespace.Name)
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


        foreach (var methodSyntax in methods) {
            string methodName;
            bool isExplicitImpl = methodSyntax.ExplicitInterfaceSpecifier != null;

            if (isExplicitImpl) {
                var interfaceName = methodSyntax.ExplicitInterfaceSpecifier.Name.ToString();
                var simpleName = methodSyntax.Identifier.Text;

                var paramTypes = methodSyntax.ParameterList.Parameters
                    .Select(p => p.Type.ToString())
                    .ToList();

                var newMethod = newType.Methods.FirstOrDefault(m =>
                    m.Name.EndsWith($".{simpleName}") &&
                    m.Parameters.Count == paramTypes.Count &&
                    m.Parameters.Select(p => p.ParameterType.Name).SequenceEqual(
                        paramTypes.Select(pt => pt.Split('.').Last())
                    )
                );

                var originalMethod = originalType.Methods.FirstOrDefault(m =>
                    m.Name.EndsWith($".{simpleName}") &&
                    m.Parameters.Count == paramTypes.Count &&
                    m.Parameters.Select(p => p.ParameterType.Name).SequenceEqual(
                        paramTypes.Select(pt => pt.Split('.').Last())
                    )
                );

                if (newMethod == null) {
                    Logger.Log(LogLevel.Warning,
                        $"Could not find explicit method {interfaceName}.{simpleName} in the compiled assembly");
                    continue;
                }

                if (originalMethod == null) {
                    originalType.Methods.Add(MethodCloner.CloneMethod(newMethod, originalAssembly.MainModule));
                    Logger.Log(LogLevel.Info, $"Added new method {newMethod.Name} to type {fullTypeName}");
                } else {
                    MethodCloner.ReplaceMethodBody(originalMethod, newMethod, originalAssembly.MainModule);
                    Logger.Log(LogLevel.Info, $"Replaced method {originalMethod.Name} in type {fullTypeName}");
                }
            } else {
                methodName = methodSyntax.Identifier.Text;
                var newMethod = newType.Methods.FirstOrDefault(m => m.Name == methodName);
                var originalMethod = originalType.Methods.FirstOrDefault(m => m.Name == methodName);

                if (newMethod == null) {
                    Logger.Log(LogLevel.Warning, $"Could not find method {methodName} in the compiled assembly");
                    continue;
                }

                if (originalMethod == null) {
                    originalType.Methods.Add(MethodCloner.CloneMethod(newMethod, originalAssembly.MainModule));
                    Logger.Log(LogLevel.Info, $"Added new method {methodName} to type {fullTypeName}");
                } else {
                    MethodCloner.ReplaceMethodBody(originalMethod, newMethod, originalAssembly.MainModule);
                    Logger.Log(LogLevel.Info, $"Replaced method {methodName} in type {fullTypeName}");
                }
            }
        }


        foreach (var variable in fields.SelectMany(field => field.Declaration.Variables)) {
            var fieldName = variable.Identifier.Text;
            if (originalType.Fields.Any(f => f.Name == fieldName)) continue;
            originalType.Fields.Add(FieldCloner.CloneField(newType.Fields.First(f => f.Name == fieldName), originalAssembly.MainModule));
            Logger.Log(LogLevel.Info, $"Added new field {fieldName}");
        }

        foreach (var propDecl in properties) {
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

        foreach (var @event in events) {
            var name = @event.Identifier.Text;
            var newEvent = newType.Events.First(e => e.Name == name);
            var oldEvent = originalType.Events.FirstOrDefault(e => e.Name == name);

            if (oldEvent != null) {
                EventCloner.UpdateEvent(oldEvent, newEvent, originalAssembly.MainModule, originalType);
                Logger.Log(LogLevel.Info, $"Updated event {name}");
            } else {
                var clone = EventCloner.CloneEvent(newEvent, originalAssembly.MainModule, originalType);
                originalType.Events.Add(clone);
                Logger.Log(LogLevel.Info, $"Added new event {name}");
            }
        }
        
        foreach (var name in eventFields.SelectMany(fld => fld.Declaration.Variables.Select(v => v.Identifier.Text))) {
            var newEv = newType.Events.First(e => e.Name == name);
            var oldEv = originalType.Events.FirstOrDefault(e => e.Name == name);

            if (oldEv != null) {
                EventCloner.UpdateEvent(oldEv, newEv, originalAssembly.MainModule, originalType);
                Logger.Log(LogLevel.Info, $"Updated event {name}");
            } else {
                var clone = EventCloner.CloneEvent(newEv, originalAssembly.MainModule, originalType);
                originalType.Events.Add(clone);
                Logger.Log(LogLevel.Info, $"Added new event {name}");
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