# WorkItemManagement

The canonical work-item domain model — entities, services, and repository interfaces.

Consumers map the entities onto their own `DbContext` and compose the services. The package holds
no EF dependency and opens no connections of its own.

## Scope

**Work item management, and nothing else.** Work items and their kinds, blockers, relations, commit
refs, and requirement links.

That boundary is deliberate and has no exceptions. Planning — the work-breakdown structure a
customer reviews before approval, the rules that judge whether it is fit for review, and the
requirement and deliverable data it is judged against — is a different domain with a different
owner. It is not shipped here, however convenient it would be for a consumer to find it here.

The distinction to keep in mind: `RequirementWorkItemLink` belongs here, because linking a work item
to a requirement is work item management. Reading, normalizing or judging requirements does not.

## Surface

| Type | What it is |
|---|---|
| `WorkItem` and its kinds (`Epic`, `Feature`, `UserStory`, `Task`, `Bug`) | The executable work that exists after a plan is approved |
| `WorkItemBlocker` | What is stopping an item, and why |
| `WorkItemRelation` | How items relate to each other |
| `WorkItemCommitRef` | The commits that delivered an item |
| `RequirementWorkItemLink` | Which requirement an item implements |

Each has a repository interface and a service. `IWorkItemStateSync` and
`IWorkItemCompletionOverride` are the extension points a consumer implements.

## Version note

**0.5.0 removes the WBS planning surface that 0.4.0 added.** 0.4.0 shipped a work-breakdown
readiness engine, a JSON read boundary, and an authoring buffer. None of that is work item
management, and it should not have been published here. If you are on 0.4.0 and using
`WorkItemManagement.Core.Planning`, `IWbsAuthoringBufferService` or `IWbsAuthoringContextService`,
those types are gone in 0.5.0 and are not being relocated into this package under another name.
