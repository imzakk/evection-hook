using ICSharpCode.Decompiler.TypeSystem;

namespace Evection.Inspector;

/// <summary>Compact C#-like signatures for members.</summary>
internal static class Format
{
    public static string Accessibility(Accessibility a) => a switch
    {
        ICSharpCode.Decompiler.TypeSystem.Accessibility.Public => "public",
        ICSharpCode.Decompiler.TypeSystem.Accessibility.Protected => "protected",
        ICSharpCode.Decompiler.TypeSystem.Accessibility.Internal => "internal",
        ICSharpCode.Decompiler.TypeSystem.Accessibility.ProtectedOrInternal => "protected internal",
        ICSharpCode.Decompiler.TypeSystem.Accessibility.ProtectedAndInternal => "private protected",
        _ => "private",
    };

    public static string TypeName(IType type)
    {
        var name = type switch
        {
            ParameterizedType p => $"{Simplify(p.GenericType.Name)}<{string.Join(", ", p.TypeArguments.Select(TypeName))}>",
            ArrayType a => TypeName(a.ElementType) + "[" + new string(',', a.Dimensions - 1) + "]",
            ByReferenceType r => "ref " + TypeName(r.ElementType),
            PointerType pt => TypeName(pt.ElementType) + "*",
            _ => Simplify(type.Name),
        };
        return type.FullName switch
        {
            "System.Int32" => "int",
            "System.Int64" => "long",
            "System.Single" => "float",
            "System.Double" => "double",
            "System.Boolean" => "bool",
            "System.String" => "string",
            "System.Void" => "void",
            "System.Object" => "object",
            "System.Byte" => "byte",
            "System.UInt32" => "uint",
            "System.Int16" => "short",
            "System.Char" => "char",
            _ => name,
        };
    }

    private static string Simplify(string name)
    {
        var tick = name.IndexOf('`');
        return tick >= 0 ? name[..tick] : name;
    }

    public static string Field(IField f)
    {
        var modifiers = f.IsConst ? " const" : (f.IsStatic ? " static" : "") + (f.IsReadOnly ? " readonly" : "");
        var value = f.IsConst && f.GetConstantValue() is { } v ? " = " + (v is string s ? $"\"{s}\"" : v is bool b ? (b ? "true" : "false") : v.ToString()) : "";
        return $"{Accessibility(f.Accessibility)}{modifiers} {TypeName(f.ReturnType)} {f.Name}{value}";
    }

    public static string Property(IProperty p)
    {
        var accessors = (p.CanGet ? "get; " : "") + (p.CanSet ? "set; " : "");
        return $"{Accessibility(p.Accessibility)}{(p.IsStatic ? " static" : "")} {TypeName(p.ReturnType)} {p.Name} {{ {accessors}}}";
    }

    public static string Method(IMethod m)
    {
        var modifiers = (m.IsStatic ? " static" : "") + (m.IsAbstract ? " abstract" : m.IsOverride ? " override" : m.IsVirtual ? " virtual" : "");
        var parameters = string.Join(", ", m.Parameters.Select(p => $"{TypeName(p.Type)} {p.Name}"));
        var name = m.IsConstructor ? m.DeclaringType.Name : m.Name;
        var returnType = m.IsConstructor ? "" : TypeName(m.ReturnType) + " ";
        return $"{Accessibility(m.Accessibility)}{modifiers} {returnType}{name}({parameters})";
    }
}
