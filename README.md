# LEDbyDan - Vacation Slideshow Player

An ASP.NET Core (Razor Pages, .NET 8) web app that turns a folder of vacation
photos/videos on a network drive into a browsable slideshow website. No
login, no database, no likes/comments - just a home page of vacations and a
slideshow viewer that works well on phones, tablets, and desktops.

> Note: the original ask was "ASP.NET Core Web Forms." Web Forms only exists
> in classic ASP.NET on .NET Framework (Windows-only, effectively legacy).
> ASP.NET Core does not support it. This project uses Razor Pages instead,
> which is the modern equivalent (a page + a code-behind class, same as Web
> Forms) and is fully supported by Visual Studio 2026.

## How adding a vacation works

There is no admin screen and nothing to configure in code. The site scans a
root folder (`VacationMedia:RootPath` in `appsettings.json`) and treats
**every subfolder as one vacation**:

```
\\NAS\Photos\Vacations\
  Yellowstone 2023\
    cover.png            (optional - used as the home page thumbnail)
    vacation.json         (optional - see below)
    IMG_0001.jpg
    IMG_0002.jpg
    clip.mp4
  Outer Banks 2024\
    IMG_0001.jpg
    ...
```

To add a new vacation, just copy a new folder of pictures/videos onto the
share. It shows up on the home page automatically (within
`CacheDurationSeconds`, default 60s) - no redeploy, no restart, no database.

Photos are sorted alphabetically by file name within a vacation, which
matches typical camera file naming (IMG_0001, IMG_0002, ...).

### Optional `vacation.json`

Drop this file inside a vacation folder to override its display name, date,
or sort position:

```json
{
  "title": "Yellowstone National Park",
  "date": "2023-08-15",
  "sortOrder": 1
}
```

All fields are optional. Without this file, the folder name is used as the
title.

### Supported file types

Configured in `appsettings.json` under `VacationMedia`:

- Images: `.jpg .jpeg .png .gif .webp .bmp .heic`
- Videos: `.mp4 .webm .mov .m4v` (browsers play `.mp4`/`.webm` most reliably)

## Project layout

```
LEDbyDan.sln
src/LEDbyDan/
  Program.cs                    App startup + the /media/{vacation}/{file} streaming endpoint
  Options/VacationMediaOptions.cs
  Models/                       MediaItem, VacationSummary, VacationDetail, VacationMetadata
  Services/
    IVacationService.cs
    FileSystemVacationService.cs  Scans RootPath, caches the result, resolves media safely
  Pages/
    Index.cshtml(.cs)           Home page: grid of vacation cards
    Slideshow.cshtml(.cs)       Slideshow viewer (prev/next, swipe, keyboard, fullscreen)
  wwwroot/css/site.css
  wwwroot/js/slideshow.js
  SampleVacations/               Sample photos used only in local Development
```

## Running it locally (no network drive needed)

`appsettings.Development.json` points `VacationMedia:RootPath` at the bundled
`SampleVacations` folder, so it works immediately:

1. Open `LEDbyDan.sln` in Visual Studio 2026 and press **Run** (or
   `dotnet run --project src/LEDbyDan` from the command line).
2. Browse to the home page - you'll see two sample vacations with placeholder
   photos.

## Pointing it at your real network drive

Edit `src/LEDbyDan/appsettings.json`:

```json
"VacationMedia": {
  "RootPath": "\\\\YOUR-NAS-OR-SERVER\\Photos\\Vacations",
  "CacheDurationSeconds": 60
}
```

Notes for deployment on IIS:

- The account the site runs under (the IIS Application Pool Identity) needs
  **Read** permission on that network share. Using a domain service account
  (or `ApplicationPoolIdentity` with the share explicitly granted to the
  machine account) is more reliable than relying on a mapped drive letter,
  since mapped drives are per-user-session and often aren't visible to a
  Windows service/IIS app pool.
- A UNC path (`\\server\share\...`) is strongly preferred over a mapped
  drive letter (`Z:\...`) for exactly that reason.

## Design notes

- **No login / no likes / no comments** - matches the ask; there is
  intentionally no database, just the file system.
- **Security**: the `/media/{vacationId}/{fileName}` endpoint is the only
  place user-supplied URL input touches the file system. It only serves a
  file if it's an exact match to a file already discovered by scanning that
  vacation's folder, which rules out path traversal.
- **Responsive**: the home page grid and the slideshow both use CSS
  Grid/Flexbox and are tested down to phone widths; the slideshow supports
  touch swipe, on-screen prev/next buttons, and left/right arrow keys.
- **Network-drive friendly**: the vacation list is cached briefly in memory
  so a slow share isn't re-scanned on every page view, and videos are served
  with HTTP range support so seeking doesn't re-download the whole file.
