// SPDX-License-Identifier: MIT
using System.Numerics;
using AdminToolbox.Core.Network;

namespace AdminToolbox.Core.Tests;

public class SubnetTests
{
    private static IpNetwork Net(string s)
    {
        Assert.True(IpNetwork.TryParse(s, out var n), s);
        return n;
    }

    [Theory]
    [InlineData("192.168.10.77/24", "192.168.10.0", "192.168.10.255", "192.168.10.1", "192.168.10.254", "256", "254", "255.255.255.0", "0.0.0.255", "private")]
    [InlineData("10.1.2.3 255.255.0.0", "10.1.0.0", "10.1.255.255", "10.1.0.1", "10.1.255.254", "65536", "65534", "255.255.0.0", "0.0.255.255", "private")]
    [InlineData("0.0.0.0/0", "0.0.0.0", "255.255.255.255", "0.0.0.1", "255.255.255.254", "4294967296", "4294967294", "0.0.0.0", "255.255.255.255", "thisnetwork")]
    [InlineData("198.51.100.8/31", "198.51.100.8", null, "198.51.100.8", "198.51.100.9", "2", "2", "255.255.255.254", "0.0.0.1", "documentation")]
    [InlineData("8.8.8.8", "8.8.8.8", null, "8.8.8.8", "8.8.8.8", "1", "1", "255.255.255.255", "0.0.0.0", "public")]
    [InlineData("100.64.1.1/30", "100.64.1.0", "100.64.1.3", "100.64.1.1", "100.64.1.2", "4", "2", "255.255.255.252", "0.0.0.3", "cgnat")]
    public void Ipv4(string input, string network, string? broadcast, string first, string last, string total, string usable, string mask, string wildcard, string type)
    {
        var info = SubnetCalculator.Describe(Net(input));
        Assert.Equal(network, info.NetworkAddress);
        Assert.Equal(broadcast, info.Broadcast);
        Assert.Equal(first, info.FirstHost);
        Assert.Equal(last, info.LastHost);
        Assert.Equal(BigInteger.Parse(total), info.TotalAddresses);
        Assert.Equal(BigInteger.Parse(usable), info.UsableHosts);
        Assert.Equal(mask, info.Netmask);
        Assert.Equal(wildcard, info.Wildcard);
        Assert.Equal(type, info.AddressType);
    }

    [Theory]
    [InlineData("2001:db8:abcd:12::1/64", "2001:db8:abcd:12::", "2001:db8:abcd:12:ffff:ffff:ffff:ffff", "18446744073709551616", "documentation", "2.1.0.0.d.c.b.a.8.b.d.0.1.0.0.2.ip6.arpa")]
    [InlineData("::/0", "::", "ffff:ffff:ffff:ffff:ffff:ffff:ffff:ffff", "340282366920938463463374607431768211456", "unspecified", "ip6.arpa")]
    [InlineData("fe80::1/127", "fe80::", "fe80::1", "2", "linklocal", null)]
    [InlineData("2a00:1450::5/128", "2a00:1450::5", "2a00:1450::5", "1", "public", null)]
    [InlineData("fd12:3456::%eth0/48", "fd12:3456::", "fd12:3456:0:ffff:ffff:ffff:ffff:ffff", "1208925819614629174706176", "ula", null)]
    public void Ipv6(string input, string network, string last, string total, string type, string? reverse)
    {
        var info = SubnetCalculator.Describe(Net(input));
        Assert.Equal(network, info.NetworkAddress);
        Assert.Equal(last, info.LastHost);
        Assert.Equal(BigInteger.Parse(total), info.TotalAddresses);
        Assert.Equal(info.TotalAddresses, info.UsableHosts);
        Assert.Equal(type, info.AddressType);
        Assert.Null(info.Broadcast);
        Assert.Null(info.Netmask);
        Assert.Null(info.Binary);
        if (reverse is not null)
        {
            Assert.Equal(reverse, info.ReverseZone);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("1.2.3")]
    [InlineData("1.2.3.256")]
    [InlineData("1.2.3.4/33")]
    [InlineData("1.2.3.4/-1")]
    [InlineData("1.2.3.4 255.0.255.0")]
    [InlineData("1.2.3.4/abc")]
    [InlineData("2001:db8::/129")]
    [InlineData("2001:db8::/255.255.0.0")]
    [InlineData("gggg::1")]
    [InlineData("1.2.3.0004")]
    public void Invalid(string input) => Assert.False(IpNetwork.TryParse(input, out _));

    [Fact]
    public void ReverseZoneAndBinary()
    {
        Assert.Equal("10.168.192.in-addr.arpa", SubnetCalculator.ReverseZone(Net("192.168.10.0/24")));
        Assert.Equal("168.192.in-addr.arpa", SubnetCalculator.ReverseZone(Net("192.168.10.0/20")));
        Assert.Equal("in-addr.arpa", SubnetCalculator.ReverseZone(Net("0.0.0.0/0")));
        Assert.Equal("11000000.10101000.00001010|.00000000", SubnetCalculator.Describe(Net("192.168.10.0/24")).Binary);
        Assert.Equal("00001010.0000|0000.00000000.00000000", SubnetCalculator.Describe(Net("10.0.0.0/12")).Binary);
        Assert.Equal("00001010.00000000.00000000.00000001", SubnetCalculator.Describe(Net("10.0.0.1/32")).Binary);
    }

    [Fact]
    public void Split()
    {
        var parts = SubnetCalculator.Split(Net("10.0.0.0/24"), 26, out var total);
        Assert.Equal(4, (int)total);
        Assert.Equal(new[] { "10.0.0.0/26", "10.0.0.64/26", "10.0.0.128/26", "10.0.0.192/26" }, parts.Select(p => p.ToString()));

        var many = SubnetCalculator.Split(Net("2001:db8::/32"), 64, out var total6);
        Assert.Equal(BigInteger.One << 32, total6);
        Assert.Equal(SubnetCalculator.MaxListedSubnets, many.Count);
        Assert.Equal("2001:db8:0:3ff::/64", many[^1].ToString());

        var all = SubnetCalculator.Split(Net("0.0.0.0/0"), 0, out var one);
        Assert.Equal(1, (int)one);
        Assert.Single(all);
        Assert.Throws<ArgumentOutOfRangeException>(() => SubnetCalculator.Split(Net("10.0.0.0/24"), 23, out _));
        Assert.Throws<ArgumentOutOfRangeException>(() => SubnetCalculator.Split(Net("10.0.0.0/24"), 33, out _));
    }

    [Fact]
    public void Summarize()
    {
        var input = new[] { "10.0.0.0/24", "10.0.1.0/24", "10.0.2.0/23", "10.0.3.5/32", "192.168.0.1", "192.168.0.2", "2001:db8::/33", "2001:db8:8000::/33" }.Select(Net);
        var result = SubnetCalculator.Summarize(input).Select(n => n.ToString()).ToList();
        Assert.Equal(new[] { "10.0.0.0/22", "192.168.0.1/32", "192.168.0.2/32", "2001:db8::/32" }, result);

        var full = SubnetCalculator.Summarize(new[] { Net("0.0.0.0/1"), Net("128.0.0.0/1") });
        Assert.Equal("0.0.0.0/0", Assert.Single(full).ToString());
        var full6 = SubnetCalculator.Summarize(new[] { Net("::/1"), Net("8000::/1"), Net("::1") });
        Assert.Equal("::/0", Assert.Single(full6).ToString());
        var range = SubnetCalculator.RangeToCidrs(IpFamily.V4, 32, 1, 6).Select(n => n.ToString());
        Assert.Equal(new[] { "0.0.0.1/32", "0.0.0.2/31", "0.0.0.4/31", "0.0.0.6/32" }, range);
    }

    [Fact]
    public void SupernetAndContains()
    {
        Assert.Equal("10.0.0.0/22", SubnetCalculator.Supernet(new[] { Net("10.0.1.0/24"), Net("10.0.3.0/24") })!.ToString());
        Assert.Equal("10.0.1.0/24", SubnetCalculator.Supernet(new[] { Net("10.0.1.0/24") })!.ToString());
        Assert.Null(SubnetCalculator.Supernet(new[] { Net("10.0.1.0/24"), Net("::1") }));
        Assert.Null(SubnetCalculator.Supernet(Array.Empty<IpNetwork>()));

        var net = Net("172.16.0.0/12");
        Assert.True(IpNetwork.TryParseAddress("172.31.255.255", out _, out var inside));
        Assert.True(IpNetwork.TryParseAddress("172.32.0.0", out _, out var outside));
        Assert.True(net.Contains(inside));
        Assert.False(net.Contains(outside));
        Assert.True(net.Contains(Net("172.20.0.0/16")));
        Assert.False(net.Contains(Net("172.0.0.0/8")));
        Assert.False(net.Contains(Net("::/0")));
        Assert.Equal("172.16.0.0/12", Net("172.20.1.1/12").Normalized.ToString());
    }

    [Theory]
    [InlineData("127.0.0.1", "loopback")]
    [InlineData("169.254.1.1", "linklocal")]
    [InlineData("224.0.0.251", "multicast")]
    [InlineData("250.1.1.1", "reserved")]
    [InlineData("255.255.255.255", "broadcast")]
    [InlineData("198.18.0.1", "benchmark")]
    [InlineData("::1", "loopback")]
    [InlineData("::ffff:192.0.2.1", "v4mapped")]
    [InlineData("64:ff9b::1", "nat64")]
    [InlineData("ff02::1", "multicast")]
    [InlineData("4000::1", "reserved")]
    public void AddressTypes(string ip, string expected)
    {
        Assert.True(IpNetwork.TryParseAddress(ip, out var family, out var value));
        Assert.Equal(expected, SubnetCalculator.AddressType(family, value));
    }
}
