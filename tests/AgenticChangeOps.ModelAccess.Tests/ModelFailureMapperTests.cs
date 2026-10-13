using AgenticChangeOps.ModelAccess;

namespace AgenticChangeOps.ModelAccess.Tests;

public class ModelFailureMapperTests
{
    [Fact]
    public void RequestTimeoutStatus_IsTimedOut() =>
        Assert.Equal(GenerationFailureCategory.TimedOut, ModelFailureMapper.FromError(408, null));

    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(404)]
    [InlineData(429)]
    [InlineData(500)]
    [InlineData(502)]
    [InlineData(503)]
    public void AuthThrottleAndServiceSideStatuses_AreUnreachable(int status) =>
        Assert.Equal(GenerationFailureCategory.Unreachable, ModelFailureMapper.FromError(status, null));

    [Fact]
    public void ContentFilterBlock_IsMalformedOutput() =>
        Assert.Equal(GenerationFailureCategory.MalformedOutput, ModelFailureMapper.FromError(400, "content_filter"));

    [Fact]
    public void TimeoutException_IsTimedOut() =>
        Assert.Equal(GenerationFailureCategory.TimedOut, ModelFailureMapper.FromException(new TimeoutException()));

    [Fact]
    public void NetworkAndOtherExceptions_AreUnreachable()
    {
        Assert.Equal(GenerationFailureCategory.Unreachable,
            ModelFailureMapper.FromException(new HttpRequestException("synthetic")));
        Assert.Equal(GenerationFailureCategory.Unreachable,
            ModelFailureMapper.FromException(new InvalidOperationException("synthetic")));
    }
}
