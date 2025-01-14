using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.TypeSystem;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Emit;
using Mono.Cecil;
using Mono.Cecil.Cil;
using P2ModLoader.Helper;
using LanguageVersion = Microsoft.CodeAnalysis.CSharp.LanguageVersion;
using MethodBody = Mono.Cecil.Cil.MethodBody;

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
            var methodDeclarations = updatedRoot.DescendantNodes().OfType<MethodDeclarationSyntax>().ToList();
            var propertyDeclarations = updatedRoot.DescendantNodes().OfType<PropertyDeclarationSyntax>().ToList();

            var hasClass = classDeclarations.Count != 0;
            var hasEnum = enumDeclarations.Count != 0;

            if (!hasClass && !hasEnum) {
                ErrorHandler.Handle($"No classes or enums found in the source file {updatedSourcePath}", null);
                return false;
            }

            if (classDeclarations.Concat<MemberDeclarationSyntax>(enumDeclarations).Count() > 1) {
                ErrorHandler.Handle($"The file {updatedSourcePath} contains multiple member definitions.", null);
                return false;
            }

            var decompiler = new CSharpDecompiler(dllPath, new DecompilerSettings {
                ShowXmlDocumentation = false,
                RemoveDeadCode = false,
                RemoveDeadStores = false
            });

            var resolver = new DefaultAssemblyResolver();
            resolver.AddSearchDirectory(dllDirectory);

            var readerParams = new ReaderParameters {
                AssemblyResolver = resolver,
                ReadWrite = true
            };
            
            File.Copy(dllPath, backupPath, true);

            using var originalAssembly = AssemblyDefinition.ReadAssembly(backupPath, readerParams);

            if (hasEnum) {
                var enumDecl = enumDeclarations.First();
                var namespaceDecl = enumDecl.Ancestors().OfType<NamespaceDeclarationSyntax>().FirstOrDefault();
                var namespaceName = namespaceDecl?.Name.ToString() ?? "";
                var fullTypeName = string.IsNullOrEmpty(namespaceName)
                    ? enumDecl.Identifier.Text
                    : $"{namespaceName}.{enumDecl.Identifier.Text}";

                var originalType = originalAssembly.MainModule.GetType(fullTypeName);
                if (originalType == null) {
                    if (!TryAddNewType(references, namespaceDecl, enumDecl, originalAssembly, readerParams))
                        return false;

                    Logger.LogInfo($"Added new enum {fullTypeName}.");
                } else {
                    if (!EnumPatcher.UpdateEnum(originalType, enumDecl, originalAssembly))
                        return false;

                    Logger.LogInfo($"Updated enum {fullTypeName} with new members.");
                }
            }

            if (hasClass) {
                var classDecl = classDeclarations.First();
                var namespaceDecl = classDecl.Ancestors().OfType<NamespaceDeclarationSyntax>().FirstOrDefault();
                var namespaceName = namespaceDecl?.Name.ToString() ?? "";
                var methodsForClass = methodDeclarations.ToList();
                var propertiesForClass = propertyDeclarations.ToList();

                var baseName = classDecl.Identifier.Text;
                var arity = classDecl.TypeParameterList?.Parameters.Count ?? 0;
                var fullType = arity > 0 ? $"{baseName}`{arity}" : baseName;
                var fullTypeName = string.IsNullOrEmpty(namespaceName) ? fullType : $"{namespaceName}.{fullType}";

                var originalType = originalAssembly.MainModule.GetType(fullTypeName);

                if (originalType == null) {
                    Logger.LogInfo($"Adding new type {fullTypeName}.");
                    if (!TryAddNewType(references, namespaceDecl, classDecl, originalAssembly, readerParams))
                        return false;
                } else {
                    if (methodsForClass.Count != 0 || propertiesForClass.Count != 0) {
                        Logger.LogInfo($"Updating class {fullTypeName} with new/changed members.");
                        if (!TryUpdateClassTypeMembers(decompiler, originalAssembly, fullTypeName, namespaceDecl,
                                classDecl, methodsForClass, propertiesForClass, updatedRoot, references, readerParams))
                            return false;
                    } else {
                        Logger.LogInfo($"Class {fullTypeName} found but no methods or properties to replace.");
                    }
                }
            }

            originalAssembly.Write(dllPath);
            Logger.LogInfo($"Successfully patched assembly at {dllPath}");
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

        var compilation = CSharpCompilation.Create(
            "WorkingCopy",
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithOptimizationLevel(OptimizationLevel.Release)
                .WithPlatform(Platform.AnyCpu)
        );

        using var ms = new MemoryStream();
        var result = compilation.Emit(ms);

        if (!result.Success) {
            PrintCompilationFailure(result, syntaxTree);
            return false;
        }

        ms.Seek(0, SeekOrigin.Begin);

        using var newAssembly =
            AssemblyDefinition.ReadAssembly(new MemoryStream(ms.ToArray()), readerParams);

        var fullTypeName = GetFullTypeName(namespaceDecl, typeDecl);

        var newType = newAssembly.MainModule.GetType(fullTypeName);
        if (newType == null) {
            ErrorHandler.Handle($"Could not find type {fullTypeName} in the compiled assembly", null);
            return false;
        }

        var importedType = CloneCreator.CloneType(newType, originalAssembly.MainModule);
        originalAssembly.MainModule.Types.Add(importedType);

        return true;
    }

    private static string GetFullTypeName(NamespaceDeclarationSyntax? nsDecl, MemberDeclarationSyntax typeDecl) {
        var namespaceName = nsDecl?.Name.ToString() ?? "";
        var typeName = typeDecl switch {
            ClassDeclarationSyntax c => c.Identifier.Text,
            EnumDeclarationSyntax e => e.Identifier.Text,
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


        var duplicateMethods = methodReplacements
            .GroupBy(m => new { m.Name, Types = string.Join(",", m.Types) })
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicateMethods.Count != 0) {
            foreach (var dup in duplicateMethods) {
                ErrorHandler.Handle(
                    $"Duplicate method definition detected in updated source: {dup.Name} with parameters ({dup.Types})",
                    null);
            }

            return false;
        }

        var methodRewriter = new MethodReplacer(methodReplacements);
        var modifiedClass = (ClassDeclarationSyntax?)methodRewriter.Visit(decompClass);

        CompilationUnitSyntax mergedRoot;
        if (namespaceDecl != null) {
            var mergedNamespace = SyntaxFactory.NamespaceDeclaration(namespaceDecl.Name)
                .WithMembers(SyntaxFactory.SingletonList<MemberDeclarationSyntax>(modifiedClass ?? decompClass));
            mergedRoot = SyntaxFactory.CompilationUnit()
                .WithUsings(ReferenceCollector.MergeUsings(decompRoot, updatedRoot))
                .WithMembers(SyntaxFactory.SingletonList<MemberDeclarationSyntax>(mergedNamespace));
        } else {
            mergedRoot = SyntaxFactory.CompilationUnit()
                .WithUsings(ReferenceCollector.MergeUsings(decompRoot, updatedRoot))
                .WithMembers(SyntaxFactory.SingletonList<MemberDeclarationSyntax>(modifiedClass ?? decompClass));
        }

        var mergedSource = mergedRoot.NormalizeWhitespace().ToFullString();
        var mergedTree = CSharpSyntaxTree.ParseText(mergedSource);

        var compilation = CSharpCompilation.Create(
            Path.GetRandomFileName(),
            [mergedTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithOptimizationLevel(OptimizationLevel.Release)
                .WithPlatform(Platform.AnyCpu)
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

        foreach (var methodName in methodGroup.Select(m => m.Identifier.Text)) {
            var newMethod = newType.Methods.FirstOrDefault(m => m.Name == methodName);
            var originalMethod = originalType.Methods.FirstOrDefault(m => m.Name == methodName);

            if (newMethod == null) {
                Logger.LogWarning($"Could not find method {methodName} in the compiled assembly");
                continue;
            }

            if (originalMethod == null) {
                Logger.LogInfo($"Adding new method {methodName} to type {fullTypeName}");
                var importedMethod = CloneCreator.CloneMethod(newMethod, originalAssembly.MainModule);
                originalType.Methods.Add(importedMethod);
                Logger.LogInfo($"Added new method {methodName}");
            } else {
                ReplaceMethodBody(originalMethod, newMethod, originalAssembly.MainModule);
                Logger.LogInfo($"Replaced method {methodName} in type {fullTypeName}");
            }
        }

        foreach (var propDecl in propertyGroup) {
            var propName = propDecl.Identifier.Text;

            var originalProp = originalType.Properties.FirstOrDefault(p => p.Name == propName);
            var newProp     = newType.Properties.FirstOrDefault(p => p.Name == propName);

            if (originalProp != null && newProp != null)
            {
                // Force the property type
                originalProp.PropertyType = originalAssembly.MainModule.ImportReference(newProp.PropertyType);

                // Clear old get method (if exists)
                if (originalProp.GetMethod != null)
                    originalType.Methods.Remove(originalProp.GetMethod);

                // Clone & assign new get method
                if (newProp.GetMethod != null)
                {
                    var clonedGet = CloneCreator.CloneMethod(newProp.GetMethod, originalAssembly.MainModule);
                    originalType.Methods.Add(clonedGet);
                    originalProp.GetMethod = clonedGet;
                    Logger.LogInfo($"Swapped in new get accessor of property {propName}");
                }

                // Clear old set method (if exists)
                if (originalProp.SetMethod != null)
                    originalType.Methods.Remove(originalProp.SetMethod);

                // Clone & assign new set method
                if (newProp.SetMethod != null)
                {
                    var clonedSet = CloneCreator.CloneMethod(newProp.SetMethod, originalAssembly.MainModule);
                    originalType.Methods.Add(clonedSet);
                    originalProp.SetMethod = clonedSet;
                    Logger.LogInfo($"Swapped in new set accessor of property {propName}");
                }
            }
        }

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

    private class MethodReplacer(List<MethodReplacement> methodReplacements) : CSharpSyntaxRewriter {
        private bool addedNewMethods;

        public override SyntaxNode VisitMethodDeclaration(MethodDeclarationSyntax node) {
            foreach (var replacement in methodReplacements) {
                if (node.Identifier.Text != replacement.Name)
                    continue;

                var nodeParameterTypes = node.ParameterList.Parameters
                    .Select(p => p.Type.ToString())
                    .ToList();

                if (nodeParameterTypes.SequenceEqual(replacement.Types)) {
                    return replacement.ReplacementMethod
                        .WithModifiers(replacement.ReplacementMethod.Modifiers)  
                        .WithAttributeLists(node.AttributeLists);
                }
            }

            return node;
        }

        public override SyntaxNode VisitClassDeclaration(ClassDeclarationSyntax node) {
            var updatedNode = (ClassDeclarationSyntax)base.VisitClassDeclaration(node);
            if (addedNewMethods) return updatedNode;
        
            var existingMethods = node.Members.OfType<MethodDeclarationSyntax>()
                .Select(m => (m.Identifier.Text, Types: m.ParameterList.Parameters.Select(p => p.Type.ToString()).ToList()));
            
            var newMethods = methodReplacements
                .Where(r => !existingMethods.Any(e => e.Text == r.Name && e.Types.SequenceEqual(r.Types)))
                .Select(r => r.ReplacementMethod);

            addedNewMethods = true;
            return updatedNode.AddMembers(newMethods.ToArray());
        }
    }

    private static void PrintCompilationFailure(EmitResult result, SyntaxTree tree) {
        Logger.LogError("Compilation failed!");
        foreach (var diagnostic in result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)) {
            var lineSpan = diagnostic.Location.GetLineSpan();
            var sourceText = tree.GetText();
            var errorLine = lineSpan.StartLinePosition.Line < sourceText.Lines.Count
                ? sourceText.Lines[lineSpan.StartLinePosition.Line].ToString()
                : "<unknown>";

            Logger.LogInfo($"{diagnostic.Id}: {diagnostic.GetMessage()}");
            Logger.LogInfo($"Location: {lineSpan}");
            Logger.LogInfo($"Source line: {errorLine}");
            Logger.LogLineBreak();
        }
    }

    private static void ReplaceMethodBody(MethodDefinition originalMethod, MethodDefinition newMethod,
        ModuleDefinition targetModule) {
        originalMethod.Body = new MethodBody(originalMethod);
        originalMethod.Attributes = newMethod.Attributes;

        var variableMap = new Dictionary<VariableDefinition, VariableDefinition>();
        foreach (var variable in newMethod.Body.Variables) {
            var newVariable = new VariableDefinition(targetModule.ImportReference(variable.VariableType));
            originalMethod.Body.Variables.Add(newVariable);
            variableMap[variable] = newVariable;
        }

        var parameterMap = new Dictionary<ParameterDefinition, ParameterDefinition>();
        for (var i = 0; i < newMethod.Parameters.Count; i++) {
            parameterMap[newMethod.Parameters[i]] = originalMethod.Parameters[i];
        }

        originalMethod.Body.InitLocals = newMethod.Body.InitLocals;
        originalMethod.Body.MaxStackSize = newMethod.Body.MaxStackSize;

        var ilProcessor = originalMethod.Body.GetILProcessor();
        var instructionMap = new Dictionary<Instruction, Instruction>();

        var currentType = originalMethod.DeclaringType;
        foreach (var instruction in newMethod.Body.Instructions) {
            var newInstruction = CloneCreator.CloneInstruction(instruction, targetModule, variableMap, parameterMap,
                instructionMap, currentType);
            instructionMap[instruction] = newInstruction;
            ilProcessor.Append(newInstruction);
        }

        foreach (var instruction in originalMethod.Body.Instructions) {
            if (instruction.Operand is Instruction targetInstruction && instructionMap.ContainsKey(targetInstruction)) {
                instruction.Operand = instructionMap[targetInstruction];
            } else if (instruction.Operand is Instruction[] targetInstructions) {
                instruction.Operand = targetInstructions
                    .Select(ti => instructionMap.GetValueOrDefault(ti, ti)).ToArray();
            }
        }

        foreach (var handler in newMethod.Body.ExceptionHandlers) {
            var newHandler = new ExceptionHandler(handler.HandlerType) {
                CatchType = handler.CatchType != null ? targetModule.ImportReference(handler.CatchType) : null,
                TryStart = instructionMap[handler.TryStart],
                TryEnd = instructionMap[handler.TryEnd],
                HandlerStart = instructionMap[handler.HandlerStart],
                HandlerEnd = instructionMap[handler.HandlerEnd]
            };
            originalMethod.Body.ExceptionHandlers.Add(newHandler);
        }

        originalMethod.CustomAttributes.Clear();
        CloneCreator.CloneAttributes(newMethod, originalMethod, targetModule);
    }
}