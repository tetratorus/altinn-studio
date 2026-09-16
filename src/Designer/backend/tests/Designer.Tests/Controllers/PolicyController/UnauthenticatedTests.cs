using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Designer.Tests.Controllers.ApiTests;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Mvc.Testing.Handlers;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace Designer.Tests.Controllers.PolicyControllerTests;

public class UnauthenticatedTests
    : DesignerEndpointsTestsBase<UnauthenticatedTests>,
        IClassFixture<WebApplicationFactory<Program>>
{
    private const string AnonymousScheme = "Anonymous";
    private const string PolicyBasePath = "designer/api/ttd/ttd-resources/policy";

    public UnauthenticatedTests(WebApplicationFactory<Program> factory)
        : base(factory) { }

    private sealed class AnonymousAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public AnonymousAuthHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder
        )
            : base(options, logger, encoder) { }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync() =>
            Task.FromResult(AuthenticateResult.NoResult());
    }

    private HttpClient CreateAnonymousClient()
    {
        string configPath = GetConfigPath();
        IConfiguration configuration = new ConfigurationBuilder()
            .AddJsonFile(configPath, false, false)
            .AddJsonStream(GenerateJsonOverrideConfig())
            .AddEnvironmentVariables()
            .Build();

        return CreateTestClient(
            builder =>
            {
                builder.UseConfiguration(configuration);
                builder.ConfigureTestServices(ConfigureTestServices);
                builder.ConfigureTestServices(services =>
                {
                    services
                        .AddAuthentication(options =>
                        {
                            options.DefaultScheme = AnonymousScheme;
                            options.DefaultChallengeScheme = AnonymousScheme;
                        })
                        .AddScheme<AuthenticationSchemeOptions, AnonymousAuthHandler>(
                            AnonymousScheme,
                            options =>
                            {
                                options.TimeProvider = System.TimeProvider.System;
                            }
                        );
                });
            },
            new CookieContainerHandler()
        );
    }

    [Theory]
    [InlineData("GET", "")]
    [InlineData("GET", "/some-resource")]
    [InlineData("GET", "/validate")]
    [InlineData("GET", "/validate/some-resource")]
    [InlineData("GET", "/subjectoptions")]
    [InlineData("GET", "/actionoptions")]
    [InlineData("GET", "/accesspackageoptions")]
    [InlineData("PUT", "")]
    [InlineData("POST", "")]
    [InlineData("PUT", "/some-resource")]
    [InlineData("POST", "/some-resource")]
    public async Task PolicyEndpoints_WithoutAuthentication_ShouldReturnUnauthorized(string method, string path)
    {
        using HttpClient client = CreateAnonymousClient();
        using var httpRequestMessage = new HttpRequestMessage(new HttpMethod(method), $"{PolicyBasePath}{path}");
        if (method != "GET")
        {
            httpRequestMessage.Content = new StringContent("{}", Encoding.UTF8, "application/json");
        }

        using HttpResponseMessage response = await client.SendAsync(httpRequestMessage);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
