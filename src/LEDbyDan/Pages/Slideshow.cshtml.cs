using System.Text.Json;
using LEDbyDan.Models;
using LEDbyDan.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LEDbyDan.Pages;

public class SlideshowModel : PageModel
{
    private static readonly JsonSerializerOptions ItemsJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IVacationService _vacationService;

    public SlideshowModel(IVacationService vacationService)
    {
        _vacationService = vacationService;
    }

    public VacationDetail Vacation { get; private set; } = null!;

    /// <summary>Media list serialized once here so the client-side player doesn't
    /// need a round trip per photo - important when the source is a network share.</summary>
    public string ItemsJson { get; private set; } = "[]";

    public async Task<IActionResult> OnGetAsync(string id)
    {
        var vacation = await _vacationService.GetVacationAsync(id);
        if (vacation is null || vacation.Items.Count == 0)
        {
            return NotFound();
        }

        Vacation = vacation;
        ItemsJson = JsonSerializer.Serialize(
            vacation.Items.Select(i => new { url = i.Url, type = i.Type.ToString().ToLowerInvariant(), name = i.FileName }),
            ItemsJsonOptions);

        return Page();
    }
}
