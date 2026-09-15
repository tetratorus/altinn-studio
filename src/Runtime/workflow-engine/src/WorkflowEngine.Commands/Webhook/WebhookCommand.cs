using System.Diagnostics;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WorkflowEngine.Commands.Extensions;
using WorkflowEngine.Models;
using WorkflowEngine.Resilience;
using WorkflowEngine.Resilience.Models;
using WorkflowEngine.Telemetry;
using WorkflowEngine.Telemetry.Extensions;

// CA1822: Mark members as static
#pragma warning disable CA1822

namespace WorkflowEngine.Commands.Webhook;

/// <summary>
/// Handles "webhook" commands by making HTTP requests to endpoints on the operator-configured host allowlist
/// (<see cref="WebhookCommandSettings"/>). If <c>Command.Data</c> includes a <c>payload</c>, sends a POST;
/// otherwise sends a GET.
/// </summary>
public sealed class WebhookCommand : Command<WebhookCommandData>
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConcurrencyLimiter _limiter;
    private readonly IOptions<WebhookCommandSettings> _settings;
    private readonly ILogger<WebhookCommand> _logger;

    private const string CommandTypeId = "webhook";

    /// <summary>
    /// Name of the <see cref="HttpClient"/> used for webhook requests; configured without automatic redirects
    /// so an allowed host cannot forward the engine to a disallowed one.
    /// </summary>
    public const string HttpClientName = "WebhookCommand";

    /// <inheritdoc/>
    public override string CommandType => CommandTypeId;

    /// <summary>
    /// Creates a new <see cref="WebhookCommand"/> with the supplied HTTP client factory, concurrency limiter,
    /// settings, and logger.
    /// </summary>
    public WebhookCommand(
        IHttpClientFactory httpClientFactory,
        IConcurrencyLimiter limiter,
        IOptions<WebhookCommandSettings> settings,
        ILogger<WebhookCommand> logger
    )
    {
        _httpClientFactory = httpClientFactory;
        _limiter = limiter;
        _settings = settings;
        _logger = logger;
    }

    /// <summary>
    /// Creates a <see cref="CommandDefinition"/> with <see cref="WebhookCommandData"/>.
    /// </summary>
    public static CommandDefinition Create(WebhookCommandData data, TimeSpan? maxExecutionTime = null) =>
        CommandDefinition.Create(CommandTypeId, data, maxExecutionTime);

    /// <inheritdoc/>
    protected override CommandValidationResult Validate(WebhookCommandData? commandData)
    {
        if (commandData is null || string.IsNullOrWhiteSpace(commandData.Uri))
            return new CommandValidationResult.Invalid("Webhook command requires a 'uri' in command data");

        if (!Uri.TryCreate(commandData.Uri, UriKind.Absolute, out var uri))
            return new CommandValidationResult.Invalid($"Webhook uri '{commandData.Uri}' is not a valid absolute URI");

        if (_settings.Value.Reject(uri) is { } reason)
            return new CommandValidationResult.Invalid($"Webhook uri '{commandData.Uri}' {reason}");

        return new CommandValidationResult.Valid();
    }

    /// <inheritdoc/>
    protected override async Task<ExecutionResult> Execute(
        CommandExecutionContext context,
        CancellationToken cancellationToken
    )
    {
        var commandData = context.GetCommandData<WebhookCommandData>();

        using var activity = Metrics.Source.StartActivity(
            "WebhookCommand.Execute",
            parentContext: context.ParentTraceContext ?? context.Step.EngineActivity?.Context,
            kind: ActivityKind.Client,
            tags: [("command.uri", commandData.Uri)]
        );

        var endpoint = commandData.Uri.ToUri(UriKind.Absolute);
        if (_settings.Value.Reject(endpoint) is { } reason)
            return ExecutionResult.CriticalError($"Webhook uri '{commandData.Uri}' {reason}");

        using var slot = await _limiter.AcquireHttpSlot(activity?.Context, cancellationToken);
        using var httpClient = _httpClientFactory.CreateClient(HttpClientName);

        using var response = commandData.Payload is not null
            ? await Post(httpClient, endpoint, commandData.Payload, commandData.ContentType, context, cancellationToken)
            : await Get(httpClient, endpoint, context, cancellationToken);

        if (response.IsSuccessStatusCode)
            return ExecutionResult.Success();

        var statusCode = (int)response.StatusCode;
        var errorBody = Truncate(
            await response.GetContentOrDefault("<no body content>", cancellationToken),
            _settings.Value.MaxErrorBodyLength
        );

        IReadOnlyList<int> nonRetryable =
            context.Step.RetryStrategy?.NonRetryableHttpStatusCodes ?? RetryStrategy.DefaultNonRetryableHttpStatusCodes;

        if (nonRetryable.Contains(statusCode))
            return ExecutionResult.CriticalError(
                $"Webhook failed with non-retryable status {statusCode}: {errorBody}",
                httpStatusCode: statusCode
            );

        return ExecutionResult.RetryableError(
            $"Webhook execution failed with status {statusCode}: {errorBody}",
            httpStatusCode: statusCode
        );
    }

    private static string Truncate(string value, int maxLength) =>
        maxLength >= 0 && value.Length > maxLength ? $"{value[..maxLength]}...[truncated]" : value;

    private async Task<HttpResponseMessage> Post(
        HttpClient httpClient,
        Uri endpoint,
        string payload,
        string? contentType,
        CommandExecutionContext context,
        CancellationToken cancellationToken
    )
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.AddWorkflowMetadataHeaders(context);

        request.Content = new StringContent(payload);
        request.Content.Headers.ContentType = contentType is not null ? new MediaTypeHeaderValue(contentType) : null;

        _logger.SendingWebhookPost(endpoint, payload.Length, contentType);
        return await httpClient.SendAsync(request, cancellationToken);
    }

    private async Task<HttpResponseMessage> Get(
        HttpClient httpClient,
        Uri endpoint,
        CommandExecutionContext context,
        CancellationToken cancellationToken
    )
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
        request.AddWorkflowMetadataHeaders(context);

        _logger.SendingWebhookGet(endpoint);
        return await httpClient.SendAsync(request, cancellationToken);
    }
}

internal static partial class WebhookCommandDescriptorLogs
{
    // Log only the payload size and content type, never the body: webhook payloads can carry
    // caller-supplied secrets or PII.
    [LoggerMessage(
        LogLevel.Information,
        "[POST] Sending Webhook to {Endpoint} with payload ({PayloadLength} chars, contentType: {ContentType})"
    )]
    internal static partial void SendingWebhookPost(
        this ILogger<WebhookCommand> logger,
        Uri endpoint,
        int payloadLength,
        string? contentType
    );

    [LoggerMessage(LogLevel.Information, "[GET] Sending Webhook to {Endpoint} without payload")]
    internal static partial void SendingWebhookGet(this ILogger<WebhookCommand> logger, Uri endpoint);
}
