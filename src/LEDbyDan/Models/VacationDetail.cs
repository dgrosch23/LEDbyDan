namespace LEDbyDan.Models;

/// <summary>
/// Full vacation used by the slideshow page: the summary plus the ordered list
/// of photos/videos to browse through.
/// </summary>
public class VacationDetail : VacationSummary
{
    public required IReadOnlyList<MediaItem> Items { get; init; }
}
