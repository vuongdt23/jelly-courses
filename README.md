# Jellyfin Courses Plugin

A Jellyfin plugin for browsing and playing downloaded educational course videos. Resolves folder structures into Course > Section > Lesson hierarchy with clean display names and folder-level playback.

## Features

- **Automatic structure detection** — parses folder hierarchies into Course > Section > Lesson
- **Smart naming** — strips number prefixes, site tags, bracketed URLs, and junk suffixes from filenames while keeping sort indexes visible
- **Folder-level playback** — play all videos in a course or section with a single click
- **Junk filtering** — skips non-video files (.txt, .url, .nfo, etc.) and junk folders

## Setup

1. Build the plugin and copy the DLL to your Jellyfin plugins directory
2. Create a library using the "Mixed content" type pointed at your courses folder
3. Go to the plugin settings page and add your library path to **Course Library Paths**
4. Scan the library

## Requirements

- Jellyfin Server 10.11.x
- .NET SDK 9.0 (for building)

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

## Building

```bash
dotnet build Jellyfin.Plugin.Courses/Jellyfin.Plugin.Courses.csproj
```

## License

GPLv3 — required for Jellyfin plugin distribution.
