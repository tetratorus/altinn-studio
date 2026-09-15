using System.Text;
using Altinn.App.Core.Configuration;
using Altinn.App.Core.Features.Redirect;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Altinn.App.Core.Tests.Features.Redirect;

public class ReturnUrlServiceTests
{
    private static ReturnUrlService CreateService(string hostName = "local.altinn.cloud") =>
        new(Options.Create(new GeneralSettings { HostName = hostName }), NullLogger<ReturnUrlService>.Instance);

    private static string Encode(string url) => Convert.ToBase64String(Encoding.UTF8.GetBytes(url));

    [Theory]
    [InlineData("https://local.altinn.cloud/ttd/app")]
    [InlineData("http://local.altinn.cloud/ttd/app")]
    [InlineData("https://ttd.apps.local.altinn.cloud/ttd/app?x=1")]
    public void Validate_AllowsHttpUrlsOnValidHost(string url)
    {
        var result = CreateService().Validate(Encode(url));

        Assert.True(result.IsValid);
        Assert.Equal(url, result.DecodedUrl);
    }

    [Theory]
    [InlineData("javascript://local.altinn.cloud/%0aalert(document.cookie)")]
    [InlineData("JavaScript://local.altinn.cloud/%0aalert(1)")]
    [InlineData("data://local.altinn.cloud/text/html,<script>alert(1)</script>")]
    [InlineData("ftp://local.altinn.cloud/file")]
    [InlineData("vbscript://local.altinn.cloud/msgbox")]
    public void Validate_RejectsNonHttpSchemes(string url)
    {
        var result = CreateService().Validate(Encode(url));

        Assert.False(result.IsValid);
        Assert.True(result.IsInvalidDomain);
        Assert.Null(result.DecodedUrl);
    }

    [Theory]
    [InlineData("https://evil.example.com/")]
    [InlineData("https://local.altinn.cloud.evil.example.com/")]
    public void Validate_RejectsInvalidHost(string url)
    {
        var result = CreateService().Validate(Encode(url));

        Assert.False(result.IsValid);
        Assert.True(result.IsInvalidDomain);
    }

    [Theory]
    [InlineData("/relative/path")]
    [InlineData("not a url")]
    public void Validate_RejectsNonAbsoluteUrls(string url)
    {
        var result = CreateService().Validate(Encode(url));

        Assert.False(result.IsValid);
        Assert.Null(result.DecodedUrl);
    }

    [Fact]
    public void Validate_RejectsInvalidBase64()
    {
        var result = CreateService().Validate("not-base64!");

        Assert.False(result.IsValid);
        Assert.False(result.IsInvalidDomain);
    }
}
