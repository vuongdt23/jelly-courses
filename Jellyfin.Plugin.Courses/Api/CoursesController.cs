using System.IO.Compression;
using System.Reflection;
using Jellyfin.Database.Implementations;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Courses.Api;

[Route("Courses")]
[Authorize]
public class CoursesController : ControllerBase
{

    private static readonly HashSet<string> _junkExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".url", ".ini", ".nfo", ".html", ".htm"
    };

    /// <summary>
    /// Subtitle / media-adjacent files that sit next to videos but are NOT resources.
    /// Jellyfin matches these to their parent video automatically.
    /// </summary>
    private static readonly HashSet<string> _subtitleExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".srt", ".sub", ".ass", ".ssa", ".vtt", ".idx", ".sup", ".pgs"
    };


    private static readonly HashSet<string> _junkFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "desktop.ini", ".ds_store", "thumbs.db"
    };

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
    [AllowAnonymous]
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

    /// <summary>
    /// Lazy resource scanner. Called per-section when the user expands a section,
    /// or for the course root. Keeps the Structure endpoint fast.
    /// </summary>
    [HttpGet("{courseId}/ResourceScan")]
    [ResponseCache(Duration = 60)]
    public ActionResult ScanSectionResources([FromRoute] Guid courseId, [FromQuery] Guid? sectionId)
    {
        var course = _libraryManager.GetItemById(courseId);
        if (course is not Folder courseFolder || !IsCoursePath(courseFolder.Path))
        {
            return NotFound("Course not found.");
        }

        string scanPath;
        if (sectionId.HasValue)
        {
            var section = _libraryManager.GetItemById(sectionId.Value);
            if (section is not Folder sectionFolder)
            {
                return NotFound("Section not found.");
            }

            // Verify the section is under this course.
            if (!sectionFolder.Path.StartsWith(courseFolder.Path + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest("Section is not part of this course.");
            }

            scanPath = sectionFolder.Path;
        }
        else
        {
            scanPath = courseFolder.Path;
        }

        var files = ScanResourceFiles(scanPath, courseFolder.Path);
        var folders = ScanResourceFolders(scanPath, courseFolder.Path, checkForVideos: true);

        return Ok(new ResourceScanDto { Files = files, Folders = folders });
    }

    [HttpGet("{courseId}/Resources")]
    public ActionResult GetResource(
        [FromRoute] Guid courseId,
        [FromQuery] string path,
        [FromQuery] bool download = false,
        [FromQuery] bool zip = false)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return BadRequest("path is required.");
        }

        // Reject path traversal attempts early.
        if (path.Contains("..", StringComparison.Ordinal))
        {
            return BadRequest("Invalid path.");
        }

        var item = _libraryManager.GetItemById(courseId);
        if (item is not Folder folder || !IsCoursePath(folder.Path))
        {
            return NotFound("Course not found.");
        }

        var courseRoot = folder.Path;
        var fullPath = Path.GetFullPath(Path.Combine(courseRoot, path));

        // Verify the resolved path is under the course root (trailing separator prevents sibling match).
        if (!fullPath.StartsWith(courseRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(fullPath, courseRoot, StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("Invalid path.");
        }

        // Directory → zip download.
        if (Directory.Exists(fullPath))
        {
            if (!zip)
            {
                return BadRequest("Use zip=true for directory downloads.");
            }

            var folderName = Path.GetFileName(fullPath);
            Response.Headers.ContentDisposition = $"attachment; filename=\"{folderName}.zip\"";
            return new FileCallbackResult("application/zip", (stream, _) =>
            {
                using var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true);
                foreach (var file in Directory.EnumerateFiles(fullPath, "*", SearchOption.AllDirectories))
                {
                    var entryName = Path.GetRelativePath(fullPath, file).Replace('\\', '/');
                    var entry = archive.CreateEntry(entryName, CompressionLevel.Fastest);
                    using var entryStream = entry.Open();
                    using var fileStream = System.IO.File.OpenRead(file);
                    fileStream.CopyTo(entryStream);
                }
                return Task.CompletedTask;
            });
        }

        // File → serve directly.
        if (!System.IO.File.Exists(fullPath))
        {
            return NotFound("File not found.");
        }

        var contentType = GetContentType(Path.GetExtension(fullPath));
        var fileName = Path.GetFileName(fullPath);

        if (download)
        {
            return PhysicalFile(fullPath, contentType, fileName);
        }

        return PhysicalFile(fullPath, contentType);
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

    private static string GetContentType(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".svg" => "image/svg+xml",
            ".webp" => "image/webp",
            ".bmp" => "image/bmp",
            ".py" or ".js" or ".ts" or ".java" or ".cs" or ".sh" or ".rb" or ".go" or ".rs"
                or ".c" or ".cpp" or ".h" or ".css" or ".xml" or ".sql"
                or ".md" or ".markdown" or ".txt" => "text/plain",
            ".json" => "application/json",
            ".yml" or ".yaml" => "text/yaml",
            ".zip" => "application/zip",
            ".tar" => "application/x-tar",
            ".gz" => "application/gzip",
            ".rar" => "application/vnd.rar",
            ".7z" => "application/x-7z-compressed",
            _ => "application/octet-stream",
        };
    }

    private static List<ResourceFileDto> ScanResourceFiles(string directoryPath, string basePath)
    {
        var resources = new List<ResourceFileDto>();
        if (!Directory.Exists(directoryPath))
        {
            return resources;
        }

        try
        {
            foreach (var filePath in Directory.EnumerateFiles(directoryPath))
            {
                var fileName = Path.GetFileName(filePath);
                var ext = Path.GetExtension(filePath);

                if (_junkFileNames.Contains(fileName) || _junkExtensions.Contains(ext)
                    || _subtitleExtensions.Contains(ext))
                {
                    continue;
                }

                if (Courses.VideoExtensions.All.Contains(ext))
                {
                    // .ts is ambiguous: MPEG Transport Stream (video) vs TypeScript (code).
                    // MPEG-TS has 0x47 sync byte at 188-byte intervals. Checking two
                    // positions avoids false positives (e.g. TypeScript starting with 'G' = 0x47).
                    if (string.Equals(ext, ".ts", StringComparison.OrdinalIgnoreCase))
                    {
                        var buf = new byte[189];
                        using var probe = System.IO.File.OpenRead(filePath);
                        var bytesRead = probe.Read(buf, 0, buf.Length);
                        var isMpegTs = bytesRead >= 189
                            ? buf[0] == 0x47 && buf[188] == 0x47
                            : bytesRead >= 1 && buf[0] == 0x47;
                        if (bytesRead == 0 || isMpegTs)
                        {
                            continue; // MPEG-TS or empty — skip
                        }
                        // Not MPEG-TS → TypeScript source, fall through to include as resource
                    }
                    else
                    {
                        continue;
                    }
                }

                var relativePath = Path.GetRelativePath(basePath, filePath).Replace('\\', '/');
                resources.Add(new ResourceFileDto
                {
                    Name = fileName,
                    RelativePath = relativePath,
                    Extension = ext.ToLowerInvariant(),
                    Size = new FileInfo(filePath).Length,
                });
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            // Permission errors or race conditions — return what we have.
        }

        return resources.OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// Recursively scan subdirectories for resource folders.
    /// When <paramref name="checkForVideos"/> is true (top-level call), directories that
    /// contain video files are skipped — they're video sections, not resource folders.
    /// Once a directory is confirmed as a resource folder, its children are scanned
    /// without the video check.
    /// </summary>
    private static List<ResourceFolderDto> ScanResourceFolders(string scanRoot, string basePath, bool checkForVideos)
    {
        var folders = new List<ResourceFolderDto>();
        if (!Directory.Exists(scanRoot))
        {
            return folders;
        }

        try
        {
            foreach (var dirPath in Directory.EnumerateDirectories(scanRoot))
            {
                if (checkForVideos)
                {
                    var hasVideo = ScanResourceFiles_HasVideo(dirPath);
                    if (hasVideo)
                    {
                        continue;
                    }
                }

                var files = ScanResourceFiles(dirPath, basePath);
                var subFolders = ScanResourceFolders(dirPath, basePath, checkForVideos: false);
                if (files.Count == 0 && subFolders.Count == 0)
                {
                    continue;
                }

                var relativePath = Path.GetRelativePath(basePath, dirPath).Replace('\\', '/');
                folders.Add(new ResourceFolderDto
                {
                    Name = Path.GetFileName(dirPath),
                    RelativePath = relativePath,
                    Files = files,
                    SubFolders = subFolders,
                });
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            // Permission errors or race conditions — return what we have.
        }

        return folders.OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// Check if a directory subtree contains any unambiguous video files.
    /// Uses a single-pass enumeration that short-circuits on first match.
    /// </summary>
    private static bool ScanResourceFiles_HasVideo(string dirPath)
    {
        try
        {
            return Directory.EnumerateFiles(dirPath, "*", SearchOption.AllDirectories)
                .Any(f => Courses.VideoExtensions.Unambiguous.Contains(Path.GetExtension(f)));
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            return false;
        }
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

public class ResourceScanDto
{
    public List<ResourceFileDto> Files { get; set; } = [];

    public List<ResourceFolderDto> Folders { get; set; } = [];
}

public class ResourceFileDto
{
    public string Name { get; set; } = string.Empty;

    public string RelativePath { get; set; } = string.Empty;

    public string Extension { get; set; } = string.Empty;

    public long Size { get; set; }
}

public class ResourceFolderDto
{
    public string Name { get; set; } = string.Empty;

    public string RelativePath { get; set; } = string.Empty;

    public List<ResourceFileDto> Files { get; set; } = [];

    public List<ResourceFolderDto> SubFolders { get; set; } = [];
}

public class FileCallbackResult : ActionResult
{
    private readonly string _contentType;
    private readonly Func<Stream, CancellationToken, Task> _callback;

    public FileCallbackResult(string contentType, Func<Stream, CancellationToken, Task> callback)
    {
        _contentType = contentType;
        _callback = callback;
    }

    public override async Task ExecuteResultAsync(ActionContext context)
    {
        var response = context.HttpContext.Response;
        response.ContentType = _contentType;
        // ZipArchive uses synchronous writes internally.
        var syncIoFeature = context.HttpContext.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpBodyControlFeature>();
        if (syncIoFeature != null)
        {
            syncIoFeature.AllowSynchronousIO = true;
        }

        await _callback(response.Body, context.HttpContext.RequestAborted);
    }
}
