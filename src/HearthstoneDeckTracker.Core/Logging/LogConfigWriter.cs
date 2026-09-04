using System.Text;

namespace HearthstoneDeckTracker.Core.Logging;

/// <summary>
/// Writes the Hearthstone <c>log.config</c> sections needed to emit <c>Power.log</c> / <c>LoadingScreen.log</c>.
/// Keys follow the HearthSim HDT wiki: <c>LogLevel=1</c>, <c>FilePrinting=True</c>, and <c>Verbose=True</c> on Power.
/// </summary>
/// <remarks>
/// This only creates/updates a local config file the game already documents. It does not patch the client.
/// Restart Hearthstone after changing the file.
/// </remarks>
public sealed class LogConfigWriter
{
    public const string PowerSectionName = "Power";
    public const string LoadingScreenSectionName = "LoadingScreen";

    /// <summary>Hearthstone on Windows expects CRLF in this file.</summary>
    private const string WindowsNewLine = "\r\n";

    public static readonly IReadOnlyDictionary<string, string> PowerSettings =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["LogLevel"] = "1",
            ["FilePrinting"] = "True",
            ["Verbose"] = "True",
        };

    public static readonly IReadOnlyDictionary<string, string> LoadingScreenSettings =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["LogLevel"] = "1",
            ["FilePrinting"] = "True",
        };

    public LogConfigWriter(string? logConfigPath = null)
    {
        LogConfigPath = logConfigPath ?? HearthstonePaths.DefaultLogConfigPath;
    }

    public string LogConfigPath { get; }

    /// <summary>
    /// Creates the Blizzard Hearthstone directory if needed and merges the required sections
    /// into <see cref="LogConfigPath"/> without dropping unrelated sections (e.g. <c>[Zone]</c>).
    /// </summary>
    public void Ensure()
    {
        var directory = Path.GetDirectoryName(LogConfigPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var existing = File.Exists(LogConfigPath)
            ? File.ReadAllText(LogConfigPath)
            : string.Empty;

        var merged = MergeRequiredSections(existing);
        File.WriteAllText(LogConfigPath, merged, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    /// <summary>
    /// Public for tests: merge required Power / LoadingScreen keys into an existing INI-like document.
    /// </summary>
    public static string MergeRequiredSections(string existing)
    {
        var document = LogConfigDocument.Parse(existing);
        document.UpsertSection(PowerSectionName, PowerSettings);
        document.UpsertSection(LoadingScreenSectionName, LoadingScreenSettings);
        return document.ToIniString();
    }

    private sealed class LogConfigDocument
    {
        private readonly List<string> _sectionOrder = [];
        private readonly Dictionary<string, Dictionary<string, string>> _sections =
            new(StringComparer.OrdinalIgnoreCase);

        public static LogConfigDocument Parse(string text)
        {
            var document = new LogConfigDocument();
            string? current = null;

            using var reader = new StringReader(text ?? string.Empty);
            while (reader.ReadLine() is { } raw)
            {
                var line = raw.Trim();
                if (line.Length == 0)
                    continue;

                if (line.StartsWith('[') && line.EndsWith(']') && line.Length >= 2)
                {
                    current = line[1..^1].Trim();
                    if (current.Length == 0)
                    {
                        current = null;
                        continue;
                    }

                    document.EnsureSection(current);
                    continue;
                }

                if (current is null)
                    continue;

                var eq = line.IndexOf('=');
                if (eq <= 0)
                    continue;

                var key = line[..eq].Trim();
                var value = line[(eq + 1)..].Trim();
                if (key.Length == 0)
                    continue;

                document._sections[current][key] = value;
            }

            return document;
        }

        public void UpsertSection(string name, IReadOnlyDictionary<string, string> required)
        {
            var section = EnsureSection(name);
            foreach (var (key, value) in required)
                section[key] = value;
        }

        public string ToIniString()
        {
            var sb = new StringBuilder();
            foreach (var name in _sectionOrder)
            {
                if (sb.Length > 0)
                    sb.Append(WindowsNewLine);

                sb.Append('[').Append(name).Append(']').Append(WindowsNewLine);
                foreach (var (key, value) in _sections[name])
                    sb.Append(key).Append('=').Append(value).Append(WindowsNewLine);
            }

            return sb.ToString();
        }

        private Dictionary<string, string> EnsureSection(string name)
        {
            if (_sections.TryGetValue(name, out var existing))
                return existing;

            var created = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _sections[name] = created;
            _sectionOrder.Add(name);
            return created;
        }
    }
}
