using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace AgenticChangeOps.Agents.Reviewer.Tests.Unit;

/// <summary>
/// Step 1 adds only logic that nothing calls (FR-021). These checks keep it that way and keep the
/// record shapes honest: a stored review round trips with enum names as strings, holds no path, quote
/// or file text, and the project stays inside its dependency and token rules. The reachability list
/// below is the one thing a later step edits (step 3 for its own tests, step 4 for the composition root).
/// </summary>
public sealed class ReviewStructureAtStep1Tests
{
    private const string AgentProject = "AgenticChangeOps.Agents.Reviewer";
    private static readonly string[] SkippedDirectories = { "bin", "obj" };

    /// <summary>Projects (folder names under src or tests) allowed to mention the Reviewer at this step.</summary>
    private static readonly string[] MayMentionTheReviewer =
    {
        "AgenticChangeOps.Agents.Reviewer",
        "AgenticChangeOps.Agents.Reviewer.Tests",
        // Step 4: the composition root registers the service (no route), and its tests prove it.
        "AgenticChangeOps.Api",
        "AgenticChangeOps.Api.Tests"
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

    private static JsonSerializerOptions Options()
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private static Review SampleReview() => new(
        ReviewId: Guid.Parse("00000000-0000-0000-0000-0000000000a1"),
        ChangeRequestId: Guid.Parse("00000000-0000-0000-0000-0000000000b2"),
        Version: 2,
        RequirementsVersion: 1,
        DesignVersion: 3,
        ProposalVersion: 4,
        GeneratedAt: new DateTime(2026, 10, 22, 9, 30, 0, DateTimeKind.Utc),
        Status: ReviewStatus.Draft,
        AgentName: Review.ReviewerAgentName,
        SystemCheck: new SystemCheck(
            SystemCheckStatus.Performed,
            new[] { new EditCheck(1, EditCheckOutcome.Found), new EditCheck(2, EditCheckOutcome.NewFileFree) }),
        Findings: new[]
        {
            new ReviewFinding(
                FindingCategory.UnmentionedRisk,
                FindingSeverity.High,
                "A reader in another time zone sees the report late.",
                new[] { new ReviewReference(ReviewDocument.Proposal, ReferenceList.Risks, 1) })
        },
        OpenQuestions: new[] { "Who confirms the new hour?" },
        AdvisoryNote: Review.AdvisoryNoteText);

    // ---- the record -------------------------------------------------------------------

    [Fact]
    public void AReview_RoundTripsThroughJson_WithEnumNamesAsStrings()
    {
        var review = SampleReview();

        var json = JsonSerializer.Serialize(review, Options());
        var back = JsonSerializer.Deserialize<Review>(json, Options());

        Assert.NotNull(back);
        Assert.Equal(json, JsonSerializer.Serialize(back, Options()));
        Assert.Contains("\"Status\":\"Draft\"", json);
        Assert.Contains("\"Outcome\":\"NewFileFree\"", json);
        Assert.Contains("\"Category\":\"UnmentionedRisk\"", json);
        Assert.Contains("\"Severity\":\"High\"", json);
        Assert.Contains("\"Document\":\"Proposal\"", json);
        Assert.Contains("\"List\":\"Risks\"", json);
        Assert.Equal(review.Version, back!.Version);
        Assert.Equal(review.ProposalVersion, back.ProposalVersion);
    }

    [Fact]
    public void AReview_IsAnAgentOutput_WithTheConstantsTheDataModelNames()
    {
        Assert.IsAssignableFrom<AgenticChangeOps.Intake.Models.IAgentOutput>(SampleReview());
        Assert.Equal("ReviewerAgent", Review.ReviewerAgentName);
        Assert.Equal("Advisory only. Nothing acts on this review.", Review.AdvisoryNoteText);
        Assert.Equal(new ReviewLimits(8, 8000), ReviewLimits.Defaults);
    }

    [Fact]
    public void NoStoredType_HasAMemberThatCouldHoldAPathAQuoteOrFileText()
    {
        var stored = new[] { typeof(Review), typeof(SystemCheck), typeof(EditCheck), typeof(ReviewFinding), typeof(ReviewReference) };
        var names = stored.SelectMany(t => t.GetProperties()).Select(p => p.Name).ToList();

        var forbidden = new[] { "Path", "Quote", "ExistingText", "ReplacementText", "FileText", "Text", "Content", "Raw" };
        Assert.DoesNotContain(names, n => forbidden.Contains(n, StringComparer.OrdinalIgnoreCase));
        Assert.Contains("Statement", names); // the one free-text member is the finding's own statement
    }

    // ---- the project ------------------------------------------------------------------

    [Fact]
    public void TheProject_ReferencesOnlyDraftingIntakeAndModelAccess()
    {
        var path = Path.Combine(RepositoryRoot(), "src", AgentProject, AgentProject + ".csproj");

        var references = Regex.Matches(File.ReadAllText(path), @"<ProjectReference\s+Include=""([^""]+)""")
            .Select(m => Path.GetFileNameWithoutExtension(m.Groups[1].Value.Replace('\\', '/')))
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(new[] { "AgenticChangeOps.Drafting", "AgenticChangeOps.Intake", "AgenticChangeOps.ModelAccess" }, references);
    }

    [Fact]
    public void TheSource_NamesNoOtherAgent_NoSampleCodeProject_NoFrameworkOrToolProtocol()
    {
        var folder = Path.Combine(RepositoryRoot(), "src", AgentProject);
        var files = Files(folder, "*.cs").ToList();
        Assert.NotEmpty(files);

        var forbidden = new[]
        {
            "AgenticChangeOps.Agents.Requirement", "AgenticChangeOps.Agents.Architect", "AgenticChangeOps.Agents.Developer",
            "AgenticChangeOps.SampleCode", "Microsoft.Agents", "Microsoft.Extensions.AI", "ModelContextProtocol"
        };

        foreach (var file in files)
        {
            var text = File.ReadAllText(file);
            foreach (var token in forbidden)
                Assert.False(text.Contains(token, StringComparison.Ordinal), $"{Path.GetFileName(file)} mentions {token}.");
        }
    }

    [Fact]
    public void TheSource_ContainsNoCopyOfASharedBehaviour()
    {
        var folder = Path.Combine(RepositoryRoot(), "src", AgentProject);
        var fence = new string('`', 3);
        var tokens = new[] { "CancellationTokenSource", "JsonDocument", "GetProperty(", "IsNullOrWhiteSpace(", fence };

        foreach (var file in Files(folder, "*.cs"))
        {
            var text = File.ReadAllText(file);
            foreach (var token in tokens)
                Assert.False(text.Contains(token, StringComparison.Ordinal), $"{Path.GetFileName(file)} contains {token}.");
        }
    }

    // ---- unreachable at this step -----------------------------------------------------

    [Fact]
    public void NothingOutsideTheReviewerAndItsTests_ReferencesTheReviewer()
    {
        var root = RepositoryRoot();
        var offenders = new List<string>();

        foreach (var area in new[] { "src", "tests" })
        {
            foreach (var project in Directory.GetDirectories(Path.Combine(root, area)))
            {
                var name = Path.GetFileName(project);
                if (MayMentionTheReviewer.Contains(name))
                    continue;

                foreach (var file in Files(project, "*.cs").Concat(Files(project, "*.csproj")))
                    if (File.ReadAllText(file).Contains(AgentProject, StringComparison.Ordinal))
                        offenders.Add(Path.GetRelativePath(root, file));
            }
        }

        Assert.True(offenders.Count == 0, "Step 1 must be unreachable, but these files mention the Reviewer: " + string.Join(", ", offenders));
    }
}
