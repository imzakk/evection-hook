using System;
using System.IO;
using System.Text;

namespace Evection.Core
{
    /// <summary>Thread-safe log file writer. Keeps the previous session's log as previous.log.</summary>
    public static class Log
    {
        private static readonly object Gate = new object();
        private static StreamWriter? writer;
        private static readonly ModLogger LoaderLogger = new ModLogger("Evection");

        public static string? FilePath { get; private set; }

        public static void Initialize(string logsDirectory)
        {
            Directory.CreateDirectory(logsDirectory);
            FilePath = Path.Combine(logsDirectory, "latest.log");
            var previous = Path.Combine(logsDirectory, "previous.log");
            try
            {
                if (File.Exists(FilePath))
                {
                    File.Copy(FilePath, previous, overwrite: true);
                }
            }
            catch (IOException)
            {
                // Log rotation is best effort.
            }

            writer = new StreamWriter(new FileStream(FilePath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite), new UTF8Encoding(false))
            {
                AutoFlush = true,
            };
            ModLogger.Sink = Write;
        }

        public static void Info(string message) => LoaderLogger.Info(message);
        public static void Warning(string message) => LoaderLogger.Warning(message);
        public static void Error(string message) => LoaderLogger.Error(message);

        public static void Write(LogLevel level, string source, string message)
        {
            var line = $"[{DateTime.Now:HH:mm:ss.fff}] [{Label(level)}] [{source}] {message}";
            lock (Gate)
            {
                try
                {
                    writer?.WriteLine(line);
                }
                catch (IOException)
                {
                    // Never let logging crash the game.
                }
            }
        }

        private static string Label(LogLevel level) => level switch
        {
            LogLevel.Debug => "DEBUG",
            LogLevel.Warning => "WARN ",
            LogLevel.Error => "ERROR",
            _ => "INFO ",
        };
    }
}
