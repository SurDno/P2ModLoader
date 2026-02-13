using Mono.Cecil;
using P2ModLoader.Logging;

namespace P2ModLoader.Patching.Assembly.ILCloning;

public static class TypeCloner {
    public static TypeDefinition CloneType(TypeDefinition src, ModuleDefinition targetModule) { 	
        var newType = new TypeDefinition(src.Namespace, src.Name, src.Attributes, src.BaseType != null ?
            targetModule.ImportReference(src.BaseType) : null);

        AttributesCloner.CloneAttributes(src, newType, targetModule);

        foreach (var @interface in src.Interfaces)
            newType.Interfaces.Add(new(targetModule.ImportReference(@interface.InterfaceType)));

        foreach (var gp in src.GenericParameters)
            newType.GenericParameters.Add(new(gp.Name, newType));

        // TODO: separate constructor cloning logic?
        foreach (var method in src.Methods.Where(m =>
                     m is { IsSetter: false, IsGetter: false, IsAddOn: false, IsRemoveOn: false }))
            newType.Methods.Add(MethodCloner.CloneMethod(method, targetModule));

        // TODO: not copy backing fields? 
        foreach (var field in src.Fields)
            newType.Fields.Add(FieldCloner.CloneField(field, targetModule));

        foreach (var property in src.Properties) 
            newType.Properties.Add(PropertyCloner.CloneProperty(property, targetModule, newType));

        foreach (var eventDef in src.Events) 
            newType.Events.Add(EventCloner.CloneEvent(eventDef, targetModule, newType));

        foreach (var nestedType in src.NestedTypes)
            newType.NestedTypes.Add(CloneType(nestedType, targetModule));
        
        foreach (var m in newType.NestedTypes.SelectMany(t => t.Methods))
            Logger.Log(LogLevel.Info, $"{m.Name} overrides: {m.Overrides.Count}");
        
        return newType;
    }
}