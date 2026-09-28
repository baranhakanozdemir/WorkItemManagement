using System.Text.Json;
using WorkItemManagement.Core;

namespace WorkItemManagement.Core.Tests;

public class WorkItemPolymorphicJsonTests
{
    [Fact]
    public void Deserialize_epic_from_type_discriminator()
    {
        const string json = """
            {
              "type": 0,
              "title": "Epic title",
              "projectId": "11111111-1111-1111-1111-111111111111"
            }
            """;

        var item = JsonSerializer.Deserialize<WorkItem>(json, JsonSerializerOptions.Web);

        Assert.IsType<Epic>(item);
        Assert.Equal(WorkItemType.Epic, item!.Type);
        Assert.Equal("Epic title", item.Title);
    }

    [Fact]
    public void Deserialize_user_story_from_type_discriminator()
    {
        const string json = """
            {
              "type": 2,
              "title": "Story",
              "acceptanceCriteria": "Given when then",
              "projectId": "11111111-1111-1111-1111-111111111111"
            }
            """;

        var item = JsonSerializer.Deserialize<WorkItem>(json, JsonSerializerOptions.Web);

        var story = Assert.IsType<UserStory>(item);
        Assert.Equal("Given when then", story.AcceptanceCriteria);
    }
}
