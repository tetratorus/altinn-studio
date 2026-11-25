using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Altinn.Studio.Designer.Clients.Interfaces;
using Altinn.Studio.Designer.Configuration;
using Altinn.Studio.Designer.Enums;
using Altinn.Studio.Designer.Models;
using Altinn.Studio.Designer.Services.Interfaces;
using Designer.Tests.Controllers.ApiTests;
using Designer.Tests.Mocks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Designer.Tests.Controllers.RepositoryController;

public class ContentsZipTests
    : DesignerEndpointsTestsBase<ContentsZipTests>,
        IClassFixture<WebApplicationFactory<Program>>
{
    private const string SecretContent = "outside-secret-content";
    private const string RegularFileContent = "regular-file-content";
    private static string VersionPrefix => "/designer/api/repos";

    private readonly Mock<IRepository> _repositoryMock = new Mock<IRepository>();
    private readonly Mock<ISourceControl> _sourceControlMock = new Mock<ISourceControl>();
    private readonly string _workingDirectory;
    private readonly string _appRoot;

    public ContentsZipTests(WebApplicationFactory<Program> factory)
        : base(factory)
    {
        _workingDirectory = Path.Combine(Path.GetTempPath(), "altinn-contents-zip-tests", Guid.NewGuid().ToString());
        _appRoot = Path.Combine(_workingDirectory, "repo");
        string outsideDirectory = Path.Combine(_workingDirectory, "outside");
        Directory.CreateDirectory(Path.Combine(_appRoot, "App"));
        Directory.CreateDirectory(outsideDirectory);

        string outsideFile = Path.Combine(outsideDirectory, "secret.txt");
        File.WriteAllText(outsideFile, SecretContent);
        File.WriteAllText(Path.Combine(_appRoot, "App", "regular.txt"), RegularFileContent);
        File.CreateSymbolicLink(Path.Combine(_appRoot, "secrets.txt"), outsideFile);
        Directory.CreateSymbolicLink(Path.Combine(_appRoot, "linked-dir"), outsideDirectory);

        _repositoryMock.Setup(r => r.GetAppPath(It.IsAny<string>(), It.IsAny<string>())).Returns(_appRoot);
    }

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        services.Configure<ServiceRepositorySettings>(c => c.RepositoryLocation = TestRepositoriesLocation);
        services.AddSingleton<IGiteaClient, IGiteaClientMock>();
        services.AddSingleton(_ => _repositoryMock.Object);
        services.AddSingleton(_ => _sourceControlMock.Object);
    }

    [Fact]
    public async Task ContentsZip_Full_ExcludesSymbolicLinks()
    {
        // Arrange
        string uri = $"{VersionPrefix}/repo/ttd/apps-test/contents.zip?full=true";

        // Act
        Dictionary<string, string> entries = await DownloadZipEntries(uri);

        // Assert
        Assert.Single(entries);
        Assert.Equal(RegularFileContent, entries.Single(e => e.Key.EndsWith("App/regular.txt")).Value);
        Assert.DoesNotContain(entries.Values, content => content.Contains(SecretContent));
    }

    [Fact]
    public async Task ContentsZip_Changes_ExcludesSymbolicLinksAndPathsOutsideRepository()
    {
        // Arrange
        string uri = $"{VersionPrefix}/repo/ttd/apps-test/contents.zip?full=false";
        _sourceControlMock
            .Setup(s => s.Status(It.IsAny<AltinnRepoEditingContext>()))
            .Returns(
                new List<RepositoryContent>
                {
                    new() { FilePath = "App/regular.txt", FileStatus = FileStatus.ModifiedInWorkdir },
                    new() { FilePath = "secrets.txt", FileStatus = FileStatus.NewInWorkdir },
                    new() { FilePath = "linked-dir/secret.txt", FileStatus = FileStatus.NewInWorkdir },
                    new() { FilePath = "../outside/secret.txt", FileStatus = FileStatus.NewInWorkdir },
                }
            );

        // Act
        Dictionary<string, string> entries = await DownloadZipEntries(uri);

        // Assert
        Assert.Single(entries);
        Assert.Equal(RegularFileContent, entries["App/regular.txt"]);
        Assert.DoesNotContain(entries.Values, content => content.Contains(SecretContent));
    }

    private async Task<Dictionary<string, string>> DownloadZipEntries(string uri)
    {
        using HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, uri);
        using HttpResponseMessage response = await HttpClient.SendAsync(httpRequestMessage);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using Stream zipStream = await response.Content.ReadAsStreamAsync();
        using ZipArchive archive = new ZipArchive(zipStream, ZipArchiveMode.Read);
        var entries = new Dictionary<string, string>();
        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            using var reader = new StreamReader(entry.Open());
            entries[entry.FullName] = await reader.ReadToEndAsync();
        }

        return entries;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (Directory.Exists(_workingDirectory))
        {
            Directory.Delete(_workingDirectory, recursive: true);
        }
    }
}
