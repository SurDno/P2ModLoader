using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace P2ModLoader.Patching.Assembly.Rewriters;

public class ConstructorReplacer(List<ConstructorReplacement> constructorReplacements, string targetTypeName) : CSharpSyntaxRewriter {
	private bool _addedNewConstructors;

	public override SyntaxNode VisitConstructorDeclaration(ConstructorDeclarationSyntax node) { 	
        bool isStatic = node.Modifiers.Any(SyntaxKind.StaticKeyword);
		foreach (var replacement in constructorReplacements.Where(r => r.IsStatic == isStatic)) {
			var nodeParameterTypes = node.ParameterList.Parameters.Select(p => p.Type!.ToString()).ToList();

			if (nodeParameterTypes.SequenceEqual(replacement.Types)) {
				return replacement.ReplacementConstructor
					.WithModifiers(replacement.ReplacementConstructor.Modifiers)  
					.WithAttributeLists(replacement.ReplacementConstructor.AttributeLists);
			}
		}

		return node;
	}

	public override SyntaxNode VisitClassDeclaration(ClassDeclarationSyntax node) { 	
        if (node.Identifier.Text != targetTypeName) return node;

		var updatedNode = (ClassDeclarationSyntax)base.VisitClassDeclaration(node)!;
		if (_addedNewConstructors) return updatedNode;
        
		var existingConstructors = node.Members.OfType<ConstructorDeclarationSyntax>()
			.Select(c => (IsStatic: c.Modifiers.Any(SyntaxKind.StaticKeyword), Types: c.ParameterList.Parameters.Select(p => p.Type!.ToString()).ToList()));
            
		var newConstructors = constructorReplacements
			.Where(r => !existingConstructors.Any(e => e.IsStatic == r.IsStatic && e.Types.SequenceEqual(r.Types)))
			.Select(r => r.ReplacementConstructor);

		_addedNewConstructors = true;
		return updatedNode.AddMembers(newConstructors.ToArray<MemberDeclarationSyntax>());
	}

	public override SyntaxNode VisitStructDeclaration(StructDeclarationSyntax node) { 	
        if (node.Identifier.Text != targetTypeName) return node;

		var updatedNode = (StructDeclarationSyntax)base.VisitStructDeclaration(node)!;
		if (_addedNewConstructors) return updatedNode;
        
		var existingConstructors = node.Members.OfType<ConstructorDeclarationSyntax>()
			.Select(c => (IsStatic: c.Modifiers.Any(SyntaxKind.StaticKeyword), Types: c.ParameterList.Parameters.Select(p => p.Type!.ToString()).ToList()));
            
		var newConstructors = constructorReplacements
			.Where(r => !existingConstructors.Any(e => e.IsStatic == r.IsStatic && e.Types.SequenceEqual(r.Types)))
			.Select(r => r.ReplacementConstructor);

		_addedNewConstructors = true;
		return updatedNode.AddMembers(newConstructors.ToArray<MemberDeclarationSyntax>());
	}
}
