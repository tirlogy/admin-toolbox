// SPDX-License-Identifier: MIT
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Numerics;

namespace AdminToolbox.Core.Network;

public enum IpFamily
{
    V4,
    V6,
}

/// <summary>An IPv4 or IPv6 network in CIDR notation. Addresses are stored as 128-bit integers.</summary>
public sealed record IpNetwork(IpFamily Family, UInt128 Address, int Prefix)
{
    public int MaxBits => Family == IpFamily.V4 ? 32 : 128;

    /// <summary>All-ones value for the family (e.g. 0xFFFFFFFF for IPv4).</summary>
    public UInt128 AllOnes => Family == IpFamily.V4 ? uint.MaxValue : UInt128.MaxValue;

    public UInt128 Mask => Prefix == 0 ? UInt128.Zero : (AllOnes << (MaxBits - Prefix)) & AllOnes;

    public UInt128 HostMask => AllOnes ^ Mask;

    public UInt128 First => Address & Mask;

    public UInt128 Last => First | HostMask;

    public BigInteger Count => BigInteger.One << (MaxBits - Prefix);

    /// <summary>The network with host bits cleared.</summary>
    public IpNetwork Normalized => this with { Address = First };

    public bool Contains(UInt128 address) => (address & Mask) == First;

    public bool Contains(IpNetwork other) =>
        other.Family == Family && other.Prefix >= Prefix && Contains(other.First);

    public override string ToString() => Format(Family, Address) + "/" + Prefix.ToString(CultureInfo.InvariantCulture);

    public static string Format(IpFamily family, UInt128 value)
    {
        if (family == IpFamily.V4)
        {
            var v = (uint)value;
            return string.Create(CultureInfo.InvariantCulture, $"{v >> 24}.{(v >> 16) & 255}.{(v >> 8) & 255}.{v & 255}");
        }

        var bytes = new byte[16];
        for (var i = 15; i >= 0; i--)
        {
            bytes[i] = (byte)(value & 0xFF);
            value >>= 8;
        }

        return new IPAddress(bytes).ToString();
    }

    /// <summary>
    /// Parses "10.0.0.1", "10.0.0.0/8", "10.0.0.0 255.0.0.0", "10.0.0.0/255.0.0.0", "2001:db8::/32".
    /// A plain address yields a /32 or /128.
    /// </summary>
    public static bool TryParse(string? text, out IpNetwork network)
    {
        network = null!;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var s = text.Trim();
        string addressPart;
        string? prefixPart = null;
        var slash = s.IndexOf('/');
        if (slash >= 0)
        {
            addressPart = s[..slash].Trim();
            prefixPart = s[(slash + 1)..].Trim();
        }
        else
        {
            var space = s.IndexOfAny(new[] { ' ', '\t' });
            addressPart = space >= 0 ? s[..space] : s;
            if (space >= 0)
            {
                prefixPart = s[(space + 1)..].Trim();
            }
        }

        if (!TryParseAddress(addressPart, out var family, out var value))
        {
            return false;
        }

        var max = family == IpFamily.V4 ? 32 : 128;
        var prefix = max;
        if (prefixPart is not null)
        {
            if (int.TryParse(prefixPart, NumberStyles.None, CultureInfo.InvariantCulture, out var p))
            {
                if (p > max)
                {
                    return false;
                }

                prefix = p;
            }
            else if (family == IpFamily.V4 && TryParseAddress(prefixPart, out var maskFamily, out var mask) && maskFamily == IpFamily.V4)
            {
                var m = (uint)mask;
                var ones = BitOperations.PopCount(m);
                var expected = ones == 0 ? 0u : uint.MaxValue << (32 - ones);
                if (m != expected)
                {
                    return false; // not a contiguous netmask
                }

                prefix = ones;
            }
            else
            {
                return false;
            }
        }

        network = new IpNetwork(family, value, prefix);
        return true;
    }

    public static bool TryParseAddress(string? text, out IpFamily family, out UInt128 value)
    {
        family = IpFamily.V4;
        value = UInt128.Zero;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var s = text.Trim();
        if (s.Contains(':'))
        {
            var zone = s.IndexOf('%');
            if (zone >= 0)
            {
                s = s[..zone];
            }

            if (!IPAddress.TryParse(s, out var v6) || v6.AddressFamily != AddressFamily.InterNetworkV6)
            {
                return false;
            }

            foreach (var b in v6.GetAddressBytes())
            {
                value = (value << 8) | b;
            }

            family = IpFamily.V6;
            return true;
        }

        var parts = s.Split('.');
        if (parts.Length != 4)
        {
            return false;
        }

        uint v4 = 0;
        foreach (var part in parts)
        {
            if (part.Length is 0 or > 3 || !byte.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out var octet))
            {
                return false;
            }

            v4 = (v4 << 8) | octet;
        }

        value = v4;
        return true;
    }
}
