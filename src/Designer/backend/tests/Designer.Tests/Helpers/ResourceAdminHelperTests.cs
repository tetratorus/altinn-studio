using System;
using Altinn.Studio.Designer.Helpers;
using Xunit;

namespace Designer.Tests.Helpers;

public class ResourceAdminHelperTests
{
    [Fact]
    public void GetResourceFileStructureName_MigratedApp_EncodesColon()
    {
        string result = ResourceAdminHelper.GetResourceFileStructureName("app_ttd_a1-myapp:1234");

        Assert.Equal("app_ttd_a1-myapp%3A1234", result);
    }

    [Theory]
    [InlineData("app_ttd_a1-../../../victim/org/app:1234")]
    [InlineData("app_ttd_a1-..\\..\\victim:1234")]
    [InlineData("app_ttd_a1-sub/dir:1234")]
    public void GetResourceFileStructureName_MigratedAppWithPathSeparators_Throws(string identifier)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ResourceAdminHelper.GetResourceFileStructureName(identifier));
    }
}
