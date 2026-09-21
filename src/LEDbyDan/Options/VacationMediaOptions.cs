namespace LEDbyDan.Options;

/// <summary>
/// Configuration for where vacation photos/videos live and how they are recognized.
/// Bound from the "VacationMedia" section of appsettings.json.
/// </summary>
public class VacationMediaOptions
{
    public const string SectionName = "VacationMedia";

    /// <summary>
    /// Root folder that contains one subfolder per vacation. Can be a local path
    /// (relative paths are resolved against the app's content root, handy for
    /// local development) or a UNC network path such as \\NAS\Photos\Vacations.
    /// </summary>
    public string RootPath { get; set; } = string.Empty;

    /// <summary>
    /// How long the discovered list of vacations/photos is cached in memory before
    /// the network drive is re-scanned. Keeps the site fast while still picking up
    /// newly added vacations automatically without an app restart.
    /// </summary>
    public int CacheDurationSeconds { get; set; } = 60;

    public string[] ImageExtensions { get; set; } =
        [".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp", ".heic"];

    public string[] VideoExtensions { get; set; } =
        [".mp4", ".webm", ".mov", ".m4v"];
}
