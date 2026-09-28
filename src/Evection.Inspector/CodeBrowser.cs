using System.Text;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;

namespace Evection.Inspector;

public enum MemberKind
{
    Type,
    Field,
    Property,
    Method,
    Event,
}

public sealed record SearchHit(MemberKind Kind, string Assembly, string TypeName, string? MemberName, string Signature);

/// <summary>Browse, search and decompile game assemblies — the core of the modder tools.</summary>
public sealed class CodeBrowser
{
    private readonly GameCode code;
    private readonly Dictionary<string, CSharpDecompiler> decompilers = new(StringComparer.OrdinalIgnoreCase);

    public CodeBrowser(GameCode code, bool includeLibraries = false)
    {
        this.code = code;
        Assemblies = includeLibraries ? code.AllAssemblies : code.GameAssemblies;
    }

    public IReadOnlyList<string> Assemblies { get; }

    public static DecompilerSettings Settings() => new(LanguageVersion.Latest)
    {
        ThrowOnAssemblyResolveErrors = false,
        ShowXmlDocumentation = false,
    };

    private CSharpDecompiler Decompiler(string assemblyPath)
    {
        if (!decompilers.TryGetValue(assemblyPath, out var decompiler))
        {
            var module = new PEFile(assemblyPath);
            decompiler = new CSharpDecompiler(module, CreateResolver(module, code.AssemblyDirectory), Settings());
            decompilers[assemblyPath] = decompiler;
        }
        return decompiler;
    }

    internal static UniversalAssemblyResolver CreateResolver(PEFile module, string searchDirectory)
    {
        var resolver = new UniversalAssemblyResolver(module.FileName, false, module.DetectTargetFrameworkId());
        resolver.AddSearchDirectory(searchDirectory);
        return resolver;
    }

    public IEnumerable<(string Assembly, ITypeDefinition Type)> AllTypes()
    {
        foreach (var assembly in Assemblies)
        {
            var name = Path.GetFileNameWithoutExtension(assembly);
            foreach (var type in Decompiler(assembly).TypeSystem.MainModule.TypeDefinitions)
            {
                if (type.Name.StartsWith('<')) // compiler-generated
                    continue;
                yield return (name, type);
            }
        }
    }

    /// <summary>Types whose full name contains <paramref name="filter"/> (or all types).</summary>
    public IEnumerable<(string Assembly, ITypeDefinition Type)> FindTypes(string? filter) =>
        AllTypes().Where(t => filter == null || t.Type.FullName.Contains(filter, StringComparison.OrdinalIgnoreCase));

    /// <summary>Resolves a type by exact full name, exact short name, or unique partial match.</summary>
    public (string Assembly, ITypeDefinition Type) GetType(string name)
    {
        var all = AllTypes().ToList();
        var exact = all.Where(t => t.Type.FullName == name || t.Type.ReflectionName == name).ToList();
        if (exact.Count == 0)
            exact = all.Where(t => string.Equals(t.Type.Name, name, StringComparison.OrdinalIgnoreCase)).ToList();
        if (exact.Count == 0)
            exact = all.Where(t => t.Type.FullName.Contains(name, StringComparison.OrdinalIgnoreCase)).ToList();

        return exact.Count switch
        {
            1 => exact[0],
            0 => throw new InspectorException($"No type matching '{name}'. Try 'search' to find it."),
            _ => throw new InspectorException(
                $"'{name}' matches {exact.Count} types — be more specific:{Environment.NewLine}  " +
                string.Join(Environment.NewLine + "  ", exact.Take(20).Select(t => t.Type.FullName))),
        };
    }

    /// <summary>Finds types and members whose name contains <paramref name="term"/> — e.g. "health", "money".</summary>
    public IEnumerable<SearchHit> Search(string term, bool includeMethods = true)
    {
        bool Match(string name) => name.Contains(term, StringComparison.OrdinalIgnoreCase);

        foreach (var (assembly, type) in AllTypes())
        {
            if (Match(type.Name))
                yield return new SearchHit(MemberKind.Type, assembly, type.FullName, null, $"{TypeKeyword(type)} {type.FullName}");
            foreach (var field in type.Fields.Where(f => Match(f.Name) && !f.Name.StartsWith('<')))
                yield return new SearchHit(MemberKind.Field, assembly, type.FullName, field.Name, Format.Field(field));
            foreach (var prop in type.Properties.Where(p => Match(p.Name)))
                yield return new SearchHit(MemberKind.Property, assembly, type.FullName, prop.Name, Format.Property(prop));
            if (!includeMethods)
                continue;
            foreach (var method in type.Methods.Where(m => !m.IsAccessor && !m.Name.StartsWith('<') && Match(m.Name)))
                yield return new SearchHit(MemberKind.Method, assembly, type.FullName, method.Name, Format.Method(method));
        }
    }

    /// <summary>A readable outline of a type: every field (with its type), property and method.</summary>
    public string Describe(string typeName)
    {
        var (assembly, type) = GetType(typeName);
        var sb = new StringBuilder();
        sb.Append($"{TypeKeyword(type)} {type.FullName}");
        var bases = type.DirectBaseTypes.Where(b => b.FullName != "System.Object").Select(b => Format.TypeName(b)).ToList();
        if (bases.Count > 0)
            sb.Append(" : ").Append(string.Join(", ", bases));
        sb.AppendLine().AppendLine($"  assembly: {assembly}.dll");

        void Section<T>(string title, IEnumerable<T> items, Func<T, string> format)
        {
            var list = items.ToList();
            if (list.Count == 0)
                return;
            sb.AppendLine().AppendLine($"  {title} ({list.Count})");
            foreach (var item in list)
                sb.AppendLine("    " + format(item));
        }

        Section("Fields", type.Fields.Where(f => !f.Name.StartsWith('<')), Format.Field);
        Section("Properties", type.Properties, Format.Property);
        Section("Methods", type.Methods.Where(m => !m.IsAccessor && !m.Name.StartsWith('<')), Format.Method);
        Section("Events", type.Events, e => $"{Format.Accessibility(e.Accessibility)} event {Format.TypeName(e.ReturnType)} {e.Name}");
        Section("Nested types", type.NestedTypes.Where(n => !n.Name.StartsWith('<')), n => $"{TypeKeyword(n)} {n.Name}");

        if (code.SignaturesOnly)
            sb.AppendLine().AppendLine("  (IL2CPP game: method bodies aren't available, only signatures.)");
        return sb.ToString();
    }

    /// <summary>Decompiled C# source for one type.</summary>
    public string Source(string typeName)
    {
        var (assembly, type) = GetType(typeName);
        var path = Assemblies.First(a => Path.GetFileNameWithoutExtension(a) == assembly);
        return Decompiler(path).DecompileTypeAsString(type.FullTypeName);
    }

    private static string TypeKeyword(ITypeDefinition type) => type.Kind switch
    {
        TypeKind.Interface => "interface",
        TypeKind.Struct => "struct",
        TypeKind.Enum => "enum",
        TypeKind.Delegate => "delegate",
        _ => type.IsStatic ? "static class" : type.IsAbstract ? "abstract class" : "class",
    };
}
