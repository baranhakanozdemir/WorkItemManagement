using DomainServices.Core.Models;
using DomainServices.Core.Validation;
using WorkItemManagement.Core;

namespace WorkItemManagement.Core.Entities;

public class WorkItemCommitRef : CoreDomainModel, IWorkItemCommitRef
{
    public const int MaxCommitShaLength = 200;
    public const int MaxRepositoryLength = 500;
    public const int MaxUrlLength = 2048;
    public const int MaxBuildStatusLength = 64;
    public const int MaxBuildMessageLength = 1000;

    public Guid ProjectId { get; set; }

    public Guid WorkItemId { get; set; }

    public string CommitSha { get; set; } = string.Empty;

    public string Repository { get; set; } = string.Empty;

    public string? CommitUrl { get; set; }

    public int? PullRequestNumber { get; set; }

    public string? PullRequestUrl { get; set; }

    public string? BuildStatus { get; set; }

    public string? BuildMessage { get; set; }

    public DateTimeOffset? BuildVerifiedAt { get; set; }

    public DateTimeOffset CommittedAt { get; set; }

    public WorkItem? WorkItem { get; set; }

    public WorkItemCommitRef()
    {
        Id = Guid.NewGuid();
    }

    public WorkItemCommitRef(string createdBy)
        : this()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(createdBy);
        SetCreate(createdBy);
    }

    protected override void OnValidate(ModelValidator validator)
    {
        validator.Require(ProjectId != Guid.Empty, nameof(ProjectId), "Project id is required.");
        validator.Require(WorkItemId != Guid.Empty, nameof(WorkItemId), "Work item id is required.");
        validator.Require(
            !string.IsNullOrWhiteSpace(CommitSha),
            nameof(CommitSha),
            "Commit SHA is required.");
        validator.Require(
            string.IsNullOrWhiteSpace(CommitSha) || CommitSha.Length <= MaxCommitShaLength,
            nameof(CommitSha),
            $"Commit SHA must be {MaxCommitShaLength} characters or fewer.");
        validator.Require(
            !string.IsNullOrWhiteSpace(Repository),
            nameof(Repository),
            "Repository is required.");
        validator.Require(
            string.IsNullOrWhiteSpace(Repository) || Repository.Length <= MaxRepositoryLength,
            nameof(Repository),
            $"Repository must be {MaxRepositoryLength} characters or fewer.");
        validator.Require(
            CommitUrl is null || CommitUrl.Length <= MaxUrlLength,
            nameof(CommitUrl),
            $"Commit URL must be {MaxUrlLength} characters or fewer.");
        validator.Require(
            PullRequestUrl is null || PullRequestUrl.Length <= MaxUrlLength,
            nameof(PullRequestUrl),
            $"Pull request URL must be {MaxUrlLength} characters or fewer.");
        validator.Require(
            PullRequestNumber is null or > 0,
            nameof(PullRequestNumber),
            "Pull request number must be positive when present.");
        validator.Require(
            BuildStatus is null || BuildStatus.Length <= MaxBuildStatusLength,
            nameof(BuildStatus),
            $"Build status must be {MaxBuildStatusLength} characters or fewer.");
        validator.Require(
            BuildMessage is null || BuildMessage.Length <= MaxBuildMessageLength,
            nameof(BuildMessage),
            $"Build message must be {MaxBuildMessageLength} characters or fewer.");
        validator.Require(
            CommittedAt > DateTimeOffset.MinValue,
            nameof(CommittedAt),
            "Committed timestamp is required.");
    }
}
