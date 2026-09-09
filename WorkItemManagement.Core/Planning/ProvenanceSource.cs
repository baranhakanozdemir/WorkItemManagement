namespace WorkItemManagement.Core.Planning;

/// <summary>
/// The single, canonical provenance model for +team.ai (#2149).
///
/// Before this type there were three disagreeing representations of "where did
/// this come from": <c>RequirementProvenance</c> (0-based), <c>DeliverableProvenance</c>
/// (1-based) and the free-form <c>IRequirement.SourceProvenanceTag</c> string. None
/// of them could express the states the revision (docs/30) introduces:
/// <list type="bullet">
///   <item>§6 needs <see cref="ExtractedFromDescriptionUnconfirmed"/> and
///   <see cref="ExtractedFromDescriptionConfirmed"/> as distinct states — a
///   customer nodding at an inference is weaker evidence than a customer writing
///   a sentence, and <c>clarify-requirements</c> probes the two differently.</item>
///   <item>§5 needs <see cref="AssumedAtClarification"/> for below-threshold gaps
///   and the "you decide / defer" route.</item>
/// </list>
///
/// This enum is the one model every provenance-bearing surface converts into, so
/// the downstream revision items (#2159, #2161, #2162) do not each invent a fourth.
/// </summary>
public enum ProvenanceSource
{
    /// <summary>Provenance is unknown or has not been recorded.</summary>
    Unspecified = 0,

    /// <summary>
    /// The customer stated this directly — typed it into a field or answered a
    /// question. Strongest evidence. Legacy <c>RequirementProvenance.CustomerDecided</c>
    /// and <c>DeliverableProvenance.CustomerProvided</c> both fold in here.
    /// </summary>
    CustomerStated = 1,

    /// <summary>Imported from a document the customer supplied.</summary>
    ImportedFromDocument = 2,

    /// <summary>Inferred from an existing codebase during brownfield intake.</summary>
    InferredFromCode = 3,

    /// <summary>
    /// The platform proposed this and the customer has not (yet) confirmed it.
    /// Legacy <c>DeliverableProvenance.PlatformProposed</c> folds in here.
    /// </summary>
    PlatformProposed = 4,

    /// <summary>
    /// Extracted from the free-text project description at intake and surfaced as
    /// a suggestion the customer has not yet touched (docs/30 §6). Distinct from
    /// <see cref="ExtractedFromDescriptionConfirmed"/> — an untouched suggestion is
    /// not yet evidence of anything.
    /// </summary>
    ExtractedFromDescriptionUnconfirmed = 5,

    /// <summary>
    /// Extracted from the project description and explicitly confirmed by the
    /// customer (docs/30 §6). Weaker than <see cref="CustomerStated"/> — the customer
    /// nodded at an inference rather than writing it — but confirmed, so it is not
    /// left as an untouched suggestion.
    /// </summary>
    ExtractedFromDescriptionConfirmed = 6,

    /// <summary>
    /// The platform resolved a below-threshold clarification gap itself, or the
    /// customer chose "you decide / defer", and the resulting assumption was
    /// recorded (docs/30 §5). Surfaced in the decisions list rather than asked.
    /// </summary>
    AssumedAtClarification = 7,

    /// <summary>
    /// The platform decided the value on the customer's behalf and the question
    /// was never surfaced to the customer at all — the survey judgement filter
    /// (docs/30 §9, filters 3/4/6) took it off the customer's plate before it
    /// could be asked or shown (#2162).
    ///
    /// The three "platform decided it" values are distinguished by a single axis —
    /// <b>can the customer see it?</b> — not by where in the pipeline the decision
    /// happened (origin belongs in the writer's rationale, not the enum):
    /// <list type="bullet">
    ///   <item><see cref="PlatformProposed"/> — decided, <i>shown</i>, and the
    ///   customer can change it (the confirmable survivor recommendation, #2207).</item>
    ///   <item><see cref="AssumedAtClarification"/> — decided and <i>surfaced as an
    ///   assumption</i> in the decisions list (docs/30 §5: a below-threshold
    ///   clarification gap must be surfaced, not asked).</item>
    ///   <item><see cref="PlatformDecided"/> — decided and <i>never surfaced</i>.</item>
    /// </list>
    /// Do not merge this with <see cref="AssumedAtClarification"/>: a
    /// <see cref="PlatformDecided"/> question never reached a clarification round,
    /// so that name would assert a provenance that did not happen.
    /// </summary>
    PlatformDecided = 8,
}
