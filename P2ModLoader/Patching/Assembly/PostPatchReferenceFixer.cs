using Mono.Cecil;

namespace P2ModLoader.Patching.Assembly;

public static class PostPatchReferenceFixer {
    public static void FixReferencesForPatchedType(TypeDefinition type, string tempAsm, ModuleDefinition module) {
        for (var i = module.AssemblyReferences.Count - 1; i >= 0; i--) {
            if (module.AssemblyReferences[i].Name == tempAsm) {
                module.AssemblyReferences.RemoveAt(i);
            }
        }

        foreach (var field in type.Fields) {
            if (field.FieldType.Scope?.Name == tempAsm) {
                if (field.FieldType.FullName == type.FullName) {
                    field.FieldType = module.ImportReference(type);
                }
            }
        }

        foreach (var prop in type.Properties) {
            if (prop.PropertyType.Scope?.Name == tempAsm &&
                prop.PropertyType.FullName == type.FullName) {
                prop.PropertyType = module.ImportReference(type);
            }
        }

        foreach (var method in type.Methods) {
            if (method.ReturnType.Scope?.Name == tempAsm &&
                method.ReturnType.FullName == type.FullName) {
                method.ReturnType = module.ImportReference(type);
            }

            foreach (var param in method.Parameters) {
                if (param.ParameterType.Scope?.Name == tempAsm &&
                    param.ParameterType.FullName == type.FullName) {
                    param.ParameterType = module.ImportReference(type);
                }
            }

            if (!method.HasBody)
                continue;

            foreach (var instruction in method.Body.Instructions) {
                switch (instruction.Operand) {
                    case TypeReference tRef:
                        if (tRef.Scope?.Name == tempAsm && tRef.FullName == type.FullName)
                            instruction.Operand = module.ImportReference(type);
                        break;
                    case MethodReference mRef:
                        if (mRef.DeclaringType.Scope?.Name == tempAsm &&
                            mRef.DeclaringType.FullName == type.FullName) {
                            var localMethod = FindLocalMethod(type, mRef);
                            if (localMethod != null) {
                                instruction.Operand = module.ImportReference(localMethod);
                            } else {
                                var localTypeRef = module.ImportReference(type);
                                mRef.DeclaringType = localTypeRef;
                            }
                        }
                        break;
                    case FieldReference fRef:
                        if (fRef.DeclaringType.Scope?.Name == tempAsm && fRef.DeclaringType.FullName == type.FullName) {
                            var localField = type.Fields.FirstOrDefault(f => f.Name == fRef.Name);
                            if (localField != null)
                                instruction.Operand = module.ImportReference(localField);
                            else
                                fRef.DeclaringType = module.ImportReference(type);
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