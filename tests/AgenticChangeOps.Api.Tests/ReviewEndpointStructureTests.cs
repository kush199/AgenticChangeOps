using System.Text.RegularExpressions;
using AgenticChangeOps.Api.Tests.Fakes;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace AgenticChangeOps.Api.Tests;

/// <summary>
/// A review stays advisory content: no other project reads it, and the only route that mentions
/// reviews is the one POST.
/// </summary>
public sealed class ReviewEndpointStructureTests
{
    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AgenticChangeOps.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    private static string[] References(string project) =>
        Regex.Matches(
                File.ReadAllText(Path.Combine(Root(), "src", project, project + ".csproj")),
                "ProjectReference Include=\"[^\"]*?([^\\\\\"]+)\\.csproj\"")
            .Select(m => m.Groups[1].Value)
            .ToArray();

    [Theory]
    [InlineData("AgenticChangeOps.Workflow")]
    [InlineData("AgenticChangeOps.Intake")]
    public void NoOtherProject_ReferencesTheReviewerProject(string project)
    {
        Assert.DoesNotContain("AgenticChangeOps.Agents.Reviewer", References(project));
    }

    [Fact]
    public void NoSourceOutsideTheApiAndTheReviewer_MentionsReviewEndpoints()
    {
        var offenders = Directory.GetFiles(Path.Combine(Root(), "src"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains(Path.Combine("src", "AgenticChangeOps.Api") + Path.DirectorySeparatorChar))
            .Where(f => File.ReadAllText(f).Contains("ReviewEndpoints", StringComparison.Ordinal))
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void ExactlyThreeRoutes_MentionReviews_OnePostAndTwoGets()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var reviewRoutes = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => (e.RoutePattern.RawText ?? string.Empty).Contains("review", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var described = reviewRoutes
            .Select(r => $"{string.Join(",", r.Metadata.GetMetadata<Microsoft.AspNetCore.Routing.HttpMethodMetadata>()!.HttpMethods)} {r.RoutePattern.RawText}")
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(
            new[]
            {
                "GET /change-requests/{id}/reviews",
                "GET /change-requests/{id}/reviews/latest",
                "POST /change-requests/{id}/reviews"
            },
            described);
    }
}
