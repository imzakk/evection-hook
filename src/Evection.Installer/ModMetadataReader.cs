using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace Evection.Installer;

/// <summary>What a mod says about itself in its [ModInfo(...)] attribute.</summary>
public sealed record ModMetadata(string Name, string Version, string Author, string? Description, IReadOnlyList<string> Games);

/// <summary>
/// Reads [ModInfo] from a mod .dll without loading it (so it works for any target framework and never runs mod code).
/// </summary>
public static class ModMetadataReader
{
    public static ModMetadata? TryRead(string dllPath)
    {
        try
        {
            using var stream = File.OpenRead(dllPath);
            using var pe = new PEReader(stream);
            if (!pe.HasMetadata)
                return null;
            var md = pe.GetMetadataReader();

            foreach (var handle in md.CustomAttributes)
            {
                var attribute = md.GetCustomAttribute(handle);
                if (GetAttributeTypeName(md, attribute) != "ModInfoAttribute")
                    continue;

                var value = attribute.DecodeValue(new StringTypeProvider());
                if (value.FixedArguments.Length < 3)
                    continue;

                string Fixed(int i) => value.FixedArguments[i].Value as string ?? "";
                var description = value.NamedArguments.FirstOrDefault(a => a.Name == "Description").Value as string;
                var games = value.NamedArguments.FirstOrDefault(a => a.Name == "Games").Value is ImmutableArray<CustomAttributeTypedArgument<string>> array
                    ? array.Select(a => a.Value as string).OfType<string>().ToList()
                    : new List<string>();
                return new ModMetadata(Fixed(0), Fixed(1), Fixed(2), description, games);
            }
        }
        catch (Exception e) when (e is BadImageFormatException or IOException or InvalidOperationException or UnauthorizedAccessException)
        {
            // Not a .NET assembly, or unreadable.
        }
        return null;
    }

    private static string? GetAttributeTypeName(MetadataReader md, CustomAttribute attribute)
    {
        switch (attribute.Constructor.Kind)
        {
            case HandleKind.MemberReference:
                var member = md.GetMemberReference((MemberReferenceHandle)attribute.Constructor);
                return member.Parent.Kind switch
                {
                    HandleKind.TypeReference => md.GetString(md.GetTypeReference((TypeReferenceHandle)member.Parent).Name),
                    HandleKind.TypeDefinition => md.GetString(md.GetTypeDefinition((TypeDefinitionHandle)member.Parent).Name),
                    _ => null,
                };
            case HandleKind.MethodDefinition:
                var method = md.GetMethodDefinition((MethodDefinitionHandle)attribute.Constructor);
                return md.GetString(md.GetTypeDefinition(method.GetDeclaringType()).Name);
            default:
                return null;
        }
    }

    /// <summary>Just enough of a type provider to decode string / string[] / int / bool attribute arguments.</summary>
    private sealed class StringTypeProvider : ICustomAttributeTypeProvider<string>
    {
        public string GetPrimitiveType(PrimitiveTypeCode typeCode) => typeCode.ToString();
        public string GetSystemType() => "System.Type";
        public string GetSZArrayType(string elementType) => elementType + "[]";
        public string GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind) => reader.GetString(reader.GetTypeDefinition(handle).Name);
        public string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind) => reader.GetString(reader.GetTypeReference(handle).Name);
        public string GetTypeFromSerializedName(string name) => name;
        public PrimitiveTypeCode GetUnderlyingEnumType(string type) => PrimitiveTypeCode.Int32;
        public bool IsSystemType(string type) => type == "System.Type";
    }
}
