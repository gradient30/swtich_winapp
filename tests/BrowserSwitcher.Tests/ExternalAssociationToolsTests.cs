using BrowserSwitcher.Services;

namespace BrowserSwitcher.Tests;

public class ExternalAssociationToolsTests
{
    [Theory]
    [InlineData("chrome", "http,https ChromeHTML")]
    [InlineData("edge", "http,https MSEdgeHTM")]
    [InlineData("brave", "http,https BraveHTML")]
    [InlineData("ie", "http,https IE.HTTP")]
    public void BuildSetUserFtaArgumentAttempts_FirstIsProgIdPair(
        string browserId,
        string expectedFirst)
    {
        var attempts = ExternalAssociationTools.BuildSetUserFtaArgumentAttempts(
            browserId);
        Assert.NotEmpty(attempts);
        Assert.Equal(expectedFirst, attempts[0]);
    }

    [Fact]
    public void BuildSetDefaultBrowserArgumentAttempts_ChromeShortcut()
    {
        var attempts = ExternalAssociationTools.BuildSetDefaultBrowserArgumentAttempts(
            "chrome");
        Assert.Equal("chrome", attempts[0]);
    }
}
