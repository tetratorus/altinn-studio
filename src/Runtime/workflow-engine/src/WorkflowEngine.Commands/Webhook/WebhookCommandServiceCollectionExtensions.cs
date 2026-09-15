using Microsoft.Extensions.DependencyInjection;
using WorkflowEngine.Models.Abstractions;

namespace WorkflowEngine.Commands.Webhook;

/// <summary>
/// DI registration for the built-in <see cref="WebhookCommand"/>.
/// </summary>
public static class WebhookCommandServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers <see cref="WebhookCommand"/>, binds <see cref="WebhookCommandSettings"/> from
        /// <paramref name="configSectionPath"/>, and configures the redirect-free HTTP client the command uses.
        /// </summary>
        public IServiceCollection AddWebhookCommand(string configSectionPath = "WebhookCommandSettings")
        {
            services.AddOptions<WebhookCommandSettings>().BindConfiguration(configSectionPath);
            services
                .AddHttpClient(WebhookCommand.HttpClientName)
                .ConfigurePrimaryHttpMessageHandler(() =>
                    new SocketsHttpHandler
                    {
                        AllowAutoRedirect = false,
                        PooledConnectionLifetime = TimeSpan.FromMinutes(2),
                    }
                );
            services.AddSingleton<ICommand, WebhookCommand>();
            return services;
        }
    }
}
