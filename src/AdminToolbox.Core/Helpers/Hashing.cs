// SPDX-License-Identifier: MIT
using System.Security.Cryptography;
using System.Text;

namespace AdminToolbox.Core.Helpers;

public enum HashKind
{
    Md5,
    Sha1,
    Sha256,
    Sha512,
}

/// <summary>Hex digests of UTF-8 text.</summary>
public static class Hashing
{
    public static IReadOnlyList<HashKind> All { get; } = new[] { HashKind.Md5, HashKind.Sha1, HashKind.Sha256, HashKind.Sha512 };

    /// <summary>MD5 and SHA-1 are broken for collision resistance.</summary>
    public static bool IsWeak(HashKind kind) => kind is HashKind.Md5 or HashKind.Sha1;

    public static string Hex(HashKind kind, string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return Hex(kind, Encoding.UTF8.GetBytes(text));
    }

    public static string Hex(HashKind kind, byte[] data)
    {
        var digest = kind switch
        {
            HashKind.Md5 => Md5.HashData(data),
            HashKind.Sha1 => SHA1.HashData(data),
            HashKind.Sha256 => SHA256.HashData(data),
            HashKind.Sha512 => SHA512.HashData(data),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
        return Convert.ToHexString(digest).ToLowerInvariant();
    }
}
