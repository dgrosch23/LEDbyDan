namespace LEDbyDan.Models;

/// <summary>
/// A single photo or video within a vacation, ready to be rendered by the slideshow.
/// </summary>
public class MediaItem
{
    public required string FileName { get; init; }
    public required string Url { get; init; }
    public required MediaType Type { get; init; }
}
