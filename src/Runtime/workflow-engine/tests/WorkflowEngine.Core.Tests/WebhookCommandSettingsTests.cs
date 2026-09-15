using Microsoft.Extensions.DependencyInjection;
using WorkflowEngine.Commands.Webhook;
using WorkflowEngine.Core.Tests.Fixtures;
using WorkflowEngine.Models;
using WorkflowEngine.Models.Abstractions;

namespace WorkflowEngine.Core.Tests;

public class WebhookCommandSettingsTests
{
    private static readonly WebhookCommandSettings _settings = new()
    {
        AllowedHosts = ["hooks.example.com", "*.internal.example.org", "10.0.0.5", "[::1]"],
        AllowedSchemes = ["https"],
    };

    [Theory]
    [InlineData("https://hooks.example.com/path")]
    [InlineData("https://HOOKS.EXAMPLE.COM:8443/path")]
    [InlineData("https://a.internal.example.org/")]
    [InlineData("https://a.b.internal.example.org/")]
    [InlineData("https://10.0.0.5/")]
    [InlineData("https://[::1]/")]
    public void Reject_AllowedTarget_ReturnsNull(string uri)
    {
        Assert.Null(_settings.Reject(new Uri(uri)));
    }

    [Theory]
    [InlineData("http://hooks.example.com/", "scheme")]
    [InlineData("https://internal.example.org/", "allowlist")]
    [InlineData("https://evil.com/", "allowlist")]
    [InlineData("https://hooks.example.com.evil.com/", "allowlist")]
    [InlineData("https://127.0.0.1/", "allowlist")]
    [InlineData("https://169.254.169.254/latest/meta-data", "allowlist")]
    [InlineData("https://kubernetes.default.svc/api", "allowlist")]
    [InlineData("https://user:pw@hooks.example.com/", "user info")]
    [InlineData("file:///etc/passwd", "scheme")]
    public void Reject_DisallowedTarget_ReturnsReason(string uri, string expectedFragment)
    {
        var reason = _settings.Reject(new Uri(uri));

        Assert.NotNull(reason);
        Assert.Contains(expectedFragment, reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Reject_EmptyAllowlist_RefusesEverything()
    {
        var settings = new WebhookCommandSettings();

        Assert.NotNull(settings.Reject(new Uri("https://hooks.example.com/")));
    }

    [Fact]
    public void Validate_HostNotAllowed_IsInvalid()
    {
        using var fixture = WorkflowEngineTestFixture.Create();
        var webhook = fixture.ServiceProvider.GetServices<ICommand>().Single(c => c.CommandType == "webhook");

        var result = webhook.Validate(new WebhookCommandData { Uri = "http://169.254.169.254/" }, null);

        Assert.IsType<CommandValidationResult.Invalid>(result);
    }

    [Fact]
    public async Task Execute_HostNotAllowed_FailsCriticallyWithoutSendingRequest()
    {
        using var fixture = WorkflowEngineTestFixture.Create();
        var executor = fixture.ServiceProvider.GetRequiredService<IWorkflowExecutor>();
        var command = WebhookCommand.Create(new WebhookCommandData { Uri = "http://10.0.0.1/admin" });
        var step = WorkflowEngineTestFixture.CreateStep(command);
        var workflow = WorkflowEngineTestFixture.CreateWorkflow(step);

        var result = await executor.Execute(workflow, step, CancellationToken.None);

        Assert.Equal(ExecutionStatus.CriticalError, result.Status);
        Assert.Empty(fixture.HttpHandler.Requests);
    }
}
