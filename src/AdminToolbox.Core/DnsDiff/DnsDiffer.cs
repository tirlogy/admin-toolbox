// SPDX-License-Identifier: MIT
using AdminToolbox.Core.Dns;

namespace AdminToolbox.Core.DnsDiff;

public enum DiffStatus
{
    Unchanged,
    Missing,
    Added,
    Changed,
    Error,
}

/// <summary>Comparison of one name/type pair between the "before" list and the live DNS.</summary>
public sealed record DiffGroup(
    string Name,
    DnsRecordType Type,
    IReadOnlyList<string> Before,
    IReadOnlyList<string> After,
    IReadOnlyList<string> Missing,
    IReadOnlyList<string> Added,
    DiffStatus Status,
    string? Error = null);

/// <summary>Compares records from a zone file or list with live DNS answers.</summary>
public sealed class DnsDiffer
{
    /// <summary>SOA serials change on every edit, so SOA is skipped.</summary>
    public static readonly IReadOnlySet<DnsRecordType> SkippedTypes = new HashSet<DnsRecordType> { DnsRecordType.SOA };

    private const int MaxParallel = 6;

    private readonly IDnsResolver _dns;

    public DnsDiffer(IDnsResolver dns) => _dns = dns ?? throw new ArgumentNullException(nameof(dns));

    public async Task<IReadOnlyList<DiffGroup>> CompareAsync(IEnumerable<ZoneRecord> before, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(before);
        var groups = before
            .Where(r => !SkippedTypes.Contains(r.Type))
            .GroupBy(r => (r.Name, r.Type))
            .OrderBy(g => g.Key.Name, StringComparer.Ordinal)
            .ThenBy(g => g.Key.Type)
            .ToList();

        using var gate = new SemaphoreSlim(MaxParallel);
        var tasks = groups.Select(async g =>
        {
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var answer = await _dns.QueryAsync(g.Key.Name, g.Key.Type, cancellationToken).ConfigureAwait(false);
                var after = answer.Records
                    .Where(r => r.Type == g.Key.Type && DnsText.NormalizeName(r.Name) == g.Key.Name)
                    .Select(r => NormalizeLive(r))
                    .ToList();
                return Compare(g.Key.Name, g.Key.Type, g.Select(r => r.Value), after);
            }
            catch (DnsQueryException ex)
            {
                var values = g.Select(r => r.Value).Distinct().ToList();
                return new DiffGroup(g.Key.Name, g.Key.Type, values, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), DiffStatus.Error, ex.Message);
            }
            finally
            {
                gate.Release();
            }
        });
        return await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    private static string NormalizeLive(DnsRecord r)
    {
        if (r.Type == DnsRecordType.TXT)
        {
            return r.Data; // already unquoted by the resolver
        }

        try
        {
            return RecordValue.Normalize(r.Type, DnsText.Tokenize(r.Data), string.Empty);
        }
        catch (FormatException)
        {
            return r.Data;
        }
    }

    /// <summary>Set comparison of before/after values of one name/type pair.</summary>
    public static DiffGroup Compare(string name, DnsRecordType type, IEnumerable<string> before, IEnumerable<string> after)
    {
        var b = before.Distinct(StringComparer.Ordinal).OrderBy(v => v, StringComparer.Ordinal).ToList();
        var a = after.Distinct(StringComparer.Ordinal).OrderBy(v => v, StringComparer.Ordinal).ToList();
        var missing = b.Except(a, StringComparer.Ordinal).ToList();
        var added = a.Except(b, StringComparer.Ordinal).ToList();
        DiffStatus status;
        if (missing.Count == 0 && added.Count == 0)
        {
            status = DiffStatus.Unchanged;
        }
        else if (added.Count == 0)
        {
            status = DiffStatus.Missing;
        }
        else if (missing.Count == 0)
        {
            status = DiffStatus.Added;
        }
        else
        {
            status = DiffStatus.Changed;
        }

        return new DiffGroup(name, type, b, a, missing, added, status);
    }
}
