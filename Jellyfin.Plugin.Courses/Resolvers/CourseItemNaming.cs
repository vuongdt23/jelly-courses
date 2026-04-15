using System.Globalization;
using System.Text.RegularExpressions;

namespace Jellyfin.Plugin.Courses.Resolvers;

public static class CourseItemNaming
{
    private static readonly Regex LeadingNumberRegex = new(@"^(\d+)(?:\.\s+|\s+[-–]\s+|-)", RegexOptions.Compiled);
    private static readonly Regex LessonNumberRegex = new(@"^[Ll]esson\s*(\d+)", RegexOptions.Compiled);
    private static readonly Regex ChapterNumberRegex = new(@"^[Cc]hapter\s+(\d+)", RegexOptions.Compiled);

    private static readonly Regex BracketedPrefixRegex = new(@"\[.*?\]\s*", RegexOptions.Compiled);
    private static readonly Regex PlatformPrefixRegex = new(@"^(?:Udemy\s*[-–]\s*)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex LeadingDashSeparator = new(@"^-\s*", RegexOptions.Compiled);
    private static readonly Regex JunkSuffixRegex = new(@"-[a-zA-Z0-9]{4}(?:-onehack\.us)?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex MultipleSpacesRegex = new(@"\s{2,}", RegexOptions.Compiled);

    public static int? ParseSortIndex(string name)
    {
        var baseName = StripExtension(name);

        var lessonMatch = LessonNumberRegex.Match(baseName);
        if (lessonMatch.Success)
        {
            return int.Parse(lessonMatch.Groups[1].Value, CultureInfo.InvariantCulture);
        }

        var chapterMatch = ChapterNumberRegex.Match(baseName);
        if (chapterMatch.Success)
        {
            return int.Parse(chapterMatch.Groups[1].Value, CultureInfo.InvariantCulture);
        }

        var leadingMatch = LeadingNumberRegex.Match(baseName);
        if (leadingMatch.Success)
        {
            return int.Parse(leadingMatch.Groups[1].Value, CultureInfo.InvariantCulture);
        }

        return null;
    }

    public static string CleanName(string name)
    {
        // 1. Strip file extension.
        var result = StripExtension(name);

        // 2. Strip bracketed site prefixes: [FreeCourseSite.com], [ WebToolTip.com ], etc.
        result = BracketedPrefixRegex.Replace(result, string.Empty);

        // 3. Strip platform prefixes: "Udemy - " (case insensitive).
        result = PlatformPrefixRegex.Replace(result, string.Empty);

        // 4. Strip leading dash separator left over from "[TutsNode.net] - Name" pattern.
        result = LeadingDashSeparator.Replace(result, string.Empty);

        // 5. Handle "lessonN" -> "Lesson N" (return early).
        var lessonMatch = LessonNumberRegex.Match(result);
        if (lessonMatch.Success)
        {
            return $"Lesson {lessonMatch.Groups[1].Value}";
        }

        // 6. Handle "Chapter N  Name" -> "Name"; bare "Chapter N" -> "Chapter N" (return early).
        var chapterMatch = ChapterNumberRegex.Match(result);
        if (chapterMatch.Success)
        {
            var afterChapter = result[chapterMatch.Length..].TrimStart();
            return string.IsNullOrEmpty(afterChapter) ? result.Trim() : afterChapter;
        }

        // 7. Strip leading number prefix using same regex as ParseSortIndex.
        var leadingMatch = LeadingNumberRegex.Match(result);
        if (leadingMatch.Success)
        {
            result = result[leadingMatch.Length..];
        }

        // 8. Strip junk suffixes: -pbc2, -DSMH, -onehack.us patterns.
        result = JunkSuffixRegex.Replace(result, string.Empty);

        // 9. Replace underscores and word-separator hyphens with spaces,
        //    collapse multiple spaces, trim.
        //    Only replace hyphens between word characters (file-name separators like
        //    "About-this-course"), NOT spaced hyphens that are meaningful punctuation
        //    (e.g., "Docker & Kubernetes - The Practical Guide").
        result = result.Replace('_', ' ');
        result = Regex.Replace(result, @"(?<=\w)-(?=\w)", " ");

        result = MultipleSpacesRegex.Replace(result, " ");
        result = result.Trim();

        return result;
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
