using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace P2ModLoader.Patching.Assembly.Rewriters {
	public class EventReplacer : CSharpSyntaxRewriter {
		private readonly Dictionary<string, EventDeclarationSyntax> _map;

		public EventReplacer(IEnumerable<EventReplacement> replacements) {
			_map = replacements.ToDictionary(r => r.Name, r => r.Replacement);
		}

		public override SyntaxNode VisitEventDeclaration(EventDeclarationSyntax node) {
			if (_map.TryGetValue(node.Identifier.Text, out var replacement)) 
				return replacement.WithModifiers(replacement.Modifiers).WithAttributeLists(replacement.AttributeLists);
			
			return base.VisitEventDeclaration(node);
		}
	}

}