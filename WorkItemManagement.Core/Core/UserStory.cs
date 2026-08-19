namespace WorkItemManagement.Core;

/// <summary>
/// #2461: a user story is the level at which "done" is defined for a customer, so
/// <see cref="AcceptanceCriteria"/> is a domain invariant rather than a field someone remembers to
/// fill in.
///
/// <para>It was <c>string?</c> for two months and nothing ever assigned it. A null acceptance-criteria
/// field was valid at every layer, so "never populated" and "legitimately empty" were
/// indistinguishable, and the first component able to tell them apart was the ambiguity gate at
/// execution — by which point the implementing agent had already been handed the story's own title
/// back as its criterion.</para>
///
/// <para><b>Non-null is not the invariant; non-blank is.</b> A <c>NOT NULL</c> column accepts
/// <c>""</c>, <c>" "</c> and <c>"TBD"</c>, so a failed generation step could write an empty string
/// and leave the schema reporting everything as healthy — the same silent absence, now harder to
/// find because the field is populated.</para>
/// </summary>
public class UserStory : WorkItem, IUserStory
{
    public const int MaxAcceptanceCriteriaLength = 8000;

    /// <inheritdoc cref="IUserStory.AcceptanceCriteria" />
    public string AcceptanceCriteria { get; set; } = string.Empty;

    public UserStory()
    {
        Type = WorkItemType.UserStory;
    }

    public UserStory(string createdBy)
        : base(createdBy)
    {
        Type = WorkItemType.UserStory;
    }

    protected override void OnValidate(DomainServices.Core.Validation.ModelValidator validator)
    {
        base.OnValidate(validator);
        validator.Require(
            !string.IsNullOrWhiteSpace(AcceptanceCriteria),
            nameof(AcceptanceCriteria),
            "Acceptance criteria are required: a user story must state at least one condition that "
            + "says when it is done.");
        // Null-safe despite the non-nullable declaration: nullable reference types are not enforced
        // at runtime, so a request body carrying `"acceptanceCriteria": null` deserializes null into
        // this property. The check above already records the right error for that payload — but
        // `validator.Require` takes a bool, so an unguarded `.Length` would evaluate first and turn a
        // bad request into a NullReferenceException instead.
        validator.Require(
            AcceptanceCriteria is null || AcceptanceCriteria.Length <= MaxAcceptanceCriteriaLength,
            nameof(AcceptanceCriteria),
            $"Acceptance criteria must be {MaxAcceptanceCriteriaLength} characters or fewer.");
    }
}
