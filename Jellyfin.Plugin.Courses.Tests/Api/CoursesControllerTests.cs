using Jellyfin.Database.Implementations;
using Jellyfin.Plugin.Courses.Api;
using Jellyfin.Plugin.Courses.Model;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using System.Reflection;

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
    public void ScanResourceFiles_IncludesHtmlFiles()
    {
        var tempDir = Directory.CreateTempSubdirectory();
        try
        {
            var htmlPath = Path.Combine(tempDir.FullName, "lesson-notes.html");
            File.WriteAllText(htmlPath, "<html><body>notes</body></html>");

            var method = typeof(CoursesController).GetMethod(
                "ScanResourceFiles",
                BindingFlags.NonPublic | BindingFlags.Static);

            Assert.NotNull(method);

            var result = Assert.IsType<List<ResourceFileDto>>(method!.Invoke(null, [tempDir.FullName, tempDir.FullName]));

            Assert.Contains(result, file => file.Name == "lesson-notes.html");
        }
        finally
        {
            tempDir.Delete(true);
        }
    }

    [Theory]
    [InlineData(".html")]
    [InlineData(".htm")]
    public void GetContentType_HtmlExtensions_ReturnTextHtml(string extension)
    {
        var method = typeof(CoursesController).GetMethod(
            "GetContentType",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);

        var result = Assert.IsType<string>(method!.Invoke(null, [extension]));

        Assert.Equal("text/html", result);
    }
}
