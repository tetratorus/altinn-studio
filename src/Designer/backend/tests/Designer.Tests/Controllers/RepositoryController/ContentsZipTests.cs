using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Altinn.Studio.Designer.Enums;
using Altinn.Studio.Designer.Models;
using Altinn.Studio.Designer.Services.Interfaces;
using Designer.Tests.Controllers.ApiTests;
using Designer.Tests.Utils;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Designer.Tests.Controllers.RepositoryController;

public class ContentsZipTests
    : DesignerEndpointsTestsBase<ContentsZipTests>,
        IClassFixture<WebApplicationFactory<Program>>
{
    private const string Org = "ttd";
    private const string Repository = "apps-test";
    private const string Developer = "testUser";
    private const string RegularFile = "App/appsettings.json";
    private const string FileLinkName = "secret-file-link";
    private const string DirectoryLinkName = "secret-directory-link";
    private const string SecretFileName = "secret.txt";
    private const string SecretContent = "outside-repository-secret";

    private readonly Mock<ISourceControl> _sourceControlMock = new();
    private static string VersionPrefix => "/designer/api/repos";

    public ContentsZipTests(WebApplicationFactory<Program> factory)
        : base(factory) { }

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        base.ConfigureTestServices(services);
        services.AddSingleton(_ => _sourceControlMock.Object);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ContentsZip_RepositoryContainsSymbolicLinks_LinksAreNotFollowed(bool full)
    {
        // Arrange
        string targetRepository = TestDataHelper.GenerateTestRepoName();
        await CopyRepositoryForTest(Org, Repository, Developer, targetRepository);

        string outsideDirectory = Path.Combine(Path.GetTempPath(), $"outside-{Guid.NewGuid()}");
        Directory.CreateDirectory(outsideDirectory);
        string secretFilePath = Path.Combine(outsideDirectory, SecretFileName);
        await File.WriteAllTextAsync(secretFilePath, SecretContent);

        try
        {
            File.CreateSymbolicLink(Path.Combine(TestRepoPath, FileLinkName), secretFilePath);
            Directory.CreateSymbolicLink(Path.Combine(TestRepoPath, DirectoryLinkName), outsideDirectory);

            _sourceControlMock
                .Setup(s => s.Status(It.IsAny<AltinnRepoEditingContext>()))
                .Returns(
                    new List<RepositoryContent>
                    {
                        new() { FilePath = RegularFile, FileStatus = FileStatus.ModifiedInWorkdir },
                        new() { FilePath = FileLinkName, FileStatus = FileStatus.NewInWorkdir },
                        new()
                        {
                            FilePath = $"{DirectoryLinkName}/{SecretFileName}",
                            FileStatus = FileStatus.NewInWorkdir,
                        },
                        new() { FilePath = "../../../outside.txt", FileStatus = FileStatus.NewInWorkdir },
                    }
                );

            string uri = $"{VersionPrefix}/repo/{Org}/{targetRepository}/contents.zip?full={full}";

            // Act
            using HttpResponseMessage response = await HttpClient.GetAsync(uri);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            await using Stream zipStream = await response.Content.ReadAsStreamAsync();
            using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

            List<string> entryNames = archive.Entries.Select(entry => entry.FullName.TrimStart('/')).ToList();
            Assert.Contains(RegularFile, entryNames);
            Assert.DoesNotContain(entryNames, name => name.Contains(FileLinkName));
            Assert.DoesNotContain(entryNames, name => name.Contains(DirectoryLinkName));
            Assert.DoesNotContain(entryNames, name => name.Contains("outside.txt"));

            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                using var reader = new StreamReader(entry.Open());
                Assert.DoesNotContain(SecretContent, await reader.ReadToEndAsync());
            }
        }
        finally
        {
            Directory.Delete(outsideDirectory, true);
        }
    }
}
