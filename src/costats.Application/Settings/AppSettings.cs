namespace costats.Application.Settings;

public sealed class AppSettings
{
    public int RefreshMinutes { get; set; } = 5;
    public string Hotkey { get; set; } = "Ctrl+Alt+U";
    public bool StartAtLogin { get; set; } = false;

    /// <summary>
    /// Whether multicc integration is enabled. Default true when multicc is detected.
    /// </summary>
    public bool MulticcEnabled { get; set; } = true;

    /// <summary>
    /// When set, only show this single profile instead of all profiles stacked.
    /// Null means "show all profiles" (stacked mode).
    /// </summary>
    public string? MulticcSelectedProfile { get; set; }

    /// <summary>
    /// Override path for multicc config directory. Null means auto-detect (~/.multicc or $MULTICC_DIR).
    /// </summary>
    public string? MulticcConfigPath { get; set; }

    /// <summary>
    /// Whether the GitHub Copilot personal usage provider is enabled.
    /// </summary>
    public bool CopilotEnabled { get; set; } = false;

    /// <summary>
    /// Whether local GitHub Copilot telemetry/log scanning is enabled.
    /// Disabled by default because telemetry roots may contain sensitive local records.
    /// </summary>
    public bool CopilotTelemetryEnabled { get; set; } = false;

    /// <summary>
    /// Explicit local roots to scan for GitHub Copilot telemetry records.
    /// Empty means no telemetry roots are scanned.
    /// </summary>
    public string[] CopilotTelemetryRoots { get; set; } = [];
}
