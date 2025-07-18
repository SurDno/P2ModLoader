using Mono.Cecil;
using P2ModLoader.Logging;

namespace P2ModLoader.Patching.Assembly.ILCloning;

public static class AttributesCloner {
	public static void CloneAttributes(ICustomAttributeProvider source, ICustomAttributeProvider target, 
		ModuleDefinition targetModule) {
		using var perf = PerformanceLogger.Log();
		foreach (var attribute in source.CustomAttributes) {
			CustomAttribute importedAttribute = new(targetModule.ImportReference(attribute.Constructor));

			foreach (var arg in attribute.ConstructorArguments)
				importedAttribute.ConstructorArguments.Add(new(targetModule.ImportReference(arg.Type), arg.Value));

			foreach (var field in attribute.Fields)
				importedAttribute.Fields.Add(new(field.Name,
					new(targetModule.ImportReference(field.Argument.Type), field.Argument.Value)));

			foreach (var property in attribute.Properties)
				importedAttribute.Properties.Add(new(property.Name,
					new(targetModule.ImportReference(property.Argument.Type), property.Argument.Value)));

			target.CustomAttributes.Add(importedAttribute);
		}
	}
}