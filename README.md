# Jellyfin Courses Plugin

A Jellyfin plugin for browsing and tracking progress through downloaded educational courses. Turns folder structures into a Course > Section > Lesson hierarchy with a collapsible sidebar, progress tracking, and one-click playback.

## Features

- **Collapsible sidebar** -- persistent course navigation panel that pushes Jellyfin's content aside, with a collapsed tab handle when closed
- **Progress tracking** -- per-section and per-course progress bars, mark lessons played/unwatched
- **Folder-level playback** -- play all videos in a course or section with a single click, continue from where you left off
- **Smart naming** -- strips number prefixes, site tags, bracketed URLs, and junk suffixes from filenames
- **Junk filtering** -- skips non-video files (.txt, .url, .nfo, etc.) and junk folders
- **Subfolder browsing** -- any subfolder with videos renders its own sidebar view with progress

## Requirements

- Jellyfin Server **10.11.x**
- [File Transformation](https://github.com/IAmParadox27/jellyfin-plugin-file-transformation) plugin (recommended) -- enables non-destructive script injection into the Jellyfin web UI without modifying files on disk. Without it, the plugin falls back to directly editing `index.html`, which may fail on read-only installs (e.g., systemd packages).
- .NET SDK **9.0** (for building)
- Docker (for local development)

## Installation

### From Plugin Repository (Recommended)

1. In Jellyfin, go to **Dashboard > Plugins > Repositories**
2. Add a new repository with this URL:
   ```
   https://raw.githubusercontent.com/vuongdt23/jelly-courses/main/manifest.json
   ```
3. Go to **Catalog**, find **Courses**, and install it
4. Also install the **File Transformation** plugin from the catalog (or [manually](https://github.com/IAmParadox27/jellyfin-plugin-file-transformation/releases))
5. Restart Jellyfin
6. Create a library using the **Home Videos & Photos** type pointed at your courses folder
7. Go to **Dashboard > Plugins > Courses** and add your library path to **Course Library Paths**
8. Scan the library

### Manual Install

1. Download the latest `Jellyfin.Plugin.Courses.zip` from [Releases](https://github.com/vuongdt23/jelly-courses/releases)
2. Extract the DLL to your Jellyfin plugins directory (e.g., `config/plugins/Courses/`)
3. Restart Jellyfin
4. Follow steps 6-8 above

## Folder Structure

The plugin expects each root-level folder in your library to be a course. Courses can be flat (videos directly inside) or sectioned (videos grouped in subfolders).

```
/Your Courses Library/
  /Course Name/
    /01 - Section Name/
      01 - Lesson.mp4
      02 - Lesson.mp4
    /02 - Another Section/
      01 - Lesson.mp4
  /Another Course/
    lesson1.mp4
    lesson2.mp4
```

---

## Development

### Prerequisites

| Tool | Version | Notes |
|------|---------|-------|
| .NET SDK | 9.0+ | [Download](https://dotnet.microsoft.com/download/dotnet/9.0) |
| Docker | Any recent | For running a local Jellyfin instance |
| Docker Compose | v2+ | Bundled with Docker Desktop |

### Jellyfin Compatibility

The NuGet packages and Docker image are pinned to **10.11.7**. When upgrading, update all three in lockstep:

| Item | Where |
|------|-------|
| `Jellyfin.Model` + `Jellyfin.Controller` | `Jellyfin.Plugin.Courses.csproj` |
| Docker image tag | `docker-compose.yml` |

### Project Structure

```
Jellyfin.Plugin.Courses/          # Plugin source
  Api/CoursesController.cs        # REST endpoints + serves client.js
  Configuration/                  # Plugin config page + model
  Resolvers/                      # Item naming and resolution
  Web/client.js                   # Frontend (injected into Jellyfin UI)
  Plugin.cs                       # Plugin entry point
Jellyfin.Plugin.Courses.Tests/    # xUnit + Moq tests
docker-compose.yml                # Local Jellyfin dev server
deploy.sh                         # Build + restart helper
jellyfin/                         # Docker volume (gitignored)
test-courses/                     # Sample media (gitignored)
```

### Getting Started

**1. Start Jellyfin**

```bash
docker compose up -d
```

Starts Jellyfin at http://localhost:8096 with config in `./jellyfin/` and test media from `./test-courses/`.

**2. First-Time Setup**

1. Open http://localhost:8096 and complete the setup wizard
2. Create a library: **Home Videos & Photos** type, pointed at `/media/test-courses`
3. Deploy the plugin (step 3), then go to **Dashboard > Plugins > Courses** and add `/media/test-courses` to **Course Library Paths**
4. Scan the library

**3. Build and Deploy**

```bash
./deploy.sh
```

Builds the plugin and restarts the Jellyfin container. The MSBuild post-build target auto-copies the DLL to `jellyfin/config/plugins/Courses/`.

Hard-refresh the browser (Cmd+Shift+R / Ctrl+Shift+R) after deploying to pick up frontend changes.

**4. Run Tests**

```bash
dotnet test
```

### Test Media

The `test-courses/` directory is gitignored. Create sample course folders to test with -- any video files work. Zero-byte `.mp4` files are fine for basic testing, though playback and duration display require real media.

### Mounting Real Media

Uncomment and edit the volume line in `docker-compose.yml`:

```yaml
- /path/to/your/courses:/media/courses:ro
```

Then add `/media/courses` to the plugin's Course Library Paths setting.

### How the Frontend Works

1. `ScriptInjectionTask` runs at Jellyfin startup. If the [File Transformation](https://github.com/IAmParadox27/jellyfin-plugin-file-transformation) plugin is installed, it registers an in-memory HTML transformation via reflection (`ScriptInjectionPatch`). Otherwise, it falls back to directly writing a `<script>` tag into `index.html` on disk.
2. `CoursesController` serves `client.js` from embedded resources, replacing the `COURSE_PATHS` placeholder with configured library paths at serve time
3. `client.js` monitors URL changes and renders a collapsible sidebar when the user navigates to course content

### MSBuild Deploy Target

The `.csproj` includes a conditional post-build target that copies the DLL to `../jellyfin/config/plugins/Courses/` -- but only when that directory exists. Local dev gets auto-deploy; CI and other machines skip it silently.

## License

GPLv3 -- required for Jellyfin plugin distribution.
