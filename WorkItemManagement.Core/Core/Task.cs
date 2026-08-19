namespace WorkItemManagement.Core;

public class Task : WorkItem, ITask
{
    public Task()
    {
        Type = WorkItemType.Task;
    }

    public Task(string createdBy)
        : base(createdBy)
    {
        Type = WorkItemType.Task;
    }
}
