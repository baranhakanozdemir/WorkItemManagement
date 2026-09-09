
namespace WorkItemManagement.Core.Planning;

/// <summary>
/// #2462, #2629: executable UserStory WBS nodes must carry an explicit judgement that they fit inside one
/// agent session. The rule is judgement-based because only the model has the project context
/// needed to decide whether a story should be split. Tasks become optional sub-items within a story
/// and do not carry session sizing.
/// </summary>
public static class WbsSessionSizingRules
{
    public const string PromptRule =
        "canCompleteInOneAgentSession is the sizing judgement for executable work. Every UserStory "
        + "node must set canCompleteInOneAgentSession to true only when one delivery agent "
        + "can complete it in a single execution session, including implementation, tests, commit, "
        + "push, and pull request. If that is not true, split the story into smaller sibling stories; "
        + "do not emit an oversized story with false. Epics, Features, and Tasks must omit the field.";

    public static IReadOnlyList<string> ValidateGeneratedPayload(WbsStructurePayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var errors = new List<string>();
        foreach (var node in payload.Nodes)
        {
            if (node.Kind == WbsNodeKind.UserStory)
            {
                if (node.CanCompleteInOneAgentSession != true)
                {
                    errors.Add(
                        $"Session sizing missing or failed: {node.Kind} '{node.EmittedKey}' ({node.Title}) "
                        + "must be split until canCompleteInOneAgentSession is true for one agent "
                        + "session.");
                }

                continue;
            }

            if (node.CanCompleteInOneAgentSession is not null)
            {
                errors.Add(
                    $"Session sizing on non-executable node: {node.Kind} '{node.EmittedKey}' ({node.Title}) "
                    + "must omit canCompleteInOneAgentSession. Only UserStory nodes may carry it.");
            }
        }

        return errors;
    }

    public static IReadOnlyList<string> ValidatePersistencePayload(WbsStructurePayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var errors = new List<string>();
        foreach (var node in payload.Nodes)
        {
            if (node.Kind == WbsNodeKind.UserStory)
            {
                if (node.CanCompleteInOneAgentSession is null)
                {
                    errors.Add(
                        $"Session sizing missing: {node.Kind} '{node.EmittedKey}' ({node.Title}) "
                        + "must carry canCompleteInOneAgentSession so review can distinguish "
                        + "certified, oversized, and legacy unknown nodes.");
                }

                continue;
            }

            if (node.CanCompleteInOneAgentSession is not null)
            {
                errors.Add(
                    $"Session sizing on non-executable node: {node.Kind} '{node.EmittedKey}' ({node.Title}) "
                    + "must omit canCompleteInOneAgentSession. Only UserStory nodes may carry it.");
            }
        }

        return errors;
    }

    public static IReadOnlyList<string> CollectReviewIssues(WbsStructurePayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        return payload.Nodes
            .Where(node => node.Kind == WbsNodeKind.UserStory)
            .Where(node => node.CanCompleteInOneAgentSession == false)
            .Select(node =>
                $"Executable node '{node.EmittedKey}' ({node.Title}) is not certified as completable "
                + "by one agent in one session; split it before approval.")
            .ToArray();
    }
}
