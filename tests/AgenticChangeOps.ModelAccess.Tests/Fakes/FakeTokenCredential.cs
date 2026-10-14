using Azure.Core;

namespace AgenticChangeOps.ModelAccess.Tests.Fakes;

/// <summary>A credential that returns a fixed, meaningless token and counts how often it was asked.</summary>
public sealed class FakeTokenCredential : TokenCredential
{
    private int _requests;

    public int TokenRequests => Volatile.Read(ref _requests);

    public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _requests);
        return new AccessToken("synthetic-token", DateTimeOffset.UtcNow.AddHours(1));
    }

    public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
        new(GetToken(requestContext, cancellationToken));
}
