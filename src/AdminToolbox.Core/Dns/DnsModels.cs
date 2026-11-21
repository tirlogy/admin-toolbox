// SPDX-License-Identifier: MIT
namespace AdminToolbox.Core.Dns;

/// <summary>DNS record types the toolbox understands (numeric values as on the wire).</summary>
public enum DnsRecordType
{
    A = 1,
    NS = 2,
    CNAME = 5,
    SOA = 6,
    PTR = 12,
    MX = 15,
    TXT = 16,
    AAAA = 28,
    SRV = 33,
    CAA = 257,
}

/// <summary>One resource record. TXT data is already unquoted and joined.</summary>
public sealed record DnsRecord(string Name, DnsRecordType Type, int Ttl, string Data);

/// <summary>Answer of a DNS query. Status uses RCODE numbers (0 = NOERROR, 3 = NXDOMAIN).</summary>
public sealed record DnsAnswer(int Status, IReadOnlyList<DnsRecord> Records)
{
    public const int NoError = 0;
    public const int NxDomain = 3;

    public bool IsNxDomain => Status == NxDomain;

    /// <summary>Data of all records of the given type.</summary>
    public IReadOnlyList<string> Data(DnsRecordType type) =>
        Records.Where(r => r.Type == type).Select(r => r.Data).ToList();

    /// <summary>True when the answer carries no record of the given type (NXDOMAIN or NODATA).</summary>
    public bool IsEmpty(DnsRecordType type) => !Records.Any(r => r.Type == type);
}

/// <summary>Abstraction over a DNS resolver, implemented by the DoH client.</summary>
public interface IDnsResolver
{
    Task<DnsAnswer> QueryAsync(string name, DnsRecordType type, CancellationToken cancellationToken = default);
}

/// <summary>Raised when no resolver endpoint could answer.</summary>
public sealed class DnsQueryException : Exception
{
    public DnsQueryException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
