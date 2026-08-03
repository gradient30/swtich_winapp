using BrowserSwitcher.Services;

namespace BrowserSwitcher.Tests;

public class ThemeServiceTests
{
    [Theory]
    [InlineData(null, "Dark")]
    [InlineData("", "Dark")]
    [InlineData("Dark", "Dark")]
    [InlineData("light", "Light")]
    [InlineData("LIGHT", "Light")]
    [InlineData("other", "Dark")]
    public void Normalize_ReturnsExpected(string? input, string expected)
    {
        Assert.Equal(expected, ThemeService.Normalize(input));
    }
}
