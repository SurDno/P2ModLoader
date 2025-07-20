using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace P2ModLoader.Patching.Assembly.Rewriters;

public class EventReplacement(string name, EventDeclarationSyntax replacement) {
	public string Name { get; } = name;
	public EventDeclarationSyntax Replacement { get; } = replacement;
}