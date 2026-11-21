// SPDX-License-Identifier: MIT
using AdminToolbox.Core.Dns;

namespace AdminToolbox.Core.Tests;

public class DnsBasicsTests
{
    [Theory]
    [InlineData("Example.COM", "example.com")]
    [InlineData("  user@Example.org ", "example.org")]
    [InlineData("https://www.example.net:8443/path?x=1", "www.example.net")]
    [InlineData("example.com.", "example.com")]
    [InlineData("bücher.example", "xn--bcher-kva.example")]
    [InlineData("_dmarc.example.com", "_dmarc.example.com")]
    public void Normalize_Valid(string input, string expected)
    {
        Assert.True(DomainName.TryNormalize(input, out var d));
        Assert.Equal(expected, d);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("localhost")]
    [InlineData("-bad.example")]
    [InlineData("bad-.example")]
    [InlineData("a..b")]
    [InlineData("exa$mple.com")]
    [InlineData("https://")]
    public void Normalize_Invalid(string? input) => Assert.False(DomainName.TryNormalize(input, out _));

    [Fact]
    public void Normalize_TooLong()
    {
        Assert.False(DomainName.TryNormalize(new string('a', 64) + ".com", out _));
        Assert.False(DomainName.TryNormalize(string.Join('.', Enumerable.Repeat(new string('a', 60), 5)), out _));
    }

    [Theory]
    [InlineData("mail.example.com", "example.com")]
    [InlineData("example.com", "example.com")]
    [InlineData("shop.example.co.uk", "example.co.uk")]
    [InlineData("a.b.firma.com.tr.", "firma.com.tr")]
    [InlineData("com", "com")]
    public void OrganizationalDomain(string name, string expected) => Assert.Equal(expected, DomainName.OrganizationalDomain(name));

    [Theory]
    [InlineData("\"v=spf1 \" \"-all\"", "v=spf1 -all")]
    [InlineData("plain text", "plain text")]
    [InlineData("\"a\\\"b\\059c\\\\\"", "a\"b;c\\")]
    [InlineData("", "")]
    public void UnquoteTxt(string input, string expected) => Assert.Equal(expected, DnsText.UnquoteTxt(input));

    [Fact]
    public void Tokenize()
    {
        Assert.Equal(new[] { "10", "issue", "\"a b\\\" c\"" }, DnsText.Tokenize("10  issue \"a b\\\" c\""));
        Assert.Empty(DnsText.Tokenize("   "));
    }

    [Fact]
    public void Answer()
    {
        var a = new DnsAnswer(DnsAnswer.NoError, new[] { new DnsRecord("x", DnsRecordType.A, 1, "192.0.2.1") });
        Assert.False(a.IsNxDomain);
        Assert.Equal(new[] { "192.0.2.1" }, a.Data(DnsRecordType.A));
        Assert.True(a.IsEmpty(DnsRecordType.TXT));
        Assert.Equal("boom", new DnsQueryException("boom").Message);
    }
}
