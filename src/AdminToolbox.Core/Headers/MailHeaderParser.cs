// SPDX-License-Identifier: MIT
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using AdminToolbox.Core.Dns;
using AdminToolbox.Core.Mail;

namespace AdminToolbox.Core.Headers;

/// <summary>Parses raw mail headers into route, authentication results, spam hints and anomalies.</summary>
public static partial class MailHeaderParser
{
    /// <summary>Delays above this are reported as slow hops.</summary>
    public static readonly TimeSpan SlowHop = TimeSpan.FromMinutes(5);

    private static readonly string[] SpamHeaderPrefixes =
    {
        "X-Spam", "X-MS-Exchange-Organization-SCL", "X-Forefront-Antispam-Report", "X-Microsoft-Antispam",
        "X-UI-Filterresults", "X-UI-Out-Filterresults", "X-Gm-Spam",
    };

    public static HeaderAnalysis Analyze(string raw)
    {
        ArgumentNullException.ThrowIfNull(raw);
        var result = new HeaderAnalysis();
        var fields = ParseFields(raw);
        result.Fields = fields;
        if (fields.Count == 0)
        {
            result.Add("header.empty", Severity.Error);
            return result;
        }

        result.From = First(fields, "From");
        result.To = First(fields, "To");
        result.Subject = First(fields, "Subject");
        result.Date = First(fields, "Date");
        result.MessageId = First(fields, "Message-ID");
        result.ReturnPath = First(fields, "Return-Path");
        result.ReplyTo = First(fields, "Reply-To");

        ParseHops(fields, result);
        ParseAuth(fields, result);
        ParseSpam(fields, result);
        CheckAnomalies(result);
        return result;
    }

    // ---------- fields ----------
    internal static List<HeaderField> ParseFields(string raw)
    {
        var fields = new List<HeaderField>();
        string? name = null;
        StringBuilder? value = null;
        foreach (var line0 in raw.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            var line = line0.TrimEnd('\r');
            if (line.Length == 0)
            {
                if (name is not null)
                {
                    break; // end of header block, body follows
                }

                continue;
            }

            if ((line[0] == ' ' || line[0] == '\t') && value is not null)
            {
                value.Append(' ').Append(line.Trim());
                continue;
            }

            var colon = line.IndexOf(':');
            if (colon <= 0 || line[..colon].Any(c => c <= ' ' || c > '~'))
            {
                continue;
            }

            if (name is not null)
            {
                fields.Add(new HeaderField(name, value!.ToString().Trim()));
            }

            name = line[..colon];
            value = new StringBuilder(line[(colon + 1)..].Trim());
        }

        if (name is not null)
        {
            fields.Add(new HeaderField(name, value!.ToString().Trim()));
        }

        return fields;
    }

    private static string? First(List<HeaderField> fields, string name) =>
        fields.FirstOrDefault(f => f.Name.Equals(name, StringComparison.OrdinalIgnoreCase))?.Value;

    private static IEnumerable<HeaderField> All(List<HeaderField> fields, string name) =>
        fields.Where(f => f.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    // ---------- hops ----------
    private static void ParseHops(List<HeaderField> fields, HeaderAnalysis result)
    {
        var received = All(fields, "Received").Reverse().ToList();
        var hops = new List<ReceivedHop>();
        DateTimeOffset? previous = null;
        for (var i = 0; i < received.Count; i++)
        {
            var hop = ParseReceived(received[i].Value, i + 1, previous);
            hops.Add(hop);
            if (hop.Date is not null)
            {
                previous = hop.Date;
            }

            if (hop.Delay is { } delay)
            {
                if (delay < TimeSpan.FromSeconds(-60))
                {
                    result.Add("hop.clockskew", Severity.Info, hop.Index.ToString(CultureInfo.InvariantCulture));
                }
                else if (delay > SlowHop)
                {
                    result.Add("hop.slow", Severity.Warning, hop.Index.ToString(CultureInfo.InvariantCulture), ((int)delay.TotalMinutes).ToString(CultureInfo.InvariantCulture));
                }
            }
        }

        result.Hops = hops;
        var dated = hops.Where(h => h.Date is not null).ToList();
        if (dated.Count >= 2)
        {
            result.TotalDelay = dated[^1].Date!.Value - dated[0].Date!.Value;
        }

        if (hops.Count == 0)
        {
            result.Add("hop.none", Severity.Info);
        }
    }

    internal static ReceivedHop ParseReceived(string value, int index, DateTimeOffset? previous)
    {
        var semicolon = value.LastIndexOf(';');
        DateTimeOffset? date = null;
        var body = value;
        if (semicolon >= 0 && RfcDate.TryParse(value[(semicolon + 1)..], out var d))
        {
            date = d;
            body = value[..semicolon];
        }

        var from = ReceivedFrom().Match(body);
        var by = ReceivedBy().Match(body);
        var with = ReceivedWith().Match(body);
        string? ip = null;
        var fromPart = from.Success ? body[from.Index..(by.Success && by.Index > from.Index ? by.Index : body.Length)] : string.Empty;
        // The connecting IP is usually the last bracketed address of the from-part ("helo (rdns [ip])").
        var bracketed = BracketIp().Matches(fromPart);
        if (bracketed.Count > 0)
        {
            ip = bracketed[^1].Groups[1].Value;
        }
        else
        {
            // Exchange Online style: "from host (198.51.100.23)"
            var bare = ParenIp().Match(fromPart);
            if (bare.Success && System.Net.IPAddress.TryParse(bare.Groups[1].Value, out _))
            {
                ip = bare.Groups[1].Value;
            }
        }

        TimeSpan? delay = date is not null && previous is not null ? date.Value - previous.Value : null;
        return new ReceivedHop(
            index,
            from.Success ? from.Groups[1].Value : null,
            by.Success ? by.Groups[1].Value : null,
            with.Success ? with.Groups[1].Value : null,
            ip,
            date,
            delay,
            value);
    }

    [GeneratedRegex(@"(?:^|\s)from\s+(\S+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ReceivedFrom();

    [GeneratedRegex(@"(?:^|\s|\))by\s+(\S+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ReceivedBy();

    [GeneratedRegex(@"\swith\s+(\S+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ReceivedWith();

    [GeneratedRegex(@"\[(?:IPv6:)?([0-9a-fA-F:.]+)\]", RegexOptions.CultureInvariant)]
    private static partial Regex BracketIp();

    [GeneratedRegex(@"\(([0-9a-fA-F:.]{7,})\)", RegexOptions.CultureInvariant)]
    private static partial Regex ParenIp();

    // ---------- authentication ----------
    private static void ParseAuth(List<HeaderField> fields, HeaderAnalysis result)
    {
        var results = new List<AuthResult>();
        foreach (var f in fields)
        {
            if (f.Name.Equals("Authentication-Results", StringComparison.OrdinalIgnoreCase))
            {
                results.AddRange(ParseAuthenticationResults(f.Name, f.Value, arc: false));
            }
            else if (f.Name.Equals("ARC-Authentication-Results", StringComparison.OrdinalIgnoreCase))
            {
                results.AddRange(ParseAuthenticationResults(f.Name, f.Value, arc: true));
            }
            else if (f.Name.Equals("Received-SPF", StringComparison.OrdinalIgnoreCase))
            {
                var word = f.Value.Split(new[] { ' ', '(', ';' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                if (word is not null)
                {
                    results.Add(new AuthResult(f.Name, null, "spf", word.ToLowerInvariant(), new Dictionary<string, string>()));
                }
            }
        }

        result.AuthResults = results;

        // Verdict: first (= topmost, closest to the recipient) result per method, Authentication-Results preferred.
        var verdicts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in new[] { "Authentication-Results", "Received-SPF", "ARC-Authentication-Results" })
        {
            foreach (var r in results.Where(r => r.Source.Equals(source, StringComparison.OrdinalIgnoreCase)))
            {
                if (r.Method is "spf" or "dkim" or "dmarc" or "compauth")
                {
                    verdicts.TryAdd(r.Method, r.Result);
                }
            }
        }

        result.Verdicts = verdicts;
        if (results.Count == 0)
        {
            result.Add("auth.none", Severity.Info);
            return;
        }

        foreach (var method in new[] { "spf", "dkim", "dmarc" })
        {
            if (!verdicts.TryGetValue(method, out var verdict))
            {
                result.Add("auth.missing", Severity.Info, method.ToUpperInvariant());
                continue;
            }

            var severity = verdict switch
            {
                "pass" => Severity.Ok,
                "fail" or "permerror" => Severity.Error,
                "softfail" or "temperror" or "none" or "policy" => Severity.Warning,
                _ => Severity.Info,
            };
            result.Add("auth." + (severity == Severity.Ok ? "pass" : "notpass"), severity, method.ToUpperInvariant(), verdict);
        }
    }

    internal static IEnumerable<AuthResult> ParseAuthenticationResults(string source, string value, bool arc)
    {
        var parts = RfcDate.StripComments(value).Split(';').Select(p => p.Trim()).Where(p => p.Length > 0).ToList();
        if (arc && parts.Count > 0 && parts[0].StartsWith("i=", StringComparison.OrdinalIgnoreCase))
        {
            parts.RemoveAt(0);
        }

        if (parts.Count == 0)
        {
            yield break;
        }

        string? servId = parts[0].Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
        var start = 1;
        if (parts[0].Contains('='))
        {
            // No authserv-id (non-standard); the first part already is a result.
            servId = null;
            start = 0;
        }

        for (var i = start; i < parts.Count; i++)
        {
            var tokens = parts[i].Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0 || tokens[0].Equals("none", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var eq = tokens[0].IndexOf('=');
            if (eq <= 0)
            {
                continue;
            }

            var method = tokens[0][..eq].Split('/')[0].ToLowerInvariant();
            var verdict = tokens[0][(eq + 1)..].ToLowerInvariant();
            var props = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var t in tokens.Skip(1))
            {
                var e = t.IndexOf('=');
                if (e > 0)
                {
                    props.TryAdd(t[..e], t[(e + 1)..]);
                }
            }

            yield return new AuthResult(source, servId, method, verdict, props);
        }
    }

    // ---------- spam ----------
    private static void ParseSpam(List<HeaderField> fields, HeaderAnalysis result)
    {
        var list = new List<SpamIndicator>();
        foreach (var f in fields)
        {
            if (!SpamHeaderPrefixes.Any(p => f.Name.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var severity = RateSpam(f.Name, f.Value);
            list.Add(new SpamIndicator(f.Name, f.Value, severity));
        }

        result.Spam = list;
        if (list.Any(s => s.Severity >= Severity.Warning))
        {
            result.Add("spam.flagged", Severity.Warning);
        }
        else if (list.Count > 0)
        {
            result.Add("spam.clean", Severity.Ok);
        }
    }

    internal static Severity RateSpam(string name, string value)
    {
        var v = value.Trim();
        if (name.Equals("X-Spam-Flag", StringComparison.OrdinalIgnoreCase))
        {
            return v.StartsWith("YES", StringComparison.OrdinalIgnoreCase) ? Severity.Warning : Severity.Ok;
        }

        if (name.Equals("X-Spam-Status", StringComparison.OrdinalIgnoreCase))
        {
            return v.StartsWith("Yes", StringComparison.OrdinalIgnoreCase) ? Severity.Warning : Severity.Ok;
        }

        if (name.Equals("X-MS-Exchange-Organization-SCL", StringComparison.OrdinalIgnoreCase))
        {
            return int.TryParse(v, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var scl) && scl >= 5 ? Severity.Warning : Severity.Ok;
        }

        if (name.Equals("X-Forefront-Antispam-Report", StringComparison.OrdinalIgnoreCase))
        {
            var tags = v.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(t => t.Split(':', 2))
                .Where(t => t.Length == 2)
                .GroupBy(t => t[0], StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First()[1], StringComparer.OrdinalIgnoreCase);
            var spam = tags.TryGetValue("SFV", out var sfv) && sfv is "SPM" or "SKS" or "SKB";
            var cat = tags.TryGetValue("CAT", out var c) && c.Length > 0 && !c.Equals("NONE", StringComparison.OrdinalIgnoreCase);
            var scl = tags.TryGetValue("SCL", out var s) && int.TryParse(s, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var n) && n >= 5;
            return spam || cat || scl ? Severity.Warning : Severity.Ok;
        }

        if (name.StartsWith("X-UI-", StringComparison.OrdinalIgnoreCase))
        {
            return v.StartsWith("junk", StringComparison.OrdinalIgnoreCase) ? Severity.Warning : Severity.Ok;
        }

        if (name.Equals("X-Gm-Spam", StringComparison.OrdinalIgnoreCase))
        {
            return v == "1" ? Severity.Warning : Severity.Ok;
        }

        return Severity.Info;
    }

    // ---------- anomalies ----------
    private static void CheckAnomalies(HeaderAnalysis r)
    {
        var fromAddress = ExtractAddress(r.From);
        var fromDomain = DomainOf(fromAddress);
        if (r.From is null)
        {
            r.Add("header.from.missing", Severity.Error);
        }

        if (r.MessageId is null)
        {
            r.Add("header.messageid.missing", Severity.Warning);
        }

        if (r.Date is null)
        {
            r.Add("header.date.missing", Severity.Warning);
        }

        if (fromDomain is null)
        {
            return;
        }

        var fromOrg = DomainName.OrganizationalDomain(fromDomain);

        var returnDomain = DomainOf(ExtractAddress(r.ReturnPath));
        if (returnDomain is not null && DomainName.OrganizationalDomain(returnDomain) != fromOrg)
        {
            r.Add("header.returnpath.mismatch", Severity.Warning, fromDomain, returnDomain);
        }

        var replyDomain = DomainOf(ExtractAddress(r.ReplyTo));
        if (replyDomain is not null && DomainName.OrganizationalDomain(replyDomain) != fromOrg)
        {
            r.Add("header.replyto.mismatch", Severity.Warning, fromDomain, replyDomain);
        }

        var display = DisplayName(r.From);
        var displayAddress = display is null ? null : AddressInText().Match(display);
        if (displayAddress is { Success: true } && !displayAddress.Value.Equals(fromAddress, StringComparison.OrdinalIgnoreCase))
        {
            r.Add("header.from.displayname", Severity.Warning, displayAddress.Value, fromAddress!);
        }

        var dkim = r.AuthResults.FirstOrDefault(a => a.Method == "dkim" && a.Result == "pass" && a.Properties.ContainsKey("header.d"));
        if (dkim is not null && DomainName.OrganizationalDomain(dkim.Properties["header.d"]) != fromOrg)
        {
            r.Add("header.dkim.notaligned", Severity.Info, dkim.Properties["header.d"], fromDomain);
        }

        var midDomain = r.MessageId is null ? null : DomainOf(r.MessageId.Trim('<', '>', ' '));
        if (midDomain is not null && DomainName.OrganizationalDomain(midDomain) != fromOrg)
        {
            r.Add("header.messageid.domain", Severity.Info, midDomain);
        }
    }

    [GeneratedRegex(@"[A-Za-z0-9._%+\-=]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}", RegexOptions.CultureInvariant)]
    private static partial Regex AddressInText();

    /// <summary>Address part of a mailbox header ("Name &lt;a@b&gt;" or "a@b").</summary>
    public static string? ExtractAddress(string? header)
    {
        if (string.IsNullOrWhiteSpace(header))
        {
            return null;
        }

        var lt = header.LastIndexOf('<');
        var gt = header.LastIndexOf('>');
        if (lt >= 0 && gt > lt)
        {
            var inner = header[(lt + 1)..gt].Trim();
            return inner.Contains('@') ? inner : null;
        }

        var m = AddressInText().Match(header);
        return m.Success ? m.Value : null;
    }

    private static string? DisplayName(string? header)
    {
        if (header is null)
        {
            return null;
        }

        var lt = header.LastIndexOf('<');
        return lt > 0 ? header[..lt].Trim().Trim('"') : null;
    }

    private static string? DomainOf(string? address)
    {
        if (address is null)
        {
            return null;
        }

        var at = address.LastIndexOf('@');
        if (at < 0 || at == address.Length - 1)
        {
            return null;
        }

        var domain = address[(at + 1)..].Trim().TrimEnd('>').ToLowerInvariant();
        return domain.Contains('.') ? domain : null;
    }
}
