// SPDX-License-Identifier: MIT
using AdminToolbox.Core.Mail;

namespace AdminToolbox.Core.Tests;

public class SpfTests
{
    [Theory]
    [InlineData("v=spf1 -all", true)]
    [InlineData("V=SPF1 include:x.org ~all", true)]
    [InlineData("v=spf1", true)]
    [InlineData("v=spf10 -all", false)]
    [InlineData("google-site-verification=abc", false)]
    public void IsSpf(string txt, bool expected) => Assert.Equal(expected, SpfRecord.IsSpf(txt));

    [Fact]
    public void Parse_AllMechanisms()
    {
        var r = SpfRecord.Parse("v=spf1 ip4:192.0.2.0/24 ip6:2001:db8::/32 a mx:mail.example.org/24 a:example.net//64 ptr include:_spf.example.com exists:%{i}.x.example.com ?all redirect=other.example exp=exp.example foo=bar");
        Assert.Empty(r.Errors);
        Assert.Equal(new[]
        {
            SpfTermKind.Ip4, SpfTermKind.Ip6, SpfTermKind.A, SpfTermKind.Mx, SpfTermKind.A, SpfTermKind.Ptr, SpfTermKind.Include,
            SpfTermKind.Exists, SpfTermKind.All, SpfTermKind.Redirect, SpfTermKind.Exp, SpfTermKind.UnknownModifier,
        }, r.Terms.Select(t => t.Kind));
        Assert.Equal(SpfQualifier.Neutral, r.Terms[8].Qualifier);
        Assert.Equal("mail.example.org", r.Terms[3].TargetDomain);
        Assert.Equal("example.net", r.Terms[4].TargetDomain);
        Assert.Null(r.Terms[2].TargetDomain);
        Assert.Null(r.Terms[0].TargetDomain);
        Assert.Equal(7, r.Terms.Count(t => t.CountsAsLookup));
    }

    [Theory]
    [InlineData("ip4:300.1.1.1")]
    [InlineData("ip4:192.0.2.0/33")]
    [InlineData("ip4:2001:db8::1")]
    [InlineData("ip6:192.0.2.1")]
    [InlineData("include")]
    [InlineData("include:")]
    [InlineData("all:foo")]
    [InlineData("a:")]
    [InlineData("a/40")]
    [InlineData("mx//200")]
    [InlineData("ptr/24")]
    [InlineData("foo:bar")]
    [InlineData("redirect=")]
    [InlineData("1abc=x")]
    public void Parse_InvalidTerms(string term) => Assert.Single(SpfRecord.Parse("v=spf1 " + term).Errors);

    [Fact]
    public async Task Evaluate_CountsLookupsAcrossIncludes()
    {
        var dns = new FakeDns()
            .Txt("example.com", "v=spf1 include:_spf.provider.test include:mail.other.test mx -all", "some-verification=1")
            .Add("example.com", Core.Dns.DnsRecordType.MX, "10 mx.example.com.")
            .Txt("_spf.provider.test", "v=spf1 include:_n1.provider.test include:_n2.provider.test ~all")
            .Txt("_n1.provider.test", "v=spf1 ip4:192.0.2.0/24 ~all")
            .Txt("_n2.provider.test", "v=spf1 ip6:2001:db8::/32 ~all")
            .Txt("mail.other.test", "v=spf1 a ~all")
            .Add("mail.other.test", Core.Dns.DnsRecordType.A, "198.51.100.1");
        var result = await new SpfEvaluator(dns).AnalyzeAsync("Example.com.");
        Assert.Equal(6, result.LookupCount);
        Assert.Equal(0, result.VoidLookups);
        Assert.Equal(2, result.Root.Children.Count);
        Assert.Equal(2, result.Root.Children[0].Children.Count);
        Assert.True(result.Has("spf.lookups.ok"));
        Assert.True(result.Has("spf.all.fail"));
        Assert.Equal(Severity.Ok, result.Severity);
    }

    [Fact]
    public async Task Evaluate_TooManyLookups()
    {
        var dns = new FakeDns().Txt("big.test", "v=spf1 " + string.Join(' ', Enumerable.Range(1, 11).Select(i => $"include:i{i}.test")) + " -all");
        for (var i = 1; i <= 11; i++)
        {
            dns.Txt($"i{i}.test", "v=spf1 ip4:192.0.2." + i + " -all");
        }

        var result = await new SpfEvaluator(dns).AnalyzeAsync("big.test");
        Assert.Equal(11, result.LookupCount);
        Assert.True(result.Has("spf.lookups.exceeded"));
        Assert.Equal(Severity.Error, result.Severity);
    }

    [Fact]
    public async Task Evaluate_NearLimitWarns()
    {
        var dns = new FakeDns().Txt("near.test", "v=spf1 " + string.Join(' ', Enumerable.Range(1, 9).Select(i => $"include:n{i}.test")) + " ~all");
        for (var i = 1; i <= 9; i++)
        {
            dns.Txt($"n{i}.test", "v=spf1 -all");
        }

        var result = await new SpfEvaluator(dns).AnalyzeAsync("near.test");
        Assert.True(result.Has("spf.lookups.near"));
        Assert.True(result.Has("spf.all.softfail"));
    }

    [Fact]
    public async Task Evaluate_MissingIncludeLoopAndVoidLookups()
    {
        var dns = new FakeDns()
            .Txt("loop.test", "v=spf1 include:a.loop.test include:gone.test a:void1.test mx:void2.test ptr +all")
            .Txt("a.loop.test", "v=spf1 include:loop.test -all");
        var result = await new SpfEvaluator(dns).AnalyzeAsync("loop.test");
        Assert.True(result.Has("spf.loop"));
        Assert.True(result.Has("spf.include.missing"));
        Assert.True(result.Has("spf.voidlookups"));
        Assert.True(result.Has("spf.all.pass"));
        Assert.True(result.Has("spf.ptr"));
        Assert.Equal(3, result.VoidLookups);
    }

    [Fact]
    public async Task Evaluate_NoRecord()
    {
        var result = await new SpfEvaluator(new FakeDns().Txt("none.test", "hello")).AnalyzeAsync("none.test");
        Assert.True(result.Has("spf.none"));
        Assert.Null(result.Root.Record);
    }

    [Fact]
    public async Task Evaluate_MultipleRecordsAndSyntax()
    {
        var dns = new FakeDns().Txt("multi.test", "v=spf1 ip4:999.1.1.1 -all", "v=spf1 -all");
        var result = await new SpfEvaluator(dns).AnalyzeAsync("multi.test");
        Assert.True(result.Has("spf.multiple"));
        Assert.True(result.Has("spf.syntax"));
    }

    [Fact]
    public async Task Evaluate_RedirectAndMissingAll()
    {
        var dns = new FakeDns()
            .Txt("r.test", "v=spf1 redirect=_spf.r.test")
            .Txt("_spf.r.test", "v=spf1 ip4:192.0.2.1 -all")
            .Txt("noall.test", "v=spf1 ip4:192.0.2.1")
            .Txt("both.test", "v=spf1 -all redirect=_spf.r.test")
            .Txt("after.test", "v=spf1 ?all ip4:192.0.2.1")
            .Txt("macro.test", "v=spf1 exists:%{i}._spf.macro.test include:%{d}.x.test -all");
        var redirect = await new SpfEvaluator(dns).AnalyzeAsync("r.test");
        Assert.Single(redirect.Root.Children);
        Assert.Equal(SpfTermKind.Redirect, redirect.Root.Children[0].Via);
        Assert.Equal(1, redirect.LookupCount);
        Assert.False(redirect.Has("spf.all.missing"));

        Assert.True((await new SpfEvaluator(dns).AnalyzeAsync("noall.test")).Has("spf.all.missing"));
        Assert.True((await new SpfEvaluator(dns).AnalyzeAsync("both.test")).Has("spf.redirect.ignored"));
        var after = await new SpfEvaluator(dns).AnalyzeAsync("after.test");
        Assert.True(after.Has("spf.all.notlast"));
        Assert.True(after.Has("spf.all.neutral"));
        Assert.True((await new SpfEvaluator(dns).AnalyzeAsync("macro.test")).Has("spf.macro"));
    }

    [Fact]
    public async Task Evaluate_StopsAtNodeLimit()
    {
        var dns = new FakeDns();
        for (var i = 0; i < 60; i++)
        {
            dns.Txt($"c{i}.test", $"v=spf1 include:c{i + 1}.test -all");
        }

        var result = await new SpfEvaluator(dns).AnalyzeAsync("c0.test");
        Assert.True(result.Has("spf.toodeep"));
    }
}
