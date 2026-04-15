using Jellyfin.Plugin.Courses.Model;
using Jellyfin.Plugin.Courses.Resolvers;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.IO;
using Microsoft.Extensions.Logging;
using Moq;

namespace Jellyfin.Plugin.Courses.Tests.Resolvers;

public class CourseResolverTests
{
    private readonly CourseResolver _resolver;
    private readonly Mock<IServerApplicationPaths> _appPaths = new();
    private readonly Mock<ILibraryManager> _libraryManager = new();

    public CourseResolverTests()
    {
        _resolver = new CourseResolver(Mock.Of<ILogger<CourseResolver>>());
    }

    private ItemResolveArgs CreateArgs(string path, bool isDirectory, Folder? parent = null)
    {
        var args = new ItemResolveArgs(_appPaths.Object, _libraryManager.Object)
        {
            Parent = parent,
            FileInfo = new FileSystemMetadata
            {
                FullName = path,
                IsDirectory = isDirectory,
            },
        };
        return args;
    }

    // --- Directory resolution with Course parent ---

    [Fact]
    public void ResolvePath_DirectoryUnderCourse_ReturnsCourseSection()
    {
        var parent = new Course { Name = "Test Course" };
        var args = CreateArgs("/media/courses/TestCourse/01 - Getting Started", true, parent);

        var result = _resolver.ResolvePath(args);

        Assert.NotNull(result);
        Assert.IsType<CourseSection>(result);
        Assert.Equal("1. Getting Started", result.Name);
    }

    [Fact]
    public void ResolvePath_DirectoryUnderCourseSection_ReturnsCourseSection()
    {
        var parent = new CourseSection { Name = "Section" };
        var args = CreateArgs("/media/courses/TestCourse/Section/01 - SubSection", true, parent);

        var result = _resolver.ResolvePath(args);

        Assert.NotNull(result);
        Assert.IsType<CourseSection>(result);
    }

    [Fact]
    public void ResolvePath_JunkDirectory_ReturnsNull()
    {
        var parent = new Course { Name = "Test Course" };
        var args = CreateArgs("/media/courses/TestCourse/0. Websites you may like", true, parent);

        var result = _resolver.ResolvePath(args);

        Assert.Null(result);
    }

    [Fact]
    public void ResolvePath_CourseSectionSortIndex()
    {
        var parent = new Course { Name = "Test Course" };
        var args = CreateArgs("/media/courses/TestCourse/03 - Advanced Topics", true, parent);

        var result = _resolver.ResolvePath(args);

        var section = Assert.IsType<CourseSection>(result);
        Assert.Equal(3, section.SortIndex);
        Assert.Equal("3. Advanced Topics", section.Name);
        Assert.Equal("0003", section.SortName);
    }

    [Fact]
    public void ResolvePath_CourseSectionNoSortIndex_SortIndexZero()
    {
        var parent = new Course { Name = "Test Course" };
        var args = CreateArgs("/media/courses/TestCourse/Bonus Content", true, parent);

        var result = _resolver.ResolvePath(args);

        var section = Assert.IsType<CourseSection>(result);
        Assert.Equal(0, section.SortIndex);
        Assert.Equal("Bonus Content", section.Name);
    }

    // --- File resolution ---

    [Fact]
    public void ResolvePath_VideoFileUnderCourse_ReturnsCourseLesson()
    {
        var parent = new Course { Name = "Test Course" };
        var args = CreateArgs("/media/courses/TestCourse/01 - Introduction.mp4", false, parent);

        var result = _resolver.ResolvePath(args);

        Assert.NotNull(result);
        Assert.IsType<CourseLesson>(result);
        Assert.Equal("1. Introduction", result.Name);
    }

    [Fact]
    public void ResolvePath_VideoFileUnderSection_ReturnsCourseLesson()
    {
        var parent = new CourseSection { Name = "Section", SortIndex = 2 };
        var args = CreateArgs("/media/courses/TestCourse/Section/03 - Details.mkv", false, parent);

        var result = _resolver.ResolvePath(args);

        var lesson = Assert.IsType<CourseLesson>(result);
        Assert.Equal(3, lesson.SortIndex);
        Assert.Equal("3. Details", lesson.Name);
        Assert.Equal("0002-0003", lesson.SortName);
    }

    [Theory]
    [InlineData(".mp4")]
    [InlineData(".mkv")]
    [InlineData(".avi")]
    [InlineData(".webm")]
    [InlineData(".mov")]
    [InlineData(".wmv")]
    [InlineData(".flv")]
    [InlineData(".m4v")]
    [InlineData(".ts")]
    public void ResolvePath_SupportedVideoExtensions_ReturnsCourseLesson(string ext)
    {
        var parent = new Course { Name = "Test" };
        var args = CreateArgs("/media/courses/Test/video" + ext, false, parent);

        var result = _resolver.ResolvePath(args);

        Assert.IsType<CourseLesson>(result);
    }

    [Theory]
    [InlineData("desktop.ini")]
    [InlineData(".ds_store")]
    [InlineData("thumbs.db")]
    public void ResolvePath_JunkFiles_ReturnsNull(string filename)
    {
        var parent = new Course { Name = "Test" };
        var args = CreateArgs("/media/courses/Test/" + filename, false, parent);

        var result = _resolver.ResolvePath(args);

        Assert.Null(result);
    }

    [Theory]
    [InlineData(".txt")]
    [InlineData(".url")]
    [InlineData(".ini")]
    [InlineData(".nfo")]
    [InlineData(".html")]
    [InlineData(".htm")]
    public void ResolvePath_JunkExtensions_ReturnsNull(string ext)
    {
        var parent = new Course { Name = "Test" };
        var args = CreateArgs("/media/courses/Test/file" + ext, false, parent);

        var result = _resolver.ResolvePath(args);

        Assert.Null(result);
    }

    [Fact]
    public void ResolvePath_NonVideoExtension_ReturnsNull()
    {
        var parent = new Course { Name = "Test" };
        var args = CreateArgs("/media/courses/Test/image.png", false, parent);

        var result = _resolver.ResolvePath(args);

        Assert.Null(result);
    }

    [Fact]
    public void ResolvePath_VideoFileNoParentCourseOrSection_ReturnsNull()
    {
        var parent = new Folder();
        var args = CreateArgs("/media/other/video.mp4", false, parent);

        var result = _resolver.ResolvePath(args);

        Assert.Null(result);
    }

    // --- Root level (no Course/CourseSection parent, needs Plugin.Instance) ---

    [Fact]
    public void ResolvePath_NoCourseParent_NoPluginInstance_ReturnsNull()
    {
        // Without Plugin.Instance, IsCourseLibraryPath returns false.
        var parent = new Folder();
        var args = CreateArgs("/media/courses/TestCourse", true, parent);

        var result = _resolver.ResolvePath(args);

        Assert.Null(result);
    }

    [Fact]
    public void Priority_IsPlugin()
    {
        Assert.Equal(MediaBrowser.Controller.Resolvers.ResolverPriority.Plugin, _resolver.Priority);
    }
}
