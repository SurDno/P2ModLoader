using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace P2ModLoader.Patching.Assembly.Rewriters;

public class PropertyReplacer : CSharpSyntaxRewriter {
	private readonly Dictionary<string, PropertyDeclarationSyntax> _map;

	public PropertyReplacer(IEnumerable<PropertyReplacement> reps) {
		_map = reps.ToDictionary(r => r.Name, r => r.Replacement);
	}

	public override SyntaxNode VisitPropertyDeclaration(PropertyDeclarationSyntax node) {
		if (_map.TryGetValue(node.Identifier.Text, out var replacement)) 
			return replacement.WithModifiers(replacement.Modifiers).WithAttributeLists(replacement.AttributeLists);

		return base.VisitPropertyDeclaration(node)!;
	}
}
