using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Designer.Tests.Controllers.ApiTests;
using Designer.Tests.Utils;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Designer.Tests.Controllers.RepositoryController;

public class ContentsZipTests
    : DesignerEndpointsTestsBase<ContentsZipTests>,
        IClassFixture<WebApplicationFactory<Program>>
{
    private const string Org = "ttd";
    private const string SourceRepository = "apps-test";
    private const string Developer = "testUser";
    private const string OutsideFileContent = "outside-secret-content";

    public ContentsZipTests(WebApplicationFactory<Program> factory)
        : base(factory) { }

    [Fact]
    public async Task ContentsZip_Full_ShouldNotFollowSymbolicLinks()
    {
        // Arrange
        string targetRepository = TestDataHelper.GenerateTestRepoName();
        await CopyRepositoryForTest(Org, SourceRepository, Developer, targetRepository);

        string outsideDirectory = Path.Combine(Path.GetTempPath(), $"outside-{Guid.NewGuid()}");
        Directory.CreateDirectory(outsideDirectory);
        string outsideFile = Path.Combine(outsideDirectory, "secret.txt");
        await File.WriteAllTextAsync(outsideFile, OutsideFileContent);

        try
        {
            File.CreateSymbolicLink(Path.Combine(TestRepoPath, "linked-file.txt"), outsideFile);
            Directory.CreateSymbolicLink(Path.Combine(TestRepoPath, "linked-directory"), outsideDirectory);
            Directory.CreateSymbolicLink(Path.Combine(TestRepoPath, "linked-parent"), TestRepoPath);

            string uri = $"/designer/api/repos/repo/{Org}/{targetRepository}/contents.zip?full=true";

            // Act
            using HttpResponseMessage response = await HttpClient.GetAsync(uri);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            await using Stream zipStream = await response.Content.ReadAsStreamAsync();
            using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

            Assert.NotEmpty(archive.Entries);
            Assert.DoesNotContain(archive.Entries, entry => entry.FullName.Contains("linked-"));
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                using var reader = new StreamReader(entry.Open());
                Assert.DoesNotContain(OutsideFileContent, await reader.ReadToEndAsync());
            }
        }
        finally
        {
            Directory.Delete(outsideDirectory, true);
        }
    }
}
