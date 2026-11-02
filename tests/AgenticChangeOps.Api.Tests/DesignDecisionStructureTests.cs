using System.Text.RegularExpressions;
using AgenticChangeOps.Api.Tests.Fakes;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace AgenticChangeOps.Api.Tests;

/// <summary>Recording a decision is a person's action: only its own route may reach the service.</summary>
public sealed class DesignDecisionStructureTests
{
    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AgenticChangeOps.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    [Fact]
    public void OnlyTheDesignEndpointsAndTheCompositionNameTheDecisionService()
    {
        var allowed = new[]
        {
            Path.Combine("AgenticChangeOps.Agents.Architect", "DesignDecision"),
            Path.Combine("AgenticChangeOps.Agents.Architect", "IDesignDecisionService"),
            Path.Combine("AgenticChangeOps.Api", "Endpoints", "DesignEndpoints"),
            Path.Combine("AgenticChangeOps.Api", "Composition", "ApiServiceRegistration"),
            Path.Combine("AgenticChangeOps.Api", "Contracts", "DesignView"),
            Path.Combine("AgenticChangeOps.Api", "Errors", "ErrorMapper")
        };

        var offenders = Directory.GetFiles(Path.Combine(Root(), "src"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
            .Where(f => Regex.IsMatch(File.ReadAllText(f), @"IDesignDecisionService|DesignDecisionService\b"))
            .Where(f => !allowed.Any(a => f.Contains(a, StringComparison.Ordinal)))
            .Select(Path.GetFileName)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Theory]
    [InlineData("AgenticChangeOps.Workflow")]
    [InlineData("AgenticChangeOps.Agents.Requirement")]
    [InlineData("AgenticChangeOps.Agents.Developer")]
    [InlineData("AgenticChangeOps.Agents.Reviewer")]
    public void NoOtherProject_ReferencesTheArchitectProjectForDecisions(string project)
    {
        var files = Directory.GetFiles(Path.Combine(Root(), "src", project), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar));

        Assert.DoesNotContain(files, f => File.ReadAllText(f).Contains("DesignDecision", StringComparison.Ordinal));
    }

    [Fact]
    public void ExactlyOneRoute_RecordsADesignDecision_AndItIsAPost()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var routes = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => (e.RoutePattern.RawText ?? string.Empty).Contains("decisions", StringComparison.OrdinalIgnoreCase)
                && (e.RoutePattern.RawText ?? string.Empty).Contains("designs", StringComparison.OrdinalIgnoreCase))
            .Select(r => $"{string.Join(",", r.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods)} {r.RoutePattern.RawText}")
            .ToArray();

        Assert.Equal(new[] { "POST /change-requests/{id}/designs/{version}/decisions" }, routes);
    }
}
