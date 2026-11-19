using Altinn.Studio.Designer.Middleware.UserRequestSynchronization.Abstractions;
using Altinn.Studio.Designer.Middleware.UserRequestSynchronization.OrgWide;
using Altinn.Studio.Designer.Middleware.UserRequestSynchronization.Services;
using Altinn.Studio.Designer.Models;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;

namespace Designer.Tests.Middleware.UserRequestSynchronization;

public class RequestSyncEvaluatorTests
{
    private readonly Mock<IOrgWideSyncEligibilityEvaluator> _eligibilityEvaluatorMock = new();
    private readonly Mock<IRequestContextResolver<AltinnOrgContext>> _contextResolverMock = new();

    [Fact]
    public void TryEvaluateShouldSyncRequest_WhenEndpointIsNotEligible_DoesNotResolveContext()
    {
        var httpContext = new DefaultHttpContext();
        _eligibilityEvaluatorMock.Setup(e => e.IsEligibleForSynchronization(httpContext)).Returns(false);

        bool shouldSync = CreateEvaluator().TryEvaluateShouldSyncRequest(httpContext, out AltinnOrgContext context);

        Assert.False(shouldSync);
        Assert.Null(context);
        AltinnOrgContext ignored;
        _contextResolverMock.Verify(r => r.TryResolveContext(It.IsAny<HttpContext>(), out ignored), Times.Never);
    }

    [Fact]
    public void TryEvaluateShouldSyncRequest_WhenEndpointIsEligibleAndContextResolves_ReturnsContext()
    {
        var httpContext = new DefaultHttpContext();
        AltinnOrgContext expectedContext = AltinnOrgContext.FromOrg("ttd");
        _eligibilityEvaluatorMock.Setup(e => e.IsEligibleForSynchronization(httpContext)).Returns(true);
        _contextResolverMock.Setup(r => r.TryResolveContext(httpContext, out expectedContext)).Returns(true);

        bool shouldSync = CreateEvaluator().TryEvaluateShouldSyncRequest(httpContext, out AltinnOrgContext context);

        Assert.True(shouldSync);
        Assert.Equal(expectedContext, context);
    }

    private RequestSyncEvaluator<AltinnOrgContext, IOrgWideSyncEligibilityEvaluator> CreateEvaluator() =>
        new([_eligibilityEvaluatorMock.Object], _contextResolverMock.Object);
}
