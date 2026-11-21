// SPDX-License-Identifier: MIT
using AdminToolbox.Core.Headers;
using AdminToolbox.Core.Mail;

namespace AdminToolbox.Core.Tests;

public class HeaderTests
{
    private static string Sample(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Samples", name));

    [Fact]
    public void Gmail()
    {
        var a = MailHeaderParser.Analyze(Sample("gmail.txt"));
        Assert.Equal("Example Newsletter <news@example.org>", a.From);
        Assert.Equal("Ihr Newsletter im November", a.Subject);
        Assert.Equal("<news@example.org>", a.ReturnPath);
        Assert.Equal(3, a.Hops.Count); // the "Received" line in the body is ignored
        Assert.Equal("localhost", a.Hops[0].From);
        Assert.Equal("mail.example.org", a.Hops[0].By);
        Assert.Equal("127.0.0.1", a.Hops[0].Ip);
        Assert.Equal("192.0.2.10", a.Hops[1].Ip);
        Assert.Equal("mx.google.com", a.Hops[1].By);
        Assert.Equal("ESMTPS", a.Hops[1].With);
        Assert.Null(a.Hops[2].From);
        Assert.Equal(TimeSpan.FromSeconds(3), a.Hops[1].Delay);
        Assert.Equal(TimeSpan.FromSeconds(4), a.TotalDelay);
        Assert.Equal("pass", a.Verdicts["spf"]);
        Assert.Equal("pass", a.Verdicts["dkim"]);
        Assert.Equal("pass", a.Verdicts["dmarc"]);
        Assert.Contains(a.AuthResults, r => r.Source == "ARC-Authentication-Results" && r.AuthServId == "mx.google.com");
        var dkim = a.AuthResults.First(r => r.Method == "dkim");
        Assert.Equal("s1", dkim.Properties["header.s"]);
        Assert.DoesNotContain(a.Findings, f => f.Severity >= Severity.Warning);
        Assert.True(a.Has("auth.pass"));
    }

    [Fact]
    public void Outlook()
    {
        var a = MailHeaderParser.Analyze(Sample("outlook.txt"));
        Assert.Equal(4, a.Hops.Count);
        Assert.Equal("mail.example.net", a.Hops[0].From);
        Assert.Equal("198.51.100.23", a.Hops[0].Ip);
        Assert.Equal("2603:10a6:10:3b:cafe::aa", a.Hops[1].Ip);
        Assert.Equal(new DateTimeOffset(2026, 11, 18, 8, 15, 31, TimeSpan.Zero), a.Hops[3].Date);
        Assert.Equal(TimeSpan.FromSeconds(2), a.TotalDelay);
        Assert.Equal("softfail", a.Verdicts["spf"]);
        Assert.Equal("none", a.Verdicts["dkim"]);
        Assert.Equal("fail", a.Verdicts["dmarc"]);
        Assert.Equal("fail", a.Verdicts["compauth"]);
        Assert.Null(a.AuthResults.First().AuthServId);
        Assert.Contains(a.AuthResults, r => r.Source == "Received-SPF" && r.Result == "softfail");
        Assert.True(a.Has("spam.flagged"));
        Assert.All(a.Spam.Where(s => s.Header != "X-Microsoft-Antispam"), s => Assert.Equal(Severity.Warning, s.Severity));
        Assert.True(a.Has("header.returnpath.mismatch"));
        Assert.True(a.Has("header.replyto.mismatch"));
        Assert.True(a.Has("header.from.displayname"));
        Assert.True(a.Has("header.messageid.domain"));
        Assert.Contains(a.Findings, f => f.Code == "auth.notpass" && f.Args[0] == "DMARC" && f.Severity == Severity.Error);
        Assert.Contains(a.Findings, f => f.Code == "auth.notpass" && f.Args[0] == "SPF" && f.Severity == Severity.Warning);
    }

    [Fact]
    public void Ionos()
    {
        var a = MailHeaderParser.Analyze(Sample("ionos.txt"));
        Assert.Equal(2, a.Hops.Count);
        Assert.Equal("203.0.113.45", a.Hops[0].Ip);
        Assert.Equal("212.227.126.131", a.Hops[1].Ip);
        Assert.Equal("ESMTPSA", a.Hops[0].With);
        Assert.True(a.Has("hop.slow"));
        Assert.True(a.Has("spam.clean"));
        Assert.True(a.Has("header.dkim.notaligned"));
        Assert.Equal("mx.kundenserver.de", a.AuthResults[0].AuthServId);
        Assert.Equal(3, a.Spam.Count);
    }

    [Fact]
    public void EmptyAndMinimal()
    {
        Assert.True(MailHeaderParser.Analyze("   \n\n").Has("header.empty"));
        var a = MailHeaderParser.Analyze("Subject: hi\r\nX-Spam-Status: Yes, score=9.1\r\nX-Gm-Spam: 1\r\nX-Spam-Score: 9.1\r\n");
        Assert.True(a.Has("header.from.missing"));
        Assert.True(a.Has("header.messageid.missing"));
        Assert.True(a.Has("header.date.missing"));
        Assert.True(a.Has("hop.none"));
        Assert.True(a.Has("auth.none"));
        Assert.True(a.Has("spam.flagged"));
        Assert.Equal(Severity.Info, a.Spam.Single(s => s.Header == "X-Spam-Score").Severity);
    }

    [Fact]
    public void ClockSkewAndMissingMethods()
    {
        var raw = "Received: from b.example by c.example; Tue, 17 Nov 2026 10:00:00 +0000\n" +
                  "Received: from a.example by b.example; Tue, 17 Nov 2026 10:05:00 +0000\n" +
                  "Authentication-Results: c.example; spf=neutral smtp.mailfrom=x@a.example; none\n" +
                  "From: x@a.example\nDate: x\nMessage-ID: <1@a.example>\n";
        var a = MailHeaderParser.Analyze(raw);
        Assert.True(a.Has("hop.clockskew"));
        Assert.Contains(a.Findings, f => f.Code == "auth.missing" && f.Args[0] == "DKIM");
        Assert.Contains(a.Findings, f => f.Code == "auth.notpass" && f.Severity == Severity.Info);
    }

    [Theory]
    [InlineData("Name <a@b.example>", "a@b.example")]
    [InlineData("a@b.example", "a@b.example")]
    [InlineData("<>", null)]
    [InlineData("", null)]
    [InlineData("undisclosed-recipients:;", null)]
    public void ExtractAddress(string header, string? expected) => Assert.Equal(expected, MailHeaderParser.ExtractAddress(header));

    [Theory]
    [InlineData("Tue, 17 Nov 2026 10:02:00 +0100", "2026-11-17T09:02:00Z")]
    [InlineData("17 Nov 2026 10:02:00 -0000", "2026-11-17T10:02:00Z")]
    [InlineData("Tue, 17 Nov 26 10:02 GMT", "2026-11-17T10:02:00Z")]
    [InlineData("Tue, 17 Nov 2026 01:02:05 -0800 (PST)", "2026-11-17T09:02:05Z")]
    [InlineData("Tue, 17 Nov 2026 01:02:05.123 PDT", "2026-11-17T08:02:05Z")]
    [InlineData("Tue Nov 17 10:02:00 2026", "2026-11-17T10:02:00Z")]
    [InlineData("17 Nov 98 10:02:00 XYZ", "1998-11-17T10:02:00Z")]
    public void RfcDate_Parses(string text, string expectedUtc)
    {
        Assert.True(RfcDate.TryParse(text, out var d));
        Assert.Equal(DateTimeOffset.Parse(expectedUtc, System.Globalization.CultureInfo.InvariantCulture), d.ToUniversalTime());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("yesterday")]
    [InlineData("32 Nov 2026 10:00:00 +0000")]
    [InlineData("17 Foo 2026 10:00:00")]
    [InlineData("17 Nov 2026 xx:00")]
    [InlineData("17 Nov 2026 10:00:zz")]
    [InlineData("Nov 17 10:00:00")]
    public void RfcDate_Rejects(string? text) => Assert.False(RfcDate.TryParse(text, out _));
}
