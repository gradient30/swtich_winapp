using BrowserSwitcher.Services;

namespace BrowserSwitcher.Tests;

public class UserChoiceHashTests
{
    [Fact]
    public void Generate_ReturnsBase64Hash_ForHttpChrome()
    {
        var sid = System.Security.Principal.WindowsIdentity.GetCurrent().User?.Value;
        Assert.NotNull(sid);

        var ts = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc);
        var hash = UserChoiceHash.Generate("http", "ChromeHTML", ts);
        Assert.NotNull(hash);
        Assert.Matches("^[A-Za-z0-9+/=]+$", hash);
    }

    [Fact]
    public void GetProgIdForBrowser_MapsBrave()
    {
        Assert.Equal("BraveHTML", UserChoiceAssociationWriter.GetProgIdForBrowser("brave"));
    }
}
