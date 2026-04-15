using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.Courses.Configuration;

public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Comma-separated list of library paths that should be treated as course libraries.
    /// Example: /media/courses,/media/test-courses
    /// </summary>
    public string CourseLibraryPaths { get; set; } = string.Empty;

    public HashSet<string> GetCourseLibraryPathSet()
    {
        if (string.IsNullOrWhiteSpace(CourseLibraryPaths))
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        return new HashSet<string>(
            CourseLibraryPaths.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            StringComparer.OrdinalIgnoreCase);
    }
}
