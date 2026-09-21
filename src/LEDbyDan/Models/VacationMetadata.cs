namespace LEDbyDan.Models;

/// <summary>
/// Optional per-vacation overrides. Drop a "vacation.json" file in a vacation's
/// folder to customize how it is displayed, e.g.:
/// { "title": "Yellowstone National Park", "date": "2023-08-15", "sortOrder": 1 }
/// None of these fields are required - without this file the folder name is
/// used as the title and vacations are sorted newest-folder-name-first.
/// </summary>
public class VacationMetadata
{
    public string? Title { get; set; }
    public DateOnly? Date { get; set; }
    public int? SortOrder { get; set; }
}
