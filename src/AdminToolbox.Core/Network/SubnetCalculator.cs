// SPDX-License-Identifier: MIT
using System.Globalization;
using System.Numerics;
using System.Text;

namespace AdminToolbox.Core.Network;

/// <summary>Details of a network for display.</summary>
public sealed record SubnetInfo(
    IpNetwork Network,
    string Input,
    string NetworkAddress,
    string? Broadcast,
    string FirstHost,
    string LastHost,
    BigInteger TotalAddresses,
    BigInteger UsableHosts,
    string? Netmask,
    string? Wildcard,
    string AddressType,
    string ReverseZone,
    string? Binary);

/// <summary>Subnet math for IPv4 and IPv6.</summary>
public static class SubnetCalculator
{
    /// <summary>Upper bound for listed subnets when splitting.</summary>
    public const int MaxListedSubnets = 1024;

    public static SubnetInfo Describe(IpNetwork n)
    {
        ArgumentNullException.ThrowIfNull(n);
        var first = n.First;
        var last = n.Last;
        BigInteger usable;
        UInt128 firstHost, lastHost;
        string? broadcast = null;
        if (n.Family == IpFamily.V4 && n.Prefix <= 30)
        {
            usable = n.Count - 2;
            firstHost = first + 1;
            lastHost = last - 1;
            broadcast = IpNetwork.Format(n.Family, last);
        }
        else
        {
            // /31 and /32 (RFC 3021) and all IPv6 networks: every address is usable.
            usable = n.Count;
            firstHost = first;
            lastHost = last;
        }

        return new SubnetInfo(
            n,
            IpNetwork.Format(n.Family, n.Address),
            IpNetwork.Format(n.Family, first),
            broadcast,
            IpNetwork.Format(n.Family, firstHost),
            IpNetwork.Format(n.Family, lastHost),
            n.Count,
            usable,
            n.Family == IpFamily.V4 ? IpNetwork.Format(n.Family, n.Mask) : null,
            n.Family == IpFamily.V4 ? IpNetwork.Format(n.Family, n.HostMask) : null,
            AddressType(n.Family, n.Address),
            ReverseZone(n),
            n.Family == IpFamily.V4 ? Binary((uint)n.Address, n.Prefix) : null);
    }

    /// <summary>Classifies an address (private, public, loopback, ...). Returns a stable code.</summary>
    public static string AddressType(IpFamily family, UInt128 address)
    {
        (string Cidr, string Type)[] table = family == IpFamily.V4
            ? new[]
            {
                ("255.255.255.255/32", "broadcast"), ("0.0.0.0/8", "thisnetwork"), ("10.0.0.0/8", "private"),
                ("100.64.0.0/10", "cgnat"), ("127.0.0.0/8", "loopback"), ("169.254.0.0/16", "linklocal"),
                ("172.16.0.0/12", "private"), ("192.0.2.0/24", "documentation"), ("192.168.0.0/16", "private"),
                ("198.18.0.0/15", "benchmark"), ("198.51.100.0/24", "documentation"), ("203.0.113.0/24", "documentation"),
                ("224.0.0.0/4", "multicast"), ("240.0.0.0/4", "reserved"),
            }
            : new[]
            {
                ("::/128", "unspecified"), ("::1/128", "loopback"), ("::ffff:0:0/96", "v4mapped"), ("64:ff9b::/96", "nat64"),
                ("2001:db8::/32", "documentation"), ("fc00::/7", "ula"), ("fe80::/10", "linklocal"), ("ff00::/8", "multicast"),
                ("2000::/3", "public"),
            };
        foreach (var (cidr, type) in table)
        {
            IpNetwork.TryParse(cidr, out var net);
            if (net.Contains(address))
            {
                return type;
            }
        }

        return family == IpFamily.V4 ? "public" : "reserved";
    }

    public static string ReverseZone(IpNetwork n)
    {
        ArgumentNullException.ThrowIfNull(n);
        var sb = new StringBuilder();
        if (n.Family == IpFamily.V4)
        {
            var octets = n.Prefix / 8;
            var v = (uint)n.First;
            for (var i = octets - 1; i >= 0; i--)
            {
                sb.Append((v >> (24 - (8 * i))) & 255).Append('.');
            }

            return sb.Append("in-addr.arpa").ToString();
        }

        var nibbles = n.Prefix / 4;
        var value = n.First;
        for (var i = nibbles - 1; i >= 0; i--)
        {
            var nibble = (int)((value >> (124 - (4 * i))) & 0xF);
            sb.Append(nibble.ToString("x", CultureInfo.InvariantCulture)).Append('.');
        }

        return sb.Append("ip6.arpa").ToString();
    }

    private static string Binary(uint v, int prefix)
    {
        var sb = new StringBuilder(36);
        for (var i = 31; i >= 0; i--)
        {
            sb.Append(((v >> i) & 1) == 1 ? '1' : '0');
            if (i % 8 == 0 && i > 0)
            {
                sb.Append('.');
            }
        }

        // mark the network/host boundary
        var pos = prefix + (prefix / 8) - (prefix % 8 == 0 && prefix > 0 ? 1 : 0);
        if (prefix is > 0 and < 32)
        {
            sb.Insert(pos, '|');
        }

        return sb.ToString();
    }

    /// <summary>Splits a network into subnets with the new prefix. Lists at most <paramref name="max"/> entries.</summary>
    public static IReadOnlyList<IpNetwork> Split(IpNetwork n, int newPrefix, out BigInteger total, int max = MaxListedSubnets)
    {
        ArgumentNullException.ThrowIfNull(n);
        if (newPrefix < n.Prefix || newPrefix > n.MaxBits)
        {
            throw new ArgumentOutOfRangeException(nameof(newPrefix));
        }

        total = BigInteger.One << (newPrefix - n.Prefix);
        var list = new List<IpNetwork>();
        var step = newPrefix == 0 ? UInt128.Zero : UInt128.One << (n.MaxBits - newPrefix);
        var current = n.First;
        for (var i = 0; i < max && i < total; i++)
        {
            list.Add(new IpNetwork(n.Family, current, newPrefix));
            current += step;
        }

        return list;
    }

    /// <summary>Minimal list of CIDR blocks covering exactly the union of the inputs (route summarization).</summary>
    public static IReadOnlyList<IpNetwork> Summarize(IEnumerable<IpNetwork> networks)
    {
        ArgumentNullException.ThrowIfNull(networks);
        var result = new List<IpNetwork>();
        foreach (var group in networks.GroupBy(n => n.Family).OrderBy(g => g.Key))
        {
            var family = group.Key;
            var maxBits = family == IpFamily.V4 ? 32 : 128;
            var allOnes = family == IpFamily.V4 ? (UInt128)uint.MaxValue : UInt128.MaxValue;
            var ranges = group.Select(n => (Start: n.First, End: n.Last)).OrderBy(r => r.Start).ToList();
            var merged = new List<(UInt128 Start, UInt128 End)>();
            foreach (var r in ranges)
            {
                if (merged.Count > 0 && (merged[^1].End == allOnes || r.Start <= merged[^1].End + 1))
                {
                    var last = merged[^1];
                    merged[^1] = (last.Start, r.End > last.End ? r.End : last.End);
                }
                else
                {
                    merged.Add(r);
                }
            }

            foreach (var (start, end) in merged)
            {
                result.AddRange(RangeToCidrs(family, maxBits, start, end));
            }
        }

        return result;
    }

    internal static IEnumerable<IpNetwork> RangeToCidrs(IpFamily family, int maxBits, UInt128 start, UInt128 end)
    {
        while (true)
        {
            var size = start == UInt128.Zero ? maxBits : Math.Min(maxBits, (int)UInt128.TrailingZeroCount(start));
            while (size > 0 && (size == 128 ? UInt128.MaxValue : start + ((UInt128.One << size) - 1)) > end)
            {
                size--;
            }

            yield return new IpNetwork(family, start, maxBits - size);
            var blockEnd = size == 128 ? UInt128.MaxValue : start + ((UInt128.One << size) - 1);
            if (blockEnd >= end)
            {
                yield break;
            }

            start = blockEnd + 1;
        }
    }

    /// <summary>Smallest single network containing all inputs (supernet). Null for mixed families or empty input.</summary>
    public static IpNetwork? Supernet(IEnumerable<IpNetwork> networks)
    {
        ArgumentNullException.ThrowIfNull(networks);
        var list = networks.ToList();
        if (list.Count == 0 || list.Select(n => n.Family).Distinct().Count() > 1)
        {
            return null;
        }

        var family = list[0].Family;
        var maxBits = list[0].MaxBits;
        var lo = list.Min(n => n.First);
        var hi = list.Max(n => n.Last);
        var diff = lo ^ hi;
        var differing = diff == UInt128.Zero ? 0 : 128 - (int)UInt128.LeadingZeroCount(diff);
        var prefix = maxBits - differing;
        return new IpNetwork(family, lo, prefix).Normalized;
    }
}
