using MediaBrowser.Controller.Entities;

namespace Jellyfin.Plugin.Courses.Model;

public class CourseSection : Folder
{
    public int SortIndex { get; set; }

    public override string GetClientTypeName() => "CourseSection";

    public override bool IsDisplayedAsFolder => true;

    public override bool SupportsDateLastMediaAdded => false;
}
