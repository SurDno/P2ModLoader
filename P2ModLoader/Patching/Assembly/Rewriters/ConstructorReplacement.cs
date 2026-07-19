using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace P2ModLoader.Patching.Assembly.Rewriters;

public record ConstructorReplacement(
	List<string> Types, 
	bool IsStatic,
	ConstructorDeclarationSyntax ReplacementConstructor
);
