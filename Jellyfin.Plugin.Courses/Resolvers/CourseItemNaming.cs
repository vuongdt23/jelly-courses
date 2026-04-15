using System.Text.RegularExpressions;

namespace Jellyfin.Plugin.Courses.Resolvers;

public static partial class CourseItemNaming
{
    private static readonly Regex LeadingNumberRegex = new(@"^(\d+)(?:\.\s+|\s+[-–]\s+|-)", RegexOptions.Compiled);
    private static readonly Regex LessonNumberRegex = new(@"^[Ll]esson\s*(\d+)", RegexOptions.Compiled);
    private static readonly Regex ChapterNumberRegex = new(@"^[Cc]hapter\s+(\d+)", RegexOptions.Compiled);

    public static int? ParseSortIndex(string name)
    {
        var baseName = StripExtension(name);

        var lessonMatch = LessonNumberRegex.Match(baseName);
        if (lessonMatch.Success)
        {
            return int.Parse(lessonMatch.Groups[1].Value);
        }

        var chapterMatch = ChapterNumberRegex.Match(baseName);
        if (chapterMatch.Success)
        {
            return int.Parse(chapterMatch.Groups[1].Value);
        }

        var leadingMatch = LeadingNumberRegex.Match(baseName);
        if (leadingMatch.Success)
        {
            return int.Parse(leadingMatch.Groups[1].Value);
        }

        return null;
    }

    public static string CleanName(string name)
    {
        throw new NotImplementedException();
    }

    private static string StripExtension(string name)
    {
        var ext = Path.GetExtension(name);
        // Only strip real file extensions (short, no spaces).
        // Path.GetExtension treats any dot as an extension, e.g.
        // "24. Technique - Vibrato" => ext = ". Technique - Vibrato"
        if (string.IsNullOrEmpty(ext) || ext.Length > 5 || ext.Contains(' '))
        {
            return name;
        }

        return name[..^ext.Length];
    }
}
