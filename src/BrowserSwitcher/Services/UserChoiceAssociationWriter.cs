using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace BrowserSwitcher.Services;

/// <summary>
/// 直接写入 UserChoice（ProgId + Hash）。个人本地在 UCPD 未拦截时可用。
/// </summary>
public static class UserChoiceAssociationWriter
{
    private static readonly string[] RequiredBrowserAssociations =
    {
        "http",
        "https"
    };

    private static readonly string[] OptionalBrowserAssociations =
    {
        ".htm",
        ".html"
    };

    /// <summary>
    /// 浏览器 Id 到 ProgId 映射。
    /// </summary>
    public static string? GetProgIdForBrowser(string browserId) =>
        browserId switch
        {
            "chrome" => "ChromeHTML",
            "edge" => "MSEdgeHTM",
            "brave" => "BraveHTML",
            "ie" => "IE.HTTP",
            _ => null
        };

    /// <summary>
    /// 尝试将指定 ProgId 设为默认浏览器（http/https/.htm/.html）。
    /// </summary>
    public static bool TrySetDefaultBrowser(string progId)
    {
        if (string.IsNullOrWhiteSpace(progId))
        {
            return false;
        }

        foreach (var assoc in RequiredBrowserAssociations)
        {
            if (!TrySetAssociation(assoc, progId))
            {
                return false;
            }
        }

        foreach (var assoc in OptionalBrowserAssociations)
        {
            TrySetAssociation(assoc, progId);
        }

        return true;
    }

    /// <summary>
    /// 按浏览器 Id 直写默认关联。
    /// </summary>
    public static bool TrySetDefaultBrowserById(string browserId)
    {
        var progId = GetProgIdForBrowser(browserId);
        return progId is not null && TrySetDefaultBrowser(progId);
    }

    private static bool TrySetAssociation(string extension, string progId)
    {
        try
        {
            var basePath = GetAssociationKeyPath(extension);
            var userChoicePath = basePath + @"\UserChoice";

            using var baseKey = Registry.CurrentUser.CreateSubKey(
                basePath,
                writable: true);
            if (baseKey is null)
            {
                return false;
            }

            using var userChoice = baseKey.CreateSubKey(
                "UserChoice",
                writable: true);
            if (userChoice is null)
            {
                return false;
            }

            // 先清掉旧 Hash，避免 ProgId 已变而 Hash 仍过期时被系统后台校验回滚。
            try
            {
                userChoice.DeleteValue("Hash", throwOnMissingValue: false);
            }
            catch (IOException)
            {
                // 忽略删除失败，继续尝试写入
            }

            userChoice.SetValue("ProgId", progId, RegistryValueKind.String);
            userChoice.Flush();

            // Hash 必须基于 ProgId 写入后的键修改时间（Firefox / 微软校验一致）。
            if (!TryGetKeyLastWriteUtc(userChoicePath, out var timestamp))
            {
                timestamp = DateTime.UtcNow;
            }

            var hash = UserChoiceHash.Generate(extension, progId, timestamp);
            if (hash is null)
            {
                return false;
            }

            userChoice.SetValue("Hash", hash, RegistryValueKind.String);
            userChoice.Flush();
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (System.Security.SecurityException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
    }

    private static string GetAssociationKeyPath(string extension)
    {
        if (extension.StartsWith('.'))
        {
            return $@"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\{extension}";
        }

        return $@"Software\Microsoft\Windows\Shell\Associations\UrlAssociations\{extension}";
    }

    private static bool TryGetKeyLastWriteUtc(string relativePath, out DateTime timestampUtc)
    {
        timestampUtc = default;
        if (RegOpenKeyEx(HkeyCurrentUser, relativePath, 0, KeyRead, out var hKey) != 0)
        {
            return false;
        }

        try
        {
            if (RegQueryInfoKey(
                    hKey,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    out var fileTime) != 0)
            {
                return false;
            }

            timestampUtc = DateTime.FromFileTimeUtc(fileTime);
            return true;
        }
        finally
        {
            RegCloseKey(hKey);
        }
    }

    private static readonly IntPtr HkeyCurrentUser = new(unchecked((int)0x80000001));
    private const int KeyRead = 0x20019;

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int RegOpenKeyEx(
        IntPtr hKey,
        string lpSubKey,
        int ulOptions,
        int samDesired,
        out IntPtr phkResult);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern int RegQueryInfoKey(
        IntPtr hKey,
        IntPtr lpClass,
        IntPtr lpcbClass,
        IntPtr lpReserved,
        IntPtr lpcSubKeys,
        IntPtr lpcbMaxSubKeyLen,
        IntPtr lpcbMaxClassLen,
        IntPtr lpcValues,
        IntPtr lpcbMaxValueNameLen,
        IntPtr lpcbMaxValueLen,
        IntPtr lpcbSecurityDescriptor,
        out long lpftLastWriteTime);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern int RegCloseKey(IntPtr hKey);
}
