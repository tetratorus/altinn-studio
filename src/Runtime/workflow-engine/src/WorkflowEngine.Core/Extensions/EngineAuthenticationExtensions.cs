using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using WorkflowEngine.Core.Authentication;
using WorkflowEngine.Models;

namespace WorkflowEngine.Core.Extensions;

/// <summary>
/// Registers API key authentication and the authorization policies used by the engine endpoints
/// and the dashboard.
/// </summary>
public static class EngineAuthenticationExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Binds <see cref="EngineAuthenticationSettings"/> from configuration, registers the
        /// <see cref="EngineAuthentication.Scheme"/> authentication scheme and the
        /// <see cref="EngineAuthentication.ApiPolicy"/> / <see cref="EngineAuthentication.OperatorPolicy"/> policies.
        /// <para>
        /// At least one API key must be configured; startup fails otherwise. When
        /// <paramref name="allowLocalDevKey"/> is <c>true</c> (Development/Docker hosts) and no keys are
        /// configured, a single operator key with the value <see cref="EngineAuthentication.LocalDevApiKey"/>
        /// is registered so local tooling can talk to the engine.
        /// </para>
        /// </summary>
        public IServiceCollection AddEngineAuthentication(bool allowLocalDevKey)
        {
            services
                .AddOptions<EngineAuthenticationSettings>()
                .BindConfiguration(EngineAuthenticationSettings.SectionName)
                .PostConfigure(settings =>
                {
                    if (allowLocalDevKey && settings.ApiKeys.Count == 0)
                    {
                        settings.ApiKeys.Add(
                            new EngineApiKey
                            {
                                Name = "local-dev",
                                Key = EngineAuthentication.LocalDevApiKey,
                                Operator = true,
                            }
                        );
                    }
                })
                .Validate(
                    settings => settings.ApiKeys.Count > 0,
                    $"{EngineAuthenticationSettings.SectionName}:ApiKeys must contain at least one API key. "
                        + "The engine API and dashboard refuse all requests without configured credentials."
                )
                .Validate(
                    settings =>
                        settings.ApiKeys.All(k =>
                            !string.IsNullOrWhiteSpace(k.Name) && !string.IsNullOrWhiteSpace(k.Key)
                        ),
                    $"Every entry in {EngineAuthenticationSettings.SectionName}:ApiKeys must have a non-empty Name and Key."
                )
                .Validate(
                    settings => settings.ApiKeys.All(k => k.Operator || k.Namespaces.Count > 0),
                    $"Every non-operator entry in {EngineAuthenticationSettings.SectionName}:ApiKeys "
                        + "must list at least one namespace."
                )
                .ValidateOnStart();

            services.AddSingleton<EngineApiKeyResolver>();
            services.AddSingleton<IAuthorizationHandler, NamespaceAccessHandler>();

            services
                .AddAuthentication(EngineAuthentication.Scheme)
                .AddScheme<AuthenticationSchemeOptions, EngineApiKeyAuthenticationHandler>(
                    EngineAuthentication.Scheme,
                    displayName: null,
                    configureOptions: null
                );

            services
                .AddAuthorizationBuilder()
                .AddPolicy(
                    EngineAuthentication.ApiPolicy,
                    policy => policy.RequireAuthenticatedUser().AddRequirements(new NamespaceAccessRequirement())
                )
                .AddPolicy(
                    EngineAuthentication.OperatorPolicy,
                    policy => policy.RequireAuthenticatedUser().RequireClaim(EngineAuthentication.OperatorClaim, "true")
                );

            return services;
        }
    }
}
