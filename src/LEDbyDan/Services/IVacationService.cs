using LEDbyDan.Models;

namespace LEDbyDan.Services;

public interface IVacationService
{
    /// <summary>All vacations found under the configured root path, for the home page.</summary>
    Task<IReadOnlyList<VacationSummary>> GetVacationsAsync();

    /// <summary>A single vacation with its full ordered media list, for the slideshow page.</summary>
    Task<VacationDetail?> GetVacationAsync(string vacationId);

    /// <summary>
    /// Resolves a requested vacation id + file name to a physical file on disk,
    /// making sure the file actually belongs to that vacation and is an allowed
    /// media type. Returns null if the request doesn't resolve to a real,
    /// allowed file (this is the only path user-supplied URL segments touch the
    /// file system, so it is the choke point for preventing path traversal).
    /// </summary>
    Task<ResolvedMediaFile?> ResolveMediaFileAsync(string vacationId, string fileName);
}

public record ResolvedMediaFile(string PhysicalPath, string ContentType);
