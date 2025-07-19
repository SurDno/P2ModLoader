using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace P2ModLoader.Patching.Assembly;

public class MethodReplacer(List<MethodReplacement> methodReplacements) : CSharpSyntaxRewriter {
	private bool _addedNewMethods;

	public override SyntaxNode VisitMethodDeclaration(MethodDeclarationSyntax node) { 	
		foreach (var replacement in methodReplacements.Where(r => node.Identifier.Text == r.Name)) {
			var nodeParameterTypes = node.ParameterList.Parameters.Select(p => p.Type.ToString()).ToList();

			if (nodeParameterTypes.SequenceEqual(replacement.Types)) {
				return replacement.ReplacementMethod
					.WithModifiers(replacement.ReplacementMethod.Modifiers)  
					.WithAttributeLists(node.AttributeLists);
			}
		}

		return node;
	}

	public override SyntaxNode VisitClassDeclaration(ClassDeclarationSyntax node) { 	
		var updatedNode = (ClassDeclarationSyntax)base.VisitClassDeclaration(node)!;
		if (_addedNewMethods) return updatedNode;
        
		var existingMethods = node.Members.OfType<MethodDeclarationSyntax>()
			.Select(m => (m.Identifier.Text, Types: m.ParameterList.Parameters.Select(p => p.Type.ToString()).ToList()));
            
		var newMethods = methodReplacements
			.Where(r => !existingMethods.Any(e => e.Text == r.Name && e.Types.SequenceEqual(r.Types)))
			.Select(r => r.ReplacementMethod);

		_addedNewMethods = true;
		return updatedNode.AddMembers(newMethods.ToArray());
	}
}