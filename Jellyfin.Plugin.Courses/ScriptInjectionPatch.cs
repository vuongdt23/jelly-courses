using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Jellyfin.Plugin.Courses;

/// <summary>
/// Callback invoked by the FileTransformation plugin to inject the client script
/// into index.html responses in memory — no filesystem writes needed.
/// </summary>
public static class ScriptInjectionPatch
{
    private const string ScriptTag =
        "<script plugin=\"Courses\" defer src=\"/Courses/client.js\"></script>";

    public static string InjectScript(PatchRequestPayload payload)
    {
        var html = payload.Contents ?? string.Empty;

        if (html.Contains("plugin=\"Courses\"", StringComparison.OrdinalIgnoreCase))
        {
            return html;
        }

        return Regex.Replace(
            html,
            @"(</body>)",
            ScriptTag + "\n$1",
            RegexOptions.IgnoreCase);
    }
}

public class PatchRequestPayload
{
    [JsonPropertyName("contents")]
    public string? Contents { get; set; }
}
