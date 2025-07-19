using Mono.Cecil;

namespace P2ModLoader.Patching.Assembly.ILCloning;

public static class FieldCloner {
	public static FieldDefinition CloneField(FieldDefinition src, ModuleDefinition targetModule) {        
		var newField = new FieldDefinition(src.Name, src.Attributes, targetModule.ImportReference(src.FieldType));
		AttributesCloner.CloneAttributes(src, newField, targetModule);
		return newField;
	}
}
