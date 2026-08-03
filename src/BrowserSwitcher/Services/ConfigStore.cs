using System.IO;
using System.Text.Json;
using BrowserSwitcher.Models;

namespace BrowserSwitcher.Services;

/// <summary>
/// 读写本地 JSON 配置；损坏时回退默认并标记提示。
/// </summary>
public sealed class ConfigStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _configPath;

    /// <summary>
    /// 最近一次 <see cref="Load"/> 是否因损坏/IO 失败而回退默认配置。
    /// </summary>
    public bool LastLoadUsedFallback { get; private set; }

    /// <summary>
    /// 最近一次回退的原因说明；未回退时为 null。
    /// </summary>
    public string? LastLoadFallbackReason { get; private set; }

    public ConfigStore(string? directory = null)
    {
        var dir = directory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "BrowserSwitcher");
        Directory.CreateDirectory(dir);
        _configPath = Path.Combine(dir, "config.json");
    }

    public AppConfig Load()
    {
        LastLoadUsedFallback = false;
        LastLoadFallbackReason = null;

        try
        {
            if (!File.Exists(_configPath))
            {
                return new AppConfig();
            }

            var json = File.ReadAllText(_configPath);
            var config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions)
                         ?? new AppConfig();
            config.ManualPaths ??= new Dictionary<string, string>();
            if (string.IsNullOrWhiteSpace(config.Theme))
            {
                config.Theme = "Dark";
            }

            return config;
        }
        catch (JsonException)
        {
            LastLoadUsedFallback = true;
            LastLoadFallbackReason = "配置文件损坏，已回退默认配置";
            return new AppConfig();
        }
        catch (IOException)
        {
            LastLoadUsedFallback = true;
            LastLoadFallbackReason = "配置文件读取失败，已回退默认配置";
            return new AppConfig();
        }
    }

    public void Save(AppConfig config)
    {
        var json = JsonSerializer.Serialize(config, JsonOptions);
        File.WriteAllText(_configPath, json);
    }
}
