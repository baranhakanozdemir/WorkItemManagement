using DomainServices.Core.Services;
using WorkItemManagement.Core;
using WorkItemManagement.Core.Services;

namespace WorkItemManagement.Core.Tests;

using Task = System.Threading.Tasks.Task;

/// <summary>
/// #13: the per-project work-item number, its <c>WI-&lt;n&gt;</c> form, and the rule that an update
/// never changes a stored number.
/// </summary>
public class WorkItemNumberTests
{
    private static readonly Guid ProjectId = Guid.Parse("8f1d2a3b-0000-4000-8000-000000000001");
    private static readonly Guid EnterpriseId = Guid.Parse("8f1d2a3b-0000-4000-8000-000000000002");

    [Fact]
    public void Format_writes_the_WI_prefix_and_the_number()
    {
        Assert.Equal("WI-42", WorkItemNumber.Format(42));
        Assert.Equal("WI-1", WorkItemNumber.Format(1));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void Format_refuses_a_number_that_is_not_positive(int number)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => WorkItemNumber.Format(number));
    }

    [Theory]
    [InlineData("WI-42", 42)]
    [InlineData("wi-42", 42)]
    [InlineData("Wi-7", 7)]
    [InlineData("WI-2147483647", int.MaxValue)]
    public void TryParse_reads_a_well_formed_number(string value, int expected)
    {
        Assert.True(WorkItemNumber.TryParse(value, out var number));
        Assert.Equal(expected, number);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("WI-")]
    [InlineData("WI-0")]
    [InlineData("WI-042")]
    [InlineData("WI-+5")]
    [InlineData("WI--5")]
    [InlineData("WI-x")]
    [InlineData("WI-4 ")]
    [InlineData(" WI-4")]
    [InlineData("WI-4x")]
    [InlineData("42")]
    [InlineData("#42")]
    [InlineData("WI-2147483648")]
    public void TryParse_refuses_anything_else(string? value)
    {
        Assert.False(WorkItemNumber.TryParse(value, out var number));
        Assert.Equal(0, number);
    }

    [Fact]
    public void A_negative_number_is_invalid_and_zero_is_valid()
    {
        var unassigned = Story(number: 0);
        Assert.True(unassigned.Validate().IsValid);

        var negative = Story(number: -1);
        var validation = negative.Validate();
        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, error => error.Property == nameof(WorkItem.Number));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(99)]
    [InlineData(-1)]
    public async Task UpdateByProjectAsync_keeps_the_stored_number(int sent)
    {
        var repository = new RecordingWorkItemRepository();
        var stored = repository.Seed(Story(number: 7));
        var service = new WorkItemService(repository, NullAuditWriter.Instance);

        var incoming = Story(number: sent, id: stored.Id, title: "Renamed");
        var response = await service.UpdateByProjectAsync(ProjectId, stored.Id, incoming, "tester");

        Assert.True(response.IsSuccessful, response.Message);
        Assert.Equal(7, response.Data!.Number);
        Assert.Equal(7, repository.Stored(stored.Id).Number);
        Assert.Equal("Renamed", repository.Stored(stored.Id).Title);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(99)]
    [InlineData(-1)]
    public async Task UpdateAsync_keeps_the_stored_number(int sent)
    {
        var repository = new RecordingWorkItemRepository();
        var stored = repository.Seed(Story(number: 7));
        var service = new WorkItemService(repository, NullAuditWriter.Instance);

        var incoming = Story(number: sent, id: stored.Id, title: "Renamed");
        var response = await service.UpdateAsync(stored.Id, incoming, "tester");

        Assert.True(response.IsSuccessful, response.Message);
        Assert.Equal(7, response.Data!.Number);
        Assert.Equal(7, repository.Stored(stored.Id).Number);
        Assert.Equal("Renamed", repository.Stored(stored.Id).Title);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(99)]
    [InlineData(-1)]
    public async Task SaveAsync_keeps_the_stored_number_of_each_existing_item(int sent)
    {
        var repository = new RecordingWorkItemRepository();
        var first = repository.Seed(Story(number: 7));
        var second = repository.Seed(Story(number: 8));
        var service = new WorkItemService(repository, NullAuditWriter.Instance);

        var batch = new List<WorkItem>
        {
            Story(number: sent, id: first.Id, title: "Renamed first"),
            Story(number: sent, id: second.Id, title: "Renamed second"),
        };
        var response = await service.SaveAsync(EnterpriseId, batch, "tester");

        Assert.True(response.IsSuccessful, response.Message);
        Assert.Equal(7, repository.Stored(first.Id).Number);
        Assert.Equal(8, repository.Stored(second.Id).Number);
        Assert.Equal("Renamed first", repository.Stored(first.Id).Title);
        Assert.Equal("Renamed second", repository.Stored(second.Id).Title);
    }

    /// <summary>
    /// #15: the server-owned audit fields survive every update path. Through the direct
    /// <c>UpdateAsync</c> and the batch <c>SaveAsync</c> only DomainServices.Core 0.3.1's base
    /// protection guards them, so this fails if the package falls back to 0.3.0.
    /// </summary>
    [Theory]
    [InlineData("update")]
    [InlineData("save")]
    [InlineData("update-by-project")]
    public async Task A_caller_cannot_change_created_created_by_or_is_deleted(string path)
    {
        var repository = new RecordingWorkItemRepository();
        var storedCreated = new DateTimeOffset(2026, 1, 15, 9, 30, 0, TimeSpan.Zero);
        var stored = Story(number: 7);
        stored.Created = storedCreated;
        stored.CreatedBy = "original-author";
        stored.IsDeleted = false;
        repository.Seed(stored);
        var service = new WorkItemService(repository, NullAuditWriter.Instance);

        var incoming = Story(number: 7, id: stored.Id, title: "Renamed");
        incoming.Created = new DateTimeOffset(2030, 6, 1, 0, 0, 0, TimeSpan.Zero);
        incoming.CreatedBy = "someone-else";
        incoming.IsDeleted = true;

        var succeeded = path switch
        {
            "update" => (await service.UpdateAsync(stored.Id, incoming, "tester")).IsSuccessful,
            "save" => (await service.SaveAsync(EnterpriseId, new List<WorkItem> { incoming }, "tester")).IsSuccessful,
            _ => (await service.UpdateByProjectAsync(ProjectId, stored.Id, incoming, "tester")).IsSuccessful,
        };

        Assert.True(succeeded);
        var after = repository.Stored(stored.Id);
        Assert.Equal("Renamed", after.Title);
        Assert.Equal(storedCreated, after.Created);
        Assert.Equal("original-author", after.CreatedBy);
        Assert.False(after.IsDeleted);
    }

    [Fact]
    public async Task UpdateAsync_for_an_unknown_id_is_still_not_found()
    {
        var repository = new RecordingWorkItemRepository();
        var service = new WorkItemService(repository, NullAuditWriter.Instance);

        var missing = Story(number: 0);
        var response = await service.UpdateAsync(missing.Id, missing, "tester");

        Assert.False(response.IsSuccessful);
        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
    }

    private static UserStory Story(int number, Guid? id = null, string title = "Pair a computer")
    {
        var story = new UserStory
        {
            ProjectId = ProjectId,
            EnterpriseId = EnterpriseId,
            Title = title,
            AcceptanceCriteria = "The computer appears in the list as online.",
            Number = number,
        };
        if (id is { } value)
        {
            story.Id = value;
        }

        return story;
    }
}
