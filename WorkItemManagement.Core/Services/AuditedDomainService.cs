using DomainServices.Core.Models;
using DomainServices.Core.Persistence;
using DomainServices.Core.Responses;

namespace WorkItemManagement.Core.Services;

/// <summary>
/// Audit actions recorded by <see cref="IAuditWriter"/>. Trimmed to the CRUD
/// actions the work-item domain emits.
/// </summary>
public enum AuditAction
{
    Created,
    Updated,
    Deleted,
}

/// <summary>
/// Writes immutable audit log entries. Implementations are append-only.
/// </summary>
public interface IAuditWriter
{
    System.Threading.Tasks.Task WriteAsync(
        string actor,
        AuditAction action,
        string entityType,
        Guid? entityId = null,
        Guid? projectId = null,
        string? details = null,
        CancellationToken cancellationToken = default,
        Guid? enterpriseId = null);
}

/// <summary>
/// No-op <see cref="IAuditWriter"/> for use in tests and contexts where audit
/// persistence is not available.
/// </summary>
public sealed class NullAuditWriter : IAuditWriter
{
    public static readonly NullAuditWriter Instance = new();

    public System.Threading.Tasks.Task WriteAsync(
        string actor,
        AuditAction action,
        string entityType,
        Guid? entityId = null,
        Guid? projectId = null,
        string? details = null,
        CancellationToken cancellationToken = default,
        Guid? enterpriseId = null)
        => System.Threading.Tasks.Task.CompletedTask;
}

/// <summary>
/// <see cref="DomainService{TModel}"/> that best-effort writes an audit entry
/// after each successful mutation.
/// </summary>
public abstract class AuditedDomainService<TModel> : DomainServices.Core.Services.DomainService<TModel>
    where TModel : class, ICoreDomainModel
{
    private static readonly string EntityTypeName = typeof(TModel).Name;
    private readonly IAuditWriter _auditWriter;

    protected AuditedDomainService(IRepository<TModel> repository, IAuditWriter auditWriter)
        : base(repository)
    {
        _auditWriter = auditWriter;
    }

    protected AuditedDomainService(IRepository<TModel> repository)
        : this(repository, NullAuditWriter.Instance)
    {
    }

    public override IBaseResponse<TModel> Add(Guid enterpriseId, TModel model, string userName)
    {
        var response = base.Add(enterpriseId, model, userName);
        if (response.IsSuccessful)
        {
            TryWriteAudit(ResolveAuditActor(userName, response.Data?.CreatedBy, model.CreatedBy), AuditAction.Created, response.Data?.Id);
        }

        return response;
    }

    public override async Task<IBaseResponse<TModel>> AddAsync(
        Guid enterpriseId,
        TModel model,
        string userName,
        CancellationToken cancellationToken = default)
    {
        var response = await base.AddAsync(enterpriseId, model, userName, cancellationToken).ConfigureAwait(false);
        if (response.IsSuccessful)
        {
            await TryWriteAuditAsync(
                ResolveAuditActor(userName, response.Data?.CreatedBy, model.CreatedBy),
                AuditAction.Created,
                response.Data?.Id,
                cancellationToken).ConfigureAwait(false);
        }

        return response;
    }

    public override IBaseResponse<TModel> Update(Guid id, TModel model, string userName)
    {
        var response = base.Update(id, model, userName);
        if (response.IsSuccessful)
        {
            TryWriteAudit(
                ResolveAuditActor(userName, response.Data?.UpdatedBy, model.UpdatedBy, model.CreatedBy),
                AuditAction.Updated,
                response.Data?.Id);
        }

        return response;
    }

    public override async Task<IBaseResponse<TModel>> UpdateAsync(
        Guid id,
        TModel model,
        string userName,
        CancellationToken cancellationToken = default)
    {
        var response = await base.UpdateAsync(id, model, userName, cancellationToken).ConfigureAwait(false);
        if (response.IsSuccessful)
        {
            await TryWriteAuditAsync(
                ResolveAuditActor(userName, response.Data?.UpdatedBy, model.UpdatedBy, model.CreatedBy),
                AuditAction.Updated,
                response.Data?.Id,
                cancellationToken).ConfigureAwait(false);
        }

        return response;
    }

    public override IBaseResponse Delete(Guid id, string userName)
    {
        var response = base.Delete(id, userName);
        if (response.IsSuccessful)
        {
            TryWriteAudit(ResolveAuditActor(userName), AuditAction.Deleted, id);
        }

        return response;
    }

    public override async Task<IBaseResponse> DeleteAsync(
        Guid id,
        string userName,
        CancellationToken cancellationToken = default)
    {
        var response = await base.DeleteAsync(id, userName, cancellationToken).ConfigureAwait(false);
        if (response.IsSuccessful)
        {
            await TryWriteAuditAsync(
                ResolveAuditActor(userName),
                AuditAction.Deleted,
                id,
                cancellationToken).ConfigureAwait(false);
        }

        return response;
    }

    public override IBaseResponse<int> Save(Guid enterpriseId, ICollection<TModel> models, string userName)
    {
        var response = base.Save(enterpriseId, models, userName);
        if (response.IsSuccessful)
        {
            TryWriteBatchAudit(ResolveAuditActor(userName), models);
        }

        return response;
    }

    public override async Task<IBaseResponse<int>> SaveAsync(
        Guid enterpriseId,
        ICollection<TModel> models,
        string userName,
        CancellationToken cancellationToken = default)
    {
        var response = await base.SaveAsync(enterpriseId, models, userName, cancellationToken).ConfigureAwait(false);
        if (response.IsSuccessful)
        {
            await TryWriteBatchAuditAsync(
                ResolveAuditActor(userName),
                models,
                cancellationToken).ConfigureAwait(false);
        }

        return response;
    }

    private void TryWriteBatchAudit(string actor, IEnumerable<TModel>? models)
    {
        if (models is null)
            return;

        foreach (var model in models)
        {
            TryWriteAudit(actor, AuditAction.Updated, model.Id);
        }
    }

    private async System.Threading.Tasks.Task TryWriteBatchAuditAsync(string actor, IEnumerable<TModel>? models, CancellationToken cancellationToken)
    {
        if (models is null)
            return;

        foreach (var model in models)
        {
            await TryWriteAuditAsync(actor, AuditAction.Updated, model.Id, cancellationToken).ConfigureAwait(false);
        }
    }

    private void TryWriteAudit(string actor, AuditAction action, Guid? entityId)
    {
        if (entityId is not { } id)
            return;

        TryWriteAuditAsync(actor, action, id, CancellationToken.None).GetAwaiter().GetResult();
    }

    private async System.Threading.Tasks.Task TryWriteAuditAsync(
        string actor,
        AuditAction action,
        Guid? entityId,
        CancellationToken cancellationToken)
    {
        if (entityId is not { } id)
            return;

        try
        {
            await _auditWriter.WriteAsync(actor, action, EntityTypeName, id, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Best-effort: audit failure must not break the CRUD operation that already committed.
        }
    }

    private static string ResolveAuditActor(params string?[] candidates) =>
        candidates.FirstOrDefault(candidate => !string.IsNullOrWhiteSpace(candidate)) ?? "system";
}
