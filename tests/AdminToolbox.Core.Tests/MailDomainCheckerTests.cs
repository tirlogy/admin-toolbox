// SPDX-License-Identifier: MIT
using System.Security.Cryptography;
using AdminToolbox.Core.Dns;
using AdminToolbox.Core.Mail;

namespace AdminToolbox.Core.Tests;

public class MailDomainCheckerTests
{
    private static string RsaKey(int bits)
    {
        using var rsa = RSA.Create(bits);
        return Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo());
    }

    private static FakeDns GoodDomain() => new FakeDns()
        .Add("good.test", DnsRecordType.MX, "10 mx1.good.test.", "20 mx2.good.test.")
        .Add("mx1.good.test", DnsRecordType.A, "192.0.2.1")
        .Add("mx2.good.test", DnsRecordType.AAAA, "2001:db8::2")
        .Txt("good.test", "v=spf1 mx -all")
        .Txt("selector1._domainkey.good.test", "v=DKIM1; k=rsa; p=" + RsaKey(2048))
        .Txt("_dmarc.good.test", "v=DMARC1; p=reject; rua=mailto:dmarc@good.test; adkim=s")
        .Txt("default._bimi.good.test", "v=BIMI1; l=https://good.test/logo.svg; a=https://good.test/vmc.pem")
        .Txt("_mta-sts.good.test", "v=STSv1; id=20261121T1")
        .Add("mta-sts.good.test", DnsRecordType.A, "192.0.2.5")
        .Txt("_smtp._tls.good.test", "v=TLSRPTv1; rua=mailto:tls@good.test");

    [Fact]
    public async Task GoodDomain_AllGreenOrInfo()
    {
        var report = await new MailDomainChecker(GoodDomain()).CheckAsync("https://GOOD.test/path");
        Assert.Equal("good.test", report.Domain);
        Assert.Equal(7, report.Checks.Count);
        Assert.Equal(Severity.Ok, report[CheckKind.Mx].Severity);
        Assert.Equal(new[] { "10 mx1.good.test", "20 mx2.good.test" }, report[CheckKind.Mx].Records);
        Assert.Equal(Severity.Ok, report[CheckKind.Spf].Severity);
        Assert.Equal(Severity.Ok, report[CheckKind.Dkim].Severity);
        Assert.Single(report.DkimKeys);
        Assert.Equal(2048, report.DkimKeys[0].KeyBits);
        Assert.Equal(Severity.Info, report[CheckKind.Dmarc].Severity); // strict alignment is informational
        Assert.True(report[CheckKind.Dmarc].Has("dmarc.p.reject"));
        Assert.True(report[CheckKind.Bimi].Has("bimi.ok"));
        Assert.True(report[CheckKind.MtaSts].Has("mtasts.found"));
        Assert.True(report[CheckKind.TlsRpt].Has("tlsrpt.ok"));
        Assert.NotNull(report.Spf);
        Assert.True(report.Severity <= Severity.Info);
    }

    [Fact]
    public async Task BrokenDomain_ReportsProblems()
    {
        var dns = new FakeDns()
            .Add("bad.test", DnsRecordType.MX, "10 192.0.2.9", "20 alias.bad.test.", "30 nowhere.bad.test.")
            .Add("alias.bad.test", DnsRecordType.CNAME, "real.bad.test.")
            .Add("real.bad.test", DnsRecordType.A, "192.0.2.10")
            .Txt("nowhere.bad.test", "x")
            .Txt("bad.test", "v=spf1 +all")
            .Txt("s1._domainkey.bad.test", "v=DKIM1; k=rsa; t=y; p=" + RsaKey(1024))
            .Txt("s2._domainkey.bad.test", "v=DKIM1; p=")
            .Txt("k1._domainkey.bad.test", "v=DKIM1; p=!!notbase64!!")
            .Txt("_dmarc.bad.test", "v=DMARC1; p=none; pct=50; rua=https://x.test; ruf=mailto:f@bad.test; aspf=x; sp=bogus; broken")
            .Txt("default._bimi.bad.test", "v=BIMI1; l=http://bad.test/logo.png")
            .Txt("_mta-sts.bad.test", "v=STSv1; id=bad-id!", "v=STSv1; id=2");
        var report = await new MailDomainChecker(dns).CheckAsync("bad.test", "custom");
        var mx = report[CheckKind.Mx];
        Assert.True(mx.Has("mx.ip"));
        Assert.True(mx.Has("mx.cname"));
        Assert.True(mx.Has("mx.unresolvable"));
        Assert.True(report[CheckKind.Spf].Has("spf.all.pass"));
        var dkim = report[CheckKind.Dkim];
        Assert.True(dkim.Has("dkim.1024"));
        Assert.True(dkim.Has("dkim.testing"));
        Assert.True(dkim.Has("dkim.revoked"));
        Assert.True(dkim.Has("dkim.invalidkey"));
        Assert.True(dkim.Has("dkim.selector.notfound"));
        var dmarc = report[CheckKind.Dmarc];
        Assert.True(dmarc.Has("dmarc.p.none"));
        Assert.True(dmarc.Has("dmarc.pct"));
        Assert.True(dmarc.Has("dmarc.rua.invalid"));
        Assert.True(dmarc.Has("dmarc.ruf"));
        Assert.True(dmarc.Has("dmarc.syntax"));
        var bimi = report[CheckKind.Bimi];
        Assert.True(bimi.Has("bimi.logo.invalid"));
        Assert.True(bimi.Has("bimi.novmc"));
        Assert.True(bimi.Has("bimi.dmarc"));
        var sts = report[CheckKind.MtaSts];
        Assert.True(sts.Has("mtasts.multiple"));
        Assert.True(sts.Has("mtasts.id.invalid"));
        Assert.True(sts.Has("mtasts.host.missing"));
        Assert.True(report[CheckKind.TlsRpt].Has("tlsrpt.none.sts"));
        Assert.Equal(Severity.Error, report.Severity);
    }

    [Fact]
    public async Task EmptyDomain()
    {
        var dns = new FakeDns().Add("empty.test", DnsRecordType.A, "192.0.2.1");
        var report = await new MailDomainChecker(dns).CheckAsync("user@empty.test", "Bad Selector!");
        Assert.True(report[CheckKind.Mx].Has("mx.implicit"));
        Assert.True(report[CheckKind.Spf].Has("spf.none"));
        Assert.True(report[CheckKind.Dkim].Has("dkim.notfound"));
        Assert.True(report[CheckKind.Dkim].Has("dkim.selector.invalid"));
        Assert.True(report[CheckKind.Dmarc].Has("dmarc.none"));
        Assert.True(report[CheckKind.Bimi].Has("bimi.none"));
        Assert.True(report[CheckKind.MtaSts].Has("mtasts.none"));
        Assert.True(report[CheckKind.TlsRpt].Has("tlsrpt.none"));
    }

    [Fact]
    public async Task NxDomain_NullMx_AndSingleMx()
    {
        var nx = await new MailDomainChecker(new FakeDns()).CheckAsync("missing.test");
        Assert.True(nx[CheckKind.Mx].Has("mx.nxdomain"));

        var noMail = new FakeDns().Add("nomail.test", DnsRecordType.MX, "0 .").Txt("nomail.test", "v=spf1 -all");
        Assert.True((await new MailDomainChecker(noMail).CheckAsync("nomail.test"))[CheckKind.Mx].Has("mx.null"));

        var nothing = new FakeDns().Txt("nothing.test", "x");
        Assert.True((await new MailDomainChecker(nothing).CheckAsync("nothing.test"))[CheckKind.Mx].Has("mx.none"));

        var single = new FakeDns().Add("one.test", DnsRecordType.MX, "10 mx.one.test.").Add("mx.one.test", DnsRecordType.A, "192.0.2.1");
        Assert.True((await new MailDomainChecker(single).CheckAsync("one.test"))[CheckKind.Mx].Has("mx.single"));
    }

    [Fact]
    public async Task Dmarc_InheritedAndMultiple()
    {
        var dns = new FakeDns()
            .Txt("_dmarc.parent.test", "v=DMARC1; p=reject; sp=quarantine; rua=mailto:a@parent.test")
            .Txt("_dmarc.multi.test", "v=DMARC1; p=none", "v=DMARC1; p=reject")
            .Txt("_dmarc.spnone.test", "v=DMARC1; p=reject; sp=none; pct=abc; rua=mailto:a@spnone.test")
            .Txt("_dmarc.nop.test", "v=DMARC1; rua=mailto:a@nop.test");
        var sub = await new MailDomainChecker(dns).CheckAsync("shop.parent.test");
        Assert.True(sub[CheckKind.Dmarc].Has("dmarc.inherited"));
        Assert.True(sub[CheckKind.Dmarc].Has("dmarc.p.quarantine"));
        Assert.True((await new MailDomainChecker(dns).CheckAsync("multi.test"))[CheckKind.Dmarc].Has("dmarc.multiple"));
        var spNone = (await new MailDomainChecker(dns).CheckAsync("spnone.test"))[CheckKind.Dmarc];
        Assert.True(spNone.Has("dmarc.sp.none"));
        Assert.True(spNone.Has("dmarc.syntax"));
        Assert.True((await new MailDomainChecker(dns).CheckAsync("nop.test"))[CheckKind.Dmarc].Has("dmarc.p.invalid"));
    }

    [Fact]
    public async Task DnsFailure_IsReportedPerCheck()
    {
        var dns = new FakeDns().Fail("fail.test").Fail("_dmarc.fail.test");
        var report = await new MailDomainChecker(dns).CheckAsync("fail.test");
        Assert.True(report[CheckKind.Mx].Has("dns.error"));
        Assert.True(report[CheckKind.Dmarc].Has("dns.error"));
    }

    [Fact]
    public async Task InvalidDomain_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => new MailDomainChecker(new FakeDns()).CheckAsync("not a domain"));
        Assert.Throws<ArgumentNullException>(() => new MailDomainChecker(null!));
    }

    [Fact]
    public void Dkim_Ed25519AndPkcs1()
    {
        var r = new CheckResult(CheckKind.Dkim);
        var ed = MailDomainChecker.ParseDkim("ed", "v=DKIM1; k=ed25519; p=" + Convert.ToBase64String(new byte[32]), r);
        Assert.Equal(256, ed.KeyBits);
        using var rsa = RSA.Create(2048);
        var pkcs1 = MailDomainChecker.ParseDkim("p1", "v=DKIM1; p=" + Convert.ToBase64String(rsa.ExportRSAPublicKey()), r);
        Assert.Equal(2048, pkcs1.KeyBits);
        // 512-bit keys cannot be generated on every platform, so the PKCS#1 key is written by hand.
        var writer = new System.Formats.Asn1.AsnWriter(System.Formats.Asn1.AsnEncodingRules.DER);
        using (writer.PushSequence())
        {
            var modulus = new byte[65];
            modulus[1] = 0xC5;
            modulus[64] = 0x01;
            writer.WriteIntegerUnsigned(modulus.AsSpan(1));
            writer.WriteInteger(65537);
        }

        MailDomainChecker.ParseDkim("weak", "p=" + Convert.ToBase64String(writer.Encode()), r);
        Assert.True(r.Has("dkim.weak"));
        MailDomainChecker.ParseDkim("v", "v=DKIM2; p=", r);
        Assert.True(r.Has("dkim.version"));
    }

    [Fact]
    public void TagList_ParsesDuplicatesAndInvalid()
    {
        var t = TagList.Parse("v=DMARC1; p=reject; p=none; junk; ; rua = mailto:x@y.test ");
        Assert.Equal("reject", t["p"]);
        Assert.Equal("mailto:x@y.test", t["RUA"]);
        Assert.Contains("p", t.Duplicates);
        Assert.Contains("junk", t.Invalid);
        Assert.Equal(3, t.Ordered.Count);
        Assert.True(t.Contains("v"));
        Assert.Null(t["missing"]);
    }

    [Fact]
    public void Finding_ToStringAndFindingList()
    {
        var f = new Finding("x.y", Severity.Warning, "a", "b");
        Assert.Equal("Warning x.y [a, b]", f.ToString());
        var list = new FindingList();
        Assert.Equal(Severity.Ok, list.Severity);
        list.Add("a", Severity.Info);
        list.AddRange(new[] { f });
        Assert.Equal(Severity.Warning, list.Severity);
    }
}
