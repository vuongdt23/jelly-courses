using System.Reflection;
using Jellyfin.Database.Implementations;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Courses.Api;

[Route("Courses")]
public class CoursesController : ControllerBase
{
    private readonly ILibraryManager _libraryManager;
    private readonly IUserDataManager _userDataManager;
    private readonly IUserManager _userManager;
    private readonly IDbContextFactory<JellyfinDbContext> _dbContextFactory;
    private readonly ILogger<CoursesController> _logger;

    public CoursesController(
        ILibraryManager libraryManager,
        IUserDataManager userDataManager,
        IUserManager userManager,
        IDbContextFactory<JellyfinDbContext> dbContextFactory,
        ILogger<CoursesController> logger)
    {
        _libraryManager = libraryManager;
        _userDataManager = userDataManager;
        _userManager = userManager;
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    [HttpGet("client.js")]
    [Produces("application/javascript")]
    [ResponseCache(NoStore = true)]
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
        var playedMap = GetPlayedMap(userId, lessons.Select(l => l.Id));
        foreach (var lesson in lessons)
        {
            playedMap.TryGetValue(lesson.Id, out var isPlayed);
            if (!isPlayed)
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
    [ResponseCache(NoStore = true)]
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

        // Collect all video IDs first, then batch-query played status from DB.
        var allVideos = new List<(Video Video, Guid? SubfolderId, string? SubfolderName)>();
        foreach (var child in children.OrderBy(x => x.SortName))
        {
            if (child is Folder subfolder && child is not Video)
            {
                foreach (var v in subfolder.GetChildren(user, true).OfType<Video>().OrderBy(l => l.SortName))
                {
                    allVideos.Add((v, subfolder.Id, subfolder.Name));
                }
            }
            else if (child is Video video)
            {
                allVideos.Add((video, null, null));
            }
        }

        var playedMap = GetPlayedMap(userId, allVideos.Select(v => v.Video.Id));
        var sections = new List<SectionDto>();
        var flatLessons = new List<LessonProgressDto>();

        // Group by subfolder.
        foreach (var group in allVideos.GroupBy(v => v.SubfolderId))
        {
            var lessons = group.Select(v => ToLessonProgress(v.Video, userId, playedMap)).ToList();

            if (group.Key is null)
            {
                flatLessons.AddRange(lessons);
            }
            else
            {
                sections.Add(new SectionDto
                {
                    Id = group.Key.Value,
                    Name = group.First().SubfolderName ?? string.Empty,
                    SortIndex = sections.Count,
                    Lessons = lessons,
                    CompletedCount = lessons.Count(l => l.Played),
                    TotalCount = lessons.Count,
                });
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

    private LessonProgressDto ToLessonProgress(Video video, Guid userId, Dictionary<Guid, bool> playedMap)
    {
        playedMap.TryGetValue(video.Id, out var played);
        return new LessonProgressDto
        {
            Id = video.Id,
            Name = video.Name,
            SortIndex = 0,
            Played = played,
            PlaybackPositionTicks = 0,
            RunTimeTicks = video.RunTimeTicks ?? 0,
        };
    }

    /// <summary>
    /// Query the DB directly to get played status, bypassing the stale in-memory cache.
    /// </summary>
    private Dictionary<Guid, bool> GetPlayedMap(Guid userId, IEnumerable<Guid> itemIds)
    {
        using var db = _dbContextFactory.CreateDbContext();
        var idSet = itemIds.ToHashSet();
        return db.UserData
            .Where(ud => ud.UserId == userId && idSet.Contains(ud.ItemId))
            .ToDictionary(ud => ud.ItemId, ud => ud.Played);
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
