using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Entities;

namespace Jellyfin.Plugin.Courses.Model;

public class CourseSection : Folder
{
    public int SortIndex { get; set; }

    public override string GetClientTypeName() => "Folder";

    public override bool IsDisplayedAsFolder => true;

    public override bool SupportsDateLastMediaAdded => false;

    public override bool SupportsAddingToPlaylist => true;

    public override bool IsPreSorted => true;

    public override MediaType MediaType => MediaType.Video;
}
