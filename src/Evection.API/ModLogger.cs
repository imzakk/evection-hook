using System;

namespace Evection
{
    public enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error,
    }

    /// <summary>Per-mod logger. Output goes to EvectionHook/Logs/latest.log (and the console, if one is open).</summary>
    public sealed class ModLogger
    {
        /// <summary>Set by the loader; receives (level, source, message).</summary>
        internal static Action<LogLevel, string, string>? Sink;

        public ModLogger(string source)
        {
            Source = source;
        }

        public string Source { get; }

        public void Debug(object message) => Write(LogLevel.Debug, message);
        public void Info(object message) => Write(LogLevel.Info, message);
        public void Warning(object message) => Write(LogLevel.Warning, message);
        public void Error(object message) => Write(LogLevel.Error, message);

        private void Write(LogLevel level, object message)
        {
            var text = message?.ToString() ?? "null";
            if (Sink != null)
                Sink(level, Source, text);
            else
                Console.WriteLine($"[{level}] [{Source}] {text}");
        }
    }
}
