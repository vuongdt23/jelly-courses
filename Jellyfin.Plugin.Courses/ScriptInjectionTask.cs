using System.Reflection;
using System.Runtime.Loader;
using System.Text.RegularExpressions;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

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
        if (TryRegisterFileTransformation())
        {
            return Task.CompletedTask;
        }

        _logger.LogInformation("Courses: FileTransformation plugin not found, falling back to direct file injection");
        FallbackFileInjection();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Register with the FileTransformation plugin via reflection.
    /// Returns true if the plugin was found and registration succeeded.
    /// </summary>
    private bool TryRegisterFileTransformation()
    {
        var assembly = AssemblyLoadContext.All
            .SelectMany(x => x.Assemblies)
            .FirstOrDefault(x => x.FullName?.Contains(".FileTransformation") ?? false);

        if (assembly is null)
        {
            return false;
        }

        var pluginInterface = assembly.GetType("Jellyfin.Plugin.FileTransformation.PluginInterface");
        var registerMethod = pluginInterface?.GetMethod("RegisterTransformation");

        if (registerMethod is null)
        {
            _logger.LogWarning("Courses: FileTransformation assembly found but RegisterTransformation method missing");
            return false;
        }

        var payload = new JObject
        {
            ["id"] = Plugin.Instance!.Id.ToString(),
            ["fileNamePattern"] = "index.html",
            ["callbackAssembly"] = GetType().Assembly.FullName,
            ["callbackClass"] = typeof(ScriptInjectionPatch).FullName,
            ["callbackMethod"] = nameof(ScriptInjectionPatch.InjectScript),
        };

        registerMethod.Invoke(null, [payload]);
        _logger.LogInformation("Courses: Registered index.html transformation via FileTransformation plugin");
        return true;
    }

    /// <summary>
    /// Fallback: directly modify the index.html file on disk.
    /// </summary>
    private void FallbackFileInjection()
    {
        var indexPath = Path.Combine(_appPaths.WebPath, "index.html");

        if (!File.Exists(indexPath))
        {
            _logger.LogWarning("Courses: index.html not found at {Path}, skipping script injection", indexPath);
            return;
        }

        var content = File.ReadAllText(indexPath);

        if (content.Contains("plugin=\"Courses\""))
        {
            _logger.LogInformation("Courses: Script tag already present in index.html");
            return;
        }

        content = Regex.Replace(content, "<script[^>]*plugin=\"Courses\"[^>]*></script>", string.Empty);

        var bodyClose = content.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase);
        if (bodyClose < 0)
        {
            _logger.LogWarning("Courses: Could not find </body> in index.html");
            return;
        }

        content = content.Insert(bodyClose, ScriptTag + "\n");

        try
        {
            File.WriteAllText(indexPath, content);
            _logger.LogInformation("Courses: Injected client script into index.html");
        }
        catch (UnauthorizedAccessException)
        {
            _logger.LogError(
                "Courses: Permission denied writing to {Path}. "
                + "Install the FileTransformation plugin for non-destructive injection, "
                + "or fix with: sudo chmod o+w \"{Path}\"",
                indexPath, indexPath);
        }
    }
}
