namespace Jellyfin.Plugin.Courses;

/// <summary>
/// Shared video extension sets used by both the resolver and API controller.
/// </summary>
public static class VideoExtensions
{
    /// <summary>
    /// All recognised video extensions, including .ts (MPEG Transport Stream).
    /// </summary>
    public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mkv", ".avi", ".webm", ".mov", ".wmv", ".flv", ".m4v", ".ts"
    };

    /// <summary>
    /// Non-ambiguous video extensions used for directory-level scanning.
    /// Excludes .ts because it's shared with TypeScript — a directory full of .ts files
    /// is far more likely to be source code than MPEG transport streams.
    /// </summary>
    public static readonly HashSet<string> Unambiguous = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mkv", ".avi", ".webm", ".mov", ".wmv", ".flv", ".m4v"
    };
}
