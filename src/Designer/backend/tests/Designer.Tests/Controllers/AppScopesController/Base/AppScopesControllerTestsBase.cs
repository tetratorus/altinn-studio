using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Altinn.Studio.Designer.Clients.Interfaces;
using Altinn.Studio.Designer.RepositoryClient.Model;
using Altinn.Studio.Designer.Services.Interfaces;
using Altinn.Studio.Designer.Services.Models;
using Designer.Tests.Controllers.ApiTests;
using Designer.Tests.Controllers.AppScopesController.Utils;
using Designer.Tests.Fixtures;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;

namespace Designer.Tests.Controllers.AppScopesController.Base;

public class AppScopesControllerTestsBase<TControllerTest> : DbDesignerEndpointsTestsBase<TControllerTest>
    where TControllerTest : class
{
    protected const string ServiceOwnerOrgWithoutAccess = "skd";

    private static readonly string[] s_serviceOwnerOrgs = ["ttd", ServiceOwnerOrgWithoutAccess];
    private static readonly string[] s_orgsWithUserAccess = ["ttd", "developer"];

    public AppScopesControllerTestsBase(WebApplicationFactory<Program> factory, DesignerDbFixture designerDbFixture)
        : base(factory, designerDbFixture) { }

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        base.ConfigureTestServices(services);

        // Replace test authentication handler with OIDC-specific test handler
        services.PostConfigure<Microsoft.AspNetCore.Authentication.AuthenticationOptions>(options =>
        {
            var testScheme = options.Schemes.FirstOrDefault(s => s.Name == TestAuthConstants.TestAuthenticationScheme);
            if (testScheme != null)
            {
                testScheme.HandlerType = typeof(TestOidcAuthHandler);
            }
        });

        var environmentsServiceMock = new Mock<IEnvironmentsService>();
        environmentsServiceMock
            .Setup(x => x.GetAltinnOrgNumber(It.IsAny<string>()))
            .ReturnsAsync((string org) => "991825827");
        environmentsServiceMock
            .Setup(x => x.IsAltinnOrg(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string org, CancellationToken _) => s_serviceOwnerOrgs.Contains(org));

        services.AddSingleton(environmentsServiceMock.Object);

        var giteaClientMock = new Mock<IGiteaClient>();
        giteaClientMock
            .Setup(x => x.GetUserOrganizations())
            .ReturnsAsync(s_orgsWithUserAccess.Select(org => new Organization { Username = org }).ToList());
        giteaClientMock
            .Setup(x => x.GetRepository(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(
                (string org, string repository) =>
                    new Repository
                    {
                        FullName = repository,
                        Permissions = new Permission { Push = s_orgsWithUserAccess.Contains(org) },
                    }
            );

        services.RemoveAll<IGiteaClient>();
        services.AddSingleton(giteaClientMock.Object);
    }
}
