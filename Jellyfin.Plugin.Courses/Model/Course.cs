using MediaBrowser.Controller.Entities;

namespace Jellyfin.Plugin.Courses.Model;

public class Course : Folder
{
    public override string GetClientTypeName() => "Folder";

    public override bool IsDisplayedAsFolder => true;

    public override bool SupportsDateLastMediaAdded => false;
}
