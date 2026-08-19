namespace WorkItemManagement.Core;

public class Epic : WorkItem, IEpic
{
    public Epic()
    {
        Type = WorkItemType.Epic;
    }

    public Epic(string createdBy)
        : base(createdBy)
    {
        Type = WorkItemType.Epic;
    }
}
