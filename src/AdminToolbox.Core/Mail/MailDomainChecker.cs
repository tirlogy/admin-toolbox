// SPDX-License-Identifier: MIT
using System.Globalization;
using System.Net;
using AdminToolbox.Core.Dns;
using AdminToolbox.Core.Helpers;

namespace AdminToolbox.Core.Mail;

public enum CheckKind
{
    Mx,
    Spf,
    Dkim,
    Dmarc,
    Bimi,
    MtaSts,
    TlsRpt,
}

/// <summary>Result of one check (traffic light, raw records, findings).</summary>
public sealed class CheckResult : FindingList
{
    public CheckResult(CheckKind kind) => Kind = kind;

    public CheckKind Kind { get; }

    /// <summary>Raw records that were evaluated, for display.</summary>
    public List<string> Records { get; } = new();
}

/// <summary>A DKIM key found under a selector.</summary>
public sealed record DkimKey(string Selector, string Record, string KeyType, int? KeyBits, bool Revoked, bool Testing);

/// <summary>Complete result of the mail domain check.</summary>
public sealed class MailDomainReport
{
    public MailDomainReport(string domain, IReadOnlyList<CheckResult> checks, SpfAnalysis? spf, IReadOnlyList<DkimKey> dkimKeys)
    {
        Domain = domain;
        Checks = checks;
        Spf = spf;
        DkimKeys = dkimKeys;
    }

    public string Domain { get; }

    public IReadOnlyList<CheckResult> Checks { get; }

    public SpfAnalysis? Spf { get; }

    public IReadOnlyList<DkimKey> DkimKeys { get; }

    public CheckResult this[CheckKind kind] => Checks.First(c => c.Kind == kind);

    public Severity Severity => Checks.Max(c => c.Severity);
}

/// <summary>Runs MX, SPF, DKIM, DMARC, BIMI, MTA-STS and TLS-RPT checks for a domain.</summary>
public sealed class MailDomainChecker
{
    /// <summary>Selectors used by common providers. A custom selector can be added per check.</summary>
    public static readonly IReadOnlyList<string> CommonSelectors = new[]
    {
        "default", "dkim", "mail", "smtp", "s1", "s2", "k1", "k2", "k3", "selector1", "selector2", "google",
        "fm1", "fm2", "fm3", "zoho", "protonmail", "protonmail2", "protonmail3", "mxvault", "sig1", "everlytickey1",
        "s1024", "s2048",
    };

    private const int MaxMxHosts = 10;

    private readonly IDnsResolver _dns;

    public MailDomainChecker(IDnsResolver dns) => _dns = dns ?? throw new ArgumentNullException(nameof(dns));

    public async Task<MailDomainReport> CheckAsync(string input, string? customSelector = null, CancellationToken cancellationToken = default)
    {
        if (!DomainName.TryNormalize(input, out var domain))
        {
            throw new ArgumentException("Invalid domain", nameof(input));
        }

        var ct = cancellationToken;
        var mx = Guard(CheckKind.Mx, r => CheckMxAsync(domain, r, ct));
        SpfAnalysis? spfAnalysis = null;
        var spf = Guard(CheckKind.Spf, async r =>
        {
            spfAnalysis = await new SpfEvaluator(_dns).AnalyzeAsync(domain, ct).ConfigureAwait(false);
            if (spfAnalysis.Root.Record is not null)
            {
                r.Records.Add(spfAnalysis.Root.Record.Text);
            }

            r.AddRange(spfAnalysis.Findings);
        });
        var dkimKeys = new List<DkimKey>();
        var dkim = Guard(CheckKind.Dkim, r => CheckDkimAsync(domain, customSelector, r, dkimKeys, ct));
        DmarcState? dmarcState = null;
        var dmarc = Guard(CheckKind.Dmarc, async r => dmarcState = await CheckDmarcAsync(domain, r, ct).ConfigureAwait(false));
        var mtaSts = Guard(CheckKind.MtaSts, r => CheckMtaStsAsync(domain, r, ct));
        var bimiTxt = SafeTxtAsync("default._bimi." + domain, ct);

        await Task.WhenAll(mx, spf, dkim, dmarc, mtaSts).ConfigureAwait(false);

        var stsPresent = mtaSts.Result.Records.Count > 0;
        var tlsRpt = await Guard(CheckKind.TlsRpt, r => CheckTlsRptAsync(domain, stsPresent, r, ct)).ConfigureAwait(false);
        var bimi = await Guard(CheckKind.Bimi, async r => CheckBimi(await bimiTxt.ConfigureAwait(false), dmarcState, r)).ConfigureAwait(false);

        var checks = new[] { mx.Result, spf.Result, dkim.Result, dmarc.Result, bimi, mtaSts.Result, tlsRpt };
        return new MailDomainReport(domain, checks, spfAnalysis, dkimKeys.OrderBy(k => k.Selector, StringComparer.Ordinal).ToList());
    }

    private static async Task<CheckResult> Guard(CheckKind kind, Func<CheckResult, Task> check)
    {
        var result = new CheckResult(kind);
        try
        {
            await check(result).ConfigureAwait(false);
        }
        catch (DnsQueryException ex)
        {
            result.Add("dns.error", Severity.Error, ex.Message);
        }

        return result;
    }

    private async Task<IReadOnlyList<string>?> SafeTxtAsync(string name, CancellationToken ct)
    {
        try
        {
            return (await _dns.QueryAsync(name, DnsRecordType.TXT, ct).ConfigureAwait(false)).Data(DnsRecordType.TXT);
        }
        catch (DnsQueryException)
        {
            return null;
        }
    }

    private async Task<IReadOnlyList<string>> TxtAsync(string name, CancellationToken ct) =>
        (await _dns.QueryAsync(name, DnsRecordType.TXT, ct).ConfigureAwait(false)).Data(DnsRecordType.TXT);

    // ---------- MX ----------
    private async Task CheckMxAsync(string domain, CheckResult r, CancellationToken ct)
    {
        var answer = await _dns.QueryAsync(domain, DnsRecordType.MX, ct).ConfigureAwait(false);
        if (answer.IsNxDomain)
        {
            r.Add("mx.nxdomain", Severity.Error, domain);
            return;
        }

        var hosts = new List<(int Priority, string Host)>();
        foreach (var data in answer.Data(DnsRecordType.MX))
        {
            var parts = data.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2 && int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var prio))
            {
                hosts.Add((prio, DnsText.NormalizeName(parts[1])));
            }
        }

        hosts.Sort((a, b) => a.Priority != b.Priority ? a.Priority.CompareTo(b.Priority) : string.CompareOrdinal(a.Host, b.Host));
        r.Records.AddRange(hosts.Select(h => h.Priority.ToString(CultureInfo.InvariantCulture) + " " + (h.Host.Length == 0 ? "." : h.Host)));

        if (hosts.Count == 1 && hosts[0].Host.Length == 0)
        {
            r.Add("mx.null", Severity.Info);
            return;
        }

        if (hosts.Count == 0)
        {
            var a = await _dns.QueryAsync(domain, DnsRecordType.A, ct).ConfigureAwait(false);
            if (a.IsEmpty(DnsRecordType.A))
            {
                r.Add("mx.none", Severity.Error);
            }
            else
            {
                r.Add("mx.implicit", Severity.Warning);
            }

            return;
        }

        var problems = 0;
        foreach (var (_, host) in hosts.Take(MaxMxHosts))
        {
            if (IPAddress.TryParse(host, out _))
            {
                r.Add("mx.ip", Severity.Error, host);
                problems++;
                continue;
            }

            var a = await _dns.QueryAsync(host, DnsRecordType.A, ct).ConfigureAwait(false);
            if (a.Records.Any(x => x.Type == DnsRecordType.CNAME))
            {
                r.Add("mx.cname", Severity.Warning, host);
                problems++;
            }

            if (a.IsEmpty(DnsRecordType.A))
            {
                var aaaa = await _dns.QueryAsync(host, DnsRecordType.AAAA, ct).ConfigureAwait(false);
                if (aaaa.IsEmpty(DnsRecordType.AAAA))
                {
                    r.Add("mx.unresolvable", Severity.Error, host);
                    problems++;
                }
            }
        }

        if (problems == 0)
        {
            r.Add(hosts.Count == 1 ? "mx.single" : "mx.ok", hosts.Count == 1 ? Severity.Info : Severity.Ok, hosts.Count.ToString(CultureInfo.InvariantCulture));
        }
    }

    // ---------- DKIM ----------
    private async Task CheckDkimAsync(string domain, string? customSelector, CheckResult r, List<DkimKey> keys, CancellationToken ct)
    {
        var selectors = CommonSelectors.ToList();
        string? custom = null;
        if (!string.IsNullOrWhiteSpace(customSelector))
        {
            custom = customSelector.Trim().ToLowerInvariant();
            if (!custom.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.') || custom.Length > 63)
            {
                r.Add("dkim.selector.invalid", Severity.Error, customSelector.Trim());
                custom = null;
            }
            else if (!selectors.Contains(custom))
            {
                selectors.Insert(0, custom);
            }
        }

        var lookups = selectors.Select(async s => (Selector: s, Txt: await TxtAsync(s + "._domainkey." + domain, ct).ConfigureAwait(false))).ToList();
        var results = await Task.WhenAll(lookups).ConfigureAwait(false);
        foreach (var (selector, txt) in results)
        {
            var record = txt.FirstOrDefault(t => t.Contains("p=", StringComparison.Ordinal));
            if (record is null)
            {
                continue;
            }

            var key = ParseDkim(selector, record, r);
            keys.Add(key);
            r.Records.Add(selector + ": " + record);
        }

        if (keys.Count == 0)
        {
            if (custom is not null)
            {
                r.Add("dkim.selector.notfound", Severity.Warning, custom);
            }

            r.Add("dkim.notfound", Severity.Warning, selectors.Count.ToString(CultureInfo.InvariantCulture));
        }
        else if (custom is not null && !keys.Any(k => k.Selector == custom))
        {
            r.Add("dkim.selector.notfound", Severity.Warning, custom);
        }
    }

    internal static DkimKey ParseDkim(string selector, string record, CheckResult r)
    {
        var tags = TagList.Parse(record);
        var keyType = (tags["k"] ?? "rsa").ToLowerInvariant();
        var p = new string((tags["p"] ?? string.Empty).Where(c => !char.IsWhiteSpace(c)).ToArray());
        var testing = (tags["t"] ?? string.Empty).Split(':').Any(f => f.Trim() == "y");
        if (tags["v"] is { } v && v != "DKIM1")
        {
            r.Add("dkim.version", Severity.Error, selector);
        }

        if (testing)
        {
            r.Add("dkim.testing", Severity.Warning, selector);
        }

        if (p.Length == 0)
        {
            r.Add("dkim.revoked", Severity.Info, selector);
            return new DkimKey(selector, record, keyType, null, true, testing);
        }

        int? bits = null;
        try
        {
            var bytes = Convert.FromBase64String(p);
            bits = keyType == "ed25519" ? (bytes.Length == 32 ? 256 : null) : PublicKeyInfo.Describe(bytes).Bits;
        }
        catch (Exception ex) when (ex is FormatException or System.Formats.Asn1.AsnContentException or IndexOutOfRangeException)
        {
            r.Add("dkim.invalidkey", Severity.Error, selector);
            return new DkimKey(selector, record, keyType, null, false, testing);
        }

        var b = bits?.ToString(CultureInfo.InvariantCulture) ?? "?";
        if (keyType == "rsa" && bits is < 1024)
        {
            r.Add("dkim.weak", Severity.Error, selector, b);
        }
        else if (keyType == "rsa" && bits is < 2048)
        {
            r.Add("dkim.1024", Severity.Warning, selector, b);
        }
        else
        {
            r.Add("dkim.found", Severity.Ok, selector, keyType.ToUpperInvariant(), b);
        }

        return new DkimKey(selector, record, keyType, bits, false, testing);
    }

    // ---------- DMARC ----------
    private sealed record DmarcState(string? Policy, int Pct);

    private async Task<DmarcState?> CheckDmarcAsync(string domain, CheckResult r, CancellationToken ct)
    {
        var records = (await TxtAsync("_dmarc." + domain, ct).ConfigureAwait(false))
            .Where(t => t.StartsWith("v=DMARC1", StringComparison.OrdinalIgnoreCase)).ToList();
        var inherited = false;
        var org = DomainName.OrganizationalDomain(domain);
        if (records.Count == 0 && org != domain)
        {
            records = (await TxtAsync("_dmarc." + org, ct).ConfigureAwait(false))
                .Where(t => t.StartsWith("v=DMARC1", StringComparison.OrdinalIgnoreCase)).ToList();
            inherited = records.Count > 0;
        }

        if (records.Count == 0)
        {
            r.Add("dmarc.none", Severity.Error);
            return null;
        }

        r.Records.AddRange(records);
        if (records.Count > 1)
        {
            r.Add("dmarc.multiple", Severity.Error, records.Count.ToString(CultureInfo.InvariantCulture));
            return null;
        }

        if (inherited)
        {
            r.Add("dmarc.inherited", Severity.Info, org);
        }

        return EvaluateDmarc(records[0], inherited, r);
    }

    private static DmarcState? EvaluateDmarc(string record, bool inherited, CheckResult r)
    {
        var tags = TagList.Parse(record);
        foreach (var bad in tags.Invalid.Concat(tags.Duplicates))
        {
            r.Add("dmarc.syntax", Severity.Error, bad);
        }

        var policy = (inherited ? tags["sp"] ?? tags["p"] : tags["p"])?.ToLowerInvariant();
        switch (policy)
        {
            case "none": r.Add("dmarc.p.none", Severity.Warning); break;
            case "quarantine": r.Add("dmarc.p.quarantine", Severity.Ok); break;
            case "reject": r.Add("dmarc.p.reject", Severity.Ok); break;
            default:
                r.Add("dmarc.p.invalid", Severity.Error, policy ?? string.Empty);
                policy = null;
                break;
        }

        var sp = tags["sp"]?.ToLowerInvariant();
        if (!inherited && sp == "none" && policy is "quarantine" or "reject")
        {
            r.Add("dmarc.sp.none", Severity.Warning);
        }
        else if (sp is not null and not ("none" or "quarantine" or "reject"))
        {
            r.Add("dmarc.syntax", Severity.Error, "sp=" + sp);
        }

        var pct = 100;
        if (tags["pct"] is { } pctText)
        {
            if (!int.TryParse(pctText, NumberStyles.None, CultureInfo.InvariantCulture, out pct) || pct > 100)
            {
                r.Add("dmarc.syntax", Severity.Error, "pct=" + pctText);
                pct = 100;
            }
            else if (pct < 100)
            {
                r.Add("dmarc.pct", Severity.Warning, pct.ToString(CultureInfo.InvariantCulture));
            }
        }

        var rua = tags["rua"];
        if (string.IsNullOrWhiteSpace(rua))
        {
            r.Add("dmarc.rua.missing", Severity.Warning);
        }
        else
        {
            var uris = rua.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var invalid = uris.Where(u => !u.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) || !u.Contains('@')).ToList();
            if (invalid.Count > 0)
            {
                r.Add("dmarc.rua.invalid", Severity.Error, string.Join(", ", invalid));
            }
            else
            {
                r.Add("dmarc.rua.ok", Severity.Ok, string.Join(", ", uris.Select(u => u[7..])));
            }
        }

        if (!string.IsNullOrWhiteSpace(tags["ruf"]))
        {
            r.Add("dmarc.ruf", Severity.Info);
        }

        foreach (var align in new[] { "adkim", "aspf" })
        {
            var value = tags[align]?.ToLowerInvariant();
            if (value is null or "r")
            {
                continue;
            }

            if (value == "s")
            {
                r.Add("dmarc.strict", Severity.Info, align);
            }
            else
            {
                r.Add("dmarc.syntax", Severity.Error, align + "=" + value);
            }
        }

        return new DmarcState(policy, pct);
    }

    // ---------- BIMI ----------
    private static void CheckBimi(IReadOnlyList<string>? txt, DmarcState? dmarc, CheckResult r)
    {
        if (txt is null)
        {
            r.Add("dns.error", Severity.Error, "BIMI");
            return;
        }

        var records = txt.Where(t => t.StartsWith("v=BIMI1", StringComparison.OrdinalIgnoreCase)).ToList();
        if (records.Count == 0)
        {
            r.Add("bimi.none", Severity.Info);
            return;
        }

        r.Records.AddRange(records);
        var tags = TagList.Parse(records[0]);
        var logo = tags["l"];
        if (string.IsNullOrWhiteSpace(logo) || !logo.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || !logo.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
        {
            r.Add("bimi.logo.invalid", Severity.Warning, logo ?? string.Empty);
        }

        if (string.IsNullOrWhiteSpace(tags["a"]))
        {
            r.Add("bimi.novmc", Severity.Info);
        }
        else
        {
            r.Add("bimi.vmc", Severity.Ok);
        }

        if (dmarc?.Policy is not ("quarantine" or "reject") || dmarc.Pct < 100)
        {
            r.Add("bimi.dmarc", Severity.Warning);
        }
        else if (!r.Findings.Any(f => f.Severity >= Severity.Warning))
        {
            r.Add("bimi.ok", Severity.Ok);
        }
    }

    // ---------- MTA-STS ----------
    private async Task CheckMtaStsAsync(string domain, CheckResult r, CancellationToken ct)
    {
        var records = (await TxtAsync("_mta-sts." + domain, ct).ConfigureAwait(false))
            .Where(t => t.StartsWith("v=STSv1", StringComparison.OrdinalIgnoreCase)).ToList();
        if (records.Count == 0)
        {
            r.Add("mtasts.none", Severity.Info);
            return;
        }

        r.Records.AddRange(records);
        if (records.Count > 1)
        {
            r.Add("mtasts.multiple", Severity.Error);
        }

        var tags = TagList.Parse(records[0]);
        var id = tags["id"];
        if (string.IsNullOrEmpty(id) || id.Length > 32 || !id.All(char.IsAsciiLetterOrDigit))
        {
            r.Add("mtasts.id.invalid", Severity.Error, id ?? string.Empty);
        }

        var host = "mta-sts." + domain;
        var a = await _dns.QueryAsync(host, DnsRecordType.A, ct).ConfigureAwait(false);
        var resolvable = !a.IsEmpty(DnsRecordType.A);
        if (!resolvable)
        {
            var aaaa = await _dns.QueryAsync(host, DnsRecordType.AAAA, ct).ConfigureAwait(false);
            resolvable = !aaaa.IsEmpty(DnsRecordType.AAAA);
        }

        if (!resolvable)
        {
            r.Add("mtasts.host.missing", Severity.Error, host);
            return;
        }

        r.Add("mtasts.found", Severity.Ok);
        r.Add("mtasts.policy.manual", Severity.Info, "https://" + host + "/.well-known/mta-sts.txt");
    }

    // ---------- TLS-RPT ----------
    private async Task CheckTlsRptAsync(string domain, bool mtaStsPresent, CheckResult r, CancellationToken ct)
    {
        var records = (await TxtAsync("_smtp._tls." + domain, ct).ConfigureAwait(false))
            .Where(t => t.StartsWith("v=TLSRPTv1", StringComparison.OrdinalIgnoreCase)).ToList();
        if (records.Count == 0)
        {
            r.Add(mtaStsPresent ? "tlsrpt.none.sts" : "tlsrpt.none", mtaStsPresent ? Severity.Warning : Severity.Info);
            return;
        }

        r.Records.AddRange(records);
        if (records.Count > 1)
        {
            r.Add("tlsrpt.multiple", Severity.Error);
        }

        var rua = TagList.Parse(records[0])["rua"];
        var uris = (rua ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (uris.Length == 0 || uris.Any(u => !u.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) && !u.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
        {
            r.Add("tlsrpt.rua.invalid", Severity.Error, rua ?? string.Empty);
            return;
        }

        r.Add("tlsrpt.ok", Severity.Ok, string.Join(", ", uris));
    }
}
