// SPDX-License-Identifier: MIT
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace AdminToolbox.Core.Helpers;

public enum JwtStatus
{
    Valid,
    Expired,
    NotYetValid,
    NoExpiry,
}

public sealed record JwtInfo(
    string HeaderJson,
    string PayloadJson,
    string? Algorithm,
    string? Type,
    DateTimeOffset? IssuedAt,
    DateTimeOffset? NotBefore,
    DateTimeOffset? Expires,
    JwtStatus Status,
    bool Unsigned);

/// <summary>Decodes (does not verify) a JSON Web Token.</summary>
public static class JwtDecoder
{
    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Indented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, // display only, never embedded in HTML unescaped
    };

    /// <summary>Throws <see cref="FormatException"/> with a code as message: jwt.format, jwt.jwe, jwt.base64, jwt.json.</summary>
    public static JwtInfo Decode(string token, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(token);
        var t = token.Trim();
        if (t.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            t = t[7..].Trim();
        }

        var parts = t.Split('.');
        if (parts.Length == 5)
        {
            throw new FormatException("jwt.jwe");
        }

        if (parts.Length != 3 || parts[0].Length == 0 || parts[1].Length == 0)
        {
            throw new FormatException("jwt.format");
        }

        using var header = ParseJson(parts[0]);
        using var payload = ParseJson(parts[1]);
        var alg = GetString(header.RootElement, "alg");
        var typ = GetString(header.RootElement, "typ");
        var iat = GetTime(payload.RootElement, "iat");
        var nbf = GetTime(payload.RootElement, "nbf");
        var exp = GetTime(payload.RootElement, "exp");

        JwtStatus status;
        if (nbf is not null && now < nbf)
        {
            status = JwtStatus.NotYetValid;
        }
        else if (exp is null)
        {
            status = JwtStatus.NoExpiry;
        }
        else
        {
            status = now >= exp ? JwtStatus.Expired : JwtStatus.Valid;
        }

        var unsigned = parts[2].Length == 0 || string.Equals(alg, "none", StringComparison.OrdinalIgnoreCase);
        return new JwtInfo(Pretty(header.RootElement), Pretty(payload.RootElement), alg, typ, iat, nbf, exp, status, unsigned);
    }

    internal static byte[] Base64UrlDecode(string s)
    {
        var b = s.Replace('-', '+').Replace('_', '/');
        b += (b.Length % 4) switch { 2 => "==", 3 => "=", 0 => string.Empty, _ => throw new FormatException("jwt.base64") };
        try
        {
            return Convert.FromBase64String(b);
        }
        catch (FormatException)
        {
            throw new FormatException("jwt.base64");
        }
    }

    private static JsonDocument ParseJson(string part)
    {
        var bytes = Base64UrlDecode(part);
        try
        {
            var doc = JsonDocument.Parse(bytes);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                doc.Dispose();
                throw new FormatException("jwt.json");
            }

            return doc;
        }
        catch (JsonException)
        {
            throw new FormatException("jwt.json");
        }
    }

    private static string Pretty(JsonElement element)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, WriterOptions))
        {
            element.WriteTo(writer);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static string? GetString(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static DateTimeOffset? GetTime(JsonElement e, string name)
    {
        if (!e.TryGetProperty(name, out var v) || v.ValueKind != JsonValueKind.Number || !v.TryGetDouble(out var seconds))
        {
            return null;
        }

        if (seconds is < -62135596800 or > 253402300799)
        {
            return null;
        }

        return DateTimeOffset.FromUnixTimeSeconds((long)seconds);
    }
}
