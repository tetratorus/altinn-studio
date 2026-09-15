using Altinn.Studio.Designer.Configuration;
using Altinn.Studio.Designer.Services.Implementation;
using Xunit;

namespace Designer.Tests.Services;

public class UrlPolicyValidatorTests
{
    [Fact]
    public void IsAllowed_WhenBlockedDomainButWildcardAllowListMatches_ShouldReturnTrue()
    {
        var validator = new UrlPolicyValidator(
            new UrlValidationSettings { AllowedList = ["example.com/repos/*/wwwroot/*"], BlockedList = ["example.com"] }
        );

        bool isAllowed = validator.IsAllowed(
            "https://example.com/repos/org-name/app-name/src/branch/master/App/wwwroot/the-image.png"
        );

        Assert.True(isAllowed);
    }

    [Fact]
    public void IsAllowed_WhenBlockedDomainAndNoAllowList_ShouldReturnFalse()
    {
        var validator = new UrlPolicyValidator(
            new UrlValidationSettings { AllowedList = [], BlockedList = ["blocked.com"] }
        );

        bool isAllowed = validator.IsAllowed("https://blocked.com/image.png");

        Assert.False(isAllowed);
    }

    [Fact]
    public void IsAllowed_WhenBlockedDomainButExplicitPathAllowed_ShouldReturnTrue()
    {
        var validator = new UrlPolicyValidator(
            new UrlValidationSettings { AllowedList = ["blocked.com/allowedpath"], BlockedList = ["blocked.com"] }
        );

        bool isAllowed = validator.IsAllowed("https://blocked.com/allowedpath");

        Assert.True(isAllowed);
    }

    [Fact]
    public void IsAllowed_WhenDomainIsNotBlocked_ShouldReturnTrue()
    {
        var validator = new UrlPolicyValidator(
            new UrlValidationSettings { AllowedList = [], BlockedList = ["blocked.com"] }
        );

        bool isAllowed = validator.IsAllowed("https://otherdomain.com/image.png");

        Assert.True(isAllowed);
    }

    [Fact]
    public void IsAllowed_WhenNoBlockedOrAllowedDomains_ShouldReturnTrueForAnyDomain()
    {
        var validator = new UrlPolicyValidator(new UrlValidationSettings { AllowedList = [], BlockedList = [] });

        bool isAllowed = validator.IsAllowed("https://anydomain.com/image.png");

        Assert.True(isAllowed);
    }

    [Fact]
    public void IsAllowed_WhenWildcardAllowListMatchesPath_ShouldRespectWildcard()
    {
        var validator = new UrlPolicyValidator(
            new UrlValidationSettings { AllowedList = ["example.com/wwwroot*"], BlockedList = ["example.com"] }
        );

        bool isAllowedForAllowedPath = validator.IsAllowed("https://example.com/wwwroot/file1.png");
        bool isAllowedForBlockedPath = validator.IsAllowed("https://example.com/other/file2.png");

        Assert.True(isAllowedForAllowedPath);
        Assert.False(isAllowedForBlockedPath);
    }

    [Fact]
    public void IsAllowed_WhenSubdomainOfBlockedDomain_ShouldBeBlocked()
    {
        var validator = new UrlPolicyValidator(
            new UrlValidationSettings { AllowedList = [], BlockedList = ["example.com"] }
        );

        bool isAllowed = validator.IsAllowed("https://sub.example.com/image.png");

        Assert.False(isAllowed);
    }

    [Theory]
    [InlineData("http://localhost/image.png")]
    [InlineData("http://sub.localhost/image.png")]
    [InlineData("http://127.0.0.1/image.png")]
    [InlineData("http://127.1.2.3:8080/image.png")]
    [InlineData("http://10.0.0.5/image.png")]
    [InlineData("http://172.16.0.1/image.png")]
    [InlineData("http://192.168.1.1/image.png")]
    [InlineData("http://169.254.169.254/latest/meta-data/")]
    [InlineData("http://100.64.0.1/image.png")]
    [InlineData("http://0.0.0.0/image.png")]
    [InlineData("http://[::1]/image.png")]
    [InlineData("http://[fe80::1]/image.png")]
    [InlineData("http://[fd00::1]/image.png")]
    [InlineData("http://[::ffff:169.254.169.254]/image.png")]
    [InlineData("http://[64:ff9b::7f00:1]/image.png")]
    public void IsAllowed_WhenHostIsPrivateOrLoopbackAddress_ShouldReturnFalse(string url)
    {
        var validator = new UrlPolicyValidator(new UrlValidationSettings { AllowedList = [], BlockedList = [] });

        bool isAllowed = validator.IsAllowed(url);

        Assert.False(isAllowed);
    }

    [Fact]
    public void IsAllowed_WhenPrivateNetworkTargetsAreAllowed_ShouldReturnTrueForLoopback()
    {
        var validator = new UrlPolicyValidator(
            new UrlValidationSettings
            {
                AllowedList = [],
                BlockedList = [],
                AllowPrivateNetworkTargets = true,
            }
        );

        bool isAllowed = validator.IsAllowed("http://127.0.0.1:5000/image.png");

        Assert.True(isAllowed);
    }

    [Fact]
    public void IsAllowed_WhenHostIsPublicIpAddress_ShouldReturnTrue()
    {
        var validator = new UrlPolicyValidator(new UrlValidationSettings { AllowedList = [], BlockedList = [] });

        bool isAllowed = validator.IsAllowed("https://93.184.216.34/image.png");

        Assert.True(isAllowed);
    }

    [Fact]
    public void IsAllowed_WhenUrlContainsUserInfo_ShouldReturnFalse()
    {
        var validator = new UrlPolicyValidator(new UrlValidationSettings { AllowedList = [], BlockedList = [] });

        bool isAllowed = validator.IsAllowed("https://user:password@example.com/image.png");

        Assert.False(isAllowed);
    }

    [Fact]
    public void IsAllowed_WhenSubdomainIsExplicitlyAllowed_ShouldReturnTrue()
    {
        var validator = new UrlPolicyValidator(
            new UrlValidationSettings { AllowedList = ["sub.example.com/allowedpath*"], BlockedList = ["example.com"] }
        );

        bool isAllowed = validator.IsAllowed("https://sub.example.com/allowedpath/image.png");

        Assert.True(isAllowed);
    }
}
