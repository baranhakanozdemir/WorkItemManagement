using DomainServices.Core.Responses;
using DomainServices.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using WorkItemManagement.Core.Services;
using WorkItemManagement.Core.Entities;
using WorkItemManagement.Core;
using WorkItemManagement.Core.Repositories;

namespace WorkItemManagement.Core.Services;

public class WorkItemService : AuditedDomainService<WorkItem>, IWorkItemService
{
    private const string DeliveryEvidenceRequiredCode = "work-item.transition.delivery-evidence-required";
    private const string DeliveryEvidenceRequiredMessage =
        "Work item cannot transition to Done without a verified passing delivery commit reference or a recorded manual delivery exception.";

    // docs/22 Part 2: linear progression. Index gap between current and target tells us
    // whether a transition skipped intermediate states.
    private static readonly WorkItemState[] LifecycleOrder =
    [
        WorkItemState.Backlog,
        WorkItemState.ToDo,
        WorkItemState.Doing,
        WorkItemState.Testing,
        WorkItemState.Review,
        WorkItemState.Done,
    ];

    private readonly IWorkItemRepository _workItemRepository;
    private readonly IWorkItemCommitRefService? _commitRefs;
    private readonly IWorkItemBlockerService? _blockers;
    private readonly IWorkItemStateSync _stateSync;
    private readonly IWorkItemCompletionOverride _completionOverride;
    private readonly ILogger<WorkItemService> _logger;

    public WorkItemService(IWorkItemRepository repository, IAuditWriter auditWriter)
        : this(repository, auditWriter, NullLogger<WorkItemService>.Instance)
    {
    }

    public WorkItemService(
        IWorkItemRepository repository,
        IAuditWriter auditWriter,
        ILogger<WorkItemService> logger)
        : this(repository, auditWriter, logger, NullWorkItemStateSync.Instance)
    {
    }

    public WorkItemService(
        IWorkItemRepository repository,
        IAuditWriter auditWriter,
        ILogger<WorkItemService> logger,
        IWorkItemStateSync stateSync)
        : base(repository, auditWriter)
    {
        _workItemRepository = repository;
        _stateSync = stateSync;
        _completionOverride = NullWorkItemCompletionOverride.Instance;
        _logger = logger;
    }

    /// <summary>
    /// Fully-wired constructor for hosts that supply commit-ref evidence, blockers,
    /// external state write-back, and an out-of-band completion override.
    /// </summary>
    public WorkItemService(
        IWorkItemRepository repository,
        IAuditWriter auditWriter,
        ILogger<WorkItemService> logger,
        IWorkItemStateSync stateSync,
        IWorkItemCommitRefService? commitRefs,
        IWorkItemBlockerService? blockers,
        IWorkItemCompletionOverride completionOverride)
        : this(repository, auditWriter, logger, stateSync)
    {
        _commitRefs = commitRefs;
        _blockers = blockers;
        _completionOverride = completionOverride;
    }

    public WorkItemService(
        IWorkItemRepository repository,
        IAuditWriter auditWriter,
        IWorkItemBlockerService? blockers)
        : this(repository, auditWriter, NullLogger<WorkItemService>.Instance, NullWorkItemStateSync.Instance)
    {
        _blockers = blockers;
    }

    public WorkItemService(
        IWorkItemRepository repository,
        IAuditWriter auditWriter,
        IWorkItemCommitRefService commitRefs,
        IWorkItemBlockerService? blockers)
        : this(repository, auditWriter, NullLogger<WorkItemService>.Instance, NullWorkItemStateSync.Instance)
    {
        _commitRefs = commitRefs;
        _blockers = blockers;
    }

    public Task<IReadOnlyCollection<WorkItem>> GetAllByProjectAsync(Guid projectId, CancellationToken cancellationToken = default) =>
        _workItemRepository.GetAllByProjectAsync(projectId, cancellationToken);

    public async Task<IReadOnlyList<WorkItem>> ListByProjectAsync(
        Guid projectId,
        WorkItemState? state = null,
        WorkItemType? type = null,
        Guid? assignedTo = null,
        bool? blocked = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        if (projectId == Guid.Empty)
        {
            return [];
        }

        if (state is { } stateValue && !Enum.IsDefined(stateValue))
        {
            throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown work item state.");
        }

        if (type is { } typeValue && !Enum.IsDefined(typeValue))
        {
            throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown work item type.");
        }

        var safePage = Math.Max(page, 1);
        var safePageSize = Math.Clamp(pageSize, 1, 200);
        return await _workItemRepository.ListByProjectAsync(
            projectId,
            state,
            type,
            assignedTo,
            blocked,
            safePage,
            safePageSize,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<WorkItem>> GetStartableByProjectAsync(
        Guid projectId,
        Guid? assignedTo = null,
        int maxPerAssignee = 1,
        CancellationToken cancellationToken = default)
    {
        if (projectId == Guid.Empty)
        {
            return [];
        }

        var safeMaxPerAssignee = Math.Clamp(maxPerAssignee, 1, 50);
        var items = (await _workItemRepository
            .GetAllByProjectAsync(projectId, cancellationToken)
            .ConfigureAwait(false))
            .Where(item => !item.IsDeleted)
            .ToArray();
        var itemsById = items.ToDictionary(item => item.Id);
        var blockersByBlockedItem = (await GetProjectBlockersAsync(projectId, items, cancellationToken).ConfigureAwait(false))
            .Where(blocker => !blocker.IsDeleted)
            .GroupBy(blocker => blocker.BlockedItemId)
            .ToDictionary(group => group.Key, group => group.ToArray());

        var startable = items
            .Where(IsStartableExecutionItem)
            .Where(item => assignedTo is null || item.AssignedToId == assignedTo)
            .Where(item => !HasActiveBlocker(item, blockersByBlockedItem, itemsById))
            .OrderBy(StartableOrderKey, StringComparer.Ordinal)
            .ThenBy(item => item.Title, StringComparer.Ordinal)
            .ThenBy(item => item.Id)
            .ToArray();

        return startable
            .GroupBy(item => item.AssignedToId ?? Guid.Empty)
            .OrderBy(group => group.Key)
            .SelectMany(group => group.Take(safeMaxPerAssignee))
            .OrderBy(StartableOrderKey, StringComparer.Ordinal)
            .ThenBy(item => item.Title, StringComparer.Ordinal)
            .ThenBy(item => item.Id)
            .ToArray();
    }

    public Task<WorkItem?> GetByProjectAsync(Guid projectId, Guid id, CancellationToken cancellationToken = default) =>
        _workItemRepository.GetByProjectAsync(projectId, id, cancellationToken);

    public Task<IReadOnlyList<WorkItem>> GetSubtreeByProjectAsync(Guid projectId, Guid id, CancellationToken cancellationToken = default) =>
        _workItemRepository.GetSubtreeAsync(id, projectId, cancellationToken);

    public async Task<IBaseResponse<WorkItem>> UpdateByProjectAsync(
        Guid projectId,
        Guid id,
        WorkItem model,
        string actor,
        CancellationToken cancellationToken = default)
    {
        if (projectId == Guid.Empty)
            return BaseResponse<WorkItem>.BadRequest("projectId is required.");
        if (id == Guid.Empty)
            return BaseResponse<WorkItem>.BadRequest("id is required.");
        if (string.IsNullOrWhiteSpace(actor))
            return BaseResponse<WorkItem>.BadRequest("actor is required.");
        ArgumentNullException.ThrowIfNull(model);
        if (id != model.Id)
            return BaseResponse<WorkItem>.BadRequest("Route id must match model id.");

        var existing = await _workItemRepository.GetByProjectAsync(projectId, id, cancellationToken)
            .ConfigureAwait(false);
        if (existing is null)
            return BaseResponse<WorkItem>.NotFound("WorkItem not found.");

        if (model.State == WorkItemState.Done
            && existing.State != WorkItemState.Done
            && !await HasCompletionEvidenceAsync(projectId, id, cancellationToken).ConfigureAwait(false))
        {
            return BaseResponse<WorkItem>.BadRequest(DeliveryEvidenceRequiredMessage);
        }

        model.ProjectId = projectId;
        model.EnterpriseId = existing.EnterpriseId;
        model.Created = existing.Created;
        model.CreatedBy = existing.CreatedBy;
        model.IsDeleted = existing.IsDeleted;

        var validation = model.Validate();
        if (!validation.IsValid)
            return BaseResponse<WorkItem>.BadRequest(validation.ErrorMessages);

        return await UpdateAsync(id, model, actor, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IBaseResponse> DeleteByProjectAsync(
        Guid projectId,
        Guid id,
        string actor,
        CancellationToken cancellationToken = default)
    {
        if (projectId == Guid.Empty)
            return BaseResponse.BadRequest("projectId is required.");
        if (id == Guid.Empty)
            return BaseResponse.BadRequest("id is required.");
        if (string.IsNullOrWhiteSpace(actor))
            return BaseResponse.BadRequest("actor is required.");

        var existing = await _workItemRepository.GetByProjectAsync(projectId, id, cancellationToken)
            .ConfigureAwait(false);
        if (existing is null)
            return BaseResponse.NotFound("WorkItem not found.");

        return await DeleteAsync(id, actor, cancellationToken).ConfigureAwait(false);
    }

    public async Task<WorkItemMutationResult> CreateWithWarningsAsync(
        Guid enterpriseId,
        WorkItem model,
        string userName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);

        // AC: priority defaults to Medium when unset. The entity default and CLR default both
        // land here; callers that omit Priority get Medium via `WorkItem.Priority` initializer.

        var warnings = ValidateHierarchy(model, parent: await ResolveParentAsync(model, cancellationToken).ConfigureAwait(false));
        LogHierarchyWarnings(model.Id, warnings);

        var response = await AddAsync(enterpriseId, model, userName, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessful || response.Data is null)
        {
            // Validation / persistence failure on create — surface, do not pretend "missing".
            throw new InvalidOperationException(
                $"Failed to create work item {model.Id}: {response.Message ?? "no message"}");
        }

        return WorkItemMutationResult.Success(response.Data, warnings);
    }

    public async Task<WorkItemMutationResult> TransitionAsync(
        Guid workItemId,
        WorkItemState to,
        string userName,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(to))
        {
            throw new ArgumentOutOfRangeException(nameof(to), to, "Unknown work item state.");
        }

        var item = await _workItemRepository.GetAsync(workItemId, cancellationToken).ConfigureAwait(false);
        if (item is null)
        {
            return WorkItemMutationResult.Missing;
        }

        var from = item.State;
        if (IsTerminal(from))
        {
            // docs/23 WS9: Done and Cancelled are terminal — block silently with a warning so the
            // UI can explain why the state did not change without throwing for a normal user action.
            var blocked = new[]
            {
                new WorkItemValidationWarning(
                    "work-item.transition.terminal",
                    $"Work item is already in terminal state '{from}'. Transition to '{to}' rejected."),
            };
            return new WorkItemMutationResult(item, blocked);
        }

        if (from == to)
        {
            return WorkItemMutationResult.Success(item);
        }

        if (to != WorkItemState.Cancelled && IsBackwardTransition(from, to))
        {
            var blocked = new[]
            {
                new WorkItemValidationWarning(
                    "work-item.transition.backward",
                    $"Work item transition from '{from}' to earlier state '{to}' rejected."),
            };
            return new WorkItemMutationResult(item, blocked);
        }

        var warnings = new List<WorkItemValidationWarning>();
        if (to == WorkItemState.Done
            && !await HasCompletionEvidenceAsync(item.ProjectId, item.Id, cancellationToken).ConfigureAwait(false))
        {
            warnings.Add(new WorkItemValidationWarning(
                DeliveryEvidenceRequiredCode,
                DeliveryEvidenceRequiredMessage));
            return new WorkItemMutationResult(item, warnings);
        }

        if (to != WorkItemState.Cancelled && SkippedIntermediates(from, to) is { Length: > 0 } skipped)
        {
            // AC: skipping states is allowed (do not block) but logged at Warning level.
            _logger.LogWarning(
                "WorkItem {WorkItemId} transitioned from {FromState} to {ToState}, skipping intermediates: {Skipped}",
                workItemId,
                from,
                to,
                string.Join(", ", skipped));
            warnings.Add(new WorkItemValidationWarning(
                "work-item.transition.skipped-states",
                $"Transition from '{from}' to '{to}' skipped intermediate state(s): {string.Join(", ", skipped)}."));
        }

        item.State = to;
        var response = await UpdateAsync(item.Id, item, userName, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessful || response.Data is null)
        {
            // Persistence / validation error on a known-existing item — surface it.
            // Genuine missing-row races would have been caught by the GetAsync above.
            throw new InvalidOperationException(
                $"Failed to transition work item {workItemId} from {from} to {to}: {response.Message ?? "no message"}");
        }

        await TryWriteBackStateAsync(response.Data, userName, cancellationToken).ConfigureAwait(false);

        return new WorkItemMutationResult(response.Data, warnings);
    }

    public async Task<WorkItemMutationResult> TransitionByProjectAsync(
        Guid projectId,
        Guid workItemId,
        WorkItemState to,
        string userName,
        CancellationToken cancellationToken = default)
    {
        var scopedItem = await _workItemRepository.GetByProjectAsync(projectId, workItemId, cancellationToken).ConfigureAwait(false);
        if (scopedItem is null)
        {
            return WorkItemMutationResult.Missing;
        }

        return await TransitionAsync(workItemId, to, userName, cancellationToken).ConfigureAwait(false);
    }

    private void LogHierarchyWarnings(Guid workItemId, IReadOnlyList<WorkItemValidationWarning> warnings)
    {
        // Per docs/23 WS9: every hierarchy violation logs at Warning level so the
        // platform retains operational visibility even when a client ignores the
        // returned warnings or aggregates them silently.
        foreach (var warning in warnings)
        {
            _logger.LogWarning(
                "WorkItem {WorkItemId} hierarchy warning {WarningCode}: {WarningMessage}",
                workItemId,
                warning.Code,
                warning.Message);
        }
    }

    private async Task<WorkItem?> ResolveParentAsync(WorkItem model, CancellationToken cancellationToken)
    {
        if (model.ParentId is not { } parentId)
        {
            return null;
        }

        // Parent must be in the same project; rely on repository filter so cross-project
        // ParentIds (a bug elsewhere) do not silently satisfy hierarchy validation.
        return await _workItemRepository.GetByProjectAsync(model.ProjectId, parentId, cancellationToken).ConfigureAwait(false);
    }

    private static IReadOnlyList<WorkItemValidationWarning> ValidateHierarchy(WorkItem model, WorkItem? parent)
    {
        // docs/22 Part 2 table — warn but never block. A parent that could not be resolved
        // (deleted, wrong project) is its own warning; a present parent is checked against
        // the type-specific expectations.
        var warnings = new List<WorkItemValidationWarning>();

        if (model.ParentId is not null && parent is null)
        {
            warnings.Add(new WorkItemValidationWarning(
                "work-item.hierarchy.parent-not-found",
                $"ParentId '{model.ParentId}' does not resolve to a work item in project '{model.ProjectId}'."));
        }

        switch (model.Type)
        {
            case WorkItemType.Epic:
                if (model.ParentId is not null)
                {
                    warnings.Add(new WorkItemValidationWarning(
                        "work-item.hierarchy.epic-has-parent",
                        "Epic work items are root-level and should not have a parent."));
                }
                break;

            case WorkItemType.Feature:
                if (parent is not null && parent.Type != WorkItemType.Epic)
                {
                    warnings.Add(new WorkItemValidationWarning(
                        "work-item.hierarchy.feature-parent-not-epic",
                        $"Feature parent should be an Epic; found '{parent.Type}'."));
                }
                break;

            case WorkItemType.UserStory:
                if (parent is not null
                    && (parent.Type == WorkItemType.Task || parent.Type == WorkItemType.Bug))
                {
                    warnings.Add(new WorkItemValidationWarning(
                        "work-item.hierarchy.story-parent-task-or-bug",
                        $"UserStory parent should be Feature or Epic; found '{parent.Type}'."));
                }
                break;

            case WorkItemType.Task:
                if (parent is not null && parent.Type == WorkItemType.Task)
                {
                    warnings.Add(new WorkItemValidationWarning(
                        "work-item.hierarchy.task-parent-task",
                        "Task parent should be UserStory or Feature; found another Task."));
                }
                break;

            case WorkItemType.Bug:
                // Bug accepts any parent type by design.
                break;
        }

        return warnings;
    }

    private static bool IsTerminal(WorkItemState state) =>
        state is WorkItemState.Done or WorkItemState.Cancelled;

    private async Task<IReadOnlyCollection<WorkItemBlocker>> GetProjectBlockersAsync(
        Guid projectId,
        IReadOnlyCollection<WorkItem> items,
        CancellationToken cancellationToken)
    {
        if (_blockers is not null)
        {
            return await _blockers.GetAllByProjectAsync(projectId, cancellationToken).ConfigureAwait(false);
        }

        return items
            .SelectMany(item => item.Blockers)
            .Where(blocker => blocker.ProjectId == projectId)
            .ToArray();
    }

    private static bool IsStartableExecutionItem(WorkItem item) =>
        item.Type is WorkItemType.Task or WorkItemType.Bug
        && item.AssignedToId is not null
        && item.State is WorkItemState.Backlog or WorkItemState.ToDo;

    private static bool HasActiveBlocker(
        WorkItem item,
        IReadOnlyDictionary<Guid, WorkItemBlocker[]> blockersByBlockedItem,
        IReadOnlyDictionary<Guid, WorkItem> itemsById)
    {
        if (!blockersByBlockedItem.TryGetValue(item.Id, out var blockers))
        {
            return false;
        }

        return blockers.Any(blocker => IsActiveBlocker(blocker, itemsById));
    }

    private static bool IsActiveBlocker(
        WorkItemBlocker blocker,
        IReadOnlyDictionary<Guid, WorkItem> itemsById) =>
        blocker.Kind switch
        {
            BlockerKind.WorkItem when blocker.BlockerItemId is { } blockerItemId =>
                !itemsById.TryGetValue(blockerItemId, out var blockerItem)
                || blockerItem.State != WorkItemState.Done,
            BlockerKind.WorkItem => true,
            BlockerKind.DecisionGate or BlockerKind.External => true,
            _ => true,
        };

    private static string StartableOrderKey(WorkItem item) =>
        item.WbsKey ?? string.Empty;

    private static bool IsBackwardTransition(WorkItemState from, WorkItemState to)
    {
        var fromIndex = Array.IndexOf(LifecycleOrder, from);
        var toIndex = Array.IndexOf(LifecycleOrder, to);
        return fromIndex >= 0 && toIndex >= 0 && toIndex < fromIndex;
    }

    private static WorkItemState[] SkippedIntermediates(WorkItemState from, WorkItemState to)
    {
        var fromIndex = Array.IndexOf(LifecycleOrder, from);
        var toIndex = Array.IndexOf(LifecycleOrder, to);
        if (fromIndex < 0 || toIndex < 0 || toIndex <= fromIndex + 1)
        {
            // Backward moves, single-step forward moves, and any state not on the linear path
            // (only Cancelled, which the caller has already filtered out) are not "skips".
            return Array.Empty<WorkItemState>();
        }

        return LifecycleOrder[(fromIndex + 1)..toIndex];
    }

    private async Task<bool> HasCompletionEvidenceAsync(
        Guid projectId,
        Guid workItemId,
        CancellationToken cancellationToken)
    {
        // An out-of-band manual completion override (e.g. a human-approved delivery
        // exception) satisfies the gate on its own, even with no commit evidence.
        if (await _completionOverride.HasForWorkItemAsync(projectId, workItemId, cancellationToken).ConfigureAwait(false))
        {
            return true;
        }

        if (_commitRefs is null)
        {
            return false;
        }

        var refs = await _commitRefs.GetAllByProjectAsync(projectId, cancellationToken).ConfigureAwait(false);
        return refs.Any(reference => reference.WorkItemId == workItemId && IsValidCompletionEvidence(reference));
    }

    private static bool IsValidCompletionEvidence(WorkItemCommitRef reference)
    {
        if (string.IsNullOrWhiteSpace(reference.CommitSha)
            || string.IsNullOrWhiteSpace(reference.Repository)
            || reference.BuildVerifiedAt is null
            || (reference.PullRequestNumber is null && string.IsNullOrWhiteSpace(reference.PullRequestUrl)))
        {
            return false;
        }

        return string.Equals(reference.BuildStatus, "Passing", StringComparison.OrdinalIgnoreCase);
    }

    private async System.Threading.Tasks.Task TryWriteBackStateAsync(
        WorkItem workItem,
        string actor,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _stateSync
                .WriteBackStateAsync(workItem, actor, cancellationToken)
                .ConfigureAwait(false);
            if (!result.Succeeded)
            {
                _logger.LogWarning(
                    "Work-management state write-back failed for work item {WorkItemId}: {Error}",
                    workItem.Id,
                    result.Error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Work-management state write-back threw for work item {WorkItemId}.",
                workItem.Id);
        }
    }

    public async Task<WorkItem?> AddByProjectAsync(Guid projectId, WorkItem model, CancellationToken cancellationToken = default)
    {
        model.ProjectId = projectId;
        var result = await AddAsync(Guid.Empty, model, model.CreatedBy ?? "system", cancellationToken);
        return result.Data;
    }
}
