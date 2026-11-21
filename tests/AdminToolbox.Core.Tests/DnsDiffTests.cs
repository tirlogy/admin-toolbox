// SPDX-License-Identifier: MIT
using AdminToolbox.Core.Dns;
using AdminToolbox.Core.DnsDiff;

namespace AdminToolbox.Core.Tests;

public class DnsDiffTests
{
    private const string Zone = """
        $ORIGIN example.com.
        $TTL 1h
        @   IN SOA ns1.example.com. hostmaster.example.com. (
                2026112101 ; serial
                3600 900 1209600 300 )
        @       IN  NS    ns1
        @       IN  NS    ns2.provider.test.
        @   300 IN  A     192.0.2.10
                IN  AAAA  2001:DB8:0:0::10
        @       IN  MX    10 mail
        @       IN  MX    20 backup.provider.test.
        @       IN  TXT   "v=spf1 mx " "-all" ; split string
        @       IN  TXT   "say \"hi\"; ok (really)"
        @       IN  CAA   0 issue "letsencrypt.org"
        www     IN  CNAME @
        _sip._tcp IN SRV  10 5 5060 sip
        mail 1d IN  A     192.0.2.20
        old         A     192.0.2.99
        """;

    [Fact]
    public void ParsesZoneFile()
    {
        var r = ZoneParser.Parse(Zone, "ignored.test");
        Assert.Empty(r.Errors);
        Assert.Equal(14, r.Records.Count);
        Assert.Contains(r.Records, x => x.Type == DnsRecordType.SOA && x.Value == "ns1.example.com hostmaster.example.com 2026112101 3600 900 1209600 300");
        Assert.Contains(r.Records, x => x.Type == DnsRecordType.NS && x.Value == "ns1.example.com");
        Assert.Contains(r.Records, x => x.Type == DnsRecordType.AAAA && x.Name == "example.com" && x.Value == "2001:db8::10");
        Assert.Contains(r.Records, x => x.Type == DnsRecordType.MX && x.Value == "10 mail.example.com");
        Assert.Contains(r.Records, x => x.Type == DnsRecordType.TXT && x.Value == "v=spf1 mx -all");
        Assert.Contains(r.Records, x => x.Type == DnsRecordType.TXT && x.Value == "say \"hi\"; ok (really)");
        Assert.Contains(r.Records, x => x.Type == DnsRecordType.CAA && x.Value == "0 issue \"letsencrypt.org\"");
        Assert.Contains(r.Records, x => x.Name == "www.example.com" && x.Type == DnsRecordType.CNAME && x.Value == "example.com");
        Assert.Contains(r.Records, x => x.Name == "_sip._tcp.example.com" && x.Value == "10 5 5060 sip.example.com");
        Assert.Equal(86400, r.Records.Single(x => x.Name == "mail.example.com").Ttl);
        Assert.Equal(300, r.Records.Single(x => x.Type == DnsRecordType.A && x.Name == "example.com").Ttl);
        Assert.Equal(3600, r.Records.Single(x => x.Name == "old.example.com").Ttl);
    }

    [Fact]
    public void ParsesSimpleListAndDigOutput()
    {
        var text = """
            ; dig output
                A 192.0.2.1
            shop.example.org.   300   IN  A   203.0.113.5
            www  CNAME  shop
            @ MX 10 mx.example.org.
            bad  A  999.1.1.1
            weird  HINFO "x" "y"
            $INCLUDE other.zone
            """;
        var r = ZoneParser.Parse(text, "Example.org.");
        Assert.Equal(3, r.Records.Count);
        Assert.Equal("shop.example.org", r.Records[1].Value);
        Assert.Contains(r.Errors, e => e.Code == "zone.rdata" && e.Line == 6);
        Assert.Contains(r.Errors, e => e.Code == "zone.type" && e.Line == 7);
        Assert.Contains(r.Errors, e => e.Code == "zone.directive" && e.Line == 8);
        Assert.Contains(r.Errors, e => e.Code == "zone.owner" && e.Line == 2);
    }

    [Theory]
    [InlineData("300", 300)]
    [InlineData("1h30m", 5400)]
    [InlineData("2d", 172800)]
    [InlineData("1W", 604800)]
    public void Ttl(string text, int seconds)
    {
        Assert.True(ZoneParser.TryTtl(text, out var s));
        Assert.Equal(seconds, s);
    }

    [Theory]
    [InlineData("1x")]
    [InlineData("h1")]
    [InlineData("1h5")]
    [InlineData("")]
    public void TtlInvalid(string text) => Assert.False(ZoneParser.TryTtl(text, out _));

    [Theory]
    [InlineData(DnsRecordType.MX, "10 Mail.Example.org.", "10 mail.example.org")]
    [InlineData(DnsRecordType.CAA, "\\# 22 00 05 69737375 65 6c 65 74 73 65 6e 63 72 79 70 74 2e 6f 72 67", "0 issue \"letsencrypt.org\"")]
    [InlineData(DnsRecordType.TXT, "\"a\" \"b\"", "ab")]
    [InlineData(DnsRecordType.PTR, "host.example.", "host.example")]
    [InlineData(DnsRecordType.A, "\\# 4 c0000201", "\\# 4 c0000201")]
    public void NormalizesLiveValues(DnsRecordType type, string data, string expected) =>
        Assert.Equal(expected, RecordValue.Normalize(type, DnsText.Tokenize(data), string.Empty));

    [Theory]
    [InlineData(DnsRecordType.A, "2001:db8::1")]
    [InlineData(DnsRecordType.A, "1.2.3")]
    [InlineData(DnsRecordType.AAAA, "192.0.2.1")]
    [InlineData(DnsRecordType.MX, "ten mail")]
    [InlineData(DnsRecordType.CNAME, "a b")]
    [InlineData(DnsRecordType.SRV, "1 2 3")]
    [InlineData(DnsRecordType.CAA, "0 issue")]
    public void RejectsInvalidValues(DnsRecordType type, string data) =>
        Assert.Throws<FormatException>(() => RecordValue.Normalize(type, DnsText.Tokenize(data), "x.test"));

    [Fact]
    public void Compare_SetSemantics()
    {
        Assert.Equal(DiffStatus.Unchanged, DnsDiffer.Compare("a", DnsRecordType.A, new[] { "1", "2" }, new[] { "2", "1" }).Status);
        Assert.Equal(DiffStatus.Missing, DnsDiffer.Compare("a", DnsRecordType.A, new[] { "1" }, Array.Empty<string>()).Status);
        var added = DnsDiffer.Compare("a", DnsRecordType.A, new[] { "1" }, new[] { "1", "3" });
        Assert.Equal(DiffStatus.Added, added.Status);
        Assert.Equal(new[] { "3" }, added.Added);
        var changed = DnsDiffer.Compare("a", DnsRecordType.A, new[] { "1", "2" }, new[] { "1", "3" });
        Assert.Equal(DiffStatus.Changed, changed.Status);
        Assert.Equal(new[] { "2" }, changed.Missing);
    }

    [Fact]
    public async Task CompareAsync_DetectsMissingMxTxtCname()
    {
        var before = ZoneParser.Parse(Zone, "example.com").Records;
        var dns = new FakeDns()
            .Add("example.com", DnsRecordType.NS, "ns1.example.com.", "ns2.provider.test.")
            .Add("example.com", DnsRecordType.A, "192.0.2.10")
            .Add("example.com", DnsRecordType.AAAA, "2001:db8::10")
            .Add("example.com", DnsRecordType.MX, "10 mail.example.com.")
            .Txt("example.com", "say \"hi\"; ok (really)")
            .Add("example.com", DnsRecordType.CAA, "0 issue \"letsencrypt.org\"", "0 issue \"pki.example\"")
            .Add("_sip._tcp.example.com", DnsRecordType.SRV, "10 5 5060 sip.example.com.")
            .Add("mail.example.com", DnsRecordType.A, "192.0.2.21")
            .Add("old.example.com", DnsRecordType.TXT, "x")
            .Add("www.example.com", DnsRecordType.A, "192.0.2.10")
            .Fail("broken.example.com");
        var extra = new ZoneRecord("broken.example.com", DnsRecordType.A, null, "192.0.2.1", 99);
        var diff = await new DnsDiffer(dns).CompareAsync(before.Append(extra));
        DiffGroup G(string name, DnsRecordType t) => diff.Single(d => d.Name == name && d.Type == t);

        Assert.DoesNotContain(diff, d => d.Type == DnsRecordType.SOA);
        Assert.Equal(DiffStatus.Unchanged, G("example.com", DnsRecordType.NS).Status);
        Assert.Equal(DiffStatus.Unchanged, G("example.com", DnsRecordType.AAAA).Status);
        var mx = G("example.com", DnsRecordType.MX);
        Assert.Equal(DiffStatus.Missing, mx.Status);
        Assert.Equal(new[] { "20 backup.provider.test" }, mx.Missing);
        Assert.Equal(new[] { "v=spf1 mx -all" }, G("example.com", DnsRecordType.TXT).Missing);
        Assert.Equal(DiffStatus.Added, G("example.com", DnsRecordType.CAA).Status);
        Assert.Equal(DiffStatus.Missing, G("www.example.com", DnsRecordType.CNAME).Status);
        Assert.Equal(DiffStatus.Changed, G("mail.example.com", DnsRecordType.A).Status);
        Assert.Equal(DiffStatus.Unchanged, G("_sip._tcp.example.com", DnsRecordType.SRV).Status);
        Assert.Equal(DiffStatus.Missing, G("old.example.com", DnsRecordType.A).Status);
        Assert.Equal(DiffStatus.Error, G("broken.example.com", DnsRecordType.A).Status);
        Assert.Throws<ArgumentNullException>(() => new DnsDiffer(null!));
    }
}
