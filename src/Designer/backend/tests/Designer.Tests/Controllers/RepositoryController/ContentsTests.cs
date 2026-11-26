using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Altinn.Studio.Designer.Clients.Interfaces;
using Altinn.Studio.Designer.Configuration;
using Altinn.Studio.Designer.Models;
using Altinn.Studio.Designer.Services.Interfaces;
using Designer.Tests.Controllers.ApiTests;
using Designer.Tests.Mocks;
using Designer.Tests.TestAttributes;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Designer.Tests.Controllers.RepositoryController;

public class ContentsTests : DesignerEndpointsTestsBase<ContentsTests>, IClassFixture<WebApplicationFactory<Program>>
{
    private readonly Mock<IRepository> _repositoryMock = new Mock<IRepository>();
    private static string VersionPrefix => "/designer/api/repos";

    public ContentsTests(WebApplicationFactory<Program> factory)
        : base(factory) { }

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        services.Configure<ServiceRepositorySettings>(c => c.RepositoryLocation = TestRepositoriesLocation);
        services.AddSingleton<IGiteaClient, IGiteaClientMock>();
        services.AddSingleton(_ => _repositoryMock.Object);
    }

    [Fact]
    public async Task Contents_ContentsReturned_OK()
    {
        // Arrange
        string uri = $"{VersionPrefix}/repo/ttd/apps-test/contents";

        _repositoryMock
            .Setup(r => r.GetContents(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(
                new List<FileSystemObject>
                {
                    new FileSystemObject
                    {
                        Name = "appsettings.Development.json",
                        Encoding = "Unicode (UTF-8)",
                        Path = "App/appsettings.Development.json",
                        Type = "File",
                    },
                }
            );

        using HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, uri);

        // Act
        using HttpResponseMessage res = await HttpClient.SendAsync(httpRequestMessage);

        // Assert
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [SkipOnOsPlatformsFact("WINDOWS")]
    public async Task ContentsZip_Full_ExcludesSymbolicLinks()
    {
        // Arrange
        string appRoot = Directory.CreateTempSubdirectory().FullName;
        string outsideDirectory = Directory.CreateTempSubdirectory().FullName;
        string outsideFile = Path.Combine(outsideDirectory, "secret.txt");
        string fileLink = Path.Combine(appRoot, "env.txt");
        string directoryLink = Path.Combine(appRoot, "linked-directory");
        string uri = $"{VersionPrefix}/repo/ttd/apps-test/contents.zip?full=true";

        try
        {
            File.WriteAllText(Path.Combine(appRoot, "regular.txt"), "regular");
            File.WriteAllText(outsideFile, "secret");
            File.CreateSymbolicLink(fileLink, outsideFile);
            Directory.CreateSymbolicLink(directoryLink, outsideDirectory);

            _repositoryMock
                .Setup(r => r.GetAppPath(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(appRoot + Path.DirectorySeparatorChar);

            using HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, uri);

            // Act
            using HttpResponseMessage res = await HttpClient.SendAsync(httpRequestMessage);

            // Assert
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            await using Stream zipStream = await res.Content.ReadAsStreamAsync();
            using ZipArchive archive = new ZipArchive(zipStream, ZipArchiveMode.Read);
            Assert.Equal(["regular.txt"], archive.Entries.Select(entry => entry.FullName));
        }
        finally
        {
            File.Delete(fileLink);
            if (Directory.Exists(directoryLink))
            {
                Directory.Delete(directoryLink);
            }
            Directory.Delete(appRoot, recursive: true);
            Directory.Delete(outsideDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task Contents_ContentsIsNull_BadRequest()
    {
        // Arrange
        string uri = $"{VersionPrefix}/repo/acn-sbuad/apps-test/contents?path=App";

        _repositoryMock
            .Setup(r => r.GetContents(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns((List<FileSystemObject>)null);

        using HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, uri);

        // Act
        using HttpResponseMessage res = await HttpClient.SendAsync(httpRequestMessage);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }
}
