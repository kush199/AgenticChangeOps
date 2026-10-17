using AgenticChangeOps.Intake.Infrastructure;
using AgenticChangeOps.Intake.Models;
using AgenticChangeOps.Intake.Models.Enums;
using AgenticChangeOps.Intake.Services;
using AgenticChangeOps.Intake.Stores;
using AgenticChangeOps.Intake.Tests.Fakes;
using AgenticChangeOps.ModelAccess;

namespace AgenticChangeOps.Agents.Architect.Tests.Fakes;

/// <summary>
/// The real design generator wired to fake model clients and a fake tool session factory, inside the
/// real drafting service over one in-memory store. Lets tests see what the service stores and audits.
/// </summary>
public sealed class SampleToolsRig
{
    public static readonly AzureOpenAIGeneratorOptions Options = new(
        new Uri("https://sentinel-endpoint.example.invalid/"), "sentinel-deployment", TimeSpan.FromSeconds(30), 32000);

    public InMemoryChangeRequestStore Store { get; } = new();
    public FakeSystemClock Clock { get; } = new();
    public CountingIdGenerator Ids { get; } = new();
    public FakeRequirementsBasisSource Basis { get; } = new();
    public FakeChatModelClient PlainClient { get; } = new();
    public FakeToolChatModelClient ToolClient { get; } = new();
    public FakeToolSessionFactory Sessions { get; } = new();
    public ChangeRequestService Intake { get; }
    public ApprovalService Approval { get; }
    public DesignDraftingService Designing { get; }

    public SampleToolsRig(bool toolsOn = true)
    {
        Intake = new ChangeRequestService(Store, Clock, Ids, new ChangeRequestValidator());
        Approval = new ApprovalService(Store, Clock, Ids, new ApprovalDecisionValidator());
        IDesignDraftGenerator generator = toolsOn
            ? new AzureOpenAIDesignGenerator(Options, PlainClient, Sessions, ToolClient)
            : new AzureOpenAIDesignGenerator(Options, PlainClient);
        Designing = new DesignDraftingService(Store, Clock, Ids, Basis, generator, new DesignContentValidator());
    }

    /// <summary>Submits a synthetic request about the orders service, gives it a requirements draft, and approves it.</summary>
    public Guid ApprovedOrdersRequest()
    {
        var result = Intake.Submit(new SubmissionRequest(
            Title: "Raise the order page size",
            Description: "Raise the number of orders shown per page in the orders service.",
            Requester: "Avery Quill",
            AffectedSystem: "Orders Service",
            Priority: Priority.Medium,
            ChangeType: ChangeType.ConfigChange));
        var id = result.ChangeRequest!.ChangeRequestId;
        Basis.Set(id, FakeRequirementsBasisSource.Basis(1));
        var decision = Approval.RecordDecision(new ApprovalDecisionRequest(id, "Jordan Reed", ApprovalDecision.Approve, null));
        if (!decision.IsSuccess)
            throw new InvalidOperationException("Test setup could not approve the request.");
        return id;
    }

    public IReadOnlyList<DesignDraft> Designs(Guid id) => Store.GetDesigns(id);

    public IReadOnlyList<ChangeRequestAuditEntry> Audit(Guid id) => Store.GetAuditEntries(id);
}
