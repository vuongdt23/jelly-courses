using MediaBrowser.Common.Configuration;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;
using Moq;

namespace Jellyfin.Plugin.Courses.Tests;

public class ScriptInjectionTaskTests : IDisposable
{
    private readonly string _tempDir;
    private readonly Mock<IApplicationPaths> _appPaths;
    private readonly Mock<ILogger<ScriptInjectionTask>> _logger;

    public ScriptInjectionTaskTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "courses-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _appPaths = new Mock<IApplicationPaths>();
        _appPaths.Setup(x => x.WebPath).Returns(_tempDir);
        _logger = new Mock<ILogger<ScriptInjectionTask>>();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    private ScriptInjectionTask CreateTask() => new(_appPaths.Object, _logger.Object);

    [Fact]
    public void Properties_ReturnExpectedValues()
    {
        var task = CreateTask();
        Assert.Equal("Courses: Inject Client Script", task.Name);
        Assert.Equal("CoursesScriptInjection", task.Key);
        Assert.Equal("Courses", task.Category);
        Assert.NotEmpty(task.Description);
    }

    [Fact]
    public void GetDefaultTriggers_ReturnsStartupTrigger()
    {
        var task = CreateTask();
        var triggers = task.GetDefaultTriggers().ToList();
        Assert.Single(triggers);
        Assert.Equal(TaskTriggerInfoType.StartupTrigger, triggers[0].Type);
    }

    [Fact]
    public async Task ExecuteAsync_NoIndexHtml_DoesNotThrow()
    {
        var task = CreateTask();
        await task.ExecuteAsync(new Progress<double>(), CancellationToken.None);
        // Should log warning but not throw.
    }

    [Fact]
    public async Task ExecuteAsync_AlreadyInjected_DoesNotDuplicate()
    {
        var indexPath = Path.Combine(_tempDir, "index.html");
        var original = "<html><body><script plugin=\"Courses\" defer src=\"/Courses/client.js\"></script></body></html>";
        await File.WriteAllTextAsync(indexPath, original);

        var task = CreateTask();
        await task.ExecuteAsync(new Progress<double>(), CancellationToken.None);

        var result = await File.ReadAllTextAsync(indexPath);
        Assert.Equal(original, result);
    }

    [Fact]
    public async Task ExecuteAsync_InjectsScriptBeforeBodyClose()
    {
        var indexPath = Path.Combine(_tempDir, "index.html");
        await File.WriteAllTextAsync(indexPath, "<html><body><div>content</div></body></html>");

        var task = CreateTask();
        await task.ExecuteAsync(new Progress<double>(), CancellationToken.None);

        var result = await File.ReadAllTextAsync(indexPath);
        Assert.Contains("plugin=\"Courses\"", result);
        Assert.Contains("/Courses/client.js", result);
        // Script should be before </body>.
        var scriptIdx = result.IndexOf("plugin=\"Courses\"", StringComparison.Ordinal);
        var bodyIdx = result.IndexOf("</body>", StringComparison.Ordinal);
        Assert.True(scriptIdx < bodyIdx);
    }

    [Fact]
    public async Task ExecuteAsync_NoBodyClose_DoesNotInject()
    {
        var indexPath = Path.Combine(_tempDir, "index.html");
        var original = "<html><div>no body tag</div></html>";
        await File.WriteAllTextAsync(indexPath, original);

        var task = CreateTask();
        await task.ExecuteAsync(new Progress<double>(), CancellationToken.None);

        var result = await File.ReadAllTextAsync(indexPath);
        Assert.DoesNotContain("plugin=\"Courses\"", result);
    }
}
