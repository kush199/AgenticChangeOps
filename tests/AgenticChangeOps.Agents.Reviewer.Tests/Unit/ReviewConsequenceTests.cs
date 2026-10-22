using System.Text.RegularExpressions;
using AgenticChangeOps.Agents.Reviewer.Tests.Fakes;
using AgenticChangeOps.Intake.Models.Enums;

namespace AgenticChangeOps.Agents.Reviewer.Tests.Unit;

/// <summary>
/// FR-011 and Principle III: a review is advisory and non-consequential. Nothing in the system acts on
/// it, it changes no status and approves, rejects or clears nothing, and the parts of the system that
/// make decisions cannot even see it.
/// </summary>
public sealed class ReviewConsequenceTests
{
    private static readonly string[] SkippedDirectories = { "bin", "obj" };

    /// <summary>Folders under src that may name the review types: the Reviewer itself and the composition root.</summary>
    private static readonly string[] MayNameReviewTypes =
    {
        "AgenticChangeOps.Agents.Reviewer",
        "AgenticChangeOps.Api"
    };

    private static readonly string[] DecisionProjects =
    {
        "AgenticChangeOps.Workflow", "AgenticChangeOps.Intake", "AgenticChangeOps.Persistence", "AgenticChangeOps.Drafting"
    };

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AgenticChangeOps.sln")))
            directory = directory.Parent;

        Assert.True(directory is not null, "Could not locate the repository root (AgenticChangeOps.sln).");
        return directory!.FullName;
    }

    private static IEnumerable<string> Files(string folder, string pattern) =>
        Directory.EnumerateFiles(folder, pattern, SearchOption.AllDirectories)
            .Where(f => !Path.GetRelativePath(folder, f).Split(Path.DirectorySeparatorChar)
                .Any(part => SkippedDirectories.Contains(part)));

    // ---- the decision-making parts cannot see a review --------------------------------

    [Theory]
    [InlineData("AgenticChangeOps.Workflow")]
    [InlineData("AgenticChangeOps.Intake")]
    [InlineData("AgenticChangeOps.Persistence")]
    [InlineData("AgenticChangeOps.Drafting")]
    public void TheDecisionProjects_DoNotReferenceTheReviewerProject(string project)
    {
        var csproj = Path.Combine(RepositoryRoot(), "src", project, project + ".csproj");

        Assert.DoesNotContain("AgenticChangeOps.Agents.Reviewer", File.ReadAllText(csproj), StringComparison.Ordinal);
    }

    [Fact]
    public void NoSourceOutsideTheReviewerAndTheCompositionRoot_NamesAReviewType()
    {
        var src = Path.Combine(RepositoryRoot(), "src");
        var names = new Regex(
            @"\b(ReviewFinding|ReviewReference|FindingSeverity|FindingCategory|SystemCheck|IReviewService|ReviewService|ReviewStatus)\b",
            RegexOptions.CultureInvariant);
        var offenders = new List<string>();

        foreach (var project in Directory.GetDirectories(src).Where(d => !MayNameReviewTypes.Contains(Path.GetFileName(d))))
            foreach (var file in Files(project, "*.cs"))
                if (names.IsMatch(File.ReadAllText(file)))
                    offenders.Add(Path.GetRelativePath(RepositoryRoot(), file));

        Assert.True(offenders.Count == 0, "Only the Reviewer and the composition root may name review types: " + string.Join(", ", offenders));
    }

    [Fact]
    public void NothingInTheDecisionProjects_ReadsASeverity()
    {
        var src = Path.Combine(RepositoryRoot(), "src");

        foreach (var project in DecisionProjects)
            foreach (var file in Files(Path.Combine(src, project), "*.cs"))
                Assert.DoesNotContain("Severity", File.ReadAllText(file), StringComparison.Ordinal);
    }

    // ---- storing a review changes nothing -------------------------------------------

    [Fact]
    public async Task AReviewWithHighSeverityOnEveryFinding_ChangesNoStatusAndNoOtherRecord()
    {
        var f = new ReviewTestFixture();
        var id = f.ApproveWithBases();
        var requestBefore = f.Store.FindById(id);
        var auditBefore = f.Store.GetAuditEntries(id).ToArray();
        f.Generator.Behavior = FakeReviewGenerator.Returns(new ReviewContent(
            new[]
            {
                FakeReviewGenerator.Finding(severity: "High"),
                FakeReviewGenerator.Finding(statement: "Every one of these is serious.", severity: "High")
            },
            Array.Empty<string>()));

        var result = await f.Reviewing.ReviewAsync(id);

        Assert.True(result.IsSuccess);
        Assert.All(result.Review!.Findings, finding => Assert.Equal(FindingSeverity.High, finding.Severity));
        Assert.Equal(requestBefore, f.Store.FindById(id));
        Assert.Equal(ChangeRequestStatus.Approved, f.Store.FindById(id)!.Status);
        var added = f.Store.GetAuditEntries(id).Except(auditBefore).ToArray();
        var entry = Assert.Single(added);
        Assert.Equal(AuditEventType.ReviewGenerated, entry.EventType);
        Assert.Equal(entry.StatusBefore, entry.StatusAfter);
    }

    [Fact]
    public async Task ReviewsOfDifferentSeverity_LeaveTheRequestAndItsAuditTrailIdentical_ExceptForTheirOwnRecord()
    {
        var low = new ReviewTestFixture();
        var high = new ReviewTestFixture();
        var lowId = low.ApproveWithBases();
        var highId = high.ApproveWithBases();
        low.Generator.Behavior = FakeReviewGenerator.Returns(new ReviewContent(
            new[] { FakeReviewGenerator.Finding(severity: "Low") }, Array.Empty<string>()));
        high.Generator.Behavior = FakeReviewGenerator.Returns(new ReviewContent(
            new[] { FakeReviewGenerator.Finding(severity: "High") }, Array.Empty<string>()));

        await low.Reviewing.ReviewAsync(lowId);
        await high.Reviewing.ReviewAsync(highId);

        Assert.Equal(low.Store.FindById(lowId)!.Status, high.Store.FindById(highId)!.Status);
        Assert.Equal(
            low.Store.GetAuditEntries(lowId).Select(e => (e.EventType, e.StatusBefore, e.StatusAfter, e.AgentName, e.DraftVersion)),
            high.Store.GetAuditEntries(highId).Select(e => (e.EventType, e.StatusBefore, e.StatusAfter, e.AgentName, e.DraftVersion)));
    }

    [Fact]
    public async Task AReview_IsNeverAnApprovalDecision_AndRecordsNoApprover()
    {
        var f = new ReviewTestFixture();
        var id = f.ApproveWithBases();

        await f.Reviewing.ReviewAsync(id);

        var entry = f.Store.GetAuditEntries(id).Single(e => e.EventType == AuditEventType.ReviewGenerated);
        Assert.NotEqual(AuditEventType.Approved, entry.EventType);
        Assert.NotEqual(AuditEventType.Rejected, entry.EventType);
        Assert.NotEqual(AuditEventType.Deferred, entry.EventType);
        Assert.Null(entry.Details);
    }
}
