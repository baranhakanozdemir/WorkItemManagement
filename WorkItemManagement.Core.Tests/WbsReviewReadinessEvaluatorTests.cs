using WorkItemManagement.Core.Planning;

namespace WorkItemManagement.Core.Tests;

public sealed class WbsReviewReadinessEvaluatorTests
{
    [Fact]
    public void Evaluate_rejects_deterministic_fallback_source()
    {
        var payload = EpicOnlyPayload(Guid.NewGuid());

        var result = WbsReviewReadinessEvaluator.Evaluate(payload, WbsStructureSource.DeterministicFallback);

        Assert.False(result.IsReviewReady);
        Assert.Contains(result.Issues, issue => issue.Contains("deterministic fallback", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Evaluate_rejects_summarizer_non_converged_source()
    {
        var payload = EpicOnlyPayload(Guid.NewGuid());

        var result = WbsReviewReadinessEvaluator.Evaluate(payload, WbsStructureSource.SummarizerNonConverged);

        Assert.False(result.IsReviewReady);
        Assert.Contains(result.Issues, issue => issue.Contains("did not converge", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Evaluate_rejects_dependency_cycle_salvaged_source()
    {
        var payload = EpicOnlyPayload(Guid.NewGuid());

        var result = WbsReviewReadinessEvaluator.Evaluate(
            payload,
            WbsStructureSource.SummarizerDependencyCycleSalvaged);

        Assert.False(result.IsReviewReady);
        Assert.Contains(
            result.Issues,
            issue => issue.Contains("Dependency links formed a cycle", StringComparison.Ordinal));
    }

    /// <summary>
    /// #3236: a plan whose orphaned links were removed is not review-ready, and it says why in its
    /// own words rather than borrowing the cycle sentence.
    /// </summary>
    /// <remarks>
    /// The two repairs mean different things to a customer. A broken cycle says the order stated
    /// was impossible. A removed orphan says something was named as a prerequisite that nobody
    /// planned — which is worth going to look for, and is invisible if both read "links were
    /// removed".
    /// </remarks>
    [Fact]
    public void Evaluate_rejects_orphan_dependency_salvaged_source_in_its_own_terms()
    {
        var payload = EpicOnlyPayload(Guid.NewGuid());

        var result = WbsReviewReadinessEvaluator.Evaluate(
            payload,
            WbsStructureSource.SummarizerOrphanDependencySalvaged);

        Assert.False(result.IsReviewReady);
        Assert.Contains(
            result.Issues,
            issue => issue.Contains("named prerequisites that are not in this plan", StringComparison.Ordinal));
        Assert.DoesNotContain(
            result.Issues,
            issue => issue.Contains("formed a cycle", StringComparison.Ordinal));
    }

    [Fact]
    public void Evaluate_rejects_mirror_deliver_and_implement_titles()
    {
        var deliverableId = Guid.NewGuid();
        var payload = new WbsStructurePayload(
        [
            new WbsNodePayload("epic-web", null, WbsNodeKind.Epic, deliverableId, null, "Web app", null, 0),
            new WbsNodePayload("feature-auth", "epic-web", WbsNodeKind.Feature, null, null, "Authentication", null, 0),
            new WbsNodePayload(
                "story-auth",
                "feature-auth",
                WbsNodeKind.UserStory,
                null,
                null,
                "Deliver: Authentication",
                null,
                0),
            new WbsNodePayload(
                "task-auth",
                "story-auth",
                WbsNodeKind.Task,
                null,
                null,
                "Implement: Authentication",
                null,
                0),
        ]);

        var result = WbsReviewReadinessEvaluator.Evaluate(payload, WbsStructureSource.Summarizer);

        Assert.False(result.IsReviewReady);
        Assert.Contains(result.Issues, issue => issue.Contains("Deliver:", StringComparison.Ordinal));
        Assert.Contains(result.Issues, issue => issue.Contains("Implement:", StringComparison.Ordinal));
    }

    [Fact]
    public void Evaluate_rejects_case_insensitive_parent_mirror_titles()
    {
        var deliverableId = Guid.NewGuid();
        var payload = new WbsStructurePayload(
        [
            new WbsNodePayload("epic-web", null, WbsNodeKind.Epic, deliverableId, null, "Web app", null, 0),
            new WbsNodePayload("feature-auth", "epic-web", WbsNodeKind.Feature, null, null, "Authentication", null, 0),
            new WbsNodePayload(
                "story-auth",
                "feature-auth",
                WbsNodeKind.UserStory,
                null,
                null,
                "authentication",
                null,
                0),
        ]);

        var result = WbsReviewReadinessEvaluator.Evaluate(payload, WbsStructureSource.Summarizer);

        Assert.False(result.IsReviewReady);
        Assert.Contains(result.Issues, issue => issue.Contains("mirrors its parent", StringComparison.Ordinal));
    }

    /// <summary>
    /// #2420. The pairs are the ones reported on the issue: a child that extends its parent's noun
    /// phrase, and a child named more briefly than its parent. Both are how a WBS is supposed to be
    /// named, and the previous containment rule flagged both — which also fed the summarizer retry
    /// loop, telling it to rename correct children.
    /// </summary>
    [Theory]
    [InlineData("Speech-to-text conversion", "Speech-to-text conversion accuracy harness")]
    [InlineData("Recording", "Recording playback controls")]
    [InlineData("Translation", "Translation glossary for radiology terms")]
    [InlineData("Set the speaking language", "Set the speaking language picker UI")]
    [InlineData("Language selection", "Persist language selection per account")]
    [InlineData("Account pairing", "Account pairing: desktop agent sign-in")]
    [InlineData("Radiology terminology handling", "Terminology")]
    [InlineData("Recording playback", "Playback")]
    public void TryValidateStructure_accepts_child_that_reuses_its_parents_wording(
        string parentTitle,
        string childTitle)
    {
        var payload = StoryUnderFeature(parentTitle, childTitle);

        Assert.True(WbsReviewReadinessEvaluator.TryValidateStructure(payload, out var errors));
        Assert.Empty(errors);
    }

    /// <summary>
    /// The rule still has to catch what it was written for: a child that restates its parent and
    /// adds nothing, whether by repeating it, re-casing it, pluralizing it, or padding it with
    /// words that describe no work ("tasks", "implementation", "general").
    /// </summary>
    [Theory]
    [InlineData("Speech-to-text conversion", "Speech-to-text conversion")]
    [InlineData("Authentication", "AUTHENTICATION")]
    [InlineData("Recording playback control", "Recording playback controls")]
    [InlineData("Recording", "Recording tasks")]
    [InlineData("Recording", "Recording implementation work")]
    [InlineData("Translation glossary", "General translation glossary items")]
    public void TryValidateStructure_rejects_child_that_only_restates_its_parent(
        string parentTitle,
        string childTitle)
    {
        var payload = StoryUnderFeature(parentTitle, childTitle);

        Assert.False(WbsReviewReadinessEvaluator.TryValidateStructure(payload, out var errors));
        Assert.Contains(errors, error => error.Contains("mirrors its parent", StringComparison.Ordinal));
    }

    /// <summary>
    /// Plural collapse has to reach the forms that are not a bare trailing "s", or a restatement
    /// slips through; the singular forms that merely end in "s" must not be collapsed into
    /// something else, which is what would create new false positives.
    /// </summary>
    [Theory]
    [InlineData("Retention policy", "Retention policies")]
    [InlineData("Mailbox", "Mailboxes")]
    [InlineData("Delivery status", "Delivery statuses")]
    [InlineData("Access class", "Access classes")]
    [InlineData("Search match", "Search matches")]
    public void TryValidateStructure_rejects_pluralized_restatement_of_its_parent(
        string parentTitle,
        string childTitle)
    {
        var payload = StoryUnderFeature(parentTitle, childTitle);

        Assert.False(WbsReviewReadinessEvaluator.TryValidateStructure(payload, out var errors));
        Assert.Contains(errors, error => error.Contains("mirrors its parent", StringComparison.Ordinal));
    }

    /// <summary>
    /// Plural collapse must not merge distinct words: "case" is not the singular of "cases" via the
    /// sibilant rule, and a title whose only word ends in "s" or "is" is not a plural at all.
    /// </summary>
    [Theory]
    [InlineData("Test case triage", "Test cases")]
    [InlineData("Delivery status", "Delivery analysis")]
    public void TryValidateStructure_accepts_titles_plural_collapse_must_not_merge(
        string parentTitle,
        string childTitle)
    {
        var payload = StoryUnderFeature(parentTitle, childTitle);

        Assert.True(WbsReviewReadinessEvaluator.TryValidateStructure(payload, out var errors));
        Assert.Empty(errors);
    }

    /// <summary>
    /// A non-Latin title must tokenize. An ASCII-only tokenizer yields nothing for both sides, and
    /// two empty token sets compare equal, so every localized pair would be reported as a mirror
    /// and the summarizer would retry valid branches forever.
    /// </summary>
    [Fact]
    public void TryValidateStructure_accepts_unrelated_non_latin_titles()
    {
        var payload = StoryUnderFeature("録音", "再生");

        Assert.True(WbsReviewReadinessEvaluator.TryValidateStructure(payload, out var errors));
        Assert.Empty(errors);
    }

    /// <summary>
    /// The reordered pair is what proves the title is tokenized rather than merely string-compared:
    /// the two titles are not equal, but they say the same thing in the same words.
    /// </summary>
    [Theory]
    [InlineData("録音", "録音")]
    [InlineData("録音 再生", "再生 録音")]
    public void TryValidateStructure_rejects_non_latin_restatement_of_its_parent(
        string parentTitle,
        string childTitle)
    {
        var payload = StoryUnderFeature(parentTitle, childTitle);

        Assert.False(WbsReviewReadinessEvaluator.TryValidateStructure(payload, out var errors));
        Assert.Contains(errors, error => error.Contains("mirrors its parent", StringComparison.Ordinal));
    }

    /// <summary>
    /// A lexical filler word ("this") is dropped like any other, and a title left with no tokens at
    /// all falls back to comparing the titles, so two all-filler titles are not equal by vacuum.
    /// </summary>
    [Fact]
    public void TryValidateStructure_rejects_child_padded_with_a_lexical_filler_word()
    {
        var payload = StoryUnderFeature("Recording", "This recording");

        Assert.False(WbsReviewReadinessEvaluator.TryValidateStructure(payload, out var errors));
        Assert.Contains(errors, error => error.Contains("mirrors its parent", StringComparison.Ordinal));
    }

    [Fact]
    public void TryValidateStructure_accepts_distinct_titles_that_reduce_to_no_tokens()
    {
        var payload = StoryUnderFeature("General items", "Other work");

        Assert.True(WbsReviewReadinessEvaluator.TryValidateStructure(payload, out var errors));
        Assert.Empty(errors);
    }

    [Fact]
    public void Evaluate_rejects_epic_only_summarizer_structure()
    {
        var result = WbsReviewReadinessEvaluator.Evaluate(
            EpicOnlyPayload(Guid.NewGuid()),
            WbsStructureSource.Summarizer);

        Assert.False(result.IsReviewReady);
        Assert.Contains(result.Issues, issue => issue.Contains("no features", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Evaluate_rejects_epic_and_feature_only_summarizer_structure()
    {
        var deliverableId = Guid.NewGuid();
        var payload = new WbsStructurePayload(
        [
            new WbsNodePayload("epic-web", null, WbsNodeKind.Epic, deliverableId, null, "Web app", null, 0),
            new WbsNodePayload("feature-auth", "epic-web", WbsNodeKind.Feature, null, null, "Authentication", null, 0),
        ]);

        var result = WbsReviewReadinessEvaluator.Evaluate(payload, WbsStructureSource.Summarizer);

        Assert.False(result.IsReviewReady);
        Assert.Contains(result.Issues, issue => issue.Contains("no user stories", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// #2628: the incident shape. One deliverable and five objectives produced a small
    /// tree; 2 of 5 features have stories. That is complete for this scope, not truncated.
    /// Without captured scope the same tree still fails the 80% rule — which is how the
    /// fillers got generated.
    /// </summary>
    [Fact]
    public void Evaluate_accepts_a_small_complete_tree_for_a_small_captured_scope()
    {
        var deliverable = Guid.NewGuid();
        var nodes = new List<WbsNodePayload>
        {
            new("epic-page", null, WbsNodeKind.Epic, deliverable, null, "Reporting page", null, 0),
        };

        for (var index = 0; index < 5; index++)
        {
            var featureKey = $"feature-{index:D2}";
            nodes.Add(new WbsNodePayload(
                featureKey,
                "epic-page",
                WbsNodeKind.Feature,
                null,
                Guid.NewGuid(),
                $"Feature {index + 1}",
                null,
                index + 1));

            if (index >= 2)
            {
                continue;
            }

            var storyKey = $"story-{index:D2}";
            nodes.Add(new WbsNodePayload(
                storyKey,
                featureKey,
                WbsNodeKind.UserStory,
                null,
                null,
                $"As a reader I can finish workflow {index + 1}",
                null,
                10 + index,
                CanCompleteInOneAgentSession: true));
            nodes.Add(new WbsNodePayload(
                $"task-{index:D2}",
                storyKey,
                WbsNodeKind.Task,
                null,
                null,
                $"Build workflow {index + 1}",
                null,
                20 + index,
                CanCompleteInOneAgentSession: true));
        }

        var payload = new WbsStructurePayload(nodes);
        var captured = new WbsTraceabilityContext(
            Enumerable.Range(1, 5)
                .Select(i => new WbsTraceabilityObjective($"OBJ-{i}", $"Objective {i}", i))
                .ToArray(),
            [new WbsTraceabilityDeliverable(deliverable, "Reporting page")]);

        var withScope = WbsReviewReadinessEvaluator.Evaluate(payload, WbsStructureSource.Summarizer, captured);
        Assert.True(withScope.IsReviewReady, string.Join("; ", withScope.Issues));
        Assert.DoesNotContain(withScope.Issues, issue => issue.Contains("depth-truncated", StringComparison.Ordinal));

        var withoutScope = WbsReviewReadinessEvaluator.Evaluate(payload, WbsStructureSource.Summarizer);
        Assert.False(withoutScope.IsReviewReady);
        Assert.Contains(withoutScope.Issues, issue => issue.Contains("depth-truncated", StringComparison.Ordinal));
    }

    /// <summary>
    /// #2628: 48 features with one story is still truncation when the same small scope is
    /// supplied — the scale does not excuse a half-emitted large level.
    /// </summary>
    [Fact]
    public void Evaluate_still_rejects_a_large_truncated_level_when_scope_is_small()
    {
        var deliverable = Guid.NewGuid();
        var nodes = new List<WbsNodePayload>
        {
            new("epic-web", null, WbsNodeKind.Epic, deliverable, null, "Web app", null, 0),
        };

        for (var index = 0; index < 48; index++)
        {
            nodes.Add(new WbsNodePayload(
                $"feature-{index:D2}",
                "epic-web",
                WbsNodeKind.Feature,
                null,
                Guid.NewGuid(),
                $"Feature {index + 1}",
                null,
                index + 1));
        }

        nodes.Add(new WbsNodePayload(
            "story-00",
            "feature-00",
            WbsNodeKind.UserStory,
            null,
            null,
            "As a portal user I can submit an inspection request",
            null,
            49,
            CanCompleteInOneAgentSession: true));
        nodes.Add(new WbsNodePayload(
            "task-00",
            "story-00",
            WbsNodeKind.Task,
            null,
            null,
            "Wire request submission validation",
            null,
            50,
            CanCompleteInOneAgentSession: true));

        var captured = new WbsTraceabilityContext(
            Enumerable.Range(1, 5)
                .Select(i => new WbsTraceabilityObjective($"OBJ-{i}", $"Objective {i}", i))
                .ToArray(),
            [new WbsTraceabilityDeliverable(deliverable, "Web app")]);

        var result = WbsReviewReadinessEvaluator.Evaluate(
            new WbsStructurePayload(nodes),
            WbsStructureSource.Summarizer,
            captured);

        Assert.False(result.IsReviewReady);
        Assert.Contains(result.Issues, issue =>
            issue.Contains("Only 1 of 48 features has user stories", StringComparison.Ordinal));
    }

    /// <summary>
    /// #2628 review: a large captured scope must not excuse a half-emitted level. 48
    /// features and one story is truncated at 100 scope items the same way it is at 6.
    /// </summary>
    [Fact]
    public void Evaluate_rejects_a_truncated_level_when_captured_scope_is_large()
    {
        var deliverable = Guid.NewGuid();
        var nodes = new List<WbsNodePayload>
        {
            new("epic-web", null, WbsNodeKind.Epic, deliverable, null, "Platform", null, 0),
        };

        for (var index = 0; index < 48; index++)
        {
            nodes.Add(new WbsNodePayload(
                $"feature-{index:D2}",
                "epic-web",
                WbsNodeKind.Feature,
                null,
                Guid.NewGuid(),
                $"Feature {index + 1}",
                null,
                index + 1));
        }

        nodes.Add(new WbsNodePayload(
            "story-00",
            "feature-00",
            WbsNodeKind.UserStory,
            null,
            null,
            "As a portal user I can submit an inspection request",
            null,
            49,
            CanCompleteInOneAgentSession: true));
        nodes.Add(new WbsNodePayload(
            "task-00",
            "story-00",
            WbsNodeKind.Task,
            null,
            null,
            "Wire request submission validation",
            null,
            50,
            CanCompleteInOneAgentSession: true));

        var captured = new WbsTraceabilityContext(
            Enumerable.Range(1, 50)
                .Select(i => new WbsTraceabilityObjective($"OBJ-{i}", $"Objective {i}", i))
                .ToArray(),
            Enumerable.Range(1, 50)
                .Select(i => new WbsTraceabilityDeliverable(Guid.NewGuid(), $"Deliverable {i}"))
                .ToArray());

        var result = WbsReviewReadinessEvaluator.Evaluate(
            new WbsStructurePayload(nodes),
            WbsStructureSource.Summarizer,
            captured);

        Assert.False(result.IsReviewReady);
        Assert.Contains(result.Issues, issue =>
            issue.Contains("Only 1 of 48 features has user stories", StringComparison.Ordinal));
    }

    [Fact]
    public void Evaluate_rejects_depth_truncated_feature_breadth()
    {
        var nodes = new List<WbsNodePayload>
        {
            new("epic-web", null, WbsNodeKind.Epic, Guid.NewGuid(), null, "Web app", null, 0),
        };

        for (var index = 0; index < 48; index++)
        {
            nodes.Add(new WbsNodePayload(
                $"feature-{index:D2}",
                "epic-web",
                WbsNodeKind.Feature,
                null,
                Guid.NewGuid(),
                $"Feature {index + 1}",
                null,
                index + 1));
        }

        nodes.Add(new WbsNodePayload(
            "story-00",
            "feature-00",
            WbsNodeKind.UserStory,
            null,
            null,
            "As a portal user I can submit an inspection request",
            null,
            49));
        nodes.Add(new WbsNodePayload(
            "task-00",
            "story-00",
            WbsNodeKind.Task,
            null,
            null,
            "Wire request submission validation",
            null,
            50));
        var payload = new WbsStructurePayload(nodes);

        var result = WbsReviewReadinessEvaluator.Evaluate(payload, WbsStructureSource.Summarizer);

        Assert.False(result.IsReviewReady);
        Assert.Contains(result.Issues, issue =>
            issue.Contains("Only 1 of 48 features has user stories", StringComparison.Ordinal));
    }

    [Fact]
    public void Evaluate_rejects_executable_node_that_failed_session_sizing()
    {
        var payload = new WbsStructurePayload(
        [
            new WbsNodePayload("epic-web", null, WbsNodeKind.Epic, Guid.NewGuid(), null, "Web app", null, 0),
            new WbsNodePayload("feature-auth", "epic-web", WbsNodeKind.Feature, null, null, "Authentication", null, 0),
            new WbsNodePayload(
                "story-auth",
                "feature-auth",
                WbsNodeKind.UserStory,
                null,
                null,
                "As a user I can sign in securely",
                null,
                0,
                CanCompleteInOneAgentSession: false),
            new WbsNodePayload(
                "task-auth",
                "story-auth",
                WbsNodeKind.Task,
                null,
                null,
                "Configure identity provider integration",
                null,
                0,
                CanCompleteInOneAgentSession: true),
        ]);

        var result = WbsReviewReadinessEvaluator.Evaluate(payload, WbsStructureSource.Summarizer);

        Assert.False(result.IsReviewReady);
        Assert.Contains(result.Issues, issue =>
            issue.Contains("not certified as completable", StringComparison.Ordinal));
    }

    [Fact]
    public void Evaluate_accepts_majority_feature_and_story_child_coverage()
    {
        var nodes = new List<WbsNodePayload>
        {
            new("epic-web", null, WbsNodeKind.Epic, Guid.NewGuid(), null, "Web app", null, 0),
        };

        for (var featureIndex = 0; featureIndex < 5; featureIndex++)
        {
            var featureKey = $"feature-{featureIndex:D2}";
            nodes.Add(new WbsNodePayload(
                featureKey,
                "epic-web",
                WbsNodeKind.Feature,
                null,
                Guid.NewGuid(),
                $"Feature {featureIndex + 1}",
                null,
                featureIndex + 1));

            if (featureIndex == 4)
            {
                continue;
            }

            var storyKey = $"story-{featureIndex:D2}";
            nodes.Add(new WbsNodePayload(
                storyKey,
                featureKey,
                WbsNodeKind.UserStory,
                null,
                null,
                $"As a portal user I can finish workflow {featureIndex + 1}",
                null,
                10 + featureIndex));
            nodes.Add(new WbsNodePayload(
                $"task-{featureIndex:D2}",
                storyKey,
                WbsNodeKind.Task,
                null,
                null,
                $"Build validation coverage for workflow {featureIndex + 1}",
                null,
                20 + featureIndex));
        }

        var result = WbsReviewReadinessEvaluator.Evaluate(
            new WbsStructurePayload(nodes),
            WbsStructureSource.Summarizer);

        Assert.True(result.IsReviewReady);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public void TryValidateStructure_accepts_epic_and_feature_only_for_non_converged_retry()
    {
        var deliverableId = Guid.NewGuid();
        var payload = new WbsStructurePayload(
        [
            new WbsNodePayload("epic-web", null, WbsNodeKind.Epic, deliverableId, null, "Web app", null, 0),
            new WbsNodePayload("feature-auth", "epic-web", WbsNodeKind.Feature, null, null, "Authentication", null, 0),
        ]);

        Assert.True(WbsReviewReadinessEvaluator.TryValidateStructure(payload, out var errors));
        Assert.Empty(errors);
    }

    [Fact]
    public void Evaluate_accepts_meaningful_summarizer_structure()
    {
        var deliverableId = Guid.NewGuid();
        var payload = new WbsStructurePayload(
        [
            new WbsNodePayload("epic-web", null, WbsNodeKind.Epic, deliverableId, null, "Web app", null, 0),
            new WbsNodePayload("feature-auth", "epic-web", WbsNodeKind.Feature, null, null, "Authentication", null, 0),
            new WbsNodePayload(
                "story-auth",
                "feature-auth",
                WbsNodeKind.UserStory,
                null,
                null,
                "As a user I can sign in securely",
                null,
                0),
            new WbsNodePayload(
                "task-auth",
                "story-auth",
                WbsNodeKind.Task,
                null,
                null,
                "Configure identity provider integration",
                null,
                0),
        ]);

        var result = WbsReviewReadinessEvaluator.Evaluate(payload, WbsStructureSource.Summarizer);

        Assert.True(result.IsReviewReady);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public void Evaluate_flags_boilerplate_entity_stories_when_pre_generated_entities_provided()
    {
        var deliverableId = Guid.NewGuid();
        var payload = new WbsStructurePayload(
        [
            new WbsNodePayload("epic-web", null, WbsNodeKind.Epic, deliverableId, null, "Web app", null, 0),
            new WbsNodePayload("feature-billing", "epic-web", WbsNodeKind.Feature, null, null, "Billing", null, 0),
            new WbsNodePayload(
                "story-create-entity",
                "feature-billing",
                WbsNodeKind.UserStory,
                null,
                null,
                "Create Invoice entity",
                null,
                0),
        ]);

        var result = WbsReviewReadinessEvaluator.Evaluate(
            payload,
            WbsStructureSource.Summarizer,
            preGeneratedEntities: ["Invoice", "Customer", "Product"]);

        Assert.False(result.IsReviewReady);
        Assert.Contains(result.Issues, issue => issue.Contains("Boilerplate domain story", StringComparison.Ordinal));
    }

    [Fact]
    public void Evaluate_flags_boilerplate_entity_stories_from_captured_scope()
    {
        var deliverableId = Guid.NewGuid();
        var payload = new WbsStructurePayload(
        [
            new WbsNodePayload("epic-web", null, WbsNodeKind.Epic, deliverableId, null, "Web app", null, 0),
            new WbsNodePayload("feature-billing", "epic-web", WbsNodeKind.Feature, null, null, "Billing", null, 0),
            new WbsNodePayload(
                "story-create-entity",
                "feature-billing",
                WbsNodeKind.UserStory,
                null,
                null,
                "Create Invoice entity",
                null,
                0),
        ]);

        var capturedScope = new WbsTraceabilityContext([], [new WbsTraceabilityDeliverable(deliverableId, "Web app")])
        {
            PreGeneratedEntities = ["Invoice", "Customer", "Product"],
        };

        var result = WbsReviewReadinessEvaluator.Evaluate(
            payload,
            WbsStructureSource.Summarizer,
            capturedScope: capturedScope);

        Assert.False(result.IsReviewReady);
        Assert.Contains(result.Issues, issue => issue.Contains("Boilerplate domain story", StringComparison.Ordinal));
    }

    [Fact]
    public void Evaluate_accepts_business_logic_stories_when_the_domain_layer_was_pre_generated()
    {
        var deliverableId = Guid.NewGuid();
        var payload = new WbsStructurePayload(
        [
            new WbsNodePayload("epic-web", null, WbsNodeKind.Epic, deliverableId, null, "Web app", null, 0),
            new WbsNodePayload("feature-billing", "epic-web", WbsNodeKind.Feature, null, null, "Billing", null, 0),
            new WbsNodePayload(
                "story-tax",
                "feature-billing",
                WbsNodeKind.UserStory,
                null,
                null,
                "As a finance user I can apply regional tax rules at invoice finalization",
                null,
                0),
            new WbsNodePayload(
                "task-tax",
                "story-tax",
                WbsNodeKind.Task,
                null,
                null,
                "Encode the tax-band table against the generated Invoice type",
                null,
                0),
        ]);

        var result = WbsReviewReadinessEvaluator.Evaluate(
            payload,
            WbsStructureSource.Summarizer,
            preGeneratedEntities: ["Invoice", "Customer", "Product"]);

        Assert.True(result.IsReviewReady, string.Join("; ", result.Issues));
        Assert.DoesNotContain(
            result.Issues,
            issue => issue.Contains("Boilerplate domain story", StringComparison.Ordinal));
    }

    [Fact]
    public void Evaluate_does_not_apply_boilerplate_gate_when_codegen_was_not_enforced()
    {
        var deliverableId = Guid.NewGuid();
        var payload = new WbsStructurePayload(
        [
            new WbsNodePayload("epic-web", null, WbsNodeKind.Epic, deliverableId, null, "Web app", null, 0),
            new WbsNodePayload("feature-billing", "epic-web", WbsNodeKind.Feature, null, null, "Billing", null, 0),
            new WbsNodePayload(
                "story-create-entity",
                "feature-billing",
                WbsNodeKind.UserStory,
                null,
                null,
                "Create Invoice entity",
                null,
                0),
        ]);

        var result = WbsReviewReadinessEvaluator.Evaluate(
            payload,
            WbsStructureSource.Summarizer,
            capturedScope: new WbsTraceabilityContext([], [new WbsTraceabilityDeliverable(deliverableId, "Web app")]));

        Assert.DoesNotContain(
            result.Issues,
            issue => issue.Contains("Boilerplate domain story", StringComparison.Ordinal));
    }

    [Fact]
    public void Evaluate_is_review_ready_when_stray_task_fields_are_stripped_and_features_are_covered()
    {
        var deliverableId = Guid.NewGuid();
        var raw = new WbsStructurePayload(
        [
            new WbsNodePayload("epic-web", null, WbsNodeKind.Epic, deliverableId, null, "Web app", null, 0),
            new WbsNodePayload("feature-1", "epic-web", WbsNodeKind.Feature, null, null, "Billing", null, 0),
            new WbsNodePayload("feature-2", "epic-web", WbsNodeKind.Feature, null, null, "Catalog", null, 1),
            new WbsNodePayload("feature-3", "epic-web", WbsNodeKind.Feature, null, null, "Checkout", null, 2),
            new WbsNodePayload("feature-4", "epic-web", WbsNodeKind.Feature, null, null, "Reporting", null, 3),
            new WbsNodePayload("feature-5", "epic-web", WbsNodeKind.Feature, null, null, "Admin", null, 4),
            new WbsNodePayload(
                "story-1",
                "feature-1",
                WbsNodeKind.UserStory,
                null,
                null,
                "As a finance user I can apply regional tax rules",
                null,
                0,
                CanCompleteInOneAgentSession: true),
            new WbsNodePayload(
                "task-1",
                "story-1",
                WbsNodeKind.Task,
                null,
                Guid.NewGuid(),
                "Encode the tax-band table",
                null,
                0,
                CanCompleteInOneAgentSession: true),
            new WbsNodePayload(
                "story-2",
                "feature-2",
                WbsNodeKind.UserStory,
                null,
                null,
                "As a merchandiser I can publish a catalog item",
                null,
                0,
                CanCompleteInOneAgentSession: true),
            new WbsNodePayload(
                "story-3",
                "feature-3",
                WbsNodeKind.UserStory,
                null,
                null,
                "As a shopper I can complete checkout",
                null,
                0,
                CanCompleteInOneAgentSession: true),
            new WbsNodePayload(
                "story-4",
                "feature-4",
                WbsNodeKind.UserStory,
                null,
                null,
                "As an analyst I can export a quarterly report",
                null,
                0,
                CanCompleteInOneAgentSession: true),
        ]);

        var payload = WbsStructurePayloadReader.Normalize(raw);
        Assert.Null(Assert.Single(payload.Nodes, node => node.Kind == WbsNodeKind.Task).CanCompleteInOneAgentSession);
        Assert.Null(Assert.Single(payload.Nodes, node => node.Kind == WbsNodeKind.Task).RequirementId);

        var result = WbsReviewReadinessEvaluator.Evaluate(payload, WbsStructureSource.Summarizer);

        Assert.True(result.IsReviewReady, string.Join("; ", result.Issues));
        Assert.DoesNotContain(result.Issues, issue => issue.Contains("depth-truncated", StringComparison.Ordinal));
    }

    private static WbsStructurePayload StoryUnderFeature(string parentTitle, string childTitle) =>
        new(
        [
            new WbsNodePayload("epic-web", null, WbsNodeKind.Epic, Guid.NewGuid(), null, "Web app", null, 0),
            new WbsNodePayload("feature-1", "epic-web", WbsNodeKind.Feature, null, null, parentTitle, null, 0),
            new WbsNodePayload("story-1", "feature-1", WbsNodeKind.UserStory, null, null, childTitle, null, 0),
        ]);

    private static WbsStructurePayload EpicOnlyPayload(Guid deliverableId) =>
        new(
        [
            new WbsNodePayload("epic-web", null, WbsNodeKind.Epic, deliverableId, null, "Web app", null, 0),
        ]);
}
