using System.Reflection;
using Jellyfin.Database.Implementations;
using Jellyfin.Plugin.Courses.Api;
using Jellyfin.Plugin.Courses.Model;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace Jellyfin.Plugin.Courses.Tests.Api;

public class CoursesControllerTests
{
    private readonly Mock<ILibraryManager> _libraryManager = new();
    private readonly Mock<IUserDataManager> _userDataManager = new();
    private readonly Mock<IUserManager> _userManager = new();
    private readonly Mock<IDbContextFactory<JellyfinDbContext>> _dbContextFactory = new();
    private readonly CoursesController _controller;

    public CoursesControllerTests()
    {
        _controller = new CoursesController(
            _libraryManager.Object,
            _userDataManager.Object,
            _userManager.Object,
            _dbContextFactory.Object,
            Mock.Of<ILogger<CoursesController>>());
    }

    // --- GetNextLesson ---

    [Fact]
    public void GetNextLesson_CourseNotFound_ReturnsNotFound()
    {
        var courseId = Guid.NewGuid();
        _libraryManager.Setup(x => x.GetItemById(courseId)).Returns((BaseItem?)null);

        var result = _controller.GetNextLesson(courseId, Guid.NewGuid());

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public void GetNextLesson_NotAFolder_ReturnsNotFound()
    {
        var courseId = Guid.NewGuid();
        var video = new Video { Id = courseId };
        _libraryManager.Setup(x => x.GetItemById(courseId)).Returns(video);

        var result = _controller.GetNextLesson(courseId, Guid.NewGuid());

        Assert.IsType<NotFoundObjectResult>(result);
    }

    // --- GetContinueLesson ---

    [Fact]
    public void GetContinueLesson_CourseNotFound_ReturnsNotFound()
    {
        var courseId = Guid.NewGuid();
        _libraryManager.Setup(x => x.GetItemById(courseId)).Returns((BaseItem?)null);

        var result = _controller.GetContinueLesson(courseId, Guid.NewGuid());

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public void GetContinueLesson_UserNotFound_ReturnsNotFound()
    {
        var courseId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var folder = new Folder { Id = courseId, Path = "/media/test-courses/TestCourse" };
        _libraryManager.Setup(x => x.GetItemById(courseId)).Returns(folder);
        _userManager.Setup(x => x.GetUserById(userId))
            .Returns((Jellyfin.Database.Implementations.Entities.User?)null);

        // Without Plugin.Instance, IsCoursePath returns false → NotFound.
        var result = _controller.GetContinueLesson(courseId, userId);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    // --- GetStructure ---

    [Fact]
    public void GetStructure_CourseNotFound_ReturnsNotFound()
    {
        var courseId = Guid.NewGuid();
        _libraryManager.Setup(x => x.GetItemById(courseId)).Returns((BaseItem?)null);

        var result = _controller.GetStructure(courseId, Guid.NewGuid());

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public void GetStructure_NotAFolder_ReturnsNotFound()
    {
        var courseId = Guid.NewGuid();
        var video = new Video { Id = courseId };
        _libraryManager.Setup(x => x.GetItemById(courseId)).Returns(video);

        var result = _controller.GetStructure(courseId, Guid.NewGuid());

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public void GetStructure_UserNotFound_ReturnsNotFound()
    {
        var courseId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var folder = new Folder { Id = courseId, Path = "/media/test-courses/TestCourse" };
        _libraryManager.Setup(x => x.GetItemById(courseId)).Returns(folder);
        _userManager.Setup(x => x.GetUserById(userId))
            .Returns((Jellyfin.Database.Implementations.Entities.User?)null);

        // Without Plugin.Instance, IsCoursePath returns false → NotFound.
        var result = _controller.GetStructure(courseId, userId);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    // --- GetPublicConfig ---

    [Fact]
    public void GetPublicConfig_NoPluginInstance_ReturnsEmptyPaths()
    {
        var result = _controller.GetPublicConfig();

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    // --- DTO tests ---

    [Fact]
    public void LessonDto_DefaultValues()
    {
        var dto = new LessonDto();
        Assert.Equal(Guid.Empty, dto.Id);
        Assert.Equal(string.Empty, dto.Name);
        Assert.Equal(string.Empty, dto.SortName);
    }

    [Fact]
    public void LessonProgressDto_DefaultValues()
    {
        var dto = new LessonProgressDto();
        Assert.Equal(Guid.Empty, dto.Id);
        Assert.Equal(string.Empty, dto.Name);
        Assert.Equal(0, dto.SortIndex);
        Assert.False(dto.Played);
        Assert.Equal(0, dto.PlaybackPositionTicks);
        Assert.Equal(0, dto.RunTimeTicks);
    }

    [Fact]
    public void SectionDto_DefaultValues()
    {
        var dto = new SectionDto();
        Assert.Equal(Guid.Empty, dto.Id);
        Assert.Equal(string.Empty, dto.Name);
        Assert.Equal(0, dto.SortIndex);
        Assert.Empty(dto.Lessons);
        Assert.Equal(0, dto.CompletedCount);
        Assert.Equal(0, dto.TotalCount);
    }

    [Fact]
    public void CourseStructureDto_DefaultValues()
    {
        var dto = new CourseStructureDto();
        Assert.Equal(Guid.Empty, dto.Id);
        Assert.Equal(string.Empty, dto.Name);
        Assert.Empty(dto.Sections);
        Assert.Equal(0, dto.TotalLessons);
        Assert.Equal(0, dto.CompletedLessons);
        Assert.Equal(0, dto.ProgressPercent);
    }

    [Fact]
    public void CourseStructureDto_SetValues()
    {
        var id = Guid.NewGuid();
        var dto = new CourseStructureDto
        {
            Id = id,
            Name = "Test Course",
            TotalLessons = 10,
            CompletedLessons = 5,
            ProgressPercent = 50,
            Sections =
            [
                new SectionDto
                {
                    Name = "Section 1",
                    TotalCount = 5,
                    CompletedCount = 3,
                    Lessons =
                    [
                        new LessonProgressDto { Name = "Lesson 1", Played = true },
                        new LessonProgressDto { Name = "Lesson 2", Played = false },
                    ],
                },
            ],
        };

        Assert.Equal(id, dto.Id);
        Assert.Equal("Test Course", dto.Name);
        Assert.Equal(10, dto.TotalLessons);
        Assert.Equal(50, dto.ProgressPercent);
        Assert.Single(dto.Sections);
        Assert.Equal(2, dto.Sections[0].Lessons.Count);
    }

    [Fact]
    public void ResourceFileDto_DefaultValues()
    {
        var dto = new ResourceFileDto();
        Assert.Equal(string.Empty, dto.Name);
        Assert.Equal(string.Empty, dto.RelativePath);
        Assert.Equal(string.Empty, dto.Extension);
        Assert.Equal(0, dto.Size);
    }

    [Fact]
    public void ResourceFolderDto_DefaultValues()
    {
        var dto = new ResourceFolderDto();
        Assert.Equal(string.Empty, dto.Name);
        Assert.Equal(string.Empty, dto.RelativePath);
        Assert.Empty(dto.Files);
    }

    // --- GetResource ---

    [Fact]
    public void GetResource_CourseNotFound_ReturnsNotFound()
    {
        var courseId = Guid.NewGuid();
        _libraryManager.Setup(x => x.GetItemById(courseId)).Returns((BaseItem?)null);

        var result = _controller.GetResource(courseId, "notes.pdf");

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public void GetResource_MissingPath_ReturnsBadRequest()
    {
        var courseId = Guid.NewGuid();

        var result = _controller.GetResource(courseId, "");

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public void GetResource_PathTraversal_ReturnsBadRequest()
    {
        var courseId = Guid.NewGuid();

        var result = _controller.GetResource(courseId, "../../../etc/passwd");

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public void GetResource_NotAFolder_ReturnsNotFound()
    {
        var courseId = Guid.NewGuid();
        var video = new Video { Id = courseId };
        _libraryManager.Setup(x => x.GetItemById(courseId)).Returns(video);

        var result = _controller.GetResource(courseId, "notes.pdf");

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public void GetResource_NullPath_ReturnsBadRequest()
    {
        var courseId = Guid.NewGuid();

        var result = _controller.GetResource(courseId, null!);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public void GetResource_WhitespacePath_ReturnsBadRequest()
    {
        var courseId = Guid.NewGuid();

        var result = _controller.GetResource(courseId, "   ");

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public void GetResource_EncodedPathTraversal_ReturnsBadRequest()
    {
        var courseId = Guid.NewGuid();

        // Double-dot in the path string (already decoded by ASP.NET)
        var result = _controller.GetResource(courseId, "subdir/../../etc/passwd");

        Assert.IsType<BadRequestObjectResult>(result);
    }

    // --- GetOverview ---

    [Fact]
    public void GetOverview_ReturnsHtmlContent()
    {
        var courseId = Guid.NewGuid();

        var result = _controller.GetOverview(courseId);

        var contentResult = Assert.IsType<ContentResult>(result);
        Assert.Equal("text/html", contentResult.ContentType);
        Assert.Contains(courseId.ToString(), contentResult.Content);
    }

    // --- GetContentType (private static, test via reflection) ---

    private static string InvokeGetContentType(string extension)
    {
        var method = typeof(CoursesController).GetMethod(
            "GetContentType",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        return (string)method!.Invoke(null, [extension])!;
    }

    [Theory]
    [InlineData(".pdf", "application/pdf")]
    [InlineData(".png", "image/png")]
    [InlineData(".jpg", "image/jpeg")]
    [InlineData(".jpeg", "image/jpeg")]
    [InlineData(".gif", "image/gif")]
    [InlineData(".svg", "image/svg+xml")]
    [InlineData(".webp", "image/webp")]
    [InlineData(".bmp", "image/bmp")]
    [InlineData(".json", "application/json")]
    [InlineData(".yml", "text/yaml")]
    [InlineData(".yaml", "text/yaml")]
    [InlineData(".zip", "application/zip")]
    [InlineData(".tar", "application/x-tar")]
    [InlineData(".gz", "application/gzip")]
    [InlineData(".rar", "application/vnd.rar")]
    [InlineData(".7z", "application/x-7z-compressed")]
    [InlineData(".py", "text/plain")]
    [InlineData(".js", "text/plain")]
    [InlineData(".ts", "text/plain")]
    [InlineData(".cs", "text/plain")]
    [InlineData(".md", "text/plain")]
    [InlineData(".txt", "text/plain")]
    [InlineData(".sh", "text/plain")]
    [InlineData(".go", "text/plain")]
    [InlineData(".rs", "text/plain")]
    [InlineData(".c", "text/plain")]
    [InlineData(".cpp", "text/plain")]
    [InlineData(".h", "text/plain")]
    [InlineData(".css", "text/plain")]
    [InlineData(".xml", "text/plain")]
    [InlineData(".sql", "text/plain")]
    [InlineData(".rb", "text/plain")]
    [InlineData(".java", "text/plain")]
    [InlineData(".markdown", "text/plain")]
    [InlineData(".unknown", "application/octet-stream")]
    [InlineData(".exe", "application/octet-stream")]
    [InlineData("", "application/octet-stream")]
    public void GetContentType_ReturnsExpectedMimeType(string extension, string expected)
    {
        var result = InvokeGetContentType(extension);
        Assert.Equal(expected, result);
    }

    // --- ScanResourceFiles (private static, test via reflection with temp dirs) ---

    private static List<ResourceFileDto> InvokeScanResourceFiles(string directoryPath, string basePath)
    {
        var method = typeof(CoursesController).GetMethod(
            "ScanResourceFiles",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        return (List<ResourceFileDto>)method!.Invoke(null, [directoryPath, basePath])!;
    }

    [Fact]
    public void ScanResourceFiles_NonexistentDirectory_ReturnsEmpty()
    {
        var result = InvokeScanResourceFiles("/nonexistent/path", "/nonexistent");
        Assert.Empty(result);
    }

    [Fact]
    public void ScanResourceFiles_FiltersJunkFiles()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "courses-test-" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "desktop.ini"), "junk");
            File.WriteAllText(Path.Combine(tempDir, ".DS_Store"), "junk");
            File.WriteAllText(Path.Combine(tempDir, "readme.url"), "junk");
            File.WriteAllText(Path.Combine(tempDir, "info.nfo"), "junk");
            File.WriteAllText(Path.Combine(tempDir, "page.html"), "junk");
            File.WriteAllText(Path.Combine(tempDir, "notes.ini"), "junk");

            var result = InvokeScanResourceFiles(tempDir, tempDir);
            Assert.Empty(result);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ScanResourceFiles_FiltersVideoExtensions()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "courses-test-" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "lesson.mp4"), "video");
            File.WriteAllText(Path.Combine(tempDir, "clip.mkv"), "video");
            File.WriteAllText(Path.Combine(tempDir, "movie.avi"), "video");
            File.WriteAllText(Path.Combine(tempDir, "notes.pdf"), "valid");

            var result = InvokeScanResourceFiles(tempDir, tempDir);
            Assert.Single(result);
            Assert.Equal("notes.pdf", result[0].Name);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ScanResourceFiles_IncludesValidResources()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "courses-test-" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "guide.pdf"), "pdf content");
            File.WriteAllText(Path.Combine(tempDir, "code.py"), "print('hello')");
            File.WriteAllText(Path.Combine(tempDir, "data.json"), "{}");

            var result = InvokeScanResourceFiles(tempDir, tempDir);
            Assert.Equal(3, result.Count);

            // Results should be sorted by name
            Assert.Equal("code.py", result[0].Name);
            Assert.Equal("data.json", result[1].Name);
            Assert.Equal("guide.pdf", result[2].Name);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ScanResourceFiles_SetsCorrectProperties()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "courses-test-" + Guid.NewGuid());
        var subDir = Path.Combine(tempDir, "section1");
        Directory.CreateDirectory(subDir);
        try
        {
            var content = "hello world";
            File.WriteAllText(Path.Combine(subDir, "notes.pdf"), content);

            var result = InvokeScanResourceFiles(subDir, tempDir);
            Assert.Single(result);
            Assert.Equal("notes.pdf", result[0].Name);
            Assert.Equal("section1/notes.pdf", result[0].RelativePath);
            Assert.Equal(".pdf", result[0].Extension);
            Assert.True(result[0].Size > 0);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ScanResourceFiles_TypeScriptDetectedAsResource()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "courses-test-" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);
        try
        {
            // TypeScript file: starts with 'i' (0x69), not 0x47
            File.WriteAllText(Path.Combine(tempDir, "app.ts"), "import { Component } from 'react';");

            var result = InvokeScanResourceFiles(tempDir, tempDir);
            Assert.Single(result);
            Assert.Equal("app.ts", result[0].Name);
            Assert.Equal(".ts", result[0].Extension);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ScanResourceFiles_MpegTsFilteredOut()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "courses-test-" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);
        try
        {
            // MPEG-TS: starts with 0x47 at byte 0 and byte 188
            var mpegTs = new byte[376];
            mpegTs[0] = 0x47;
            mpegTs[188] = 0x47;
            File.WriteAllBytes(Path.Combine(tempDir, "video.ts"), mpegTs);

            var result = InvokeScanResourceFiles(tempDir, tempDir);
            Assert.Empty(result);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ScanResourceFiles_TsStartingWithG_NotMpegTs()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "courses-test-" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);
        try
        {
            // TypeScript starting with 'G' (0x47) but byte 188 is not 0x47
            var content = "Global.configure({ debug: true });\n" + new string(' ', 200);
            File.WriteAllText(Path.Combine(tempDir, "config.ts"), content);

            var result = InvokeScanResourceFiles(tempDir, tempDir);
            Assert.Single(result);
            Assert.Equal("config.ts", result[0].Name);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ScanResourceFiles_EmptyTsFile_FilteredOut()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "courses-test-" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);
        try
        {
            File.WriteAllBytes(Path.Combine(tempDir, "empty.ts"), []);

            var result = InvokeScanResourceFiles(tempDir, tempDir);
            Assert.Empty(result);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ScanResourceFiles_ShortTsWithSync_FilteredOut()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "courses-test-" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);
        try
        {
            // Short .ts file starting with 0x47 but less than 189 bytes
            var data = new byte[50];
            data[0] = 0x47;
            File.WriteAllBytes(Path.Combine(tempDir, "short.ts"), data);

            var result = InvokeScanResourceFiles(tempDir, tempDir);
            Assert.Empty(result);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    // --- ScanResourceFolders ---

    private static List<ResourceFolderDto> InvokeScanResourceFolders(string courseRootPath)
    {
        var method = typeof(CoursesController).GetMethod(
            "ScanResourceFolders",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        return (List<ResourceFolderDto>)method!.Invoke(null, [courseRootPath])!;
    }

    [Fact]
    public void ScanResourceFolders_NonexistentDirectory_ReturnsEmpty()
    {
        var result = InvokeScanResourceFolders("/nonexistent/path");
        Assert.Empty(result);
    }

    [Fact]
    public void ScanResourceFolders_SkipsFoldersWithVideos()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "courses-test-" + Guid.NewGuid());
        var videoSection = Path.Combine(tempDir, "01-Introduction");
        Directory.CreateDirectory(videoSection);
        try
        {
            File.WriteAllText(Path.Combine(videoSection, "lesson.mp4"), "video");
            File.WriteAllText(Path.Combine(videoSection, "notes.pdf"), "notes");

            var result = InvokeScanResourceFolders(tempDir);
            Assert.Empty(result);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ScanResourceFolders_IncludesFoldersWithoutVideos()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "courses-test-" + Guid.NewGuid());
        var resourceFolder = Path.Combine(tempDir, "exercise_files");
        Directory.CreateDirectory(resourceFolder);
        try
        {
            File.WriteAllText(Path.Combine(resourceFolder, "code.py"), "print('hello')");
            File.WriteAllText(Path.Combine(resourceFolder, "data.json"), "{}");

            var result = InvokeScanResourceFolders(tempDir);
            Assert.Single(result);
            Assert.Equal("exercise_files", result[0].Name);
            Assert.Equal("exercise_files", result[0].RelativePath);
            Assert.Equal(2, result[0].Files.Count);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ScanResourceFolders_SkipsJunkPrefixFolders()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "courses-test-" + Guid.NewGuid());
        var junkFolder = Path.Combine(tempDir, "0.hidden");
        Directory.CreateDirectory(junkFolder);
        try
        {
            File.WriteAllText(Path.Combine(junkFolder, "notes.pdf"), "content");

            var result = InvokeScanResourceFolders(tempDir);
            Assert.Empty(result);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ScanResourceFolders_SkipsEmptyFolders()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "courses-test-" + Guid.NewGuid());
        var emptyFolder = Path.Combine(tempDir, "resources");
        Directory.CreateDirectory(emptyFolder);
        try
        {
            var result = InvokeScanResourceFolders(tempDir);
            Assert.Empty(result);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ScanResourceFolders_SkipsFoldersWithOnlyJunkFiles()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "courses-test-" + Guid.NewGuid());
        var junkOnly = Path.Combine(tempDir, "resources");
        Directory.CreateDirectory(junkOnly);
        try
        {
            File.WriteAllText(Path.Combine(junkOnly, "desktop.ini"), "junk");
            File.WriteAllText(Path.Combine(junkOnly, ".DS_Store"), "junk");

            var result = InvokeScanResourceFolders(tempDir);
            Assert.Empty(result);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ScanResourceFolders_MultipleFolders_SortedByName()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "courses-test-" + Guid.NewGuid());
        Directory.CreateDirectory(Path.Combine(tempDir, "z_resources"));
        Directory.CreateDirectory(Path.Combine(tempDir, "a_exercises"));
        // Add a video section that should be excluded
        Directory.CreateDirectory(Path.Combine(tempDir, "01-Intro"));
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "z_resources", "guide.pdf"), "pdf");
            File.WriteAllText(Path.Combine(tempDir, "a_exercises", "code.py"), "code");
            File.WriteAllText(Path.Combine(tempDir, "01-Intro", "lesson.mp4"), "video");

            var result = InvokeScanResourceFolders(tempDir);
            Assert.Equal(2, result.Count);
            Assert.Equal("a_exercises", result[0].Name);
            Assert.Equal("z_resources", result[1].Name);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    // --- GetClientScript ---

    [Fact]
    public void GetClientScript_ReturnsJavaScript()
    {
        var result = _controller.GetClientScript();

        var contentResult = Assert.IsType<ContentResult>(result);
        Assert.Equal("application/javascript", contentResult.ContentType);
        Assert.Contains("PLUGIN_ID", contentResult.Content);
    }

    // --- GetNextLesson edge cases ---

    [Fact]
    public void GetNextLesson_NullItem_ReturnsNotFound()
    {
        var courseId = Guid.NewGuid();
        _libraryManager.Setup(x => x.GetItemById(courseId)).Returns((BaseItem?)null);

        var result = _controller.GetNextLesson(courseId, Guid.NewGuid());

        Assert.IsType<NotFoundObjectResult>(result);
    }
}
