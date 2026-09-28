using ICSharpCode.Decompiler.CSharp.ProjectDecompiler;
using ICSharpCode.Decompiler.Metadata;

namespace Evection.Inspector;

/// <summary>
/// Decompiles whole assemblies into a folder of .cs files + a .csproj per assembly, so modders
/// can open the game's code in VS Code / Rider / Visual Studio and use search, go-to-definition etc.
/// </summary>
public static class ProjectExporter
{
    public static string Export(GameCode code, IEnumerable<string> assemblyPaths, string outputDirectory, Action<string>? progress = null)
    {
        Directory.CreateDirectory(outputDirectory);
        foreach (var assemblyPath in assemblyPaths)
        {
            var name = Path.GetFileNameWithoutExtension(assemblyPath);
            progress?.Invoke($"Decompiling {name}...");
            var target = Path.Combine(outputDirectory, name);
            if (Directory.Exists(target))
                Directory.Delete(target, recursive: true);
            Directory.CreateDirectory(target);

            var module = new PEFile(assemblyPath);
            var resolver = CodeBrowser.CreateResolver(module, code.AssemblyDirectory);
            var decompiler = new WholeProjectDecompiler(CodeBrowser.Settings(), resolver, projectWriter: null, new AssemblyReferenceClassifier(), debugInfoProvider: null);
            decompiler.DecompileProject(module, target);
        }

        if (code.SignaturesOnly)
        {
            File.WriteAllText(Path.Combine(outputDirectory, "README.txt"),
                "This is an IL2CPP game, so the code was rebuilt from metadata by Cpp2IL.\n" +
                "Classes, fields and method signatures are real; method bodies are empty stubs.\n");
        }
        return outputDirectory;
    }
}
