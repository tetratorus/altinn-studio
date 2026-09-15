using System.Text.Json.Serialization;

namespace WorkflowEngine.Models;

/// <summary>
/// Authentication settings for the workflow engine HTTP API and dashboard.
/// Bound from the <c>EngineAuthentication</c> configuration section.
/// </summary>
public sealed record EngineAuthenticationSettings
{
    /// <summary>
    /// Configuration section path.
    /// </summary>
    public const string SectionName = "EngineAuthentication";

    /// <summary>
    /// API keys accepted by the engine. Every request to the engine API or dashboard must present one of
    /// these keys; each key is bound to the namespaces it may act on.
    /// </summary>
    [JsonPropertyName("apiKeys")]
    public List<EngineApiKey> ApiKeys { get; set; } = [];
}

/// <summary>
/// A single API key and the authority it carries.
/// </summary>
public sealed record EngineApiKey
{
    /// <summary>
    /// Human-readable identifier for the key (used as the authenticated principal name and in logs).
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    /// <summary>
    /// The secret key value presented as <c>Authorization: Bearer &lt;key&gt;</c> or <c>X-Api-Key</c>.
    /// </summary>
    [JsonPropertyName("key")]
    public required string Key { get; set; }

    /// <summary>
    /// Namespaces (<c>{org}/{app}</c>) this key may read and mutate. Compared case-insensitively.
    /// </summary>
    [JsonPropertyName("namespaces")]
    public List<string> Namespaces { get; set; } = [];

    /// <summary>
    /// Whether this key is an operator key. Operator keys may act on every namespace and access
    /// the dashboard and the cross-namespace listing endpoints.
    /// </summary>
    [JsonPropertyName("operator")]
    public bool Operator { get; set; }
}
