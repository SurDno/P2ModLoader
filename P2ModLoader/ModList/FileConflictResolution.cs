using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using P2ModLoader.Logging;

namespace P2ModLoader.ModList;

public static class FileConflictResolution {
    public static bool AreFilesCompatible(string path1, string path2) {
        using var perf = PerformanceLogger.Log();
        var ext = Path.GetExtension(path1).ToLowerInvariant();
        return ext switch {
            // TODO: add checks that no same node modification is taking place. 
            ".xml" or ".gz" => true,
            ".cs" => AreCSharpFilesCompatible(path1, path2),
            _ => FilesAreIdentical(path1, path2)
        };
    }

    private static bool AreCSharpFilesCompatible(string path1, string path2) {
        using var perf = PerformanceLogger.Log();
        var tree1 = CSharpSyntaxTree.ParseText(File.ReadAllText(path1));
        var tree2 = CSharpSyntaxTree.ParseText(File.ReadAllText(path2));
        
        var methods1 = GetMethodSignatures(tree1.GetRoot());
        var methods2 = GetMethodSignatures(tree2.GetRoot());
        
        return !methods1.Intersect(methods2).Any();
    }

    private static IEnumerable<string> GetMethodSignatures(SyntaxNode root) => 
        root.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Select(m => $"{GetFullTypeName(m)}.{m.Identifier.Text}");

    private static string GetFullTypeName(MethodDeclarationSyntax method) {
        using var perf = PerformanceLogger.Log();
        var parts = new List<string>();
        var current = method.Parent;
        
        while (current is TypeDeclarationSyntax type) {
            parts.Add(type.Identifier.Text);
            current = current.Parent;
        }
        
        parts.Reverse();
        return string.Join(".", parts);
    }

    private static bool FilesAreIdentical(string path1, string path2) {
        using var perf = PerformanceLogger.Log();
        using var fs1 = new FileStream(path1, FileMode.Open, FileAccess.Read);
        using var fs2 = new FileStream(path2, FileMode.Open, FileAccess.Read);
        
        if (fs1.Length != fs2.Length) return false;
        
        const int bufferSize = 4096;
        var buffer1 = new byte[bufferSize];
        var buffer2 = new byte[bufferSize];

        while (true) {
            var count1 = fs1.Read(buffer1, 0, bufferSize);
            var count2 = fs2.Read(buffer2, 0, bufferSize);
            
            if (count1 != count2) return false;
            if (count1 == 0) return true;
            
            for (var i = 0; i < count1; i++)
                if (buffer1[i] != buffer2[i]) return false;
        }
    }
}