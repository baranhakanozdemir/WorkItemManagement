namespace WorkItemManagement.Core;

public interface IUserStory : IWorkItem
{
    /// <summary>
    /// #2461: the story's definition of done — at least one condition, never null and never
    /// whitespace. docs/22 specifies it as "the acceptance-criteria expression derived from the
    /// requirements' fit criteria"; it is a rendered view of the story node's
    /// <c>WbsNodeAcceptanceCriterion</c> rows, which stay authoritative because they carry order and
    /// provenance.
    /// </summary>
    string AcceptanceCriteria { get; set; }
}
