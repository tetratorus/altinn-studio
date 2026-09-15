using System.Net.Http.Headers;

namespace WorkflowEngine.TestKit;

/// <summary>
/// API keys configured on the test host by <see cref="EngineWebApplicationFactory{TProgram}"/>.
/// </summary>
public static class TestAuth
{
    /// <summary>Operator key: may act on every namespace and access the dashboard.</summary>
    public const string OperatorApiKey = "test-operator-api-key";

    /// <summary>Tenant key bound to <see cref="TenantNamespace"/> only.</summary>
    public const string TenantApiKey = "test-tenant-api-key";

    /// <summary>The namespace <see cref="TenantApiKey"/> is bound to.</summary>
    public static string TenantNamespace => EngineApiClient.DefaultNamespace;

    /// <summary>
    /// Sets the <c>Authorization: Bearer</c> header on <paramref name="client"/>.
    /// Pass <c>null</c> to send requests unauthenticated.
    /// </summary>
    public static HttpClient Authenticate(HttpClient client, string? apiKey)
    {
        client.DefaultRequestHeaders.Authorization = apiKey is null
            ? null
            : new AuthenticationHeaderValue("Bearer", apiKey);
        return client;
    }
}
