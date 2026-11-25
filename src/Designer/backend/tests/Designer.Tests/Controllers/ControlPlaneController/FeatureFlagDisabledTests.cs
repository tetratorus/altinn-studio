using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Altinn.Studio.Designer.Constants;
using Designer.Tests.Controllers.ApiTests;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Mvc.Testing.Handlers;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Designer.Tests.Controllers.ControlPlaneController;

public class FeatureFlagDisabledTests
    : DesignerEndpointsTestsBase<FeatureFlagDisabledTests>,
        IClassFixture<WebApplicationFactory<Program>>
{
    private const string VersionPrefix = "/designer/api/v1/controlplane/health";

    private static readonly HttpStatusCode[] s_rejectedStatusCodes =
    [
        HttpStatusCode.Unauthorized,
        HttpStatusCode.Forbidden,
        HttpStatusCode.NotFound,
    ];

    public FeatureFlagDisabledTests(WebApplicationFactory<Program> factory)
        : base(factory)
    {
        JsonConfigOverrides.Add(
            $$"""
               {
                     "FeatureManagement": {
                         "{{StudioFeatureFlags.Maskinporten}}": false
                     }
               }
            """
        );
    }

    private HttpClient CreateUnauthenticatedClient()
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
                builder.ConfigureAppConfiguration(
                    (_, conf) =>
                    {
                        conf.AddJsonFile(configPath);
                        conf.AddJsonStream(GenerateJsonOverrideConfig());
                    }
                );
                builder.ConfigureTestServices(ConfigureTestServices);
            },
            new CookieContainerHandler()
        );
    }

    [Fact]
    public async Task Health_IsRejected_WhenFeatureFlagDisabled()
    {
        using var client = CreateUnauthenticatedClient();
        using var response = await client.GetAsync(VersionPrefix);

        Assert.Contains(response.StatusCode, s_rejectedStatusCodes);
    }

    [Theory]
    [InlineData("/designer/api/v1/ttd/someapp/deployments/webhooks/events")]
    [InlineData("/designer/api/v1/admin/alerts/ttd/production")]
    public async Task MaskinportenEndpoints_AreRejected_WhenFeatureFlagDisabled(string url)
    {
        using var client = CreateUnauthenticatedClient();
        using var response = await client.PostAsJsonAsync(url, new { });

        Assert.Contains(response.StatusCode, s_rejectedStatusCodes);
    }
}
