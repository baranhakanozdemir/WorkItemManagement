using WorkItemManagement.Core.Entities;
using WorkItemManagement.Core.Planning;
using WorkItemManagement.Core.Repositories;

namespace WorkItemManagement.Core.Services;

public class WbsAuthoringBufferService : IWbsAuthoringBufferService
{
    private readonly IWbsAuthoringBufferRepository _repository;
    private readonly TimeProvider _timeProvider;

    public WbsAuthoringBufferService(IWbsAuthoringBufferRepository repository)
        : this(repository, TimeProvider.System)
    {
    }

    public WbsAuthoringBufferService(IWbsAuthoringBufferRepository repository, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _repository = repository;
        _timeProvider = timeProvider;
    }

    public async Task<WbsStructurePayload?> GetAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        var buffer = await _repository.GetByProjectAsync(projectId, cancellationToken).ConfigureAwait(false);
        if (buffer is null)
        {
            return null;
        }

        // A body that cannot be read back is reported as absent rather than thrown. The buffer is
        // authoring state, and the honest answer to "what has been proposed" for an unreadable row
        // is "nothing usable" — throwing here would make a stale row block every later read.
        return WbsStructurePayloadReader.TryRead(buffer.PayloadJson, out var payload, out _, out _)
            ? payload
            : null;
    }

    public async Task<WbsAuthoringBufferSnapshot?> GetSnapshotAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        var buffer = await _repository.GetByProjectAsync(projectId, cancellationToken).ConfigureAwait(false);
        return buffer is null ? null : Describe(buffer);
    }

    public async Task<WbsAuthoringBufferSnapshot> ReplaceAsync(
        Guid projectId,
        WbsStructurePayload payload,
        string authoredBy,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentException.ThrowIfNullOrWhiteSpace(authoredBy);
        if (projectId == Guid.Empty)
        {
            throw new ArgumentException("Project id is required.", nameof(projectId));
        }

        // Normalized on the way in by the same rules the reader applies, so a proposal cannot mean
        // one thing on write and another on read.
        var normalized = WbsStructurePayloadReader.Normalize(payload);
        var json = WbsStructurePayloadWriter.Write(normalized);
        var now = _timeProvider.GetUtcNow();

        var existing = await _repository.GetByProjectAsync(projectId, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            var created = new WbsAuthoringBuffer(authoredBy)
            {
                ProjectId = projectId,
                PayloadJson = json,
                NodeCount = normalized.Nodes?.Count ?? 0,
                AuthoredBy = authoredBy,
                AuthoredAt = now,
            };

            var stored = await _repository.AddAsync(created, cancellationToken).ConfigureAwait(false);
            return Describe(stored ?? created);
        }

        existing.PayloadJson = json;
        existing.NodeCount = normalized.Nodes?.Count ?? 0;
        existing.AuthoredBy = authoredBy;
        existing.AuthoredAt = now;
        existing.SetUpdate(authoredBy);

        var updated = await _repository.UpdateAsync(existing, cancellationToken).ConfigureAwait(false);
        return Describe(updated ?? existing);
    }

    public Task<bool> DeleteAsync(Guid projectId, CancellationToken cancellationToken = default) =>
        _repository.DeleteByProjectAsync(projectId, cancellationToken);

    private static WbsAuthoringBufferSnapshot Describe(WbsAuthoringBuffer buffer) =>
        new(buffer.ProjectId, buffer.NodeCount, buffer.AuthoredBy, buffer.AuthoredAt);
}
