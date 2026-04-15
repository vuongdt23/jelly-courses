using MediaBrowser.Controller.Entities;

namespace Jellyfin.Plugin.Courses.Model;

public class CourseSection : Folder
{
    public int SortIndex { get; set; }

    public override string GetClientTypeName() => "Folder";

    public override bool IsDisplayedAsFolder => true;

    public override bool SupportsDateLastMediaAdded => false;
}
