namespace WorkItemManagement.Core;

public class Feature : WorkItem, IFeature
{
    public Feature()
    {
        Type = WorkItemType.Feature;
    }

    public Feature(string createdBy)
        : base(createdBy)
    {
        Type = WorkItemType.Feature;
    }
}
