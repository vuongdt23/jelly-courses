using System.Text.RegularExpressions;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Courses;

public class ScriptInjectionTask : IScheduledTask
{
    private const string ScriptTag =
        "<script plugin=\"Courses\" defer src=\"/Courses/client.js\"></script>";

    private readonly IApplicationPaths _appPaths;
    private readonly ILogger<ScriptInjectionTask> _logger;

    public ScriptInjectionTask(IApplicationPaths appPaths, ILogger<ScriptInjectionTask> logger)
    {
        _appPaths = appPaths;
        _logger = logger;
    }

    public string Name => "Courses: Inject Client Script";

    public string Key => "CoursesScriptInjection";

    public string Description => "Injects the Courses plugin client script into the Jellyfin web UI.";

    public string Category => "Courses";

    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        return [new TaskTriggerInfo { Type = TaskTriggerInfoType.StartupTrigger }];
    }

    public Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        var indexPath = Path.Combine(_appPaths.WebPath, "index.html");

        if (!File.Exists(indexPath))
        {
            _logger.LogWarning("Courses: index.html not found at {Path}, skipping script injection", indexPath);
            return Task.CompletedTask;
        }

        var content = File.ReadAllText(indexPath);

        if (content.Contains("plugin=\"Courses\""))
        {
            _logger.LogInformation("Courses: Script tag already present in index.html");
            return Task.CompletedTask;
        }

        // Remove any stale variants first.
        content = Regex.Replace(content, "<script[^>]*plugin=\"Courses\"[^>]*></script>", string.Empty);

        var bodyClose = content.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase);
        if (bodyClose < 0)
        {
            _logger.LogWarning("Courses: Could not find </body> in index.html");
            return Task.CompletedTask;
        }

        content = content.Insert(bodyClose, ScriptTag + "\n");
        File.WriteAllText(indexPath, content);

        _logger.LogInformation("Courses: Injected client script into index.html");
        return Task.CompletedTask;
    }
}
