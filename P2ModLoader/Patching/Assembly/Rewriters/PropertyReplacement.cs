using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace P2ModLoader.Patching.Assembly.Rewriters;

public class PropertyReplacement(string name, PropertyDeclarationSyntax replacement) {
	public string Name { get; } = name;
	public PropertyDeclarationSyntax Replacement { get; } = replacement;
}