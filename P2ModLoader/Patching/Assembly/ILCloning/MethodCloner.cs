using Mono.Cecil;
using Mono.Cecil.Cil;

namespace P2ModLoader.Patching.Assembly.ILCloning;

public static class MethodCloner {

    public static MethodDefinition CloneMethod(MethodDefinition src, ModuleDefinition targetModule) { 	
        MethodDefinition newMethod = new (src.Name, src.Attributes, targetModule.ImportReference(src.ReturnType));
        AttributesCloner.CloneAttributes(src, newMethod, targetModule);
        foreach (var newGp in src.GenericParameters.Select(gp => new GenericParameter(gp.Name, newMethod))) 
            newMethod.GenericParameters.Add(newGp);
        foreach (var p in src.Parameters) 
            newMethod.Parameters.Add(new(p.Name, p.Attributes, targetModule.ImportReference(p.ParameterType)));
        if (!src.HasBody)
            return newMethod;
        newMethod.Body = new MethodBody(newMethod);
        CopyMethodBody(src.Body, newMethod.Body, targetModule, newMethod, src.DeclaringType);
        return newMethod;
    }

    public static void ReplaceMethodBody(MethodDefinition originalMethod, MethodDefinition newMethod,
        ModuleDefinition targetModule) { 	
        originalMethod.Attributes = newMethod.Attributes;
        originalMethod.Body = new(originalMethod);
        CopyMethodBody(newMethod.Body, originalMethod.Body, targetModule, newMethod, originalMethod.DeclaringType);
        originalMethod.CustomAttributes.Clear();
        AttributesCloner.CloneAttributes(newMethod, originalMethod, targetModule);
    }

    private static void CopyMethodBody(MethodBody sourceBody, MethodBody targetBody, 
        ModuleDefinition module, MethodDefinition contextMethod, TypeDefinition currentType) {
        var sourceParams = sourceBody.Method.Parameters;
        var targetParams = targetBody.Method.Parameters;
        
        var variableMap = new Map<VariableDefinition>();
        foreach (var v in sourceBody.Variables) {
            var nv = new VariableDefinition(module.ImportReference(v.VariableType));
            targetBody.Variables.Add(nv);
            variableMap[v] = nv;
        }
        targetBody.InitLocals = sourceBody.InitLocals;
        targetBody.MaxStackSize = sourceBody.MaxStackSize;
        
        var parameterMap = new Map<ParameterDefinition>();
        for (var i = 0; i < sourceParams.Count; i++) 
            parameterMap[sourceParams[i]] = targetParams[i];
        
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
            var nh = new ExceptionHandler(h.HandlerType) {
                CatchType = h.CatchType != null ? module.ImportReference(h.CatchType) : null,
                TryStart = instructionMap[h.TryStart],
                TryEnd = instructionMap[h.TryEnd],
                HandlerStart = instructionMap[h.HandlerStart],
                HandlerEnd = instructionMap[h.HandlerEnd]
            };
            targetBody.ExceptionHandlers.Add(nh);
        }
    }
}
