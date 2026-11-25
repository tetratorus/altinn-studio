using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Studio.Designer.Clients.Interfaces;
using Altinn.Studio.Designer.Models;
using Altinn.Studio.Designer.Models.Dto;
using Altinn.Studio.Designer.Services.Interfaces;
using Designer.Tests.Controllers.ApiTests;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Designer.Tests.Controllers.OrgCodeListController;

public class PublishCodeListTests(WebApplicationFactory<Program> factory)
    : DesignerEndpointsTestsBase<PublishCodeListTests>(factory),
        IClassFixture<WebApplicationFactory<Program>>
{
    private readonly Mock<IUserOrganizationService> _userOrganizationServiceMock = new();
    private readonly Mock<ISharedContentClient> _sharedContentClientMock = new();

    private const string Org = "ttd";
    private const string CodeListTitle = "someCodeList";
    private const string PublishedVersion = "1";

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        base.ConfigureTestServices(services);
        services.AddSingleton(_ => _userOrganizationServiceMock.Object);
        services.AddSingleton(_ => _sharedContentClientMock.Object);
    }

    [Fact]
    public async Task PublishCodeList_Returns_200Ok_When_User_Is_Member_Of_Org()
    {
        // Arrange
        _userOrganizationServiceMock.Setup(s => s.UserIsMemberOfOrganization(Org)).ReturnsAsync(true);
        _sharedContentClientMock
            .Setup(c => c.PublishCodeList(Org, CodeListTitle, It.IsAny<CodeList>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PublishedVersion);

        // Act
        using HttpResponseMessage response = await HttpClient.PostAsJsonAsync(ApiUrl(Org), CreateRequest());

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        PublishedVersionResponse responseBody = await response.Content.ReadFromJsonAsync<PublishedVersionResponse>();
        Assert.Equal(PublishedVersion, responseBody.PublishedVersion);
    }

    [Fact]
    public async Task PublishCodeList_Returns_403Forbidden_When_User_Is_Not_Member_Of_Org()
    {
        // Arrange
        const string OtherOrg = "other-org";
        _userOrganizationServiceMock.Setup(s => s.UserIsMemberOfAnyOrganization()).ReturnsAsync(true);
        _userOrganizationServiceMock.Setup(s => s.UserIsMemberOfOrganization(Org)).ReturnsAsync(true);
        _userOrganizationServiceMock.Setup(s => s.UserIsMemberOfOrganization(OtherOrg)).ReturnsAsync(false);

        // Act
        using HttpResponseMessage response = await HttpClient.PostAsJsonAsync(ApiUrl(OtherOrg), CreateRequest());

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        _sharedContentClientMock.VerifyNoOtherCalls();
    }

    private static PublishCodeListRequest CreateRequest()
    {
        Dictionary<string, string> label = new() { { "nb", "En" } };
        Code code = new(Value: "1", Label: label, Description: null, HelpText: null, Tags: null);
        List<Code> codes = [code];
        return new PublishCodeListRequest(CodeListTitle, new CodeList(codes, null, null));
    }

    private static string ApiUrl(string org) => $"/designer/api/{org}/code-lists/new/publish";
}
