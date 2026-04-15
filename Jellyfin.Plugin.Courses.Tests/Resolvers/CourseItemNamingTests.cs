using Jellyfin.Plugin.Courses.Resolvers;

namespace Jellyfin.Plugin.Courses.Tests.Resolvers;

public class CourseItemNamingTests
{
    [Theory]
    [InlineData("24. Technique - Vibrato", 24)]
    [InlineData("7. Our first song", 7)]
    [InlineData("11 - Kafka Wikimedia", 11)]
    [InlineData("01 - Getting Started", 1)]
    [InlineData("02 - Docker Images & Containers", 2)]
    [InlineData("001-About-this-course-pbc2-onehack.us.mp4", 1)]
    [InlineData("002-Introduction-DSMH-onehack.us.mp4", 2)]
    [InlineData("01-Variables.mp4", 1)]
    [InlineData("lesson10.mp4", 10)]
    [InlineData("lesson1.mp4", 1)]
    [InlineData("Chapter 1  Introduction", 1)]
    [InlineData("Chapter 8  Kubernetes Implementation", 8)]
    [InlineData("No Number Here", null)]
    [InlineData("Advanced Scala 3 and Functional Programming.txt", null)]
    public void ParseSortIndex_ExtractsCorrectIndex(string name, int? expected)
    {
        var result = CourseItemNaming.ParseSortIndex(name);
        Assert.Equal(expected, result);
    }
}
