namespace WorkItemManagement.Core;

public class Bug : WorkItem, IBug
{
    public const int MaxCausedByCommitRefLength = 200;

    public Guid? CausedByWorkItemId { get; set; }

    public string? CausedByCommitRef { get; set; }

    public Bug()
    {
        Type = WorkItemType.Bug;
    }

    public Bug(string createdBy)
        : base(createdBy)
    {
        Type = WorkItemType.Bug;
    }

    protected override void OnValidate(DomainServices.Core.Validation.ModelValidator validator)
    {
        base.OnValidate(validator);
        validator.Require(
            CausedByCommitRef is null || CausedByCommitRef.Length <= MaxCausedByCommitRefLength,
            nameof(CausedByCommitRef),
            $"Caused-by commit reference must be {MaxCausedByCommitRefLength} characters or fewer.");
    }
}
