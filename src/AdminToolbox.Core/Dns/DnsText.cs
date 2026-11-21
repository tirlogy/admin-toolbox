// SPDX-License-Identifier: MIT
using System.Text;

namespace AdminToolbox.Core.Dns;

/// <summary>Helpers for DNS presentation format.</summary>
public static class DnsText
{
    /// <summary>
    /// Joins the character-strings of a TXT record in presentation format.
    /// <c>"v=spf1 " "-all"</c> becomes <c>v=spf1 -all</c>. Unquoted input is returned trimmed.
    /// </summary>
    public static string UnquoteTxt(string data)
    {
        ArgumentNullException.ThrowIfNull(data);
        data = data.Trim();
        if (data.Length == 0 || data[0] != '"')
        {
            return data;
        }

        var sb = new StringBuilder(data.Length);
        var inQuotes = false;
        for (var i = 0; i < data.Length; i++)
        {
            var c = data[i];
            if (c == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (!inQuotes)
            {
                continue;
            }

            if (c == '\\' && i + 1 < data.Length)
            {
                if (i + 3 < data.Length && char.IsAsciiDigit(data[i + 1]) && char.IsAsciiDigit(data[i + 2]) && char.IsAsciiDigit(data[i + 3]))
                {
                    sb.Append((char)int.Parse(data.AsSpan(i + 1, 3), System.Globalization.CultureInfo.InvariantCulture));
                    i += 3;
                }
                else
                {
                    sb.Append(data[i + 1]);
                    i++;
                }

                continue;
            }

            sb.Append(c);
        }

        return sb.ToString();
    }

    /// <summary>Lower-case name without trailing dot.</summary>
    public static string NormalizeName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return name.Trim().TrimEnd('.').ToLowerInvariant();
    }

    /// <summary>
    /// Splits presentation-format rdata into tokens. Quoted strings stay one token including their quotes.
    /// </summary>
    public static IReadOnlyList<string> Tokenize(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var tokens = new List<string>();
        var sb = new StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (inQuotes)
            {
                sb.Append(c);
                if (c == '\\' && i + 1 < text.Length)
                {
                    sb.Append(text[++i]);
                }
                else if (c == '"')
                {
                    inQuotes = false;
                }

                continue;
            }

            if (c == '"')
            {
                inQuotes = true;
                sb.Append(c);
            }
            else if (char.IsWhiteSpace(c))
            {
                if (sb.Length > 0)
                {
                    tokens.Add(sb.ToString());
                    sb.Clear();
                }
            }
            else
            {
                sb.Append(c);
            }
        }

        if (sb.Length > 0)
        {
            tokens.Add(sb.ToString());
        }

        return tokens;
    }
}
