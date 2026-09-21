using System.Text;
using System.Text.Json;
using LEDbyDan.Models;
using LEDbyDan.Options;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace LEDbyDan.Services;

/// <summary>
/// Discovers vacations by scanning a root folder on disk (typically a mapped
/// network drive): every top-level subfolder becomes one vacation, and every
/// image/video file directly inside it becomes a slide. Adding a new vacation
/// is just copying a new folder of photos onto the share - no code change,
/// no redeploy, and no database.
///
/// The scan result is cached briefly (see VacationMediaOptions.CacheDurationSeconds)
/// so a slow network share doesn't get hit on every single page view, while new
/// vacations still show up automatically within that window.
/// </summary>
public class FileSystemVacationService : IVacationService
{
    private const string CacheKey = "VacationIndex";
    private const string MetadataFileName = "vacation.json";

    private static readonly FileExtensionContentTypeProvider ContentTypeProvider = new();
    private static readonly JsonSerializerOptions MetadataJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly VacationMediaOptions _options;
    private readonly IMemoryCache _cache;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<FileSystemVacationService> _logger;

    public FileSystemVacationService(
        IOptions<VacationMediaOptions> options,
        IMemoryCache cache,
        IWebHostEnvironment environment,
        ILogger<FileSystemVacationService> logger)
    {
        _options = options.Value;
        _cache = cache;
        _environment = environment;
        _logger = logger;
    }

    public async Task<IReadOnlyList<VacationSummary>> GetVacationsAsync()
    {
        var index = await GetIndexAsync();
        return index
            .Select(v => (VacationSummary)ToSummary(v))
            .ToList();
    }

    public async Task<VacationDetail?> GetVacationAsync(string vacationId)
    {
        var index = await GetIndexAsync();
        var entry = index.FirstOrDefault(v => string.Equals(v.Id, vacationId, StringComparison.OrdinalIgnoreCase));
        if (entry is null)
        {
            return null;
        }

        var summary = ToSummary(entry);
        var items = entry.Files
            .Select(f => new MediaItem
            {
                FileName = f.FileName,
                Url = BuildMediaUrl(entry.Id, f.FileName),
                Type = f.Type
            })
            .ToList();

        return new VacationDetail
        {
            Id = summary.Id,
            Title = summary.Title,
            CoverImageUrl = summary.CoverImageUrl,
            ItemCount = summary.ItemCount,
            Date = summary.Date,
            Items = items
        };
    }

    public async Task<ResolvedMediaFile?> ResolveMediaFileAsync(string vacationId, string fileName)
    {
        // Reject anything that isn't a bare file name (no traversal segments,
        // no directory separators) before it ever touches the file system.
        if (string.IsNullOrWhiteSpace(fileName) || fileName != Path.GetFileName(fileName))
        {
            return null;
        }

        var index = await GetIndexAsync();
        var entry = index.FirstOrDefault(v => string.Equals(v.Id, vacationId, StringComparison.OrdinalIgnoreCase));
        if (entry is null)
        {
            return null;
        }

        var fileEntry = entry.Files.FirstOrDefault(f => string.Equals(f.FileName, fileName, StringComparison.OrdinalIgnoreCase));
        if (fileEntry is null)
        {
            return null;
        }

        var physicalPath = Path.Combine(entry.FolderPath, fileEntry.FileName);
        if (!File.Exists(physicalPath))
        {
            return null;
        }

        if (!ContentTypeProvider.TryGetContentType(physicalPath, out var contentType))
        {
            contentType = "application/octet-stream";
        }

        return new ResolvedMediaFile(physicalPath, contentType);
    }

    private async Task<IReadOnlyList<VacationIndexEntry>> GetIndexAsync()
    {
        var cached = await _cache.GetOrCreateAsync(CacheKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(Math.Max(1, _options.CacheDurationSeconds));
            return Task.Run(BuildIndex);
        });

        return cached ?? [];
    }

    private IReadOnlyList<VacationIndexEntry> BuildIndex()
    {
        var rootPath = ResolveRootPath();
        if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
        {
            _logger.LogWarning("Vacation media root path '{RootPath}' was not found. Configure VacationMedia:RootPath in appsettings.json.", rootPath);
            return [];
        }

        var results = new List<VacationIndexEntry>();
        var usedSlugs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        IEnumerable<string> folders;
        try
        {
            folders = Directory.EnumerateDirectories(rootPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Unable to read vacation media root path '{RootPath}'.", rootPath);
            return [];
        }

        foreach (var folder in folders)
        {
            var folderName = Path.GetFileName(folder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (string.IsNullOrEmpty(folderName) || folderName.StartsWith('.') || folderName.StartsWith('_'))
            {
                continue;
            }

            var metadata = ReadMetadata(folder);
            var files = EnumerateMediaFiles(folder);
            if (files.Count == 0)
            {
                continue;
            }

            var slug = MakeUniqueSlug(metadata?.Title ?? folderName, folderName, usedSlugs);
            var coverFileName = files.FirstOrDefault(f =>
                    Path.GetFileNameWithoutExtension(f.FileName).Equals("cover", StringComparison.OrdinalIgnoreCase)
                    && f.Type == MediaType.Image)?.FileName
                ?? files.FirstOrDefault(f => f.Type == MediaType.Image)?.FileName;

            results.Add(new VacationIndexEntry
            {
                Id = slug,
                FolderPath = folder,
                Title = metadata?.Title ?? HumanizeFolderName(folderName),
                Date = metadata?.Date,
                SortOrder = metadata?.SortOrder ?? int.MaxValue,
                Files = files,
                CoverFileName = coverFileName
            });
        }

        return results
            .OrderBy(v => v.SortOrder)
            .ThenByDescending(v => v.Date)
            .ThenByDescending(v => v.FolderPath, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private string ResolveRootPath()
    {
        var configured = _options.RootPath;
        if (string.IsNullOrWhiteSpace(configured))
        {
            return string.Empty;
        }

        return Path.IsPathRooted(configured)
            ? configured
            : Path.Combine(_environment.ContentRootPath, configured);
    }

    private VacationMetadata? ReadMetadata(string folder)
    {
        var metadataPath = Path.Combine(folder, MetadataFileName);
        if (!File.Exists(metadataPath))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(metadataPath);
            return JsonSerializer.Deserialize<VacationMetadata>(json, MetadataJsonOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            _logger.LogWarning(ex, "Could not read {MetadataFile} in '{Folder}'; falling back to defaults.", MetadataFileName, folder);
            return null;
        }
    }

    private List<MediaFileEntry> EnumerateMediaFiles(string folder)
    {
        var files = new List<MediaFileEntry>();

        IEnumerable<string> entries;
        try
        {
            entries = Directory.EnumerateFiles(folder);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Could not list files in '{Folder}'.", folder);
            return files;
        }

        foreach (var path in entries)
        {
            var fileName = Path.GetFileName(path);
            var extension = Path.GetExtension(fileName);

            if (_options.ImageExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            {
                files.Add(new MediaFileEntry { FileName = fileName, Type = MediaType.Image });
            }
            else if (_options.VideoExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            {
                files.Add(new MediaFileEntry { FileName = fileName, Type = MediaType.Video });
            }
        }

        files.Sort((a, b) => string.Compare(a.FileName, b.FileName, StringComparison.OrdinalIgnoreCase));
        return files;
    }

    private static VacationSummary ToSummary(VacationIndexEntry entry) => new()
    {
        Id = entry.Id,
        Title = entry.Title,
        CoverImageUrl = entry.CoverFileName is null ? null : BuildMediaUrl(entry.Id, entry.CoverFileName),
        ItemCount = entry.Files.Count,
        Date = entry.Date
    };

    private static string BuildMediaUrl(string vacationId, string fileName) =>
        $"/media/{Uri.EscapeDataString(vacationId)}/{Uri.EscapeDataString(fileName)}";

    private static string HumanizeFolderName(string folderName) =>
        folderName.Replace('_', ' ').Replace('-', ' ');

    private static string MakeUniqueSlug(string preferredSource, string fallbackSource, HashSet<string> usedSlugs)
    {
        var baseSlug = Slugify(preferredSource);
        if (string.IsNullOrEmpty(baseSlug))
        {
            baseSlug = Slugify(fallbackSource);
        }
        if (string.IsNullOrEmpty(baseSlug))
        {
            baseSlug = "vacation";
        }

        var slug = baseSlug;
        var suffix = 2;
        while (!usedSlugs.Add(slug))
        {
            slug = $"{baseSlug}-{suffix++}";
        }

        return slug;
    }

    private static string Slugify(string value)
    {
        var builder = new StringBuilder(value.Length);
        var lastWasHyphen = false;

        foreach (var c in value.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c))
            {
                builder.Append(c);
                lastWasHyphen = false;
            }
            else if (!lastWasHyphen && builder.Length > 0)
            {
                builder.Append('-');
                lastWasHyphen = true;
            }
        }

        return builder.ToString().TrimEnd('-');
    }

    private class VacationIndexEntry
    {
        public required string Id { get; init; }
        public required string FolderPath { get; init; }
        public required string Title { get; init; }
        public DateOnly? Date { get; init; }
        public int SortOrder { get; init; }
        public required IReadOnlyList<MediaFileEntry> Files { get; init; }
        public string? CoverFileName { get; init; }
    }

    private class MediaFileEntry
    {
        public required string FileName { get; init; }
        public required MediaType Type { get; init; }
    }
}
