using DomainServices.Core.Query;
using WorkItemManagement.Core;
using WorkItemManagement.Core.Repositories;

namespace WorkItemManagement.Core.Tests;

using Task = System.Threading.Tasks.Task;

/// <summary>
/// An in-memory <see cref="IWorkItemRepository"/> that keeps the instance it is given: the getters
/// return the stored object itself, so a service that mutates what it read changes
/// <see cref="Stored"/> without saving anything. A test that needs to prove a write happened
/// asserts on <see cref="Updates"/>, the state each item had when <see cref="UpdateAsync"/> was
/// called.
/// </summary>
internal sealed class RecordingWorkItemRepository : IWorkItemRepository
{
    private readonly Dictionary<Guid, WorkItem> _rows = new();
    private readonly List<(Guid Id, WorkItemState State)> _updates = new();

    public WorkItem Seed(WorkItem item)
    {
        _rows[item.Id] = item;
        return item;
    }

    public WorkItem Stored(Guid id) => _rows[id];

    /// <summary>Every <see cref="UpdateAsync"/> call, with the item's state at that moment.</summary>
    public IReadOnlyList<(Guid Id, WorkItemState State)> Updates => _updates;

    public Task<WorkItem?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_rows.TryGetValue(id, out var item) ? item : null);

    public Task<WorkItem?> GetByProjectAsync(Guid projectId, Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_rows.TryGetValue(id, out var item) && item.ProjectId == projectId ? item : null);

    public Task<WorkItem> UpdateAsync(WorkItem model, CancellationToken cancellationToken = default)
    {
        _updates.Add((model.Id, model.State));
        _rows[model.Id] = model;
        return Task.FromResult(model);
    }

    public Task<WorkItem> AddAsync(WorkItem model, CancellationToken cancellationToken = default)
    {
        _rows[model.Id] = model;
        return Task.FromResult(model);
    }

    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_rows.ContainsKey(id));

    public Task<IReadOnlyList<WorkItem>> GetAllAsync(
        Guid enterpriseId,
        QueryParameterModel? query = null,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<WorkItem>>(_rows.Values.ToList());

    public Task<IReadOnlyList<WorkItem>> SearchAllAsync(
        Guid enterpriseId,
        string searchTerm,
        QueryParameterModel? query = null,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_rows.Remove(id));

    public Task<int> SaveAsync(IEnumerable<WorkItem> models, CancellationToken cancellationToken = default)
    {
        var count = 0;
        foreach (var model in models)
        {
            _rows[model.Id] = model;
            count++;
        }

        return Task.FromResult(count);
    }

    public Task<IReadOnlyCollection<WorkItem>> GetAllByProjectAsync(Guid projectId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyCollection<WorkItem>>(_rows.Values.Where(item => item.ProjectId == projectId).ToList());

    public Task<IReadOnlyList<WorkItem>> ListByProjectAsync(
        Guid projectId,
        WorkItemState? state = null,
        WorkItemType? type = null,
        Guid? assignedTo = null,
        bool? blocked = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<WorkItem?> UpdateByProjectAsync(Guid projectId, WorkItem model, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<bool> DeleteByProjectAsync(Guid projectId, Guid id, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<WorkItem>> GetSubtreeAsync(Guid rootId, Guid projectId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<WorkItem>> GetByStateAsync(WorkItemState state, Guid projectId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<WorkItem>> GetBlockedAsync(Guid projectId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<WorkItem>> GetByRequirementAsync(Guid requirementId, Guid projectId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}
