#nullable enable
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Altinn.Studio.Designer.Helpers;
using DotNet.Testcontainers.Builders;
using VerifyXunit;
using Xunit;

namespace Designer.Tests.Helpers;

public class PackageVersionHelperTests
{
    [Theory]
    [InlineData("Altinn.App.Api")]
    [InlineData("NonExistingNuget")]
    public async Task TryGetPackageVersionFromCsprojFile_GivenValidCsprojFile_ReturnsTrue(string packageName)
    {
        string testTemplateCsProjPath = Path.Join(
            CommonDirectoryPath.GetSolutionDirectory().DirectoryPath,
            "..",
            "testdata",
            "App",
            "App.csproj"
        );

        string[] packages = [packageName, $"{packageName}.Experimental"];
        string[][] inputs = [packages, packages.Reverse().ToArray()];
        var outputs = new List<(bool, string?)>();
        foreach (var input in inputs)
        {
            bool result = PackageVersionHelper.TryGetPackageVersionFromCsprojFile(
                testTemplateCsProjPath,
                input,
                out var version
            );

            outputs.Add((result, version?.ToString()));
        }

        var snapshot = new { Inputs = inputs, Outputs = outputs };
        await Verifier.Verify(snapshot).UseParameters(packageName);
    }

    [Theory]
    [InlineData("Altinn.App.Api")]
    public async Task TryGetPackageVersionFromCsprojFile_GivenValidCsprojFileWithPinnedVersions_ReturnsTrue(
        string packageName
    )
    {
        // In this directory, the csproj file has versions pinned with [ and ]
        string testTemplateCsProjPath = Path.Join(
            CommonDirectoryPath.GetSolutionDirectory().DirectoryPath,
            "..",
            "testdata",
            "App",
            "App.csproj"
        );

        string[] packages = [packageName, $"{packageName}.Experimental"];
        string[][] inputs = [packages, packages.Reverse().ToArray()];
        var outputs = new List<(bool, string?)>();
        foreach (var input in inputs)
        {
            bool result = PackageVersionHelper.TryGetPackageVersionFromCsprojFile(
                testTemplateCsProjPath,
                input,
                out var version
            );

            outputs.Add((result, version?.ToString()));
        }

        var snapshot = new { Inputs = inputs, Outputs = outputs };
        await Verifier.Verify(snapshot).UseParameters(packageName);
    }

    [Theory]
    [InlineData("""<PackageReference Include="Altinn.App.Api" Version="9.1.2" />""", "9.1.2")]
    [InlineData("""<PackageReference Include="Altinn.App.Api" Version=" 9.1.2 " />""", "9.1.2")]
    [InlineData("""<PackageReference Include="Altinn.App.Api" Version="[9.0.0-preview.4]" />""", "9.0.0-preview.4")]
    [InlineData("""<PackageReference Include="Altinn.App.Api" Version="[9.0,10)" />""", "9.0.0")]
    [InlineData("""<PackageReference Include="Altinn.App.Api" Version="9.*" />""", "9.0.0")]
    [InlineData("""<PackageReference Include="Altinn.App.Api" Version="9.2" />""", "9.2.0")]
    [InlineData("""<PackageReference Include="Altinn.App.Api"><Version>9.1.2</Version></PackageReference>""", "9.1.2")]
    [InlineData("""<PackageReference Include="Altinn.App.Api" VersionOverride="9.1.2" />""", "9.1.2")]
    [InlineData(
        """<PackageReference Include="Altinn.App.Api"><VersionOverride>9.1.2</VersionOverride></PackageReference>""",
        "9.1.2"
    )]
    [InlineData("""<PackageReference Include="Altinn.App.Api" Version="8.0.0" VersionOverride="9.1.2" />""", "9.1.2")]
    [InlineData("""<PackageReference Include="Altinn.App.Api.Experimental" Version="9.1.2" />""", "9.1.2")]
    public void TryGetPackageVersionFromCsprojContent_GivenSupportedVersionShape_ReturnsVersion(
        string packageReference,
        string expectedVersion
    )
    {
        byte[] csprojContent = CreateCsproj(packageReference);

        bool result = PackageVersionHelper.TryGetPackageVersionFromCsprojContent(
            csprojContent,
            s_appLibPackageNames,
            out var version
        );

        Assert.True(result);
        Assert.Equal(expectedVersion, version.ToNormalizedString());
    }

    [Theory]
    [InlineData("""<PackageReference Include="Altinn.App.Api" />""")]
    [InlineData("""<PackageReference Include="Altinn.App.Api" Version="$(AltinnAppApiVersion)" />""")]
    [InlineData("""<PackageReference Include="Altinn.App.Api" Version="(,10.0)" />""")]
    [InlineData("""<PackageReference Include="Altinn.App.Core" Version="9.1.2" />""")]
    public void TryGetPackageVersionFromCsprojContent_GivenUnresolvableVersion_ReturnsFalse(string packageReference)
    {
        byte[] csprojContent = CreateCsproj(packageReference);

        bool result = PackageVersionHelper.TryGetPackageVersionFromCsprojContent(
            csprojContent,
            s_appLibPackageNames,
            out var version
        );

        Assert.False(result);
        Assert.Null(version);
    }

    private static readonly string[] s_appLibPackageNames = ["Altinn.App.Api", "Altinn.App.Api.Experimental"];

    private static byte[] CreateCsproj(string packageReference) =>
        Encoding.UTF8.GetBytes(
            $"""
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <ItemGroup>
                {packageReference}
              </ItemGroup>
            </Project>
            """
        );
}
