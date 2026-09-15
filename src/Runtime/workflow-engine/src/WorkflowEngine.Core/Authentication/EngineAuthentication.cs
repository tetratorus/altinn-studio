using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using WorkflowEngine.Data.Constants;
using WorkflowEngine.Models;

namespace WorkflowEngine.Core.Authentication;

/// <summary>
/// Constants for the engine's API key authentication scheme, claims and authorization policies.
/// </summary>
public static class EngineAuthentication
{
    /// <summary>Authentication scheme name.</summary>
    public const string Scheme = "WorkflowEngineApiKey";

    /// <summary>Header carrying the API key as an alternative to <c>Authorization: Bearer</c>.</summary>
    public const string ApiKeyHeader = "X-Api-Key";

    /// <summary>Cookie the dashboard login sets so browser requests (fetch/EventSource) authenticate.</summary>
    public const string DashboardCookie = "wfe_dashboard";

    /// <summary>Claim type for each namespace a key may act on (normalized).</summary>
    public const string NamespaceClaim = "wfe:namespace";

    /// <summary>Claim type present (with value <c>true</c>) on operator principals.</summary>
    public const string OperatorClaim = "wfe:operator";

    /// <summary>Policy: any authenticated API key.</summary>
    public const string ApiPolicy = "WorkflowEngine.Api";

    /// <summary>Policy: operator API key (dashboard and cross-namespace endpoints).</summary>
    public const string OperatorPolicy = "WorkflowEngine.Operator";

    /// <summary>
    /// API key used when no keys are configured in a local (Development/Docker) environment.
    /// Mirrors the local-dev callback secret convention used by <c>studioctl</c>.
    /// </summary>
    public const string LocalDevApiKey = "LOCAL-DEV-ONLY-workflow-engine-api-key";

    /// <summary>
    /// Returns whether <paramref name="user"/> may act on <paramref name="normalizedNamespace"/>.
    /// </summary>
    public static bool CanAccessNamespace(ClaimsPrincipal user, string normalizedNamespace)
    {
        if (user.Identity?.IsAuthenticated != true)
            return false;

        if (IsOperator(user))
            return true;

        return user.Claims.Any(c =>
            c.Type == NamespaceClaim && string.Equals(c.Value, normalizedNamespace, StringComparison.Ordinal)
        );
    }

    /// <summary>
    /// Returns whether <paramref name="user"/> is an authenticated operator.
    /// </summary>
    public static bool IsOperator(ClaimsPrincipal user) =>
        user.Identity?.IsAuthenticated == true && user.HasClaim(OperatorClaim, "true");
}

/// <summary>
/// Resolves presented API keys against the configured <see cref="EngineAuthenticationSettings"/>
/// using constant-time comparison.
/// </summary>
internal sealed class EngineApiKeyResolver(IOptionsMonitor<EngineAuthenticationSettings> settings)
{
    /// <summary>
    /// Returns the principal for <paramref name="presentedKey"/>, or <c>null</c> when it matches no configured key.
    /// </summary>
    public ClaimsPrincipal? Resolve(string? presentedKey)
    {
        if (string.IsNullOrEmpty(presentedKey))
            return null;

        var presented = Encoding.UTF8.GetBytes(presentedKey);
        EngineApiKey? match = null;

        // Compare against every key so timing does not reveal which (if any) matched.
        foreach (var candidate in settings.CurrentValue.ApiKeys)
        {
            if (string.IsNullOrEmpty(candidate.Key))
                continue;

            var expected = Encoding.UTF8.GetBytes(candidate.Key);
            if (CryptographicOperations.FixedTimeEquals(presented, expected))
                match ??= candidate;
        }

        if (match is null)
            return null;

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, match.Name),
            new(ClaimTypes.NameIdentifier, match.Name),
        };

        foreach (var ns in match.Namespaces)
        {
            if (!string.IsNullOrWhiteSpace(ns))
                claims.Add(new Claim(EngineAuthentication.NamespaceClaim, WorkflowNamespace.Normalize(ns)));
        }

        if (match.Operator)
            claims.Add(new Claim(EngineAuthentication.OperatorClaim, "true"));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, EngineAuthentication.Scheme));
    }
}
