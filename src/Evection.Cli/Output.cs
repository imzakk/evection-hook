namespace Evection.Cli;

internal static class Output
{
    private static readonly bool Color = !Console.IsOutputRedirected && Environment.GetEnvironmentVariable("NO_COLOR") == null;

    public static void Write(string text, ConsoleColor color)
    {
        if (Color) Console.ForegroundColor = color;
        Console.Write(text);
        if (Color) Console.ResetColor();
    }

    public static void Line(string text = "") => Console.WriteLine(text);
    public static void Info(string text) { Write(text, ConsoleColor.Cyan); Console.WriteLine(); }
    public static void Ok(string text) { Write("✔ ", ConsoleColor.Green); Console.WriteLine(text); }
    public static void Warn(string text) { Write("! ", ConsoleColor.Yellow); Console.WriteLine(text); }

    public static void Error(string text)
    {
        if (Color) Console.ForegroundColor = ConsoleColor.Red;
        Console.Error.WriteLine("✘ " + text);
        if (Color) Console.ResetColor();
    }

    public static void Field(string label, string? value)
    {
        Write($"  {label,-14}", ConsoleColor.DarkGray);
        Console.WriteLine(value ?? "-");
    }
}
