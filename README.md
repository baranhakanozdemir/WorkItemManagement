# WorkItemManagement

The canonical work-item domain model — entities, services, and repository interfaces — plus the
work-breakdown planning surface that decides whether a proposed breakdown is fit for review.

Consumers map the entities onto their own `DbContext` and compose the services. The package holds
no EF dependency and opens no connections of its own.

## Surfaces

### Work items (`WorkItemManagement.Core`)

The post-approval executable work: `WorkItem` and its kinds, blockers, relations, commit refs, and
requirement links, with a repository interface and a service per aggregate.

### Planning (`WorkItemManagement.Core.Planning`)

The pre-approval proposal: reading a breakdown a client submitted, and judging whether it is ready
for a customer to review.

```csharp
if (!WbsStructurePayloadReader.TryRead(body, out var payload, out var errors, out var warnings))
{
    return Reject(errors);
}

var readiness = WbsReviewReadinessEvaluator.Evaluate(
    payload,
    WbsStructureSource.Summarizer,
    traceabilityContext);
```

`Evaluate` is a pure function — no dependency injection, no `DbContext`, no HTTP. That is what makes
shipping the rules viable: a consumer runs the compiled rules rather than a copy of them, so the
rules cannot fork.

**Read JSON only through `WbsStructurePayloadReader`.** `WbsStructurePayload` carries explicit
`JsonPropertyName` attributes on every member, which makes deserializing it directly look safe. It
is not. Seven normalizations and validations sit between a client's body and a payload that can be
evaluated, each added after the missing one produced a wrong answer in production — most sharply,
`kind` is an enum whose zero value is `Epic`, so a plain deserialize reads an omitted kind as a
deliberate `Epic`. Every one of those defects fails quietly: the payload parses, evaluation runs,
and the verdict is wrong. The reader's own documentation lists all seven.

Structural validation — node title and key lengths — is deliberately *not* in this package. Those
bounds are the columns of the table that stores approved nodes, which this package does not own.

### Authoring buffer

`WbsAuthoringBuffer` (table `wbs_authoring_buffers`) holds one proposed breakdown per project while
it is being authored. `EnterpriseId` and `IsDeleted` behave exactly as they do on `WorkItem`, so
register the same tenant and soft-delete query filters — a divergence there returns wrong rows
silently rather than failing.

`IWbsAuthoringBufferService.ReplaceAsync` returns a write result, never a readiness verdict. Writing
and judging stay separate: folding the gate into the write makes a rejected evaluation look like a
failed write when the proposal is in fact stored, and an author told the write failed will write it
again.

### Authoring context

`IWbsAuthoringContextService` returns the requirements and deliverables a breakdown must cover — a
service rather than mapped entities, because requirements are stored as a base type with subtypes
across more than one table. A consumer mapping a single flat table would read some rows and miss
others, and since the evaluator reports the missing ones as uncovered, a partial read produces a
coverage failure the breakdown does not deserve, indistinguishable from a real one.

Requirement descriptions are returned in full. A caller cites requirements by identifier and needs
the text to plan against; a truncated description defeats the purpose of the call.

## Versioning note for consumers

Evaluation-rule changes reach a consumer only when it upgrades the package version. That is the
intended trade — one compiled rule, versioned — but it means a rule fix is not live for a consumer
until they bump.

## Tests

`dotnet test` runs the suite. The planning tests are ported verbatim from the codebase the rules
were extracted from: their assertions are the parity evidence that the moved rules still decide the
same way. A green build is not that evidence — this package's own 0.3.1 shipped an inverted enum
that compiled cleanly.
