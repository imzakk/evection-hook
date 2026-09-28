using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Evection
{
    /// <summary>
    /// A simple "key = value" settings file per mod, with comments. Players can edit it with any text editor.
    /// <code>
    /// var speed = Config.Get("walkSpeed", 10f, "How fast the player walks");
    /// </code>
    /// </summary>
    public sealed class ModConfig
    {
        private readonly Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> comments = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> order = new List<string>();

        public ModConfig(string path)
        {
            FilePath = path;
            Reload();
        }

        public string FilePath { get; }

        /// <summary>Reads a setting, creating it with <paramref name="defaultValue"/> if it doesn't exist yet.</summary>
        public T Get<T>(string key, T defaultValue, string? description = null)
        {
            if (description != null)
                comments[key] = description;
            if (values.TryGetValue(key, out var raw) && TryConvert(raw, out T parsed))
                return parsed;
            Set(key, defaultValue);
            return defaultValue;
        }

        public void Set<T>(string key, T value)
        {
            if (!values.ContainsKey(key))
                order.Add(key);
            values[key] = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
            Save();
        }

        public void Reload()
        {
            values.Clear();
            order.Clear();
            if (!File.Exists(FilePath))
                return;
            foreach (var line in File.ReadAllLines(FilePath))
            {
                var trimmed = line.Trim();
                if (trimmed.Length == 0 || trimmed.StartsWith("#"))
                    continue;
                var eq = trimmed.IndexOf('=');
                if (eq <= 0)
                    continue;
                var key = trimmed.Substring(0, eq).Trim();
                if (!values.ContainsKey(key))
                    order.Add(key);
                values[key] = trimmed.Substring(eq + 1).Trim();
            }
        }

        public void Save()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            var lines = order.SelectMany(key =>
                comments.TryGetValue(key, out var comment)
                    ? new[] { "# " + comment, $"{key} = {values[key]}", "" }
                    : new[] { $"{key} = {values[key]}", "" });
            File.WriteAllLines(FilePath, lines);
        }

        private static bool TryConvert<T>(string raw, out T result)
        {
            try
            {
                var type = typeof(T);
                object value = type.IsEnum
                    ? Enum.Parse(type, raw, ignoreCase: true)
                    : Convert.ChangeType(raw, type, CultureInfo.InvariantCulture);
                result = (T)value;
                return true;
            }
            catch (Exception)
            {
                result = default!;
                return false;
            }
        }
    }
}
