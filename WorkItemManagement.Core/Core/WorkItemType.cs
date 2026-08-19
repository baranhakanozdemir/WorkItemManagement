namespace WorkItemManagement.Core;

/// <summary>
/// Discriminator for the canonical work-item subtypes (docs/22 Part 1).
/// </summary>
public enum WorkItemType
{
    /// <summary>The largest unit; normally a root, children are normally Features.</summary>
    Epic = 0,

    /// <summary>Children normally User Stories.</summary>
    Feature = 1,

    /// <summary>Children normally Tasks; carries acceptance criteria derived from requirements.</summary>
    UserStory = 2,

    /// <summary>The smallest unit of executable work; typically produces commits.</summary>
    Task = 3,

    /// <summary>Defect work item; may attach at any hierarchy level.</summary>
    Bug = 4,
}
