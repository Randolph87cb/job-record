using JobRecord.Core.Enums;
using System.IO;

namespace JobRecord.App.Preview;

public enum PreviewWorkState
{
    Running = 0,
    Paused = 1,
    Idle = 2
}

public sealed class PreviewLaunchOptions
{
    public static PreviewLaunchOptions Disabled { get; } = new();

    public bool IsEnabled { get; init; }
    public DockMode DockMode { get; init; } = DockMode.TopCenter;
    public bool IsExpanded { get; init; }
    public bool IsCompact { get; init; }
    public PreviewWorkState WorkState { get; init; } = PreviewWorkState.Running;
    public string? ScreenshotPath { get; init; }
    public bool ShouldCaptureScreenshot => IsEnabled && !string.IsNullOrWhiteSpace(ScreenshotPath);

    public static PreviewLaunchOptions Parse(string[] args)
    {
        if (args.Length == 0 || !args.Any(static arg => string.Equals(arg, "--preview", StringComparison.OrdinalIgnoreCase)))
        {
            return Disabled;
        }

        var values = args
            .Where(static arg => arg.StartsWith("--", StringComparison.OrdinalIgnoreCase))
            .Select(static arg => arg.Split('=', 2, StringSplitOptions.TrimEntries))
            .Where(static parts => parts.Length == 2)
            .ToDictionary(
                static parts => parts[0][2..],
                static parts => parts[1],
                StringComparer.OrdinalIgnoreCase);

        return new PreviewLaunchOptions
        {
            IsEnabled = true,
            DockMode = ParseDockMode(GetValue(values, "dock")),
            IsExpanded = ParseBool(GetValue(values, "expanded")),
            IsCompact = ParseBool(GetValue(values, "compact")),
            WorkState = ParseWorkState(GetValue(values, "state")),
            ScreenshotPath = GetValue(values, "screenshot-path")
        };
    }

    public string GetDatabasePath()
    {
        var previewsDir = Path.Combine(Path.GetTempPath(), "JobRecord", "Preview");
        Directory.CreateDirectory(previewsDir);

        var scenarioKey = $"{DockMode}-{WorkState}-expanded-{IsExpanded}-compact-{IsCompact}"
            .ToLowerInvariant()
            .Replace(" ", string.Empty);

        return Path.Combine(previewsDir, $"{scenarioKey}.db");
    }

    private static string? GetValue(IReadOnlyDictionary<string, string> values, string key)
        => values.TryGetValue(key, out var value) ? value : null;

    private static bool ParseBool(string? value)
        => bool.TryParse(value, out var parsed) && parsed;

    private static DockMode ParseDockMode(string? value)
        => value?.ToLowerInvariant() switch
        {
            "left" => DockMode.LeftEdge,
            "right" => DockMode.RightEdge,
            "floating" => DockMode.Floating,
            _ => DockMode.TopCenter
        };

    private static PreviewWorkState ParseWorkState(string? value)
        => value?.ToLowerInvariant() switch
        {
            "paused" => PreviewWorkState.Paused,
            "idle" => PreviewWorkState.Idle,
            _ => PreviewWorkState.Running
        };
}
