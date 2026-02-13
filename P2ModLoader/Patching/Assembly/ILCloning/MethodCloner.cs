using Mono.Cecil;
using Mono.Cecil.Cil;
using P2ModLoader.Logging;

namespace P2ModLoader.Patching.Assembly.ILCloning;

public static class MethodCloner {

    public static MethodDefinition CloneMethod(MethodDefinition src, ModuleDefinition targetModule) { 	
        MethodDefinition newMethod = new (src.Name, src.Attributes, targetModule.ImportReference(src.ReturnType));
        AttributesCloner.CloneAttributes(src, newMethod, targetModule);

        foreach (var gp in src.GenericParameters)
            newMethod.GenericParameters.Add(new(gp.Name, newMethod));
        foreach (var p in src.Parameters)
            newMethod.Parameters.Add(new(p.Name, p.Attributes, targetModule.ImportReference(p.ParameterType)));
        foreach (var ov in src.Overrides)
            newMethod.Overrides.Add(targetModule.ImportReference(ov));
        newMethod.ImplAttributes = src.ImplAttributes;
        
        if (!src.HasBody)
            return newMethod;
        newMethod.Body = new MethodBody(newMethod);
        CopyMethodBody(src.Body, newMethod.Body, targetModule, newMethod, src.DeclaringType);
        return newMethod;
    }

    public static void ReplaceMethodBody(MethodDefinition originalMethod, MethodDefinition newMethod,
        ModuleDefinition targetModule) { 	
        var requiredTypes = new HashSet<TypeDefinition>();
        var requiredMethods = new HashSet<MethodDefinition>();

        CollectHelpers(newMethod, newMethod.DeclaringType, requiredTypes, requiredMethods);
        
        foreach (var helperType in requiredTypes) {
            var existing = originalMethod.DeclaringType.NestedTypes.FirstOrDefault(t => t.Name == helperType.Name);
            if (existing != null)
                originalMethod.DeclaringType.NestedTypes.Remove(existing);

            originalMethod.DeclaringType.NestedTypes.Add(TypeCloner.CloneType(helperType, targetModule));
        }

        foreach (var helperMethod in requiredMethods) {
            if (originalMethod.DeclaringType.Methods.Any(m => m.Name == helperMethod.Name)) continue;
            originalMethod.DeclaringType.Methods.Add(CloneMethod(helperMethod, targetModule));
        }

        foreach (var m in originalMethod.DeclaringType.NestedTypes.SelectMany(t => t.Methods))
            Logger.Log(LogLevel.Info, $"{m.Name} overrides: {m.Overrides.Count}");
        
        originalMethod.Attributes = newMethod.Attributes;
        originalMethod.Body = new(originalMethod);
        CopyMethodBody(newMethod.Body, originalMethod.Body, targetModule, newMethod, originalMethod.DeclaringType);
        originalMethod.CustomAttributes.Clear();
        AttributesCloner.CloneAttributes(newMethod, originalMethod, targetModule);
    }

    private static void CollectHelpers(MethodDefinition method, TypeDefinition sourceDeclaringType,
        HashSet<TypeDefinition> types, HashSet<MethodDefinition> methods) {
        if (!method.HasBody) return;

        foreach (var nestedType in sourceDeclaringType.NestedTypes) {
            Logger.Log(LogLevel.Info, $"Found nested type {nestedType.FullName} with {nestedType.Methods.Count} " +
                                      $"methods and {nestedType.Fields.Count} properties.");    
        }

        if (sourceDeclaringType.NestedTypes.Count == 0) {
            Logger.Log(LogLevel.Info, $"No nested types for {sourceDeclaringType.FullName}.");
        }
        
        foreach (var instr in method.Body.Instructions) {
            if (instr.Operand is MethodReference mr) {

                if (mr.DeclaringType != null && mr.DeclaringType.FullName == sourceDeclaringType.FullName &&
                    IsCompilerGeneratedName(mr.Name)) {

                    var md = sourceDeclaringType.Methods.FirstOrDefault(m =>
                        m.Name == mr.Name &&
                        m.Parameters.Count == mr.Parameters.Count);

                    if (md != null)
                        methods.Add(md);
                }

                if (mr.DeclaringType == null || mr.DeclaringType.DeclaringType?.FullName != sourceDeclaringType.FullName) continue;
                var nested = sourceDeclaringType.NestedTypes.FirstOrDefault(t => t.Name == mr.DeclaringType.Name);
                TryAddType(nested, types, sourceDeclaringType);
            } else if (instr.Operand is TypeReference { DeclaringType: not null } tr && tr.DeclaringType.FullName == sourceDeclaringType.FullName) {
                var nested = sourceDeclaringType.NestedTypes.FirstOrDefault(t => t.Name == tr.Name);
                TryAddType(nested, types, sourceDeclaringType);
            }
        }
    }

    private static void TryAddType(TypeDefinition td, HashSet<TypeDefinition> result, TypeDefinition sourceDeclaringType) {
        if (td == null) return;
        if (!IsCompilerGenerated(td)) return;
        if (!result.Add(td)) return;

        foreach (var m in td.Methods)
            CollectHelpers(m, sourceDeclaringType, result, []);
    }

    private static bool IsCompilerGenerated(IMemberDefinition member) =>
        member.Name.Contains('<') ||
        member.CustomAttributes.Any(a =>
            a.AttributeType.FullName ==
            "System.Runtime.CompilerServices.CompilerGeneratedAttribute"
        );

    private static bool IsCompilerGeneratedName(string name) => name.Contains('<');

    private static void CopyMethodBody(MethodBody sourceBody, MethodBody targetBody, 
        ModuleDefinition module, MethodDefinition contextMethod, TypeDefinition currentType) {

        var variableMap = new Map<VariableDefinition>();
        foreach (var v in sourceBody.Variables) {
            var nv = new VariableDefinition(module.ImportReference(v.VariableType));
            targetBody.Variables.Add(nv);
            variableMap[v] = nv;
        }
        targetBody.InitLocals = sourceBody.InitLocals;
        targetBody.MaxStackSize = sourceBody.MaxStackSize;
        
        var parameterMap = new Map<ParameterDefinition>();
        for (var i = 0; i < sourceBody.Method.Parameters.Count; i++)
            parameterMap[sourceBody.Method.Parameters[i]] = targetBody.Method.Parameters[i];
        
        var ilProcessor = targetBody.GetILProcessor();
        var instructionMap = new Map<Instruction>();
        foreach (var instr in sourceBody.Instructions) {
            var cloned = InstructionCloner.CloneInstruction(instr, module, variableMap, parameterMap, instructionMap, contextMethod, currentType);
            instructionMap[instr] = cloned;
            ilProcessor.Append(cloned);
        }
        
        foreach (var ci in targetBody.Instructions) {
            ci.Operand = ci.Operand switch {
                Instruction oldTarget when instructionMap.TryGetValue(oldTarget, out var value) => value,
                Instruction[] targets => targets.Select(t => instructionMap.GetValueOrDefault(t, t)).ToArray(),
                _ => ci.Operand
            };
        }
        
        foreach (var h in sourceBody.ExceptionHandlers) {
            targetBody.ExceptionHandlers.Add(new ExceptionHandler(h.HandlerType) {
                CatchType = h.CatchType != null ? module.ImportReference(h.CatchType) : null,
                TryStart = instructionMap[h.TryStart],
                TryEnd = instructionMap[h.TryEnd],
                HandlerStart = instructionMap[h.HandlerStart],
                HandlerEnd = instructionMap[h.HandlerEnd]
            });
        }
    }
}
