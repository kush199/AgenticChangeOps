using AgenticChangeOps.Api.Contracts;
using AgenticChangeOps.Intake.Models;
using AgenticChangeOps.Intake.Stores;
using AgenticChangeOps.Agents.Architect;
using AgenticChangeOps.Agents.Developer;
using AgenticChangeOps.Agents.Requirement;
using AgenticChangeOps.Workflow;

namespace AgenticChangeOps.Api.Errors;

/// <summary>
/// The single table that turns a service failure into a status and error body. Adds no rules:
/// it only classifies the field names and categories the services already report.
/// </summary>
public static class ErrorMapper
{
    private const string ChangeRequestIdField = "ChangeRequestId";
    private const string StatusField = "Status";
    private const string MalformedOutputCategory = "MalformedOutput";
    private const string RequirementsDraftField = "RequirementsDraft";
    private const string DesignDraftField = "DesignDraft";
    private const string DesignDraftVersionField = "DesignDraftVersion";

    private static readonly HashSet<string> DesignSectionFields = new(StringComparer.Ordinal)
    {
        "ApproachSummary", "AffectedComponents", "ImpactAndRisks", "Dependencies",
        "RollbackConsiderations", "OpenQuestions"
    };

    private static readonly HashSet<string> DraftSectionFields = new(StringComparer.Ordinal)
    {
        "Summary", "FunctionalRequirements", "AcceptanceCriteria", "Assumptions", "OpenQuestions"
    };

    public static (int StatusCode, ErrorResponse Body) ForSubmission(IReadOnlyList<ValidationError> errors) =>
        ValidationFailed(errors);

    public static (int StatusCode, ErrorResponse Body) ForQuery(IReadOnlyList<ValidationError> errors) =>
        IsNotFound(errors) ? NotFound() : ValidationFailed(errors);

    public static (int StatusCode, ErrorResponse Body) ForDecision(IReadOnlyList<ValidationError> errors)
    {
        if (IsNotFound(errors))
            return NotFound();

        // Only state refusals are a conflict; any other failing field makes it a validation
        // failure, and then every error (including the state one) is listed.
        return errors.All(e => e.FieldName == StatusField) ? Conflict(errors) : ValidationFailed(errors);
    }

    public static (int StatusCode, ErrorResponse Body) ForDrafting(DraftingResult result)
    {
        var errors = result.Errors;

        if (IsNotFound(errors))
            return NotFound();

        if (result.FailureCategory is { } category)
            return GenerationFailed(category.ToString());

        // Content that failed the draft validation rules is unusable model output, not a
        // client mistake; its messages are never forwarded.
        if (errors.Any(e => DraftSectionFields.Contains(e.FieldName)))
            return GenerationFailed(MalformedOutputCategory);

        if (errors.All(e => e.FieldName == StatusField))
            return Conflict(errors);

        // Anything else (for example a generator exception with no category) is unexpected.
        return Unexpected();
    }

    public static (int StatusCode, ErrorResponse Body) ForDesigning(DesignDraftingResult result)
    {
        var errors = result.Errors;

        if (IsNotFound(errors))
            return NotFound();

        if (result.FailureCategory is { } category)
            return DesignGenerationFailed(category.ToString());

        // Content that failed the design validation rules is unusable model output, not a
        // client mistake; its messages are never forwarded.
        if (errors.Any(e => DesignSectionFields.Contains(e.FieldName)))
            return DesignGenerationFailed(MalformedOutputCategory);

        // Not Approved, or no requirements draft (or it changed): the request conflicts with the
        // current state of the change request.
        if (errors.All(e => e.FieldName is StatusField or RequirementsDraftField))
            return Conflict(errors);

        return Unexpected();
    }

    public static (int StatusCode, ErrorResponse Body) ForProposing(ProposalResult result)
    {
        var errors = result.Errors;

        if (IsNotFound(errors))
            return NotFound();

        // The system's own checks refused what the model produced: only the reason is reported.
        if (result.Rejection is { } rejection)
            return (502, new ErrorResponse(
                "proposal-rejected",
                "The change proposal was refused by the system's checks.",
                Category: rejection.ToString()));

        if (result.FailureCategory is { } category)
            return (502, new ErrorResponse(
                "generation-failed",
                "The change proposal could not be generated.",
                Category: category.ToString()));

        // Not Approved, no requirements draft, no design draft, or the design is not current: the
        // request conflicts with the current state of the change request.
        if (errors.All(e => e.FieldName is StatusField or RequirementsDraftField or DesignDraftField or DesignDraftVersionField))
            return Conflict(errors);

        return Unexpected();
    }

    public static (int StatusCode, ErrorResponse Body) NoProposal() =>
        (404, new ErrorResponse("no-proposal", "No change proposal exists yet for this change request."));

    public static (int StatusCode, ErrorResponse Body) ProposalNotConfigured() =>
        (503, new ErrorResponse("proposal-not-configured", "Change proposals are not configured on this server."));

    public static (int StatusCode, ErrorResponse Body) ProposalToolsUnavailable() =>
        (503, new ErrorResponse(
            "proposal-tools-unavailable",
            "Change proposals need the sample codebase tools, which are not switched on."));

    public static (int StatusCode, ErrorResponse Body) NoStepDue(WorkflowReason reason) =>
        (409, new ErrorResponse(
            "no-step-due",
            reason.Message,
            new[] { new ErrorField("Reason", reason.Code.ToString()) }));

    public static (int StatusCode, ErrorResponse Body) NoDesignDraft() =>
        (404, new ErrorResponse("no-design-draft", "No design draft exists yet for this change request."));

    public static (int StatusCode, ErrorResponse Body) DesignNotConfigured() =>
        (503, new ErrorResponse("design-not-configured", "Design drafting is not configured on this server."));

    public static (int StatusCode, ErrorResponse Body) NoDraft() =>
        (404, new ErrorResponse("no-draft", "No draft exists yet for this change request."));

    public static (int StatusCode, ErrorResponse Body) DraftingNotConfigured() =>
        (503, new ErrorResponse("drafting-not-configured", "Requirements drafting is not configured on this server."));

    public static (int StatusCode, ErrorResponse Body) NotFound() =>
        (404, new ErrorResponse("not-found", "No change request with that identifier exists."));

    public static (int StatusCode, ErrorResponse Body) InvalidIdentifier() =>
        (400, new ErrorResponse("invalid-identifier", "The change request identifier is not valid."));

    public static (int StatusCode, ErrorResponse Body) InvalidBody() =>
        (400, new ErrorResponse("invalid-body", "The request body is missing, malformed or not a JSON object."));

    public static (int StatusCode, ErrorResponse Body) RouteNotFound() =>
        (404, new ErrorResponse("route-not-found", "No such route."));

    public static (int StatusCode, ErrorResponse Body) MethodNotAllowed() =>
        (405, new ErrorResponse("method-not-allowed", "That method is not supported for this route."));

    public static (int StatusCode, ErrorResponse Body) UnsupportedMediaType() =>
        (415, new ErrorResponse("unsupported-media-type", "The request body must be application/json."));

    public static (int StatusCode, ErrorResponse Body) BodyTooLarge() =>
        (413, new ErrorResponse("body-too-large", "The request body is larger than the allowed size."));

    public static (int StatusCode, ErrorResponse Body) InvalidHost() =>
        (400, new ErrorResponse("invalid-host", "The request host is not allowed."));

    public static (int StatusCode, ErrorResponse Body) StorageUnavailable(StorageFailureCategory category) =>
        (503, new ErrorResponse(
            "storage-unavailable",
            "The change request storage is unavailable.",
            Category: category.ToString()));

    public static (int StatusCode, ErrorResponse Body) VersionConflict() =>
        (409, new ErrorResponse(
            "version-conflict",
            "The request conflicts with a concurrent change; try again."));

    public static (int StatusCode, ErrorResponse Body) Unexpected() =>
        (500, new ErrorResponse("unexpected-error", "An unexpected error occurred."));

    private static bool IsNotFound(IReadOnlyList<ValidationError> errors) =>
        errors.Any(e => e.FieldName == ChangeRequestIdField);

    private static (int StatusCode, ErrorResponse Body) GenerationFailed(string category) =>
        (502, new ErrorResponse(
            "generation-failed",
            "The requirements draft could not be generated.",
            Category: category));

    private static (int StatusCode, ErrorResponse Body) DesignGenerationFailed(string category) =>
        (502, new ErrorResponse(
            "generation-failed",
            "The design draft could not be generated.",
            Category: category));

    private static (int StatusCode, ErrorResponse Body) Conflict(IReadOnlyList<ValidationError> errors) =>
        (409, new ErrorResponse(
            "conflict",
            "The request conflicts with the current state of the change request.",
            errors.Select(e => new ErrorField(e.FieldName, e.Message)).ToArray()));

    private static (int StatusCode, ErrorResponse Body) ValidationFailed(IReadOnlyList<ValidationError> errors) =>
        (400, new ErrorResponse(
            "validation-failed",
            "One or more fields are invalid.",
            errors.Select(e => new ErrorField(e.FieldName, e.Message)).ToArray()));
}
