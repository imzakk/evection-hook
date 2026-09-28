namespace Evection.Cli;

/// <summary>Tiny argument parser: positional args plus --flag and --option value.</summary>
internal sealed class Args
{
    private readonly List<string> positional = [];
    private readonly Dictionary<string, string?> options = new(StringComparer.OrdinalIgnoreCase);

    public Args(IEnumerable<string> args)
    {
        var list = args.ToList();
        for (var i = 0; i < list.Count; i++)
        {
            var arg = list[i];
            if (arg.StartsWith("--"))
            {
                var name = arg[2..];
                var eq = name.IndexOf('=');
                if (eq >= 0)
                    options[name[..eq]] = name[(eq + 1)..];
                else if (ValueOptions.Contains(name) && i + 1 < list.Count)
                    options[name] = list[++i];
                else
                    options[name] = null;
            }
            else
            {
                positional.Add(arg);
            }
        }
    }

    /// <summary>Options that take a value (everything else is a boolean flag).</summary>
    private static readonly HashSet<string> ValueOptions = new(StringComparer.OrdinalIgnoreCase) { "out", "payload", "assembly", "profile", "copy" };

    public int Count => positional.Count;
    public string? this[int index] => index < positional.Count ? positional[index] : null;
    public bool Has(string flag) => options.ContainsKey(flag);
    public string? Option(string name) => options.TryGetValue(name, out var v) ? v : null;

    public string Require(int index, string what) =>
        this[index] ?? throw new UsageException($"Missing {what}.");
}

internal sealed class UsageException(string message) : Exception(message);
