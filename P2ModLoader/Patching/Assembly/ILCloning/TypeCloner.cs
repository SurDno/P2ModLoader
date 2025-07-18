using Mono.Cecil;
using P2ModLoader.Logging;

namespace P2ModLoader.Patching.Assembly.ILCloning;

public static class TypeCloner {
    public static TypeDefinition CloneType(TypeDefinition src, ModuleDefinition targetModule) {
        using var perf = PerformanceLogger.Log();
        var newType = new TypeDefinition(
            src.Namespace,
            src.Name,
            src.Attributes,
            src.BaseType != null ? targetModule.ImportReference(src.BaseType) : null
        );

        AttributesCloner.CloneAttributes(src, newType, targetModule);

        foreach (var @interface in src.Interfaces)
            newType.Interfaces.Add(new(targetModule.ImportReference(@interface.InterfaceType)));

        foreach (var gp in src.GenericParameters)
            newType.GenericParameters.Add(new(gp.Name, newType));

        foreach (var method in src.Methods)
            newType.Methods.Add(MethodCloner.CloneMethod(method, targetModule));

        foreach (var field in src.Fields)
            newType.Fields.Add(FieldCloner.CloneField(field, targetModule));

        foreach (var property in src.Properties) {
            PropertyDefinition newProperty = new(property.Name, property.Attributes,
                targetModule.ImportReference(property.PropertyType));

            if (property.GetMethod != null)
                newProperty.GetMethod = newType.Methods.FirstOrDefault(m => m.Name == property.GetMethod.Name);
            if (property.SetMethod != null)
                newProperty.SetMethod = newType.Methods.FirstOrDefault(m => m.Name == property.SetMethod.Name);

            newType.Properties.Add(newProperty);
        }

        foreach (var eventDef in src.Events) {
            var newEvent = new EventDefinition(
                eventDef.Name,
                eventDef.Attributes,
                targetModule.ImportReference(eventDef.EventType)
            );

            if (eventDef.AddMethod != null) {
                newEvent.AddMethod = newType.Methods.FirstOrDefault(m => m.Name == eventDef.AddMethod.Name);
            }

            if (eventDef.RemoveMethod != null) {
                newEvent.RemoveMethod = newType.Methods.FirstOrDefault(m => m.Name == eventDef.RemoveMethod.Name);
            }

            newType.Events.Add(newEvent);
        }

        foreach (var nestedType in src.NestedTypes)
            newType.NestedTypes.Add(CloneType(nestedType, targetModule));

        return newType;
    }
}