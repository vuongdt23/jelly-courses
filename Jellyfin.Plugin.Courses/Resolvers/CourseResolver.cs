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
    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mkv", ".avi", ".webm", ".mov", ".wmv", ".flv", ".m4v", ".ts"
    };

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
        // Only activate for libraries with no specific collection type (i.e. "Mixed content").
        // A dedicated "courses" CollectionType does not exist in the Jellyfin enum, so this is
        // the closest match. If the parent is already a Course or CourseSection we always
        // continue resolving children regardless of the collection type flag.
        if (args.CollectionType is not null
            && args.Parent is not Course
            && args.Parent is not CourseSection)
        {
            return null;
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

        if (IsJunkFolder(name))
        {
            _logger.LogDebug("Skipping junk folder: {Path}", args.Path);
            return null;
        }

        if (args.Parent is Course or CourseSection)
        {
            var sortIndex = CourseItemNaming.ParseSortIndex(name) ?? 0;
            return new CourseSection
            {
                SortIndex = sortIndex,
                Name = CourseItemNaming.CleanName(name),
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

        if (!VideoExtensions.Contains(ext))
        {
            return null;
        }

        if (args.Parent is not (Course or CourseSection))
        {
            return null;
        }

        var sortIndex = CourseItemNaming.ParseSortIndex(fileName) ?? 0;
        return new CourseLesson
        {
            SortIndex = sortIndex,
            Name = CourseItemNaming.CleanName(fileName),
            SortName = sortIndex.ToString("D4"),
        };
    }

    private static bool IsJunkFolder(string name)
    {
        // "0. Websites you may like" pattern — common in pirated course bundles.
        return name.StartsWith("0.", StringComparison.Ordinal);
    }
}
