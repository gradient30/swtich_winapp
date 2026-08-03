using System.IO;
using System.Reflection;
using System.Text;

namespace BrowserSwitcher.Services;

/// <summary>
/// 读取嵌入的发布说明，供帮助对话框展示。
/// </summary>
public sealed class ReleaseNotesService
{
    private const string ResourcePath =
        "BrowserSwitcher.Resources.ReleaseNotes.md";

    /// <summary>
    /// 从程序集嵌入资源加载说明；失败时返回兜底文案。
    /// </summary>
    /// <returns>面向用户的帮助文本。</returns>
    public string LoadHelpText()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream(ResourcePath);
            if (stream == null)
            {
                // WPF Resource 与 EmbeddedResource 命名可能不同，再试 pack 旁路文件
                return LoadFromPackOrFallback();
            }

            using var reader = new StreamReader(stream, Encoding.UTF8);
            return FormatForDisplay(reader.ReadToEnd());
        }
        catch (Exception)
        {
            return LoadFromPackOrFallback();
        }
    }

    /// <summary>
    /// 解析版本号标题行（如「# 1.1.0」）。
    /// </summary>
    /// <param name="markdown">说明原文。</param>
    /// <returns>版本号；无法解析时为 null。</returns>
    public static string? ParseVersionHeading(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return null;
        }

        using var reader = new StringReader(markdown);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            line = line.Trim();
            if (line.StartsWith("# ", StringComparison.Ordinal))
            {
                return line[2..].Trim();
            }
        }

        return null;
    }

    /// <summary>
    /// 将简单 Markdown 转为纯文本展示。
    /// </summary>
    /// <param name="markdown">说明原文。</param>
    /// <returns>对话框可用文本。</returns>
    public static string FormatForDisplay(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return FallbackText();
        }

        var sb = new StringBuilder();
        using var reader = new StringReader(markdown);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            var trimmed = line.TrimEnd();
            if (trimmed.StartsWith("# ", StringComparison.Ordinal))
            {
                sb.AppendLine("版本 " + trimmed[2..].Trim());
                sb.AppendLine();
                continue;
            }

            if (trimmed.StartsWith("## ", StringComparison.Ordinal))
            {
                sb.AppendLine(trimmed[3..].Trim());
                sb.AppendLine();
                continue;
            }

            if (trimmed.StartsWith("- ", StringComparison.Ordinal))
            {
                sb.AppendLine("· " + trimmed[2..].Trim());
                continue;
            }

            if (string.IsNullOrWhiteSpace(trimmed))
            {
                sb.AppendLine();
                continue;
            }

            sb.AppendLine(trimmed);
        }

        return sb.ToString().Trim();
    }

    private static string LoadFromPackOrFallback()
    {
        try
        {
            var uri = new Uri(
                "pack://application:,,,/Resources/ReleaseNotes.md",
                UriKind.Absolute);
            var info = System.Windows.Application.GetResourceStream(uri);
            if (info?.Stream != null)
            {
                using var reader = new StreamReader(info.Stream, Encoding.UTF8);
                return FormatForDisplay(reader.ReadToEnd());
            }
        }
        catch (Exception)
        {
            // 忽略，走兜底
        }

        return FallbackText();
    }

    private static string FallbackText()
    {
        return """
            版本未知

            作用
            在 Windows 托盘快速管理默认浏览器，减少在系统设置中层层查找。

            本次更新
            · 请查看 docs/releases 目录下的版本说明
            """.Trim();
    }
}
