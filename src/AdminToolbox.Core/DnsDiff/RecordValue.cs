// SPDX-License-Identifier: MIT
using System.Globalization;
using System.Net;
using System.Text;
using AdminToolbox.Core.Dns;

namespace AdminToolbox.Core.DnsDiff;

/// <summary>Brings record data from zone files and DoH answers into one comparable form.</summary>
public static class RecordValue
{
    /// <summary>
    /// Normalizes rdata. Relative names are completed with <paramref name="origin"/> (zone file semantics).
    /// Throws <see cref="FormatException"/> for invalid data.
    /// </summary>
    public static string Normalize(DnsRecordType type, IReadOnlyList<string> tokens, string origin)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        if (tokens.Count == 0)
        {
            throw new FormatException("empty");
        }

        if (tokens[0] == "\\#" && type != DnsRecordType.TXT)
        {
            return DecodeGeneric(type, tokens);
        }

        switch (type)
        {
            case DnsRecordType.A:
            case DnsRecordType.AAAA:
                if (tokens.Count != 1 || !IPAddress.TryParse(tokens[0], out var ip)
                    || (type == DnsRecordType.A) != (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    || (type == DnsRecordType.A && tokens[0].Split('.').Length != 4))
                {
                    throw new FormatException(type + " " + string.Join(' ', tokens));
                }

                return ip.ToString();
            case DnsRecordType.CNAME:
            case DnsRecordType.NS:
            case DnsRecordType.PTR:
                Expect(tokens, 1, type);
                return Name(tokens[0], origin);
            case DnsRecordType.MX:
                Expect(tokens, 2, type);
                return Number(tokens[0]) + " " + Name(tokens[1], origin);
            case DnsRecordType.SRV:
                Expect(tokens, 4, type);
                return Number(tokens[0]) + " " + Number(tokens[1]) + " " + Number(tokens[2]) + " " + Name(tokens[3], origin);
            case DnsRecordType.TXT:
                return string.Concat(tokens.Select(DnsText.UnquoteTxt));
            case DnsRecordType.CAA:
                Expect(tokens, 3, type);
                return Number(tokens[0]) + " " + tokens[1].ToLowerInvariant() + " \"" + DnsText.UnquoteTxt(tokens[2]) + "\"";
            case DnsRecordType.SOA:
                Expect(tokens, 7, type);
                return Name(tokens[0], origin) + " " + Name(tokens[1], origin) + " " + string.Join(' ', tokens.Skip(2).Select(Number));
            default:
                return string.Join(' ', tokens);
        }
    }

    /// <summary>Completes a possibly relative name and normalizes it.</summary>
    public static string Name(string name, string origin)
    {
        if (name == "@")
        {
            return DnsText.NormalizeName(origin);
        }

        if (name.EndsWith('.') || origin.Length == 0)
        {
            return DnsText.NormalizeName(name);
        }

        return DnsText.NormalizeName(name + "." + origin);
    }

    private static void Expect(IReadOnlyList<string> tokens, int count, DnsRecordType type)
    {
        if (tokens.Count != count)
        {
            throw new FormatException(type + " " + string.Join(' ', tokens));
        }
    }

    private static string Number(string token) =>
        uint.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out var n)
            ? n.ToString(CultureInfo.InvariantCulture)
            : throw new FormatException(token);

    // RFC 3597 "\# len hex" as returned for CAA by some resolvers.
    private static string DecodeGeneric(DnsRecordType type, IReadOnlyList<string> tokens)
    {
        var hex = string.Concat(tokens.Skip(2));
        byte[] bytes;
        try
        {
            bytes = Convert.FromHexString(hex);
        }
        catch (FormatException)
        {
            return string.Join(' ', tokens);
        }

        if (type == DnsRecordType.CAA && bytes.Length >= 2 && bytes.Length >= 2 + bytes[1])
        {
            var tag = Encoding.ASCII.GetString(bytes, 2, bytes[1]).ToLowerInvariant();
            var value = Encoding.UTF8.GetString(bytes, 2 + bytes[1], bytes.Length - 2 - bytes[1]);
            return bytes[0].ToString(CultureInfo.InvariantCulture) + " " + tag + " \"" + value + "\"";
        }

        return string.Join(' ', tokens);
    }
}
