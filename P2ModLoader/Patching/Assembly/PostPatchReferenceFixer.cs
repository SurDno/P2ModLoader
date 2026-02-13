using Mono.Cecil;
using P2ModLoader.Logging;

namespace P2ModLoader.Patching.Assembly;

public static class PostPatchReferenceFixer {
    public static void FixReferencesForPatchedType(TypeDefinition type, string tempAsm, ModuleDefinition module) {
        for (var i = module.AssemblyReferences.Count - 1; i >= 0; i--) {
            if (module.AssemblyReferences[i].Name != tempAsm) continue;
            module.AssemblyReferences.RemoveAt(i);
            Logger.Log(LogLevel.Debug, $"Removed assembly reference '{tempAsm}' from module");
        }

        foreach (var field in type.Fields.Where(f => f.FieldType.Scope?.Name == tempAsm && f.FieldType.FullName == type.FullName)) {
            field.FieldType = module.ImportReference(type);
            Logger.Log(LogLevel.Debug, $"Fixed field type reference for '{field.Name}'");
        }

        foreach (var prop in type.Properties.Where(p => p.PropertyType.Scope?.Name == tempAsm && p.PropertyType.FullName == type.FullName)) {
            prop.PropertyType = module.ImportReference(type);
            Logger.Log(LogLevel.Debug, $"Fixed property type reference for '{prop.Name}'");
        }

        foreach (var method in type.Methods) {
            if (method.ReturnType.Scope?.Name == tempAsm && method.ReturnType.FullName == type.FullName) {
                method.ReturnType = module.ImportReference(type);
                Logger.Log(LogLevel.Debug, $"Fixed return type for method '{method.Name}'");
            }

            foreach (var param in method.Parameters.Where(p => p.ParameterType.Scope?.Name == tempAsm && p.ParameterType.FullName == type.FullName)) {
                param.ParameterType = module.ImportReference(type);
                Logger.Log(LogLevel.Debug, $"Fixed parameter '{param.Name}' in method '{method.Name}'");
            }

            if (!method.HasBody)
                continue;

            foreach (var instruction in method.Body.Instructions) {
                switch (instruction.Operand) {
                    case TypeReference tRef:
                        if (tRef.Scope?.Name == tempAsm && tRef.FullName == type.FullName) {
                            instruction.Operand = module.ImportReference(type);
                            Logger.Log(LogLevel.Debug,
                                $"Fixed TypeReference operand at in type {type.DeclaringType?.FullName} to '{type.FullName}'");
                        }
                        break;
                    case GenericInstanceMethod oldGim:
                        var defRef =  module.ImportReference(oldGim.ElementMethod);
                        var newGim = new GenericInstanceMethod(defRef);
                        foreach (var ga in oldGim.GenericArguments)
                            newGim.GenericArguments.Add(module.ImportReference(ga));
                        instruction.Operand = newGim;
                        Logger.Log(LogLevel.Debug,
                            $"Fixed GenericInstanceMethod at IL offset {instruction.Offset} to {newGim.FullName}");
                        break;
                    case MethodReference mRef:
                        if (mRef.DeclaringType.Scope?.Name == tempAsm && mRef.DeclaringType.FullName == type.FullName) {
                            var localMethod = FindLocalMethod(type, mRef);
                            if (localMethod != null) {
                                instruction.Operand = module.ImportReference(localMethod);
                                Logger.Log(LogLevel.Debug, $"Fixed MethodReference in type {localMethod.DeclaringType.FullName}" +
                                                           $" to local method '{localMethod.Name}'");
                            } else {
                                var localTypeRef = module.ImportReference(type);
                                mRef.DeclaringType = localTypeRef;
                                Logger.Log(LogLevel.Debug, $"Updated DeclaringType for MethodReference at IL offset " +
                                                           $"{instruction.Offset} to patched type '{type.FullName}'");
                            }
                        }
                        break;
                    case FieldReference fRef:
                        if (fRef.DeclaringType.Scope?.Name == tempAsm && fRef.DeclaringType.FullName == type.FullName) {
                            var localField = type.Fields.FirstOrDefault(f => f.Name == fRef.Name);
                            if (localField != null) {
                                instruction.Operand = module.ImportReference(localField);
                                Logger.Log(LogLevel.Debug, $"Fixed FieldReference in type {localField.DeclaringType.FullName}" +
                                                           $" to local field '{localField.Name}'");
                            } else {
                                fRef.DeclaringType = module.ImportReference(type);
                                Logger.Log(LogLevel.Debug, $"Updated DeclaringType for FieldReference at IL offset " +
                                                           $"{instruction.Offset} to patched type '{type.FullName}'");
                            }
                        }
                        break;
                }
            }
        }

        foreach (var nested in type.NestedTypes)
            FixReferencesForPatchedType(nested, tempAsm, module);
    }

    private static MethodDefinition? FindLocalMethod(TypeDefinition originalType, MethodReference oldMethodRef) {
        var matching = originalType.Methods.Where(m => m.Name == oldMethodRef.Name
                                                       && m.Parameters.Count == oldMethodRef.Parameters.Count);

        foreach (var candidate in matching) {
            var allParamsMatch = true;
            for (var i = 0; i < candidate.Parameters.Count; i++) {
                var candidateTypeName = candidate.Parameters[i].ParameterType.FullName;
                var oldTypeName = oldMethodRef.Parameters[i].ParameterType.FullName;
                if (candidateTypeName == oldTypeName) continue;
                allParamsMatch = false;
                break;
            }

            if (allParamsMatch)
                return candidate;
        }

        return null;
    }
}