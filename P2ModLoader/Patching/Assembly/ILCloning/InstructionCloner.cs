using Mono.Cecil;
using Mono.Cecil.Cil;

namespace P2ModLoader.Patching.Assembly.ILCloning;

public static class InstructionCloner {
    public static Instruction CloneInstruction(Instruction src, ModuleDefinition targetModule,
        Map<VariableDefinition> variableMap, Map<ParameterDefinition> parameterMap, Map<Instruction> instructionMap,
        IGenericParameterProvider contextProvider, TypeDefinition currentType) { 	
        var opcode = src.OpCode;
        var operand = src.Operand;

        switch (operand) {
            case null:
                return Instruction.Create(opcode);
            case GenericInstanceMethod gim:
                var defRef = targetModule.ImportReference(gim.ElementMethod);
                var inst = new GenericInstanceMethod(defRef);
                foreach (var arg in gim.GenericArguments) {
                    if (arg is GenericParameter gp)
                        inst.GenericArguments.Add(defRef.GenericParameters[gp.Position]);
                    else
                        inst.GenericArguments.Add(targetModule.ImportReference(arg));
                }
                return Instruction.Create(opcode, inst);
            case GenericInstanceType git:
                var typeReference = targetModule.ImportReference(git.ElementType);
                var instance = new GenericInstanceType(typeReference);
                foreach (var arg in git.GenericArguments) {
                    if (arg is GenericParameter gp)
                        instance.GenericArguments.Add(typeReference.GenericParameters[gp.Position]);
                    else
                        instance.GenericArguments.Add((TypeReference)targetModule.ImportReference(arg));
                }
                return Instruction.Create(opcode, instance);
            case GenericParameter gp:
                var mapped = contextProvider.GenericParameters[gp.Position];
                return Instruction.Create(opcode, mapped);
            case TypeReference typeRef:
                return Instruction.Create(opcode, targetModule.ImportReference(typeRef));
            case MethodReference methodRef:
                if (methodRef.DeclaringType.FullName != currentType.FullName) {
                    try {
                        return Instruction.Create(opcode, targetModule.ImportReference(methodRef, currentType));
                    } catch {
                        return Instruction.Create(opcode, targetModule.ImportReference(methodRef));
                    }
                }

                var candidates = currentType.Methods
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
                return Instruction.Create(opcode, targetModule.ImportReference(resolvedMethod ?? methodRef));
            case FieldReference fieldRef:
                if (fieldRef.DeclaringType.FullName != currentType.FullName)
                    return Instruction.Create(opcode, targetModule.ImportReference(fieldRef));
                var originalField = currentType.Fields.FirstOrDefault(f => f.Name == fieldRef.Name);
                return Instruction.Create(opcode, targetModule.ImportReference(originalField ?? fieldRef));
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
                var newCallSite = new CallSite(targetModule.ImportReference(callSite.ReturnType)) {
                    CallingConvention = callSite.CallingConvention,
                    HasThis = callSite.HasThis,
                    ExplicitThis = callSite.ExplicitThis
                };

                foreach (var newParam in callSite.Parameters.Select(p =>
                             new ParameterDefinition(targetModule.ImportReference(p.ParameterType)))) {
                    newCallSite.Parameters.Add(newParam);
                }

                return Instruction.Create(opcode, newCallSite);
            default:
                throw new NotSupportedException($"Unsupported operand type: {operand.GetType()}");
        }
    }
}