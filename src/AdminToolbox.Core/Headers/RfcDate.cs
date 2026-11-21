// SPDX-License-Identifier: MIT
using System.Globalization;
using System.Text;

namespace AdminToolbox.Core.Headers;

/// <summary>Lenient parser for RFC 5322 dates as found in Date and Received headers.</summary>
public static class RfcDate
{
    private static readonly string[] Months = { "jan", "feb", "mar", "apr", "may", "jun", "jul", "aug", "sep", "oct", "nov", "dec" };

    private static readonly Dictionary<string, int> Zones = new(StringComparer.OrdinalIgnoreCase)
    {
        ["UT"] = 0,
        ["UTC"] = 0,
        ["GMT"] = 0,
        ["Z"] = 0,
        ["EST"] = -5,
        ["EDT"] = -4,
        ["CST"] = -6,
        ["CDT"] = -5,
        ["MST"] = -7,
        ["MDT"] = -6,
        ["PST"] = -8,
        ["PDT"] = -7,
        ["CET"] = 1,
        ["CEST"] = 2,
        ["MEZ"] = 1,
        ["MESZ"] = 2,
    };

    public static bool TryParse(string? text, out DateTimeOffset value)
    {
        value = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var tokens = StripComments(text).Replace(",", " ", StringComparison.Ordinal)
            .Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries).ToList();
        if (tokens.Count > 0 && char.IsAsciiLetter(tokens[0][0]) && MonthIndex(tokens[0]) < 0)
        {
            tokens.RemoveAt(0); // day of week
        }

        if (tokens.Count < 4)
        {
            return false;
        }

        int day, month, year;
        int timeIndex;
        if (int.TryParse(tokens[0], NumberStyles.None, CultureInfo.InvariantCulture, out day))
        {
            month = MonthIndex(tokens[1]);
            if (month < 0 || !int.TryParse(tokens[2], NumberStyles.None, CultureInfo.InvariantCulture, out year))
            {
                return false;
            }

            timeIndex = 3;
        }
        else
        {
            // asctime-like: "Nov 19 10:00:00 2026"
            month = MonthIndex(tokens[0]);
            if (month < 0 || !int.TryParse(tokens[1], NumberStyles.None, CultureInfo.InvariantCulture, out day)
                || tokens.Count < 4 || !int.TryParse(tokens[3], NumberStyles.None, CultureInfo.InvariantCulture, out year))
            {
                return false;
            }

            tokens.RemoveAt(3);
            tokens.Insert(2, year.ToString(CultureInfo.InvariantCulture));
            timeIndex = 3;
        }

        if (year < 100)
        {
            year += year < 50 ? 2000 : 1900;
        }

        var timeParts = tokens[timeIndex].Split(':');
        if (timeParts.Length < 2
            || !int.TryParse(timeParts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var hour)
            || !int.TryParse(timeParts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var minute))
        {
            return false;
        }

        var second = 0;
        if (timeParts.Length > 2)
        {
            var secText = timeParts[2].Split('.')[0];
            if (!int.TryParse(secText, NumberStyles.None, CultureInfo.InvariantCulture, out second))
            {
                return false;
            }
        }

        var offset = TimeSpan.Zero;
        if (tokens.Count > timeIndex + 1)
        {
            offset = ParseZone(tokens[timeIndex + 1]);
        }

        try
        {
            value = new DateTimeOffset(year, month + 1, day, hour, minute, Math.Min(second, 59), offset);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    private static TimeSpan ParseZone(string zone)
    {
        if ((zone[0] == '+' || zone[0] == '-') && zone.Length == 5
            && int.TryParse(zone.AsSpan(1, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var h)
            && int.TryParse(zone.AsSpan(3, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var m)
            && h <= 14 && m < 60)
        {
            var span = new TimeSpan(h, m, 0);
            return zone[0] == '-' ? -span : span;
        }

        return Zones.TryGetValue(zone, out var hours) ? TimeSpan.FromHours(hours) : TimeSpan.Zero;
    }

    private static int MonthIndex(string token) =>
        token.Length >= 3 ? Array.IndexOf(Months, token[..3].ToLowerInvariant()) : -1;

    internal static string StripComments(string text)
    {
        var sb = new StringBuilder(text.Length);
        var depth = 0;
        foreach (var c in text)
        {
            if (c == '(')
            {
                depth++;
            }
            else if (c == ')' && depth > 0)
            {
                depth--;
            }
            else if (depth == 0)
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }
}
