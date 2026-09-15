using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace WorkflowEngine.Core.Authentication;

/// <summary>
/// Authenticates requests with a configured engine API key, presented as
/// <c>Authorization: Bearer &lt;key&gt;</c>, the <c>X-Api-Key</c> header, or (for the dashboard's
/// browser session) the <see cref="EngineAuthentication.DashboardCookie"/> cookie.
/// </summary>
internal sealed class EngineApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    EngineApiKeyResolver resolver
) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    private const string BearerPrefix = "Bearer ";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var presented = ExtractKey(Request);
        if (presented is null)
            return Task.FromResult(AuthenticateResult.NoResult());

        var principal = resolver.Resolve(presented);
        if (principal is null)
            return Task.FromResult(AuthenticateResult.Fail("Invalid API key."));

        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.Headers.WWWAuthenticate = new StringValues($"Bearer realm=\"{Scheme.Name}\"");
        return Task.CompletedTask;
    }

    private static string? ExtractKey(HttpRequest request)
    {
        string? authorization = request.Headers.Authorization;
        if (
            !string.IsNullOrEmpty(authorization)
            && authorization.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase)
        )
        {
            var token = authorization[BearerPrefix.Length..].Trim();
            return token.Length > 0 ? token : null;
        }

        string? apiKey = request.Headers[EngineAuthentication.ApiKeyHeader];
        if (!string.IsNullOrEmpty(apiKey))
            return apiKey;

        if (request.Cookies.TryGetValue(EngineAuthentication.DashboardCookie, out var cookie) && cookie.Length > 0)
            return cookie;

        return null;
    }
}
