using Jellyfin.Plugin.Courses.Model;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Resolvers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Courses.Resolvers;

/// <summary>
/// Resolves folder structures into Course / CourseSection / CourseLesson items.
///
/// Jellyfin's <see cref="Jellyfin.Data.Enums.CollectionType"/> enum does not include a
/// "courses" value, so this resolver activates for libraries with <c>CollectionType == null</c>,
/// which corresponds to "Mixed content" (or any unrecognised collection type) in the Add Library
/// dialog. Once a folder has been resolved as a <see cref="Course"/>, child folders and files are
/// resolved as sections/lessons because the parent type check succeeds.
/// </summary>
public class CourseResolver : IItemResolver
{

    private static readonly HashSet<string> JunkExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".url", ".ini", ".nfo", ".html", ".htm"
    };

    private static readonly HashSet<string> JunkFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "desktop.ini", ".ds_store", "thumbs.db"
    };

    private readonly ILogger<CourseResolver> _logger;

    public CourseResolver(ILogger<CourseResolver> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Use Plugin priority so this resolver runs before the built-in ones and can claim
    /// folders/files in "mixed" libraries before they get resolved as generic items.
    /// </summary>
    public ResolverPriority Priority => ResolverPriority.Plugin;

    /// <inheritdoc />
    public BaseItem? ResolvePath(ItemResolveArgs args)
    {
        // If the parent is already a Course or CourseSection, keep resolving children.
        if (args.Parent is not Course and not CourseSection)
        {
            // Otherwise, only activate for paths configured as course libraries.
            if (!IsCourseLibraryPath(args.Path))
            {
                return null;
            }
        }

        if (args.IsDirectory)
        {
            return ResolveDirectory(args);
        }

        return ResolveFile(args);
    }

    private BaseItem? ResolveDirectory(ItemResolveArgs args)
    {
        var name = Path.GetFileName(args.Path);

        if (args.Parent is Course or CourseSection)
        {
            // Skip directories that contain no video files. This prevents Jellyfin
            // from descending into resource/exercise directories (e.g. an Angular
            // project with dozens of .ts source files) and running ffprobe on every
            // non-video file it finds.
            if (Directory.Exists(args.Path) && !ContainsVideoFiles(args.Path))
            {
                _logger.LogDebug("Skipping non-video directory: {Path}", args.Path);
                return null;
            }

            var sortIndex = CourseItemNaming.ParseSortIndex(name) ?? 0;
            var cleanName = CourseItemNaming.CleanName(name);
            return new CourseSection
            {
                SortIndex = sortIndex,
                Name = sortIndex > 0 ? $"{sortIndex}. {cleanName}" : cleanName,
                SortName = sortIndex.ToString("D4"),
            };
        }

        // Root-level folder inside the library = Course.
        return new Course
        {
            Name = CourseItemNaming.CleanName(name),
        };
    }

    private BaseItem? ResolveFile(ItemResolveArgs args)
    {
        var fileName = Path.GetFileName(args.Path);
        var ext = Path.GetExtension(args.Path);

        if (JunkFileNames.Contains(fileName) || JunkExtensions.Contains(ext))
        {
            return null;
        }

        if (!Courses.VideoExtensions.All.Contains(ext))
        {
            return null;
        }

        // .ts is ambiguous: MPEG Transport Stream (video) vs TypeScript (code).
        // MPEG-TS has 0x47 sync byte at 188-byte intervals. If not MPEG-TS, skip
        // so Jellyfin doesn't try to ffprobe TypeScript files.
        if (string.Equals(ext, ".ts", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var buf = new byte[189];
                using var probe = File.OpenRead(args.Path);
                var bytesRead = probe.Read(buf, 0, buf.Length);
                var isMpegTs = bytesRead >= 189
                    ? buf[0] == 0x47 && buf[188] == 0x47
                    : bytesRead >= 1 && buf[0] == 0x47;
                if (!isMpegTs)
                {
                    _logger.LogDebug("Skipping non-MPEG-TS .ts file: {Path}", args.Path);
                    return null;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogDebug("Cannot probe .ts file, skipping: {Path}", args.Path);
                return null;
            }
        }

        if (args.Parent is not (Course or CourseSection))
        {
            return null;
        }

        var sortIndex = CourseItemNaming.ParseSortIndex(fileName) ?? 0;
        var cleanName = CourseItemNaming.CleanName(fileName);
        var sectionSort = args.Parent is CourseSection section ? section.SortIndex : 0;
        return new CourseLesson
        {
            SortIndex = sortIndex,
            Name = sortIndex > 0 ? $"{sortIndex}. {cleanName}" : cleanName,
            SortName = CourseItemNaming.BuildSortName(sectionSort, sortIndex),
        };
    }

    /// <summary>
    /// Checks whether a directory (or any of its descendants) contains at least one
    /// file with an unambiguous video extension. Uses <see cref="VideoExtensions.Unambiguous"/>
    /// so that directories full of TypeScript .ts files are not mistaken for video sections.
    /// </summary>
    private bool ContainsVideoFiles(string directoryPath)
    {
        try
        {
            return Directory.EnumerateFiles(directoryPath, "*", SearchOption.AllDirectories)
                .Any(f => Courses.VideoExtensions.Unambiguous.Contains(Path.GetExtension(f)));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Cannot enumerate files in {Path}, skipping directory", directoryPath);
            return false;
        }
    }

    private static bool IsCourseLibraryPath(string path)
    {
        var config = Plugin.Instance?.Configuration;
        if (config is null)
        {
            return false;
        }

        var libraryPaths = config.GetCourseLibraryPathSet();
        if (libraryPaths.Count == 0)
        {
            return false;
        }

        // Check if the path starts with any configured library path.
        foreach (var libPath in libraryPaths)
        {
            if (path.StartsWith(libPath, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
