// SPDX-License-Identifier: MIT
using System.Text;

namespace AdminToolbox.Core.Helpers;

public enum EncodingKind
{
    Base64,
    Base64Url,
    Url,
    Hex,
}

/// <summary>Decoded text, or a hex dump when the bytes are not valid UTF-8.</summary>
public sealed record DecodeResult(string Text, bool IsBinary);

/// <summary>Base64, Base64url, URL and hex encoding of UTF-8 text.</summary>
public static class Encoders
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public static string Encode(EncodingKind kind, string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var bytes = Encoding.UTF8.GetBytes(text);
        return kind switch
        {
            EncodingKind.Base64 => Convert.ToBase64String(bytes),
            EncodingKind.Base64Url => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_'),
            EncodingKind.Url => Uri.EscapeDataString(text),
            EncodingKind.Hex => Convert.ToHexString(bytes).ToLowerInvariant(),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
    }

    /// <summary>Throws <see cref="FormatException"/> for invalid input.</summary>
    public static DecodeResult Decode(EncodingKind kind, string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (kind == EncodingKind.Url)
        {
            return new DecodeResult(Uri.UnescapeDataString(text.Replace('+', ' ')), false);
        }

        var compact = new string(text.Where(c => !char.IsWhiteSpace(c)).ToArray());
        byte[] bytes;
        switch (kind)
        {
            case EncodingKind.Base64:
            case EncodingKind.Base64Url:
                var b = compact.Replace('-', '+').Replace('_', '/').TrimEnd('=');
                if (b.Length % 4 == 1)
                {
                    throw new FormatException("base64");
                }

                b += new string('=', (4 - (b.Length % 4)) % 4);
                bytes = Convert.FromBase64String(b);
                break;
            case EncodingKind.Hex:
                var h = compact.Replace(":", string.Empty, StringComparison.Ordinal).Replace("-", string.Empty, StringComparison.Ordinal);
                if (h.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                {
                    h = h[2..];
                }

                bytes = Convert.FromHexString(h);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind));
        }

        try
        {
            return new DecodeResult(StrictUtf8.GetString(bytes), false);
        }
        catch (DecoderFallbackException)
        {
            return new DecodeResult(Convert.ToHexString(bytes).ToLowerInvariant(), true);
        }
    }
}
