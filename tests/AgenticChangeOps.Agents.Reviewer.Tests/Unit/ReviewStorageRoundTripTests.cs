using System.Text.Json;
using System.Text.Json.Serialization;
using AgenticChangeOps.Intake.Models;
using AgenticChangeOps.Intake.Models.Enums;
using AgenticChangeOps.Intake.Stores;
using AgenticChangeOps.Persistence;

namespace AgenticChangeOps.Agents.Reviewer.Tests.Unit;

/// <summary>
/// The typed review record through both stores: saved with its audit entry, read back in version order,
/// identical after a restart of the durable store, immutable, and no schema change.
/// </summary>
public sealed class ReviewStorageRoundTripTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "changeops-review-" + Guid.NewGuid().ToString("N"));

    public ReviewStorageRoundTripTests() => Directory.CreateDirectory(_folder);

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { Directory.Delete(_folder, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string DbPath => Path.Combine(_folder, "changeops.db");

    private static string Json(Review review)
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new JsonStringEnumConverter());
        return JsonSerializer.Serialize(review, options);
    }

    private static Review Review(Guid id, int version, bool notPerformed = false) => new(
        ReviewId: new Guid(version, 0, 0, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }),
        ChangeRequestId: id,
        Version: version,
        RequirementsVersion: 2,
        DesignVersion: 3,
        ProposalVersion: 4,
        GeneratedAt: new DateTime(2026, 1, 15, 9, 0, version, DateTimeKind.Utc),
        Status: ReviewStatus.Draft,
        AgentName: Reviewer.Review.ReviewerAgentName,
        SystemCheck: notPerformed
            ? new SystemCheck(SystemCheckStatus.NotPerformed, Array.Empty<EditCheck>())
            : new SystemCheck(SystemCheckStatus.Performed, new[]
            {
                new EditCheck(1, EditCheckOutcome.Found),
                new EditCheck(2, EditCheckOutcome.NewFileFree),
                new EditCheck(3, EditCheckOutcome.QuoteAmbiguous)
            }),
        Findings: new[]
        {
            new ReviewFinding(
                FindingCategory.DesignConsistency, FindingSeverity.High, "The notes file is not in the design. é",
                new[]
                {
                    new ReviewReference(ReviewDocument.Design, ReferenceList.AffectedComponents, 2),
                    new ReviewReference(ReviewDocument.Proposal, ReferenceList.Changes, 3)
                }),
            new ReviewFinding(
                FindingCategory.UnmentionedRisk, FindingSeverity.Low, "A reader sees the report late.",
                new[] { new ReviewReference(ReviewDocument.Proposal, ReferenceList.Risks, 1) })
        },
        OpenQuestions: new[] { "Who confirms the new hour?" },
        AdvisoryNote: Reviewer.Review.AdvisoryNoteText);

    private static ChangeRequest Request() => new(
        Guid.NewGuid(), "Move nightly report schedule", "Move the job.", "Avery Quill", "Sample Billing Portal",
        Priority.Medium, ChangeType.ConfigChange, ChangeRequestStatus.Approved,
        new DateTime(2026, 1, 15, 9, 0, 0, DateTimeKind.Utc));

    private static ChangeRequestAuditEntry Entry(Review r) => new(
        AuditEntryId: Guid.NewGuid(),
        ChangeRequestId: r.ChangeRequestId,
        EventType: AuditEventType.ReviewGenerated,
        OccurredAt: r.GeneratedAt,
        Details: null,
        StatusBefore: ChangeRequestStatus.Approved,
        StatusAfter: ChangeRequestStatus.Approved,
        AgentName: r.AgentName,
        DraftVersion: r.Version);

    public static IEnumerable<object[]> Stores()
    {
        yield return new object[] { "memory" };
        yield return new object[] { "sqlite" };
    }

    private IChangeRequestStore Open(string kind) => kind == "memory"
        ? new InMemoryChangeRequestStore()
        : new SqliteChangeRequestStore(new SqliteStorageOptions(DbPath));

    [Theory]
    [MemberData(nameof(Stores))]
    public void AReview_IsStoredWithItsAuditEntry_AndReadBackEqual(string kind)
    {
        var store = Open(kind);
        var request = Request();
        store.Save(request);
        var review = Review(request.ChangeRequestId, 1);

        store.SaveReview(review, Entry(review));

        var read = Assert.Single(store.GetReviews(request.ChangeRequestId));
        Assert.Equal(Json(review), Json(read));
        Assert.Equal(ReviewStatus.Draft, read.Status);
        Assert.Equal((2, 3, 4), (read.RequirementsVersion, read.DesignVersion, read.ProposalVersion));
        Assert.Equal(FindingSeverity.High, read.Findings[0].Severity);
        Assert.Equal(EditCheckOutcome.QuoteAmbiguous, read.SystemCheck.Edits[2].Outcome);
        Assert.Single(store.GetAuditEntries(request.ChangeRequestId), e => e.EventType == AuditEventType.ReviewGenerated);
    }

    [Theory]
    [MemberData(nameof(Stores))]
    public void ANotPerformedCheck_RoundTripsWithAnEmptyEditList(string kind)
    {
        var store = Open(kind);
        var request = Request();
        store.Save(request);
        var review = Review(request.ChangeRequestId, 1, notPerformed: true);

        store.SaveReview(review, Entry(review));

        var read = Assert.Single(store.GetReviews(request.ChangeRequestId));
        Assert.Equal(SystemCheckStatus.NotPerformed, read.SystemCheck.Status);
        Assert.Empty(read.SystemCheck.Edits);
    }

    [Theory]
    [MemberData(nameof(Stores))]
    public void Reviews_AreReturnedInAscendingVersionOrder_PerChangeRequest(string kind)
    {
        var store = Open(kind);
        var a = Request();
        var b = Request();
        store.Save(a);
        store.Save(b);
        foreach (var version in new[] { 1, 2, 3 })
        {
            var r = Review(a.ChangeRequestId, version);
            store.SaveReview(r, Entry(r));
        }

        var other = Review(b.ChangeRequestId, 1);
        store.SaveReview(other, Entry(other));

        Assert.Equal(new[] { 1, 2, 3 }, store.GetReviews(a.ChangeRequestId).Select(r => r.Version));
        Assert.Single(store.GetReviews(b.ChangeRequestId));
    }

    [Theory]
    [MemberData(nameof(Stores))]
    public void ADuplicateVersion_IsRefused_AndNothingIsAdded(string kind)
    {
        var store = Open(kind);
        var request = Request();
        store.Save(request);
        var first = Review(request.ChangeRequestId, 1);
        store.SaveReview(first, Entry(first));
        var auditBefore = store.GetAuditEntries(request.ChangeRequestId).Count;

        Assert.ThrowsAny<Exception>(() => store.SaveReview(Review(request.ChangeRequestId, 1, notPerformed: true), Entry(first)));

        var kept = Assert.Single(store.GetReviews(request.ChangeRequestId));
        Assert.Equal(Json(first), Json(kept)); // the first is untouched by the refused second
        Assert.Equal(auditBefore, store.GetAuditEntries(request.ChangeRequestId).Count);
    }

    [Fact]
    public void TheDurableStore_ReturnsTheSameReviews_AfterARestart()
    {
        var request = Request();
        var review = Review(request.ChangeRequestId, 1);
        using (var store = (SqliteChangeRequestStore)Open("sqlite"))
        {
            store.Save(request);
            store.SaveReview(review, Entry(review));
        }
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

        using var reopened = new SqliteChangeRequestStore(new SqliteStorageOptions(DbPath));

        var read = Assert.Single(reopened.GetReviews(request.ChangeRequestId));
        Assert.Equal(Json(review), Json(read));
    }

    [Fact]
    public void AStoreWithNoReviews_ReadsAnEmptyList_ForKnownAndUnknownIds()
    {
        var store = Open("memory");
        var request = Request();
        store.Save(request);

        Assert.Empty(store.GetReviews(request.ChangeRequestId));
        Assert.Empty(store.GetReviews(Guid.NewGuid()));
    }

    [Fact]
    public void StoringAReview_LeavesTheStorageLayoutVersionAtTwo()
    {
        var request = Request();
        var review = Review(request.ChangeRequestId, 1);
        using (var store = (SqliteChangeRequestStore)Open("sqlite"))
        {
            store.Save(request);
            store.SaveReview(review, Entry(review));
        }
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

        using var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=" + DbPath + ";Mode=ReadOnly");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version";

        Assert.Equal(2L, command.ExecuteScalar());
    }

    [Theory]
    [MemberData(nameof(Stores))]
    public void TheStoredJson_HoldsNoKeyTheRecordDoesNotDefine(string kind)
    {
        var store = Open(kind);
        var request = Request();
        store.Save(request);
        var review = Review(request.ChangeRequestId, 1);
        store.SaveReview(review, Entry(review));

        var read = Assert.Single(store.GetReviews(request.ChangeRequestId));

        var keys = JsonDocument.Parse(Json(read)).RootElement.EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal);
        Assert.Equal(
            new[]
            {
                "AdvisoryNote", "AgentName", "ChangeRequestId", "DesignVersion", "Findings", "GeneratedAt", "OpenQuestions",
                "ProposalVersion", "RequirementsVersion", "ReviewId", "Status", "SystemCheck", "Version"
            },
            keys);
    }
}
