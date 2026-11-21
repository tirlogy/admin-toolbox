// SPDX-License-Identifier: MIT
using System.Globalization;
using AdminToolbox.Core.Dns;

namespace AdminToolbox.Core.Mail;

/// <summary>One SPF record in the include/redirect tree.</summary>
public sealed class SpfNode
{
    public SpfNode(string domain, SpfTermKind? via)
    {
        Domain = domain;
        Via = via;
    }

    public string Domain { get; }

    /// <summary>Include or Redirect for child nodes, null for the root.</summary>
    public SpfTermKind? Via { get; }

    public SpfRecord? Record { get; internal set; }

    /// <summary>Error code when this node could not be evaluated (e.g. spf.include.missing).</summary>
    public string? Error { get; internal set; }

    /// <summary>DNS lookups caused by the terms of this record itself.</summary>
    public int OwnLookups { get; internal set; }

    public List<SpfNode> Children { get; } = new();
}

/// <summary>Result of the SPF analysis of a domain.</summary>
public sealed class SpfAnalysis : FindingList
{
    public SpfAnalysis(SpfNode root) => Root = root;

    public SpfNode Root { get; }

    /// <summary>Total DNS-querying terms in the whole tree (limit 10).</summary>
    public int LookupCount { get; internal set; }

    /// <summary>Lookups that returned no data (limit 2).</summary>
    public int VoidLookups { get; internal set; }
}

/// <summary>Resolves an SPF record including all includes and redirects and counts DNS lookups.</summary>
public sealed class SpfEvaluator
{
    public const int LookupLimit = 10;
    public const int VoidLookupLimit = 2;

    // Hard stop to keep broken or hostile records from causing hundreds of queries.
    private const int MaxNodes = 40;

    private readonly IDnsResolver _dns;

    public SpfEvaluator(IDnsResolver dns) => _dns = dns ?? throw new ArgumentNullException(nameof(dns));

    public async Task<SpfAnalysis> AnalyzeAsync(string domain, CancellationToken cancellationToken = default)
    {
        var root = new SpfNode(DnsText.NormalizeName(domain), null);
        var analysis = new SpfAnalysis(root);
        var state = new State();
        await EvaluateAsync(root, analysis, state, new HashSet<string>(StringComparer.Ordinal), cancellationToken).ConfigureAwait(false);
        Summarize(analysis, state);
        return analysis;
    }

    private async Task EvaluateAsync(SpfNode node, SpfAnalysis analysis, State state, HashSet<string> path, CancellationToken ct)
    {
        state.Nodes++;
        if (!path.Add(node.Domain))
        {
            node.Error = "spf.loop";
            state.Errors.Add(new Finding("spf.loop", Severity.Error, node.Domain));
            return;
        }

        try
        {
            var answer = await _dns.QueryAsync(node.Domain, DnsRecordType.TXT, ct).ConfigureAwait(false);
            var records = answer.Data(DnsRecordType.TXT).Where(SpfRecord.IsSpf).ToList();
            if (records.Count == 0)
            {
                if (node.Via is not null)
                {
                    analysis.VoidLookups += answer.IsEmpty(DnsRecordType.TXT) ? 1 : 0;
                    node.Error = "spf.include.missing";
                    state.Errors.Add(new Finding("spf.include.missing", Severity.Error, node.Domain));
                }

                return;
            }

            if (records.Count > 1)
            {
                node.Error = "spf.multiple";
                state.Errors.Add(new Finding("spf.multiple", Severity.Error, node.Domain, records.Count.ToString(CultureInfo.InvariantCulture)));
            }

            var record = SpfRecord.Parse(records[0]);
            node.Record = record;
            foreach (var bad in record.Errors)
            {
                state.Errors.Add(new Finding("spf.syntax", Severity.Error, node.Domain, bad));
            }

            node.OwnLookups = record.Terms.Count(t => t.CountsAsLookup);
            analysis.LookupCount += node.OwnLookups;

            var hasAll = record.Terms.Any(t => t.Kind == SpfTermKind.All);
            foreach (var term in record.Terms)
            {
                ct.ThrowIfCancellationRequested();
                switch (term.Kind)
                {
                    case SpfTermKind.Include:
                    case SpfTermKind.Redirect when !hasAll:
                        await FollowAsync(node, term, analysis, state, path, ct).ConfigureAwait(false);
                        break;
                    case SpfTermKind.A:
                    case SpfTermKind.Mx:
                        await CheckVoidAsync(node, term, analysis, ct).ConfigureAwait(false);
                        break;
                    case SpfTermKind.Ptr:
                        state.UsesPtr = true;
                        break;
                    case SpfTermKind.Exists when term.Value!.Contains('%'):
                        state.UsesMacros = true;
                        break;
                }
            }
        }
        finally
        {
            path.Remove(node.Domain);
        }
    }

    private async Task FollowAsync(SpfNode parent, SpfTerm term, SpfAnalysis analysis, State state, HashSet<string> path, CancellationToken ct)
    {
        var target = term.TargetDomain!;
        if (target.Contains('%'))
        {
            state.UsesMacros = true;
            return;
        }

        var child = new SpfNode(target, term.Kind);
        parent.Children.Add(child);
        if (state.Nodes >= MaxNodes)
        {
            child.Error = "spf.toodeep";
            if (!state.Errors.Any(f => f.Code == "spf.toodeep"))
            {
                state.Errors.Add(new Finding("spf.toodeep", Severity.Error, MaxNodes.ToString(CultureInfo.InvariantCulture)));
            }

            return;
        }

        await EvaluateAsync(child, analysis, state, path, ct).ConfigureAwait(false);
    }

    private async Task CheckVoidAsync(SpfNode node, SpfTerm term, SpfAnalysis analysis, CancellationToken ct)
    {
        var target = term.TargetDomain ?? node.Domain;
        if (target.Contains('%'))
        {
            return;
        }

        if (term.Kind == SpfTermKind.Mx)
        {
            var mx = await _dns.QueryAsync(target, DnsRecordType.MX, ct).ConfigureAwait(false);
            analysis.VoidLookups += mx.IsEmpty(DnsRecordType.MX) ? 1 : 0;
            return;
        }

        var a = await _dns.QueryAsync(target, DnsRecordType.A, ct).ConfigureAwait(false);
        if (a.IsEmpty(DnsRecordType.A))
        {
            var aaaa = await _dns.QueryAsync(target, DnsRecordType.AAAA, ct).ConfigureAwait(false);
            analysis.VoidLookups += aaaa.IsEmpty(DnsRecordType.AAAA) ? 1 : 0;
        }
    }

    private static void Summarize(SpfAnalysis analysis, State state)
    {
        var record = analysis.Root.Record;
        if (record is null)
        {
            if (analysis.Root.Error is null)
            {
                analysis.Add("spf.none", Severity.Error);
            }

            analysis.AddRange(state.Errors);
            return;
        }

        analysis.AddRange(state.Errors);

        var count = analysis.LookupCount.ToString(CultureInfo.InvariantCulture);
        if (analysis.LookupCount > LookupLimit)
        {
            analysis.Add("spf.lookups.exceeded", Severity.Error, count);
        }
        else if (analysis.LookupCount >= LookupLimit - 1)
        {
            analysis.Add("spf.lookups.near", Severity.Warning, count);
        }
        else
        {
            analysis.Add("spf.lookups.ok", Severity.Ok, count);
        }

        if (analysis.VoidLookups > VoidLookupLimit)
        {
            analysis.Add("spf.voidlookups", Severity.Error, analysis.VoidLookups.ToString(CultureInfo.InvariantCulture));
        }

        var terms = record.Terms;
        var allIndex = -1;
        for (var i = 0; i < terms.Count; i++)
        {
            if (terms[i].Kind == SpfTermKind.All)
            {
                allIndex = i;
                break;
            }
        }

        if (allIndex >= 0)
        {
            switch (terms[allIndex].Qualifier)
            {
                case SpfQualifier.Pass: analysis.Add("spf.all.pass", Severity.Error); break;
                case SpfQualifier.Neutral: analysis.Add("spf.all.neutral", Severity.Warning); break;
                case SpfQualifier.SoftFail: analysis.Add("spf.all.softfail", Severity.Ok); break;
                case SpfQualifier.Fail: analysis.Add("spf.all.fail", Severity.Ok); break;
            }

            if (terms.Skip(allIndex + 1).Any(t => t.Kind is not (SpfTermKind.Exp or SpfTermKind.UnknownModifier or SpfTermKind.Redirect)))
            {
                analysis.Add("spf.all.notlast", Severity.Warning);
            }

            if (terms.Any(t => t.Kind == SpfTermKind.Redirect))
            {
                analysis.Add("spf.redirect.ignored", Severity.Info);
            }
        }
        else if (!terms.Any(t => t.Kind == SpfTermKind.Redirect))
        {
            analysis.Add("spf.all.missing", Severity.Warning);
        }

        if (state.UsesPtr)
        {
            analysis.Add("spf.ptr", Severity.Warning);
        }

        if (state.UsesMacros)
        {
            analysis.Add("spf.macro", Severity.Info);
        }
    }

    private sealed class State
    {
        public int Nodes { get; set; }

        public bool UsesPtr { get; set; }

        public bool UsesMacros { get; set; }

        public List<Finding> Errors { get; } = new();
    }
}
