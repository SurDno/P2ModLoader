using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;

namespace P2ModLoader.Patching.Assembly;

public static class MethodMatcher {
    public static MethodDefinition? FindMatchingMethod(TypeDefinition type, string methodName, List<string> paramTypes, bool isExplicit = false) {
        var candidates = type.Methods.Where(m => 
            (isExplicit ? m.Name.EndsWith("." + methodName) : m.Name == methodName) && 
            m.Parameters.Count == paramTypes.Count
        ).ToList();

        if (candidates.Count == 0) return null;
        if (candidates.Count == 1) return candidates[0];

        foreach (var candidate in candidates) {
            bool allMatch = true;
            for (int i = 0; i < paramTypes.Count; i++) {
                string roslynType = paramTypes[i];
                string cecilType = candidate.Parameters[i].ParameterType.Name;
                
                if (cecilType.EndsWith("&")) {
                    cecilType = cecilType.Substring(0, cecilType.Length - 1);
                }

                int genericIdx = roslynType.IndexOf('<');
                if (genericIdx > 0) {
                    roslynType = roslynType.Substring(0, genericIdx);
                }
                
                string mappedRoslynType = MapCSharpAliasToBcl(roslynType.Split('.').Last());
                string mappedCecilType = cecilType.Split('`')[0];

                if (!string.Equals(mappedRoslynType, mappedCecilType, StringComparison.OrdinalIgnoreCase) && 
                    !string.Equals(roslynType, mappedCecilType, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(roslynType.Split('.').Last(), mappedCecilType, StringComparison.OrdinalIgnoreCase)) {
                    allMatch = false;
                    break;
                }
            }
            if (allMatch) return candidate;
        }

        return null;
    }

    private static string MapCSharpAliasToBcl(string alias) {
        string arraySuffix = "";
        if (alias.EndsWith("[]")) {
            arraySuffix = "[]";
            alias = alias.Substring(0, alias.Length - 2);
        }

        string mapped = alias switch {
            "bool" => "Boolean",
            "byte" => "Byte",
            "sbyte" => "SByte",
            "char" => "Char",
            "decimal" => "Decimal",
            "double" => "Double",
            "float" => "Single",
            "int" => "Int32",
            "uint" => "UInt32",
            "nint" => "IntPtr",
            "nuint" => "UIntPtr",
            "long" => "Int64",
            "ulong" => "UInt64",
            "short" => "Int16",
            "ushort" => "UInt16",
            "object" => "Object",
            "string" => "String",
            "dynamic" => "Object",
            _ => alias
        };
        return mapped + arraySuffix;
    }
}
