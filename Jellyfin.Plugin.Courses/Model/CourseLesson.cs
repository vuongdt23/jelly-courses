using MediaBrowser.Controller.Entities;

namespace Jellyfin.Plugin.Courses.Model;

public class CourseLesson : Video
{
    public int SortIndex { get; set; }

    public override string GetClientTypeName() => "Video";
}
