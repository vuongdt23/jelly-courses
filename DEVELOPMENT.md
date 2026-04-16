# Development Guide

## Prerequisites

| Tool | Version | Notes |
|------|---------|-------|
| .NET SDK | 9.0+ | [Download](https://dotnet.microsoft.com/download/dotnet/9.0) |
| Docker | Any recent | For running a local Jellyfin instance |
| Docker Compose | v2+ | Bundled with Docker Desktop |

## Jellyfin Compatibility

This plugin targets **Jellyfin 10.11.x**. The NuGet packages and Docker image are pinned to `10.11.7`.

| Package | Version |
|---------|---------|
| `Jellyfin.Model` | 10.11.7 |
| `Jellyfin.Controller` | 10.11.7 |
| Docker image | `jellyfin/jellyfin:10.11.7` |

When upgrading Jellyfin, update all three in lockstep: the two PackageReferences in `Jellyfin.Plugin.Courses.csproj` and the image tag in `docker-compose.yml`.

## Project Structure

```
Jellyfin.Plugin.Courses/          # Plugin source
  Api/CoursesController.cs        # REST endpoints + serves client.js
  Configuration/                  # Plugin config page + model
  Resolvers/                      # Item naming and resolution
  Web/client.js                   # Frontend (injected into Jellyfin UI)
  Plugin.cs                       # Plugin entry point
Jellyfin.Plugin.Courses.Tests/    # xUnit tests
docker-compose.yml                # Local Jellyfin dev server
deploy.sh                         # Build + restart helper
jellyfin/                         # Docker volume (gitignored)
test-courses/                     # Sample media (gitignored)
```

## Getting Started

### 1. Start Jellyfin

```bash
docker compose up -d
```

This starts Jellyfin at **http://localhost:8096** with config stored in `./jellyfin/` and test media from `./test-courses/`.

### 2. First-Time Jellyfin Setup

1. Open http://localhost:8096 and complete the setup wizard
2. Create a library: **Mixed Content** type, pointed at `/media/test-courses`
3. After the plugin is deployed (step 3), go to **Dashboard > Plugins > Courses** and add `/media/test-courses` to **Course Library Paths**
4. Scan the library

### 3. Build and Deploy

```bash
./deploy.sh
```

This builds the plugin and restarts the Jellyfin container. The DLL is automatically copied to `jellyfin/config/plugins/Courses/` by the MSBuild post-build target.

After deploying, hard-refresh the browser (Cmd+Shift+R / Ctrl+Shift+R) to pick up frontend changes — `client.js` is served with `no-cache` headers but the browser may still hold a stale copy.

### 4. Run Tests

```bash
dotnet test
```

Tests use xUnit + Moq. Run from the repo root to execute all test projects via the `.slnx` solution.

## Test Media

The `test-courses/` directory is gitignored. Create sample course folders to test with:

```
test-courses/
  My Course/
    01 - Section A/
      01 - Lesson One.mp4
      02 - Lesson Two.mp4
    02 - Section B/
      01 - Lesson Three.mp4
  Flat Course/
    lesson1.mp4
    lesson2.mp4
```

Any video files will work — the plugin only reads metadata, not content. You can create zero-byte `.mp4` files for basic testing, though playback and duration display require real media.

## How It Works

### Frontend Injection

1. `ScriptInjectionTask` runs at Jellyfin startup and injects `<script src="/Courses/client.js">` into `index.html`
2. `CoursesController.GetClientScript()` serves `client.js` from embedded resources, replacing the `COURSE_PATHS` placeholder with the configured library paths at serve time
3. `client.js` monitors URL changes and renders a collapsible sidebar when the user navigates to course content

### Build Target

The `.csproj` includes a conditional MSBuild target that copies the built DLL to `../jellyfin/config/plugins/Courses/` after every build — but only when that directory exists. This means:
- Local dev: DLL is auto-deployed on build
- CI / other machines: the target is silently skipped

## Mounting Real Media

To test with your own course library, uncomment and edit the volume line in `docker-compose.yml`:

```yaml
- /path/to/your/courses:/media/courses:ro
```

Then add `/media/courses` to the plugin's Course Library Paths setting.
