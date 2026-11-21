// SPDX-License-Identifier: MIT
using AdminToolbox.Core.Dns;
using AdminToolbox.Core.Mail;

namespace AdminToolbox.Dns.Tests;

/// <summary>
/// Full mail domain checks against DoH answers recorded from cloudflare-dns.com on 2026-11-21.
/// Re-record with: RECORD_DOH=1 dotnet test --filter RecordedDomainTests
/// </summary>
public class RecordedDomainTests
{
    private static (MailDomainChecker Checker, FixtureHandler Handler) Create()
    {
        var handler = new FixtureHandler();
        var client = new DohClient(new HttpClient(handler), new DohOptions { Endpoints = new[] { new Uri("https://cloudflare-dns.com/dns-query") } });
        return (new MailDomainChecker(client), handler);
    }

    [Fact]
    public async Task GoogleCom_SpfIncludeTree()
    {
        var (checker, handler) = Create();
        var report = await checker.CheckAsync("google.com");
        Assert.Empty(handler.Missing);
        var spf = report.Spf!;
        Assert.Equal("v=spf1 include:_spf.google.com ~all", spf.Root.Record!.Text);
        var include = Assert.Single(spf.Root.Children);
        Assert.Equal("_spf.google.com", include.Domain);
        Assert.Empty(include.Children); // Google flattened its netblocks into _spf.google.com in 2026
        Assert.Contains(include.Record!.Terms, t => t.Kind == SpfTermKind.Ip6);
        Assert.Equal(1, spf.LookupCount);
        Assert.Equal(0, spf.VoidLookups);
        Assert.True(spf.Has("spf.lookups.ok"));
        Assert.True(spf.Has("spf.all.softfail"));
        Assert.NotEmpty(report[CheckKind.Mx].Records);
        Assert.True(report[CheckKind.Mx].Severity < Severity.Warning);
        Assert.True(report[CheckKind.Dmarc].Has("dmarc.p.reject"));
    }

    [Fact]
    public async Task OutlookCom_SpfIncludeTree()
    {
        var (checker, handler) = Create();
        var report = await checker.CheckAsync("outlook.com");
        Assert.Empty(handler.Missing);
        var spf = report.Spf!;
        Assert.Contains("include:spf2.outlook.com", spf.Root.Record!.Text, StringComparison.Ordinal);
        Assert.InRange(spf.LookupCount, 1, SpfEvaluator.LookupLimit);
        Assert.Equal(spf.LookupCount, CountLookups(spf.Root));
        Assert.DoesNotContain(spf.Findings, f => f.Code is "spf.include.missing" or "spf.loop" or "spf.syntax");
        Assert.NotNull(report[CheckKind.Dmarc].Records.FirstOrDefault());
    }

    [Theory]
    [InlineData("github.com", 10)]
    [InlineData("atlassian.com", 6)]
    public async Task DeepIncludeChains_CountEveryLookup(string domain, int expected)
    {
        var (checker, handler) = Create();
        var report = await checker.CheckAsync(domain);
        Assert.Empty(handler.Missing);
        var spf = report.Spf!;
        Assert.Equal(expected, spf.LookupCount);
        Assert.Equal(spf.LookupCount, CountLookups(spf.Root));
        var code = spf.LookupCount > SpfEvaluator.LookupLimit ? "spf.lookups.exceeded"
            : spf.LookupCount >= SpfEvaluator.LookupLimit - 1 ? "spf.lookups.near" : "spf.lookups.ok";
        Assert.True(spf.Has(code));
    }

    private static int CountLookups(SpfNode node) => node.OwnLookups + node.Children.Sum(CountLookups);
}
