// SPDX-License-Identifier: MIT
using System.Globalization;
using System.Text;
using AdminToolbox.Core.Dns;

namespace AdminToolbox.Core.DnsDiff;

public sealed record ZoneRecord(string Name, DnsRecordType Type, int? Ttl, string Value, int Line);

public sealed record ZoneError(int Line, string Code, string Text);

public sealed class ZoneParseResult
{
    public List<ZoneRecord> Records { get; } = new();

    public List<ZoneError> Errors { get; } = new();
}

/// <summary>
/// Parses BIND zone files and simple lists ("www CNAME example.net.", dig output).
/// Supports $ORIGIN, $TTL, comments, parentheses, relative names and an inherited owner.
/// </summary>
public static class ZoneParser
{
    public const int MaxRecords = 2000;

    private static readonly HashSet<string> Classes = new(StringComparer.OrdinalIgnoreCase) { "IN", "CH", "HS", "CS" };

    public static ZoneParseResult Parse(string text, string origin)
    {
        ArgumentNullException.ThrowIfNull(text);
        var result = new ZoneParseResult();
        origin = DnsText.NormalizeName(origin ?? string.Empty);
        int? defaultTtl = null;
        string? lastOwner = null;

        foreach (var (lineNo, startsWithSpace, tokens) in LogicalLines(text))
        {
            if (tokens.Count == 0)
            {
                continue;
            }

            var first = tokens[0];
            if (first.StartsWith('$'))
            {
                switch (first.ToUpperInvariant())
                {
                    case "$ORIGIN" when tokens.Count >= 2:
                        origin = RecordValue.Name(tokens[1], origin);
                        break;
                    case "$TTL" when tokens.Count >= 2 && TryTtl(tokens[1], out var t):
                        defaultTtl = t;
                        break;
                    default:
                        result.Errors.Add(new ZoneError(lineNo, "zone.directive", first));
                        break;
                }

                continue;
            }

            var i = 0;
            string owner;
            if (startsWithSpace)
            {
                if (lastOwner is null)
                {
                    result.Errors.Add(new ZoneError(lineNo, "zone.owner", string.Join(' ', tokens)));
                    continue;
                }

                owner = lastOwner;
            }
            else
            {
                owner = RecordValue.Name(first, origin);
                i = 1;
            }

            int? ttl = defaultTtl;
            DnsRecordType? type = null;
            for (; i < tokens.Count && type is null; i++)
            {
                var tok = tokens[i];
                if (TryTtl(tok, out var parsedTtl))
                {
                    ttl = parsedTtl;
                }
                else if (Classes.Contains(tok))
                {
                    // class is ignored, only IN is used in practice
                }
                else if (Enum.TryParse<DnsRecordType>(tok, true, out var parsedType) && !int.TryParse(tok, out _))
                {
                    type = parsedType;
                }
                else
                {
                    result.Errors.Add(new ZoneError(lineNo, "zone.type", tok));
                    break;
                }
            }

            if (type is null)
            {
                if (!result.Errors.Any(e => e.Line == lineNo))
                {
                    result.Errors.Add(new ZoneError(lineNo, "zone.type", string.Join(' ', tokens)));
                }

                continue;
            }

            lastOwner = owner;
            var rdata = tokens.Skip(i).ToList();
            try
            {
                var value = RecordValue.Normalize(type.Value, rdata, origin);
                if (result.Records.Count >= MaxRecords)
                {
                    result.Errors.Add(new ZoneError(lineNo, "zone.toomany", MaxRecords.ToString(CultureInfo.InvariantCulture)));
                    break;
                }

                result.Records.Add(new ZoneRecord(owner, type.Value, ttl, value, lineNo));
            }
            catch (FormatException)
            {
                result.Errors.Add(new ZoneError(lineNo, "zone.rdata", string.Join(' ', tokens)));
            }
        }

        return result;
    }

    /// <summary>TTL in seconds, also BIND units like 1h30m or 2d.</summary>
    public static bool TryTtl(string token, out int seconds)
    {
        seconds = 0;
        if (token.Length == 0 || !char.IsAsciiDigit(token[0]))
        {
            return false;
        }

        if (int.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out seconds))
        {
            return true;
        }

        long total = 0;
        long current = 0;
        var sawDigit = false;
        foreach (var c in token.ToLowerInvariant())
        {
            if (char.IsAsciiDigit(c))
            {
                current = (current * 10) + (c - '0');
                sawDigit = true;
                continue;
            }

            long factor = c switch { 's' => 1, 'm' => 60, 'h' => 3600, 'd' => 86400, 'w' => 604800, _ => -1 };
            if (factor < 0 || !sawDigit)
            {
                return false;
            }

            total += current * factor;
            current = 0;
            sawDigit = false;
        }

        if (sawDigit || total > int.MaxValue)
        {
            return false;
        }

        seconds = (int)total;
        return true;
    }

    private static IEnumerable<(int Line, bool StartsWithSpace, List<string> Tokens)> LogicalLines(string text)
    {
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var sb = new StringBuilder();
        var depth = 0;
        var startLine = 0;
        var startsWithSpace = false;
        for (var n = 0; n < lines.Length; n++)
        {
            var line = StripComment(lines[n]);
            if (depth == 0)
            {
                startLine = n + 1;
                startsWithSpace = line.Length > 0 && (line[0] == ' ' || line[0] == '\t');
                sb.Clear();
            }

            var inQuotes = false;
            foreach (var c in line)
            {
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                    sb.Append(c);
                }
                else if (inQuotes)
                {
                    sb.Append(c);
                }
                else if (c == '(')
                {
                    depth++;
                    sb.Append(' ');
                }
                else if (c == ')' && depth > 0)
                {
                    depth--;
                    sb.Append(' ');
                }
                else
                {
                    sb.Append(c);
                }
            }

            sb.Append(' ');
            if (depth == 0)
            {
                yield return (startLine, startsWithSpace, DnsText.Tokenize(sb.ToString()).ToList());
            }
        }

        if (depth > 0)
        {
            yield return (startLine, startsWithSpace, DnsText.Tokenize(sb.ToString()).ToList());
        }
    }

    private static string StripComment(string line)
    {
        var inQuotes = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '\\')
            {
                i++;
            }
            else if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == ';' && !inQuotes)
            {
                return line[..i];
            }
        }

        return line;
    }
}
