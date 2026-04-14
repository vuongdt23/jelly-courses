using System;
using Jellyfin.Plugin.Courses.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.Courses;

public class Plugin : BasePlugin<PluginConfiguration>
{
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    public override string Name => "Courses";

    public override Guid Id => new("a1b2c3d4-e5f6-7890-abcd-ef1234567890");

    public static Plugin? Instance { get; private set; }
}
