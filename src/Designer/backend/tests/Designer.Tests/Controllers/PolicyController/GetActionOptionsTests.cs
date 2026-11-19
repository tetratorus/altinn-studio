using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Studio.Designer.TypedHttpClients.AltinnAuthorization;
using Designer.Tests.Controllers.ApiTests;
using Designer.Tests.Utils;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using PolicyAdmin.Models;
using Xunit;

namespace Designer.Tests.Controllers.PolicyControllerTests;

public class GetActionOptionsTests
    : DesignerEndpointsTestsBase<GetActionOptionsTests>,
        IClassFixture<WebApplicationFactory<Program>>
{
    private readonly string _versionPrefix = "designer/api";

    public GetActionOptionsTests(WebApplicationFactory<Program> factory)
        : base(factory) { }

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        base.ConfigureTestServices(services);
        var policyOptionsMock = new Mock<IPolicyOptions>();
        policyOptionsMock
            .Setup(options => options.GetActionOptions(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ActionOption { ActionId = "read", ActionTitle = "Les" }]);
        services.AddSingleton(policyOptionsMock.Object);
    }

    [Fact]
    public async Task Get_ActionOptions()
    {
        var targetRepository = TestDataHelper.GenerateTestRepoName();
        await CopyRepositoryForTest("ttd", "ttd-resources", "testUser", targetRepository);

        string dataPathWithData = $"{_versionPrefix}/ttd/{targetRepository}/policy/actionoptions";
        List<ActionOption> actionOptions;

        using (HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, dataPathWithData))
        {
            HttpResponseMessage response = await HttpClient.SendAsync(httpRequestMessage);
            response.EnsureSuccessStatusCode();
            string responseBody = await response.Content.ReadAsStringAsync();
            actionOptions = System.Text.Json.JsonSerializer.Deserialize<List<ActionOption>>(
                responseBody,
                new JsonSerializerOptions() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }
            );
        }

        Assert.NotNull(actionOptions);
        Assert.Equal("read", Assert.Single(actionOptions).ActionId);
    }
}
