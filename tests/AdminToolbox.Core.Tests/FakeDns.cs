// SPDX-License-Identifier: MIT
using AdminToolbox.Core.Dns;

namespace AdminToolbox.Core.Tests;

/// <summary>In-memory resolver. Unknown names answer NXDOMAIN.</summary>
internal sealed class FakeDns : IDnsResolver
{
    private readonly Dictionary<(string, DnsRecordType), List<DnsRecord>> _records = new();
    private readonly HashSet<string> _existing = new(StringComparer.Ordinal);
    private readonly HashSet<string> _failing = new(StringComparer.Ordinal);

    public int Queries { get; private set; }

    public FakeDns Add(string name, DnsRecordType type, params string[] data)
    {
        name = DnsText.NormalizeName(name);
        _existing.Add(name);
        if (!_records.TryGetValue((name, type), out var list))
        {
            list = new List<DnsRecord>();
            _records[(name, type)] = list;
        }

        list.AddRange(data.Select(d => new DnsRecord(name, type, 300, d)));
        return this;
    }

    public FakeDns Txt(string name, params string[] data) => Add(name, DnsRecordType.TXT, data);

    public FakeDns Fail(string name)
    {
        _failing.Add(DnsText.NormalizeName(name));
        return this;
    }

    public Task<DnsAnswer> QueryAsync(string name, DnsRecordType type, CancellationToken cancellationToken = default)
    {
        Queries++;
        name = DnsText.NormalizeName(name);
        if (_failing.Contains(name))
        {
            throw new DnsQueryException("simulated failure");
        }

        var records = new List<DnsRecord>();
        if (_records.TryGetValue((name, DnsRecordType.CNAME), out var cname) && type != DnsRecordType.CNAME)
        {
            records.AddRange(cname);
            var target = DnsText.NormalizeName(cname[0].Data);
            if (_records.TryGetValue((target, type), out var targetRecords))
            {
                records.AddRange(targetRecords);
            }
        }
        else if (_records.TryGetValue((name, type), out var list))
        {
            records.AddRange(list);
        }

        var status = records.Count == 0 && !_existing.Contains(name) ? DnsAnswer.NxDomain : DnsAnswer.NoError;
        return Task.FromResult(new DnsAnswer(status, records));
    }
}
