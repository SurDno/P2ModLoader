using Mono.Cecil;
using Mono.Cecil.Cil;
using P2ModLoader.Logging;

namespace P2ModLoader.Patching.Assembly.ILCloning;

public static class InstructionCloner {

    public static Instruction CloneInstruction(Instruction src, ModuleDefinition module,
        Map<VariableDefinition> variableMap, Map<ParameterDefinition> parameterMap, Map<Instruction> instructionMap,
        IGenericParameterProvider contextProvider, TypeDefinition type) { 	
        var clone = CloneInstructionImpl(src, module, variableMap, parameterMap, instructionMap, contextProvider, type);
        
        var srcText = $"{src.OpCode} “{src.Operand}” ({src.Operand?.GetType().Name})";
        var cloneText = $"{clone.OpCode} “{clone.Operand}” ({clone.Operand?.GetType().Name})";
        if (srcText == cloneText) return clone;
        Logger.Log(LogLevel.Error, $"Instruction mismatch after cloning! Some data may be lost!");
        Logger.Log(LogLevel.Error, $"[BEFORE CLONE] IL_{src.Offset:X4}: {srcText}");
        Logger.Log(LogLevel.Error, $"[AFTER  CLONE] IL_{clone.Offset:X4}: {cloneText}");

        return clone;
    }
    
    private static Instruction CloneInstructionImpl(Instruction src, ModuleDefinition module,
        Map<VariableDefinition> variableMap, Map<ParameterDefinition> parameterMap, Map<Instruction> instructionMap,
        IGenericParameterProvider contextProvider, TypeDefinition type) { 	
        var opcode = src.OpCode;
        var operand = src.Operand;

        switch (operand) {
            case null:
                return Instruction.Create(opcode);
            case GenericInstanceMethod gim:
                var defRef = module.ImportReference(gim.ElementMethod);
                var inst = new GenericInstanceMethod(defRef);
                foreach (var arg in gim.GenericArguments) {
                    if (arg is GenericParameter gp)
                        inst.GenericArguments.Add(defRef.GenericParameters[gp.Position]);
                    else
                        inst.GenericArguments.Add(module.ImportReference(arg));
                }
                return Instruction.Create(opcode, inst);
            case GenericInstanceType git:
                var typeReference = module.ImportReference(git.ElementType);
                var instance = new GenericInstanceType(typeReference);
                foreach (var arg in git.GenericArguments) {
                    if (arg is GenericParameter gp)
                        instance.GenericArguments.Add(typeReference.GenericParameters[gp.Position]);
                    else
                        instance.GenericArguments.Add((TypeReference)module.ImportReference(arg));
                }
                return Instruction.Create(opcode, instance);
            case GenericParameter gp:
                var mapped = contextProvider.GenericParameters[gp.Position];
                return Instruction.Create(opcode, mapped);
            case TypeReference typeRef:
                return Instruction.Create(opcode, module.ImportReference(typeRef));
            case MethodReference methodRef:
                if (methodRef.DeclaringType.FullName != type.FullName) {
                    try {
                        return Instruction.Create(opcode, module.ImportReference(methodRef, type));
                    } catch {
                        return Instruction.Create(opcode, module.ImportReference(methodRef));
                    }
                }

                var candidates = type.Methods
                    .Where(m => m.Name == methodRef.Name && m.Parameters.Count == methodRef.Parameters.Count);
                var resolvedMethod = candidates.FirstOrDefault(m =>
                    m.Parameters.Select(p => new {
                            Type = p.ParameterType.FullName,
                            Modifier = p.IsOut ? "out" : p.ParameterType.IsByReference ? "ref" : "in"
                        })
                        .SequenceEqual(
                            methodRef.Parameters.Select(rp => new {
                                Type = rp.ParameterType.FullName,
                                Modifier = rp.IsOut ? "out" : rp.ParameterType.IsByReference ? "ref" : "in"
                            })
                        )
                );
                return Instruction.Create(opcode, module.ImportReference(resolvedMethod ?? methodRef));
            case FieldReference fieldRef:
                if (fieldRef.DeclaringType.FullName != type.FullName)
                    return Instruction.Create(opcode, module.ImportReference(fieldRef));
                var originalField = type.Fields.FirstOrDefault(f => f.Name == fieldRef.Name);
                return Instruction.Create(opcode, module.ImportReference(originalField ?? fieldRef));
            case string strOperand:
                return Instruction.Create(opcode, strOperand);
            case sbyte sbyteOperand:
                return Instruction.Create(opcode, sbyteOperand);
            case byte byteOperand:
                return Instruction.Create(opcode, byteOperand);
            case int intOperand:
                return Instruction.Create(opcode, intOperand);
            case long longOperand:
                return Instruction.Create(opcode, longOperand);
            case float floatOperand:
                return Instruction.Create(opcode, floatOperand);
            case double doubleOperand:
                return Instruction.Create(opcode, doubleOperand);
            case Instruction targetInstruction:
                return Instruction.Create(opcode,
                    instructionMap.GetValueOrDefault(targetInstruction, targetInstruction));
            case Instruction[] targetInstructions:
                return Instruction.Create(opcode,
                    targetInstructions.Select(ti => instructionMap.GetValueOrDefault(ti, ti)).ToArray());
            case VariableDefinition variable:
                return Instruction.Create(opcode, variableMap[variable]);
            case ParameterDefinition parameter:
                return Instruction.Create(opcode, parameterMap[parameter]);
            case CallSite callSite: 
                var newCallSite = new CallSite(module.ImportReference(callSite.ReturnType)) {
                    CallingConvention = callSite.CallingConvention,
                    HasThis = callSite.HasThis,
                    ExplicitThis = callSite.ExplicitThis
                };

                foreach (var newParam in callSite.Parameters.Select(p =>
                             new ParameterDefinition(module.ImportReference(p.ParameterType)))) {
                    newCallSite.Parameters.Add(newParam);
                }

                return Instruction.Create(opcode, newCallSite);
            default:
                throw new NotSupportedException($"Unsupported operand type: {operand.GetType()}");
        }
    }
}