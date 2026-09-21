using LEDbyDan.Options;
using LEDbyDan.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<VacationMediaOptions>(
    builder.Configuration.GetSection(VacationMediaOptions.SectionName));

builder.Services.AddMemoryCache();
builder.Services.AddSingleton<IVacationService, FileSystemVacationService>();
builder.Services.AddRazorPages();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.MapRazorPages();

// Streams a single photo/video from the vacation media root. This is the only
// place a request touches the file system based on user-supplied input, and
// IVacationService.ResolveMediaFileAsync makes sure the requested file name
// actually belongs to the requested vacation before anything is opened.
app.MapGet("/media/{vacationId}/{fileName}", async (string vacationId, string fileName, IVacationService vacationService) =>
{
    var resolved = await vacationService.ResolveMediaFileAsync(vacationId, fileName);
    if (resolved is null)
    {
        return Results.NotFound();
    }

    var lastModified = File.GetLastWriteTimeUtc(resolved.PhysicalPath);
    // enableRangeProcessing lets videos seek/scrub instead of re-downloading
    // from the start, which matters a lot on a slower network share.
    return Results.File(resolved.PhysicalPath, resolved.ContentType, enableRangeProcessing: true, lastModified: lastModified);
});

app.Run();
