using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32;

namespace BrowserSwitcher.Services;

/// <summary>
/// 生成 Windows UserChoice Hash（Firefox 同源实现，适用于 http/https/.htm/.html）。
/// </summary>
internal static class UserChoiceHash
{
    private const string UserExperience =
        "User Choice set via Windows User Experience "
        + "{D18B6DD5-6124-4341-9318-804003BAFA0B}";

    /// <summary>
    /// 生成 UserChoice Hash（Base64）。
    /// </summary>
    public static string? Generate(string extension, string progId, DateTime keyTimestampUtc)
    {
        var sid = WindowsIdentity.GetCurrent().User?.Value;
        if (string.IsNullOrWhiteSpace(sid))
        {
            return null;
        }

        var input = FormatUserChoiceString(
            extension,
            sid,
            progId,
            keyTimestampUtc);
        if (input is null)
        {
            return null;
        }

        return HashString(input);
    }

    internal static string? FormatUserChoiceString(
        string extension,
        string userSid,
        string progId,
        DateTime timestampUtc)
    {
        var local = timestampUtc.ToLocalTime();
        local = new DateTime(
            local.Year,
            local.Month,
            local.Day,
            local.Hour,
            local.Minute,
            0,
            DateTimeKind.Local);

        var fileTime = local.ToFileTime();
        var high = (uint)((fileTime >> 32) & 0xFFFFFFFF);
        var low = (uint)(fileTime & 0xFFFFFFFF);

        var raw = $"{extension}{userSid}{progId}{high:x8}{low:x8}{UserExperience}";
        return raw.ToLowerInvariant();
    }

    internal static string? HashString(string input)
    {
        var inputBytes = Encoding.Unicode.GetBytes(input + "\0");
        if (inputBytes.Length < 8)
        {
            return null;
        }

        var md5 = MD5.HashData(inputBytes);
        if (md5.Length < 8)
        {
            return null;
        }

        var blockCount = inputBytes.Length / 8;
        uint h0 = 0;
        uint h1 = 0;
        uint h0Acc = 0;
        uint h1Acc = 0;

        var c0Sets = new[]
        {
            new[]
            {
                BitConverter.ToUInt32(md5, 0) | 1,
                0xCF98B111u,
                0x87085B9Fu,
                0x12CEB96Du,
                0x257E1D83u
            },
            new[]
            {
                BitConverter.ToUInt32(md5, 4) | 1,
                0xA27416F5u,
                0xD38396FFu,
                0x7C932B89u,
                0xBFA49F69u
            }
        };

        var c1Sets = new[]
        {
            new[]
            {
                BitConverter.ToUInt32(md5, 0) | 1,
                0xEF0569FBu,
                0x689B6B9Fu,
                0x79F8A395u,
                0xC3EFEA97u
            },
            new[]
            {
                BitConverter.ToUInt32(md5, 4) | 1,
                0xC31713DBu,
                0xDDCD1F0Fu,
                0x59C3AF2Du,
                0x35BD1EC9u
            }
        };

        for (var block = 0; block < blockCount; block++)
        {
            for (var j = 0; j < 2; j++)
            {
                var word = BitConverter.ToUInt32(inputBytes, (block * 2 + j) * 4);
                var c0 = c0Sets[j];
                var c1 = c1Sets[j];

                h0 += word;
                h0 *= c0[0];
                h0 = WordSwap(h0) * c0[1];
                h0 = WordSwap(h0) * c0[2];
                h0 = WordSwap(h0) * c0[3];
                h0 = WordSwap(h0) * c0[4];
                h0Acc += h0;

                h1 += word;
                h1 = WordSwap(h1) * c1[1] + h1 * c1[0];
                h1 = (h1 >> 16) * c1[2] + h1 * c1[3];
                h1 = WordSwap(h1) * c1[4] + h1;
                h1Acc += h1;
            }
        }

        var hash = new[] { h0 ^ h1, h0Acc ^ h1Acc };
        return Convert.ToBase64String(MemoryMarshal.AsBytes(hash.AsSpan()));
    }

    private static uint WordSwap(uint v) => (v >> 16) | (v << 16);
}
