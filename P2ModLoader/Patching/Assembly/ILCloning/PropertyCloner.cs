using Mono.Cecil;
using P2ModLoader.Logging;

namespace P2ModLoader.Patching.Assembly.ILCloning;

public static class PropertyCloner {
    public static PropertyDefinition
        CloneProperty(PropertyDefinition src, ModuleDefinition module, TypeDefinition type) {
        PropertyDefinition newProperty = new(src.Name, src.Attributes, module.ImportReference(src.PropertyType));
        AttributesCloner.CloneAttributes(src, newProperty, module);
        if (src.GetMethod != null) {
            var clonedGet = MethodCloner.CloneMethod(src.GetMethod, module);
            type.Methods.Add(clonedGet);
            newProperty.GetMethod = clonedGet;
        }

        if (src.SetMethod != null) {
            var clonedSet = MethodCloner.CloneMethod(src.SetMethod, module);
            type.Methods.Add(clonedSet);
            newProperty.SetMethod = clonedSet;
        }

        AssignBackingField(src, type, module);
        return newProperty;
    }

    public static void UpdateProperty(PropertyDefinition src, PropertyDefinition newProp, ModuleDefinition module,
        TypeDefinition type) {
        src.Attributes = newProp.Attributes;
        src.PropertyType = module.ImportReference(newProp.PropertyType);
        src.CustomAttributes.Clear();
        AttributesCloner.CloneAttributes(newProp, src, module);
        src.GetMethod = SyncAccessor(src.GetMethod, newProp.GetMethod, module, type);
        src.SetMethod = SyncAccessor(src.SetMethod, newProp.SetMethod, module, type);
        AssignBackingField(src, type, module);
    }

    private static MethodDefinition? SyncAccessor(MethodDefinition? existing, MethodDefinition? updated,
        ModuleDefinition module, TypeDefinition type) {
        if (updated != null) {
            if (existing != null) {
                existing.Attributes = updated.Attributes;
                existing.CustomAttributes.Clear();
                AttributesCloner.CloneAttributes(updated, existing, module);
                MethodCloner.ReplaceMethodBody(existing, updated, module);
                Logger.Log(LogLevel.Info, $"Replaced {existing.Name}");
            } else {
                var clone = MethodCloner.CloneMethod(updated, module);
                type.Methods.Add(clone);
                existing = clone;
                Logger.Log(LogLevel.Info, $"Added {clone.Name}");
            }
        } else if (existing != null) {
            type.Methods.Remove(existing);
            Logger.Log(LogLevel.Info, $"Removed {existing.Name}");
            existing = null;
        }

        return existing;
    }

    private static void AssignBackingField(PropertyDefinition src, TypeDefinition type, ModuleDefinition module) {
        var backingFieldName = $"<{src.Name}>k__BackingField";
        var srcField = src.DeclaringType.Fields.FirstOrDefault(f => f.Name == backingFieldName);
        if (srcField != null && type.Fields.All(f => f.Name != backingFieldName))
            type.Fields.Add(FieldCloner.CloneField(srcField, module));
    }
}
