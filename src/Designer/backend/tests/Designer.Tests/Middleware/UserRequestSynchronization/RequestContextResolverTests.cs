using System.Security.Claims;
using Altinn.Studio.Designer.Middleware.UserRequestSynchronization.OrgWide.Services;
using Altinn.Studio.Designer.Middleware.UserRequestSynchronization.RepoUserWide.Services;
using Altinn.Studio.Designer.Middleware.UserRequestSynchronization.Services;
using Altinn.Studio.Designer.Models;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Designer.Tests.Middleware.UserRequestSynchronization;

public class RequestContextResolverTests
{
    private const string Developer = "testUser";

    [Theory]
    [InlineData("_ttd")]
    [InlineData("ttd!")]
    public void OrgWideResolver_WithInvalidOrg_ReturnsFalseWithoutThrowing(string org)
    {
        var resolver = new OrgWideRequestContextResolver(new HttpContextDataExtractor());
        HttpContext httpContext = CreateHttpContext(org, app: null);

        bool resolved = resolver.TryResolveContext(httpContext, out AltinnOrgContext context);

        Assert.False(resolved);
        Assert.Null(context);
    }

    [Fact]
    public void OrgWideResolver_WithValidOrg_ResolvesContext()
    {
        var resolver = new OrgWideRequestContextResolver(new HttpContextDataExtractor());
        HttpContext httpContext = CreateHttpContext("ttd", app: null);

        bool resolved = resolver.TryResolveContext(httpContext, out AltinnOrgContext context);

        Assert.True(resolved);
        Assert.Equal("ttd", context.Org);
    }

    [Theory]
    [InlineData("_ttd", "valid-app")]
    [InlineData("ttd", "1nvalid.app")]
    [InlineData("ttd", "datamodels")]
    public void RepoUserWideResolver_WithInvalidOrgOrApp_ReturnsFalseWithoutThrowing(string org, string app)
    {
        var resolver = new RepoUserWideRequestContextResolver(new HttpContextDataExtractor());
        HttpContext httpContext = CreateHttpContext(org, app);

        bool resolved = resolver.TryResolveContext(httpContext, out AltinnRepoEditingContext context);

        Assert.False(resolved);
        Assert.Null(context);
    }

    [Fact]
    public void RepoUserWideResolver_WithValidOrgAndApp_ResolvesContext()
    {
        var resolver = new RepoUserWideRequestContextResolver(new HttpContextDataExtractor());
        HttpContext httpContext = CreateHttpContext("ttd", "valid-app");

        bool resolved = resolver.TryResolveContext(httpContext, out AltinnRepoEditingContext context);

        Assert.True(resolved);
        Assert.Equal("ttd", context.Org);
        Assert.Equal("valid-app", context.Repo);
        Assert.Equal(Developer, context.Developer);
    }

    private static HttpContext CreateHttpContext(string org, string app)
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, Developer)], "test")),
        };
        httpContext.Request.RouteValues["org"] = org;
        if (app is not null)
        {
            httpContext.Request.RouteValues["app"] = app;
        }

        return httpContext;
    }
}
