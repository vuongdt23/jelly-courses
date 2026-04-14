# Jellyfin Courses Plugin

A Jellyfin plugin that adds a custom "Courses" library type for downloaded educational video content. Provides structured course navigation, sequential playback, and progress tracking — replacing the poor experience of using "Home Videos and Photos" for course libraries.

## Features

- **Custom library type** — register a "Courses" library in Jellyfin pointed at your course folder
- **Automatic structure detection** — parses folder hierarchies into Course > Section > Lesson
- **Smart naming** — strips number prefixes, site tags, and junk from filenames
- **Sequential playback** — auto-plays next lesson, with next/previous navigation
- **Progress tracking** — per-lesson, per-section, and per-course completion status
- **Course overview UI** — dedicated page with sections, lessons, and a "Continue Course" button

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
