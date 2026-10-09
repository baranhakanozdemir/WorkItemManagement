using DomainServices.Core.Query;
using WorkItemManagement.Core;
using WorkItemManagement.Core.Repositories;

namespace WorkItemManagement.Core.Tests;

using Task = System.Threading.Tasks.Task;

/// <summary>
/// Holds its own copy of each row, so the stored item and the caller's model are never the same
/// object — a test that shares one instance would pass whatever the service did.
/// </summary>
internal sealed class RecordingWorkItemRepository : IWorkItemRepository
{
    private readonly Dictionary<Guid, WorkItem> _rows = new();

    public WorkItem Seed(WorkItem item)
    {
        _rows[item.Id] = item;
        return item;
    }

    public WorkItem Stored(Guid id) => _rows[id];

    public Task<WorkItem?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_rows.TryGetValue(id, out var item) ? item : null);

    public Task<WorkItem?> GetByProjectAsync(Guid projectId, Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_rows.TryGetValue(id, out var item) && item.ProjectId == projectId ? item : null);

    public Task<WorkItem> UpdateAsync(WorkItem model, CancellationToken cancellationToken = default)
    {
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
