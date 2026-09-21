using LEDbyDan.Models;
using LEDbyDan.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LEDbyDan.Pages;

public class IndexModel : PageModel
{
    private readonly IVacationService _vacationService;

    public IndexModel(IVacationService vacationService)
    {
        _vacationService = vacationService;
    }

    public IReadOnlyList<VacationSummary> Vacations { get; private set; } = [];

    public async Task OnGetAsync()
    {
        Vacations = await _vacationService.GetVacationsAsync();
    }
}
