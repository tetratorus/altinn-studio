using System.Net;
using System.Text.Json.Serialization;

namespace WorkflowEngine.Commands.Webhook;

/// <summary>
/// Configuration for the built-in <see cref="WebhookCommand"/>. Webhook targets are caller-supplied, so the
/// engine only sends requests to hosts an operator has explicitly allowed; with an empty
/// <see cref="AllowedHosts"/> every webhook command is rejected at enqueue time.
/// </summary>
public sealed record WebhookCommandSettings
{
    /// <summary>
    /// Hosts the engine may send webhook requests to. An entry is either an exact host name or IP literal
    /// (<c>hooks.example.com</c>, <c>127.0.0.1</c>) or a wildcard suffix (<c>*.example.com</c>) matching any
    /// subdomain but not the apex. Matching is case-insensitive. Empty by default, which disables the command.
    /// </summary>
    [JsonPropertyName("allowedHosts")]
    public IReadOnlyList<string> AllowedHosts { get; set; } = [];

    /// <summary>
    /// URI schemes the engine may use for webhook requests. Defaults to <c>https</c> only.
    /// </summary>
    [JsonPropertyName("allowedSchemes")]
    public IReadOnlyList<string> AllowedSchemes { get; set; } = [Uri.UriSchemeHttps];

    /// <summary>
    /// Maximum number of characters of a non-2xx response body copied into the step's persisted error history.
    /// </summary>
    [JsonPropertyName("maxErrorBodyLength")]
    public int MaxErrorBodyLength { get; set; } = 512;

    /// <summary>
    /// Returns <c>null</c> when <paramref name="uri"/> may be called, otherwise the reason it was refused.
    /// </summary>
    public string? Reject(Uri uri)
    {
        if (!string.IsNullOrEmpty(uri.UserInfo))
            return "must not contain user info";

        if (!AllowedSchemes.Any(s => string.Equals(s, uri.Scheme, StringComparison.OrdinalIgnoreCase)))
            return $"scheme '{uri.Scheme}' is not allowed";

        if (uri.HostNameType == UriHostNameType.Unknown || uri.HostNameType == UriHostNameType.Basic)
            return "has no valid host";

        if (!IsAllowedHost(uri))
            return $"host '{uri.Host}' is not in the webhook host allowlist";

        return null;
    }

    private bool IsAllowedHost(Uri uri)
    {
        var host = uri.IdnHost;
        var literal = uri.HostNameType is UriHostNameType.IPv4 or UriHostNameType.IPv6;

        foreach (var entry in AllowedHosts)
        {
            var pattern = entry.Trim().TrimEnd('.');
            if (pattern.Length == 0)
                continue;

            if (pattern.StartsWith("*.", StringComparison.Ordinal))
            {
                if (literal)
                    continue;

                var suffix = pattern[1..];
                if (host.Length > suffix.Length && host.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                    return true;

                continue;
            }

            if (literal)
            {
                if (
                    IPAddress.TryParse(pattern.Trim('[', ']'), out var allowed)
                    && IPAddress.TryParse(host, out var actual)
                    && allowed.Equals(actual)
                )
                    return true;

                continue;
            }

            if (string.Equals(pattern, host, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
