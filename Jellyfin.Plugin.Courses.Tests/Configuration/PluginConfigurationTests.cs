using Jellyfin.Plugin.Courses.Configuration;

namespace Jellyfin.Plugin.Courses.Tests.Configuration;

public class PluginConfigurationTests
{
    [Fact]
    public void GetCourseLibraryPathSet_DefaultEmpty_ReturnsEmptySet()
    {
        var config = new PluginConfiguration();
        var result = config.GetCourseLibraryPathSet();
        Assert.Empty(result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null!)]
    public void GetCourseLibraryPathSet_BlankOrNull_ReturnsEmptySet(string? value)
    {
        var config = new PluginConfiguration { CourseLibraryPaths = value! };
        var result = config.GetCourseLibraryPathSet();
        Assert.Empty(result);
    }

    [Fact]
    public void GetCourseLibraryPathSet_SinglePath_ReturnsSingleItem()
    {
        var config = new PluginConfiguration { CourseLibraryPaths = "/media/courses" };
        var result = config.GetCourseLibraryPathSet();
        Assert.Single(result);
        Assert.Contains("/media/courses", result);
    }

    [Fact]
    public void GetCourseLibraryPathSet_MultiplePaths_ReturnsAll()
    {
        var config = new PluginConfiguration { CourseLibraryPaths = "/media/courses,/media/test-courses" };
        var result = config.GetCourseLibraryPathSet();
        Assert.Equal(2, result.Count);
        Assert.Contains("/media/courses", result);
        Assert.Contains("/media/test-courses", result);
    }

    [Fact]
    public void GetCourseLibraryPathSet_TrimsWhitespace()
    {
        var config = new PluginConfiguration { CourseLibraryPaths = "  /media/courses , /media/test  " };
        var result = config.GetCourseLibraryPathSet();
        Assert.Equal(2, result.Count);
        Assert.Contains("/media/courses", result);
        Assert.Contains("/media/test", result);
    }

    [Fact]
    public void GetCourseLibraryPathSet_IgnoresEmptyEntries()
    {
        var config = new PluginConfiguration { CourseLibraryPaths = "/media/courses,,, /media/test" };
        var result = config.GetCourseLibraryPathSet();
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void GetCourseLibraryPathSet_CaseInsensitive()
    {
        var config = new PluginConfiguration { CourseLibraryPaths = "/Media/Courses" };
        var result = config.GetCourseLibraryPathSet();
        Assert.Contains("/media/courses", result);
        Assert.Contains("/MEDIA/COURSES", result);
    }
}
