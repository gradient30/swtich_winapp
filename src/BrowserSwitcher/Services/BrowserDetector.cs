using System.IO;
using BrowserSwitcher.Models;
using Microsoft.Win32;

namespace BrowserSwitcher.Services;

/// <summary>
/// 检测本机已安装的常见浏览器，并与配置中的手动路径合并。
/// </summary>
public sealed class BrowserDetector
{
    private static readonly BrowserDefinition[] Definitions =
    {
        new(
            "brave",
            "Brave",
            "brave.exe",
            new[]
            {
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    "BraveSoftware",
                    "Brave-Browser",
                    "Application",
                    "brave.exe"),
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                    "BraveSoftware",
                    "Brave-Browser",
                    "Application",
                    "brave.exe"),
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "BraveSoftware",
                    "Brave-Browser",
                    "Application",
                    "brave.exe"),
            }),
        new(
            "chrome",
            "Google Chrome",
            "chrome.exe",
            new[]
            {
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    "Google",
                    "Chrome",
                    "Application",
                    "chrome.exe"),
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                    "Google",
                    "Chrome",
                    "Application",
                    "chrome.exe"),
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Google",
                    "Chrome",
                    "Application",
                    "chrome.exe"),
            }),
        new(
            "ie",
            "Internet Explorer",
            "iexplore.exe",
            new[]
            {
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    "Internet Explorer",
                    "iexplore.exe"),
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                    "Internet Explorer",
                    "iexplore.exe"),
            }),
        new(
            "edge",
            "Microsoft Edge",
            "msedge.exe",
            new[]
            {
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    "Microsoft",
                    "Edge",
                    "Application",
                    "msedge.exe"),
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                    "Microsoft",
                    "Edge",
                    "Application",
                    "msedge.exe"),
            }),
    };

    /// <summary>
    /// 检测四个内置浏览器的安装状态。
    /// </summary>
    /// <param name="config">应用配置；ManualPaths 键为 DisplayName。</param>
    /// <returns>固定四个 BrowserInfo，顺序为 brave/chrome/ie/edge。</returns>
    public IReadOnlyList<BrowserInfo> Detect(AppConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        var results = new List<BrowserInfo>(Definitions.Length);
        foreach (var definition in Definitions)
        {
            results.Add(DetectOne(definition, config));
        }

        return results;
    }

    private static BrowserInfo DetectOne(BrowserDefinition definition, AppConfig config)
    {
        if (config.ManualPaths.TryGetValue(definition.DisplayName, out var manualPath)
            && !string.IsNullOrWhiteSpace(manualPath)
            && File.Exists(manualPath))
        {
            return new BrowserInfo
            {
                Id = definition.Id,
                DisplayName = definition.DisplayName,
                ExecutablePath = manualPath,
                Status = BrowserStatus.Manual
            };
        }

        var installedPath = FindInstalledPath(definition);
        if (installedPath is not null)
        {
            return new BrowserInfo
            {
                Id = definition.Id,
                DisplayName = definition.DisplayName,
                ExecutablePath = installedPath,
                Status = BrowserStatus.Installed
            };
        }

        return new BrowserInfo
        {
            Id = definition.Id,
            DisplayName = definition.DisplayName,
            ExecutablePath = null,
            Status = BrowserStatus.NotFound
        };
    }

    private static string? FindInstalledPath(BrowserDefinition definition)
    {
        var fromRegistry = TryReadAppPath(definition.ExecutableName);
        if (fromRegistry is not null && File.Exists(fromRegistry))
        {
            return fromRegistry;
        }

        foreach (var candidate in definition.CommonPaths)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>
    /// 从 HKLM App Paths 读取可执行文件路径（同时查 64/32 位视图）。
    /// </summary>
    private static string? TryReadAppPath(string executableName)
    {
        var subKeyPath =
            $@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\{executableName}";

        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
                using var key = baseKey.OpenSubKey(subKeyPath);
                var value = key?.GetValue(null) as string;
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return Environment.ExpandEnvironmentVariables(value.Trim('"'));
                }
            }
            catch (System.Security.SecurityException)
            {
                // 无权限时跳过该视图，继续尝试其他来源
            }
            catch (UnauthorizedAccessException)
            {
                // 同上
            }
        }

        return null;
    }

    private sealed record BrowserDefinition(
        string Id,
        string DisplayName,
        string ExecutableName,
        string[] CommonPaths);
}
