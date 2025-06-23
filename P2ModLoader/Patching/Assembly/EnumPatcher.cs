using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Mono.Cecil;
using P2ModLoader.Helper;
using P2ModLoader.Logging;

namespace P2ModLoader.Patching.Assembly
{
    public static class EnumPatcher
    {
        public static bool UpdateEnum(TypeDefinition originalEnum, EnumDeclarationSyntax enumDecl, AssemblyDefinition originalAssembly) {
            using var perf = PerformanceLogger.Log();
            if (!originalEnum.IsEnum) {
                ErrorHandler.Handle($"Type {originalEnum.FullName} is not an enum, cannot update it as one.", null);
                return false;
            }

            var existingFields = originalEnum.Fields
                .Where(f => f.IsStatic && f.IsLiteral)
                .Select(f => f.Name)
                .ToHashSet();

            foreach (var member in enumDecl.Members) {
                var memberName = member.Identifier.Text;
                if (!existingFields.Contains(memberName)) {
                    int? value = null;
                    if (member.EqualsValue?.Value is LiteralExpressionSyntax literal &&
                        literal.IsKind(SyntaxKind.NumericLiteralExpression)) {
                        value = (int)literal.Token.Value;
                    }
                    
                    var field = new FieldDefinition(memberName,
                        FieldAttributes.Public | FieldAttributes.Static | FieldAttributes.Literal,
                        originalAssembly.MainModule.ImportReference(originalEnum))
                        {
                            Constant = value ?? GetNextEnumValue(originalEnum)
                        };

                    originalEnum.Fields.Add(field);
                }
            }

            return true;
        }

        private static int GetNextEnumValue(TypeDefinition enumType) {
            using var perf = PerformanceLogger.Log();
            var maxValue = 0;
            var foundAny = false;
            foreach (var field in enumType.Fields.Where(f => f.IsStatic && f.IsLiteral)) {
                if (field.Constant is not int val) continue;
                if (!foundAny) {
                    maxValue = val;
                    foundAny = true;
                } else if (val > maxValue) {
                    maxValue = val;
                }
            }

            return foundAny ? maxValue + 1 : 0;
        }
    }
}
