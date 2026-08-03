using BrowserSwitcher.Services;

namespace BrowserSwitcher.Tests;

public class ReleaseNotesServiceTests
{
    [Fact]
    public void ParseVersionHeading_ReadsHashTitle()
    {
        var version = ReleaseNotesService.ParseVersionHeading(
            "# 1.1.0\n\n## 作用\n说明");
        Assert.Equal("1.1.0", version);
    }

    [Fact]
    public void FormatForDisplay_ConvertsBullets()
    {
        var text = ReleaseNotesService.FormatForDisplay(
            """
            # 1.1.0

            ## 本次更新

            - 界面焕新
            - 托盘一键切换
            """);

        Assert.Contains("版本 1.1.0", text);
        Assert.Contains("本次更新", text);
        Assert.Contains("· 界面焕新", text);
        Assert.Contains("· 托盘一键切换", text);
    }

    [Fact]
    public void LoadHelpText_ReturnsNonEmpty()
    {
        var text = new ReleaseNotesService().LoadHelpText();
        Assert.False(string.IsNullOrWhiteSpace(text));
        Assert.Contains("版本", text);
    }
}
