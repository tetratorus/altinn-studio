using System.Net;
using System.Net.Http.Json;
using WorkflowEngine.Integration.Tests.Fixtures;
using WorkflowEngine.Models;
using WorkflowEngine.TestKit;

namespace WorkflowEngine.Integration.Tests;

/// <summary>
/// Every engine API and dashboard route requires an API key, and namespace-scoped routes are bound
/// to the namespaces the key is configured for. Operator keys may act on every namespace and the dashboard.
/// </summary>
[Collection(EngineAppCollection.Name)]
public sealed class EngineAuthorizationTests(EngineAppFixture<Program> fixture) : IAsyncLifetime
{
    private const string OtherNamespace = "other-org-other-app";

    private readonly EngineApiClient _client = new(fixture);
    private readonly TestHelpers _testHelpers = new(fixture);

    public async ValueTask InitializeAsync()
    {
        await fixture.Reset();
        await _testHelpers.AssertDbEmpty();
    }

    public ValueTask DisposeAsync()
    {
        _client.Dispose();
        return ValueTask.CompletedTask;
    }

    private HttpClient CreateClient(string? apiKey) => TestAuth.Authenticate(fixture.CreateEngineClient(), apiKey);

    /// <summary>
    /// A client that does not follow redirects or manage cookies, so redirect and Set-Cookie responses
    /// can be asserted directly.
    /// </summary>
    private HttpClient CreateRawClient() =>
        TestAuth.Authenticate(fixture.CreateEngineClient(new HttpExchangeRecorder()), apiKey: null);

    private async Task<Guid> EnqueueInDefaultNamespace()
    {
        var request = _testHelpers.CreateEnqueueRequest(
            _testHelpers.CreateWorkflow("wf", [_testHelpers.CreateWebhookStep("/hook")])
        );
        var response = await _client.Enqueue(request);
        var id = response.Workflows.Single().DatabaseId;
        await _client.WaitForWorkflowStatus(id, PersistentItemStatus.Completed);
        return id;
    }

    [Theory]
    [InlineData("GET", "/api/v1/namespaces")]
    [InlineData("GET", "/api/v1/throttles")]
    [InlineData("GET", "/api/v1/ttd-e2e-tests/workflows")]
    [InlineData("POST", "/api/v1/ttd-e2e-tests/workflows")]
    [InlineData("GET", "/api/v1/ttd-e2e-tests/throttle")]
    [InlineData("GET", "/api/v1/ttd-e2e-tests/collections")]
    [InlineData("POST", "/api/v1/ttd-e2e-tests/mailboxes")]
    [InlineData("GET", "/dashboard/namespaces")]
    [InlineData("GET", "/dashboard/query")]
    [InlineData("GET", "/dashboard/state?wf=00000000-0000-0000-0000-000000000001&ns=ttd-e2e-tests")]
    [InlineData("GET", "/dashboard/step?wf=00000000-0000-0000-0000-000000000001&ns=ttd-e2e-tests&step=x")]
    [InlineData("GET", "/dashboard/stream/live")]
    [InlineData("GET", "/index.html")]
    [InlineData("GET", "/")]
    [InlineData("GET", "/app.js")]
    public async Task Unauthenticated_Request_Returns401(string method, string path)
    {
        // Arrange
        using var client = CreateClient(apiKey: null);
        using var request = new HttpRequestMessage(HttpMethod.Parse(method), path);

        // Act
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEmpty(response.Headers.WwwAuthenticate);
    }

    [Theory]
    [InlineData("not-a-configured-key")]
    [InlineData("LOCAL-DEV-ONLY-workflow-engine-api-key")]
    public async Task InvalidApiKey_Returns401(string apiKey)
    {
        // Arrange
        using var client = CreateClient(apiKey);

        // Act
        using var response = await client.GetAsync(
            $"/api/v1/{TestAuth.TenantNamespace}/workflows",
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ApiKeyHeader_IsAcceptedAsAlternativeToBearer()
    {
        // Arrange
        using var client = CreateClient(apiKey: null);
        client.DefaultRequestHeaders.Add("X-Api-Key", TestAuth.TenantApiKey);

        // Act
        using var response = await client.GetAsync(
            $"/api/v1/{TestAuth.TenantNamespace}/workflows",
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task TenantKey_OwnNamespace_IsAllowed()
    {
        // Arrange
        var workflowId = await EnqueueInDefaultNamespace();
        using var client = CreateClient(TestAuth.TenantApiKey);

        // Act
        using var response = await client.GetAsync(
            $"/api/v1/{TestAuth.TenantNamespace}/workflows/{workflowId}",
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task TenantKey_NamespaceComparison_IsCaseInsensitive()
    {
        // Arrange
        using var client = CreateClient(TestAuth.TenantApiKey);

        // Act
        using var response = await client.GetAsync(
            $"/api/v1/{TestAuth.TenantNamespace.ToUpperInvariant()}/workflows",
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Theory]
    [InlineData("GET", "/workflows")]
    [InlineData("POST", "/workflows")]
    [InlineData("GET", "/workflows/00000000-0000-0000-0000-000000000001")]
    [InlineData("POST", "/workflows/00000000-0000-0000-0000-000000000001/cancel")]
    [InlineData("POST", "/workflows/00000000-0000-0000-0000-000000000001/resume")]
    [InlineData("POST", "/workflows/00000000-0000-0000-0000-000000000001/nudge")]
    [InlineData("POST", "/workflows/00000000-0000-0000-0000-000000000001/fail")]
    [InlineData("GET", "/throttle")]
    [InlineData("POST", "/throttle")]
    [InlineData("DELETE", "/throttle")]
    [InlineData("GET", "/collections")]
    [InlineData("POST", "/mailboxes")]
    public async Task TenantKey_OtherNamespace_Returns403(string method, string suffix)
    {
        // Arrange
        using var client = CreateClient(TestAuth.TenantApiKey);
        using var request = new HttpRequestMessage(HttpMethod.Parse(method), $"/api/v1/{OtherNamespace}{suffix}");

        // Act
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task TenantKey_CannotEnqueueIntoOtherNamespace_NothingPersisted()
    {
        // Arrange
        using var client = CreateClient(TestAuth.TenantApiKey);
        var request = _testHelpers.CreateEnqueueRequest(
            _testHelpers.CreateWorkflow("wf", [_testHelpers.CreateWebhookStep("/hook")])
        );
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/{OtherNamespace}/workflows")
        {
            Content = JsonContent.Create(request),
        };
        httpRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

        // Act
        using var response = await client.SendAsync(httpRequest, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await _testHelpers.AssertDbEmpty();
    }

    [Theory]
    [InlineData("/api/v1/namespaces")]
    [InlineData("/api/v1/throttles")]
    [InlineData("/dashboard/namespaces")]
    [InlineData("/dashboard/query")]
    [InlineData("/dashboard/labels?key=org")]
    [InlineData("/dashboard/state?wf=00000000-0000-0000-0000-000000000001&ns=ttd-e2e-tests")]
    [InlineData("/dashboard/step?wf=00000000-0000-0000-0000-000000000001&ns=ttd-e2e-tests&step=x")]
    [InlineData("/dashboard/stream/live")]
    [InlineData("/index.html")]
    [InlineData("/")]
    [InlineData("/app.js")]
    public async Task TenantKey_OperatorRoutes_Returns403(string path)
    {
        // Arrange
        using var client = CreateClient(TestAuth.TenantApiKey);

        // Act
        using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task TenantKey_CannotReadOtherNamespaceStateViaDashboard()
    {
        // Arrange
        var workflowId = await EnqueueInDefaultNamespace();
        using var client = CreateClient(TestAuth.TenantApiKey);

        // Act
        using var response = await client.GetAsync(
            $"/dashboard/state?wf={workflowId}&ns={TestAuth.TenantNamespace}",
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task OperatorKey_AnyNamespace_IsAllowed()
    {
        // Arrange
        using var client = CreateClient(TestAuth.OperatorApiKey);

        // Act
        using var workflows = await client.GetAsync(
            $"/api/v1/{OtherNamespace}/workflows",
            TestContext.Current.CancellationToken
        );
        using var namespaces = await client.GetAsync("/api/v1/namespaces", TestContext.Current.CancellationToken);
        using var dashboard = await client.GetAsync("/dashboard/namespaces", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, workflows.StatusCode);
        Assert.Equal(HttpStatusCode.OK, namespaces.StatusCode);
        Assert.Equal(HttpStatusCode.OK, dashboard.StatusCode);
    }

    [Fact]
    public async Task Dashboard_UnauthenticatedBrowserRequest_RedirectsToLogin()
    {
        // Arrange
        using var client = CreateRawClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Accept.ParseAdd("text/html");

        // Act
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal("/dashboard/login", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task DashboardLogin_OperatorKey_SetsCookieThatAuthenticates()
    {
        // Arrange
        using var client = CreateRawClient();
        using var form = new FormUrlEncodedContent([new("apiKey", TestAuth.OperatorApiKey)]);

        // Act
        using var login = await client.PostAsync("/dashboard/login", form, TestContext.Current.CancellationToken);
        var cookie = login
            .Headers.GetValues("Set-Cookie")
            .Single(c => c.StartsWith("wfe_dashboard=", StringComparison.Ordinal));
        using var request = new HttpRequestMessage(HttpMethod.Get, "/dashboard/namespaces");
        request.Headers.Add("Cookie", cookie.Split(';')[0]);
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Found, login.StatusCode);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task DashboardLogin_TenantKey_IsRejected()
    {
        // Arrange
        using var client = CreateRawClient();
        using var form = new FormUrlEncodedContent([new("apiKey", TestAuth.TenantApiKey)]);

        // Act
        using var login = await client.PostAsync("/dashboard/login", form, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
        Assert.False(login.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task HealthAndOpenApi_RemainAnonymous()
    {
        // Arrange
        using var client = CreateClient(apiKey: null);

        // Act
        using var live = await client.GetAsync("/api/v1/health/live", TestContext.Current.CancellationToken);
        using var openApi = await client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.Equal(HttpStatusCode.OK, openApi.StatusCode);
    }
}
