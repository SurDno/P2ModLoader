using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace P2ModLoader.Patching.Assembly;

public class MethodReplacement(string name, List<string> types, MethodDeclarationSyntax replacementMethod) {
	public string Name { get; } = name;
	public List<string> Types { get; } = types;
	public MethodDeclarationSyntax ReplacementMethod { get; } = replacementMethod;
}