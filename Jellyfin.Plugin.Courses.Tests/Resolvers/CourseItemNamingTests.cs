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

    [Theory]
    [InlineData("[FreeCourseSite.com] Udemy - Hibernate and Spring Data JPA Beginner to Guru", "Hibernate and Spring Data JPA Beginner to Guru")]
    [InlineData("[ WebToolTip.com ] Udemy - Openstack Masterclass 2025", "Openstack Masterclass 2025")]
    [InlineData("[GigaCourse.Com] Udemy - The Git & Github Bootcamp", "The Git & Github Bootcamp")]
    [InlineData("[TutsNode.net] - LeetCode in Java", "LeetCode in Java")]
    [InlineData("24. Technique - Vibrato", "Technique - Vibrato")]
    [InlineData("7. Our first song", "Our first song")]
    [InlineData("01 - Getting Started", "Getting Started")]
    [InlineData("02 - Docker Images & Containers", "Docker Images & Containers")]
    [InlineData("001-About-this-course-pbc2-onehack.us.mp4", "About this course")]
    [InlineData("002-Introduction-DSMH-onehack.us.mp4", "Introduction")]
    [InlineData("lesson10.mp4", "Lesson 10")]
    [InlineData("lesson1.mp4", "Lesson 1")]
    [InlineData("Chapter 1  Introduction", "Introduction")]
    [InlineData("Chapter 8  Kubernetes Implementation", "Kubernetes Implementation")]
    [InlineData("Docker & Kubernetes - The Practical Guide 2025 Jan Update", "Docker & Kubernetes - The Practical Guide 2025 Jan Update")]
    [InlineData("Become a Violin Master from Scratch", "Become a Violin Master from Scratch")]
    [InlineData("1. Twinkle Twinkle Little Star - Main theme.mp4", "Twinkle Twinkle Little Star - Main theme")]
    public void CleanName_ProducesExpectedOutput(string name, string expected)
    {
        var result = CourseItemNaming.CleanName(name);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(1, 1, "0001-0001")]
    [InlineData(1, 2, "0001-0002")]
    [InlineData(2, 1, "0002-0001")]
    [InlineData(0, 5, "0000-0005")]
    [InlineData(10, 99, "0010-0099")]
    public void BuildSortName_FormatsCorrectly(int section, int lesson, string expected)
    {
        Assert.Equal(expected, CourseItemNaming.BuildSortName(section, lesson));
    }

    [Fact]
    public void BuildSortName_LexicographicOrder_CrossesSections()
    {
        var names = new[]
        {
            CourseItemNaming.BuildSortName(2, 1),
            CourseItemNaming.BuildSortName(1, 2),
            CourseItemNaming.BuildSortName(1, 1),
            CourseItemNaming.BuildSortName(2, 2),
        };

        var sorted = names.OrderBy(n => n).ToArray();

        Assert.Equal("0001-0001", sorted[0]);
        Assert.Equal("0001-0002", sorted[1]);
        Assert.Equal("0002-0001", sorted[2]);
        Assert.Equal("0002-0002", sorted[3]);
    }
}
