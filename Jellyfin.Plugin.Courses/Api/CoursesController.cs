using System.Reflection;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Courses.Api;

[Route("Courses")]
public class CoursesController : ControllerBase
{
    private readonly ILibraryManager _libraryManager;
    private readonly IUserDataManager _userDataManager;
    private readonly IUserManager _userManager;
    private readonly ILogger<CoursesController> _logger;

    public CoursesController(
        ILibraryManager libraryManager,
        IUserDataManager userDataManager,
        IUserManager userManager,
        ILogger<CoursesController> logger)
    {
        _libraryManager = libraryManager;
        _userDataManager = userDataManager;
        _userManager = userManager;
        _logger = logger;
    }

    [HttpGet("client.js")]
    [Produces("application/javascript")]
    public ActionResult GetClientScript()
    {
        var stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("Jellyfin.Plugin.Courses.Web.client.js");
        if (stream is null)
        {
            return NotFound();
        }

        using var reader = new StreamReader(stream);
        var js = reader.ReadToEnd();

        // Inject course paths so the client doesn't need to hit the (admin-only) config API.
        var config = Plugin.Instance?.Configuration;
        var paths = config?.CourseLibraryPaths ?? string.Empty;
        var pathsJson = System.Text.Json.JsonSerializer.Serialize(
            paths.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        js = js.Replace("var COURSE_PATHS = null;", $"var COURSE_PATHS = {pathsJson};");

        return Content(js, "application/javascript");
    }

    [HttpGet("Config")]
    public ActionResult GetPublicConfig()
    {
        var config = Plugin.Instance?.Configuration;
        var paths = config?.CourseLibraryPaths ?? string.Empty;
        return Ok(new { CourseLibraryPaths = paths });
    }

    [HttpGet("{courseId}/NextLesson")]
    public ActionResult GetNextLesson([FromRoute] Guid courseId, [FromQuery] Guid currentLessonId)
    {
        var course = _libraryManager.GetItemById(courseId);
        if (course is not Folder folder || !IsCoursePath(folder.Path))
        {
            return NotFound("Course not found.");
        }

        var lessons = GetAllVideosInOrder(folder);
        var currentIndex = Array.FindIndex(lessons, l => l.Id == currentLessonId);
        if (currentIndex < 0)
        {
            return NotFound("Current lesson not found in this course.");
        }

        if (currentIndex >= lessons.Length - 1)
        {
            return NoContent();
        }

        var next = lessons[currentIndex + 1];
        return Ok(new LessonDto { Id = next.Id, Name = next.Name, SortName = next.SortName });
    }

    [HttpGet("{courseId}/ContinueLesson")]
    public ActionResult GetContinueLesson([FromRoute] Guid courseId, [FromQuery] Guid userId)
    {
        var course = _libraryManager.GetItemById(courseId);
        if (course is not Folder folder || !IsCoursePath(folder.Path))
        {
            return NotFound("Course not found.");
        }

        var user = _userManager.GetUserById(userId);
        if (user is null)
        {
            return NotFound("User not found.");
        }

        var lessons = GetAllVideosInOrder(folder);
        foreach (var lesson in lessons)
        {
            var userData = _userDataManager.GetUserData(user, lesson);
            if (userData is null || !userData.Played)
            {
                return Ok(new LessonDto { Id = lesson.Id, Name = lesson.Name, SortName = lesson.SortName });
            }
        }

        // All played — return first lesson for replay.
        if (lessons.Length > 0)
        {
            var first = lessons[0];
            return Ok(new LessonDto { Id = first.Id, Name = first.Name, SortName = first.SortName });
        }

        return NoContent();
    }

    [HttpGet("{courseId}/Structure")]
    public ActionResult GetStructure([FromRoute] Guid courseId, [FromQuery] Guid userId)
    {
        var item = _libraryManager.GetItemById(courseId);
        if (item is not Folder folder || !IsCoursePath(folder.Path))
        {
            _logger.LogWarning("Courses: Structure requested for {Id} but item is {Type}, path={Path}",
                courseId, item?.GetType().Name ?? "null", item?.Path ?? "null");
            return NotFound("Course not found.");
        }

        var user = _userManager.GetUserById(userId);
        if (user is null)
        {
            return NotFound("User not found.");
        }

        var children = folder.GetChildren(user, true);
        var sections = new List<SectionDto>();
        var flatLessons = new List<LessonProgressDto>();

        foreach (var child in children.OrderBy(x => x.SortName))
        {
            if (child is Folder subfolder && child is not Video)
            {
                // Subfolder = section
                var sectionLessons = subfolder.GetChildren(user, true)
                    .OfType<Video>()
                    .OrderBy(l => l.SortName)
                    .Select(l => ToLessonProgress(l, user))
                    .ToList();

                sections.Add(new SectionDto
                {
                    Id = subfolder.Id,
                    Name = subfolder.Name,
                    SortIndex = sections.Count,
                    Lessons = sectionLessons,
                    CompletedCount = sectionLessons.Count(l => l.Played),
                    TotalCount = sectionLessons.Count,
                });
            }
            else if (child is Video video)
            {
                flatLessons.Add(ToLessonProgress(video, user));
            }
        }

        // Flat lessons (no sections) go into a single implicit section.
        if (flatLessons.Count > 0)
        {
            sections.Insert(0, new SectionDto
            {
                Id = folder.Id,
                Name = folder.Name,
                SortIndex = 0,
                Lessons = flatLessons,
                CompletedCount = flatLessons.Count(l => l.Played),
                TotalCount = flatLessons.Count,
            });
        }

        var totalLessons = sections.Sum(s => s.TotalCount);
        var completedLessons = sections.Sum(s => s.CompletedCount);

        return Ok(new CourseStructureDto
        {
            Id = folder.Id,
            Name = folder.Name,
            Sections = sections,
            TotalLessons = totalLessons,
            CompletedLessons = completedLessons,
            ProgressPercent = totalLessons > 0 ? (int)(completedLessons * 100.0 / totalLessons) : 0,
        });
    }

    [HttpGet("{courseId}/Overview")]
    public ActionResult GetOverview([FromRoute] Guid courseId)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = "Jellyfin.Plugin.Courses.Web.courseOverview.html";

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            return NotFound("Overview page not found.");
        }

        using var reader = new StreamReader(stream);
        var html = reader.ReadToEnd();
        html = html.Replace("{{COURSE_ID}}", courseId.ToString());

        return Content(html, "text/html");
    }

    private static bool IsCoursePath(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        var coursePaths = Plugin.Instance?.Configuration?.GetCourseLibraryPathSet();
        if (coursePaths is null || coursePaths.Count == 0)
        {
            return false;
        }

        foreach (var cp in coursePaths)
        {
            if (path.StartsWith(cp, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private LessonProgressDto ToLessonProgress(Video video, Jellyfin.Database.Implementations.Entities.User user)
    {
        var userData = _userDataManager.GetUserData(user, video);
        return new LessonProgressDto
        {
            Id = video.Id,
            Name = video.Name,
            SortIndex = 0,
            Played = userData?.Played ?? false,
            PlaybackPositionTicks = userData?.PlaybackPositionTicks ?? 0,
            RunTimeTicks = video.RunTimeTicks ?? 0,
        };
    }

    private static Video[] GetAllVideosInOrder(Folder folder)
    {
        return folder
            .GetRecursiveChildren(i => i is Video)
            .Cast<Video>()
            .OrderBy(l => l.SortName)
            .ToArray();
    }
}

public class LessonDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string SortName { get; set; } = string.Empty;
}

public class LessonProgressDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public int SortIndex { get; set; }

    public bool Played { get; set; }

    public long PlaybackPositionTicks { get; set; }

    public long RunTimeTicks { get; set; }
}

public class SectionDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public int SortIndex { get; set; }

    public List<LessonProgressDto> Lessons { get; set; } = [];

    public int CompletedCount { get; set; }

    public int TotalCount { get; set; }
}

public class CourseStructureDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public List<SectionDto> Sections { get; set; } = [];

    public int TotalLessons { get; set; }

    public int CompletedLessons { get; set; }

    public int ProgressPercent { get; set; }
}
