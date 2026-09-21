namespace LEDbyDan.Models;

/// <summary>
/// Lightweight info shown on the home page grid, one card per vacation.
/// </summary>
public class VacationSummary
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public string? CoverImageUrl { get; init; }
    public int ItemCount { get; init; }
    public DateOnly? Date { get; init; }
}
