using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using P2ModLoader.Helper;
using P2ModLoader.Logging;
using UsingList = Microsoft.CodeAnalysis.SyntaxList<Microsoft.CodeAnalysis.CSharp.Syntax.UsingDirectiveSyntax>;

namespace P2ModLoader.Patching.Assembly;

public static class ReferenceCollector {
	public static List<MetadataReference> CollectReferences(string dllDirectory, string dllPath) {
		using var perf = PerformanceLogger.Log();
		var references = new List<MetadataReference>();

		foreach (var file in Directory.GetFiles(dllDirectory, "*.dll")) {
			var fileName = Path.GetFileName(file);
			try {
				if (dllPath.Contains(fileName)) continue;
				references.Add(MetadataReference.CreateFromFile(file));
			} catch (Exception ex) {
				Logger.Log(LogLevel.Warning, $"Could not load local assembly {Path.GetFileName(file)}: {ex.Message}");
			}
		}

		return references;
	}

	public static UsingList CollectAllUsings(SyntaxNode root) {
		using var perf = PerformanceLogger.Log();
		var allUsings = root.DescendantNodes().OfType<UsingDirectiveSyntax>().ToList();

		return SyntaxFactory.List(allUsings);
	}

	private static UsingList MergeUsings(UsingList originalUsings, UsingList updatedUsings) {
		using var perf = PerformanceLogger.Log();
		var allUsings = originalUsings
			.Concat(updatedUsings)
			.GroupBy(u => u.ToString().Trim())
			.Select(g => g.First())
			.OrderBy(u => u.ToString())
			.ToList();

		return SyntaxFactory.List(allUsings);
	}

	public static UsingList MergeUsings(SyntaxNode originalNode, SyntaxNode updatedNode) {
		using var perf = PerformanceLogger.Log();
		return MergeUsings(CollectAllUsings(originalNode), CollectAllUsings(updatedNode));
	}
}