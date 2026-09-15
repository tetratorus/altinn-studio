using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using WorkflowEngine.Data.Constants;

namespace WorkflowEngine.Core.Authentication;

/// <summary>
/// Authorization requirement for <c>/api/v1/{namespace}/...</c> routes: the authenticated caller must be an
/// operator or hold a <see cref="EngineAuthentication.NamespaceClaim"/> for the route's namespace.
/// Evaluated by the authorization middleware, before any parameter binding or handler code runs.
/// </summary>
internal sealed class NamespaceAccessRequirement : IAuthorizationRequirement
{
    public const string RouteParameter = "namespace";
}

internal sealed class NamespaceAccessHandler : AuthorizationHandler<NamespaceAccessRequirement, HttpContext>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        NamespaceAccessRequirement requirement,
        HttpContext httpContext
    )
    {
        var routeValue = httpContext.GetRouteValue(NamespaceAccessRequirement.RouteParameter) as string;

        string normalized;
        try
        {
            normalized = WorkflowNamespace.Normalize(routeValue);
        }
        catch (ArgumentException)
        {
            return Task.CompletedTask;
        }

        if (EngineAuthentication.CanAccessNamespace(context.User, normalized))
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}
