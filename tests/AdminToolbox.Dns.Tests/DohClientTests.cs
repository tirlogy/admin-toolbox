// SPDX-License-Identifier: MIT
using System.Net;
using AdminToolbox.Core.Dns;

namespace AdminToolbox.Dns.Tests;

public class DohClientTests
{
    private const string TxtAnswer = """
        {"Status":0,"TC":false,"Answer":[{"name":"example.com.","type":16,"TTL":120,"data":"\"v=spf1 \" \"-all\""},{"name":"example.com.","type":16,"TTL":60,"data":"plain"}]}
        """;

    private static readonly Uri Primary = new("https://primary.test/dns-query");
    private static readonly Uri Fallback = new("https://fallback.test/resolve");

    private static DohOptions Options(TimeSpan? timeout = null) => new()
    {
        Endpoints = new[] { Primary, Fallback },
        Timeout = timeout ?? TimeSpan.FromSeconds(5),
    };

    [Fact]
    public async Task ParsesAndUnquotesTxt()
    {
        var handler = new StubHandler((_, _) => Task.FromResult(StubHandler.Json(TxtAnswer)));
        var client = new DohClient(new HttpClient(handler), Options());
        var answer = await client.QueryAsync("Example.COM.", DnsRecordType.TXT);
        Assert.Equal(new[] { "v=spf1 -all", "plain" }, answer.Data(DnsRecordType.TXT));
        Assert.Equal("example.com", answer.Records[0].Name);
        var uri = Assert.Single(handler.Requests);
        Assert.Equal("https://primary.test/dns-query?name=example.com&type=16", uri.ToString());
    }

    [Fact]
    public async Task CachesByTtlAndDeduplicates()
    {
        var now = new DateTimeOffset(2026, 11, 21, 12, 0, 0, TimeSpan.Zero);
        var gate = new TaskCompletionSource<HttpResponseMessage>();
        var handler = new StubHandler((_, _) => gate.Task);
        var client = new DohClient(new HttpClient(handler), Options(), () => now);
        var first = client.QueryAsync("example.com", DnsRecordType.TXT);
        var second = client.QueryAsync("EXAMPLE.com", DnsRecordType.TXT);
        gate.SetResult(StubHandler.Json(TxtAnswer));
        await Task.WhenAll(first, second);
        Assert.Equal(1, client.RequestCount);

        now = now.AddSeconds(59);
        gate = new TaskCompletionSource<HttpResponseMessage>();
        gate.SetResult(StubHandler.Json(TxtAnswer));
        await client.QueryAsync("example.com", DnsRecordType.TXT);
        Assert.Equal(1, client.RequestCount); // min TTL of the answer is 60 s

        now = now.AddSeconds(2);
        await client.QueryAsync("example.com", DnsRecordType.TXT);
        Assert.Equal(2, client.RequestCount);
    }

    [Fact]
    public async Task NegativeAnswersAreCachedBriefly()
    {
        var now = DateTimeOffset.UnixEpoch;
        var handler = new StubHandler((_, _) => Task.FromResult(StubHandler.Json("{\"Status\":3}")));
        var client = new DohClient(new HttpClient(handler), Options(), () => now);
        var answer = await client.QueryAsync("nope.example", DnsRecordType.A);
        Assert.True(answer.IsNxDomain);
        await client.QueryAsync("nope.example", DnsRecordType.A);
        Assert.Equal(1, client.RequestCount);
        now = now.AddSeconds(31);
        await client.QueryAsync("nope.example", DnsRecordType.A);
        Assert.Equal(2, client.RequestCount);
    }

    [Theory]
    [InlineData("http500")]
    [InlineData("servfail")]
    [InlineData("badjson")]
    [InlineData("network")]
    public async Task FallsBackToSecondEndpoint(string failure)
    {
        var handler = new StubHandler((req, _) =>
        {
            if (req.RequestUri!.Host == Primary.Host)
            {
                return failure switch
                {
                    "http500" => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)),
                    "servfail" => Task.FromResult(StubHandler.Json("{\"Status\":2}")),
                    "badjson" => Task.FromResult(StubHandler.Json("<html>")),
                    _ => throw new HttpRequestException("offline"),
                };
            }

            return Task.FromResult(StubHandler.Json(TxtAnswer));
        });
        var client = new DohClient(new HttpClient(handler), Options());
        var answer = await client.QueryAsync("example.com", DnsRecordType.TXT);
        Assert.Equal(2, answer.Records.Count);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(Fallback.Host, handler.Requests[1].Host);
    }

    [Fact]
    public async Task TimeoutOnAllEndpointsThrowsAndIsNotCached()
    {
        var calls = 0;
        var handler = new StubHandler(async (_, ct) =>
        {
            if (Interlocked.Increment(ref calls) <= 2)
            {
                await Task.Delay(Timeout.Infinite, ct);
            }

            return StubHandler.Json(TxtAnswer);
        });
        var client = new DohClient(new HttpClient(handler), Options(TimeSpan.FromMilliseconds(50)));
        var ex = await Assert.ThrowsAsync<DnsQueryException>(() => client.QueryAsync("example.com", DnsRecordType.TXT));
        Assert.Contains("timeout", ex.Message, StringComparison.Ordinal);
        var retry = await client.QueryAsync("example.com", DnsRecordType.TXT);
        Assert.Equal(2, retry.Records.Count);
    }

    [Fact]
    public async Task CallerCancellationDoesNotBreakSharedQuery()
    {
        var gate = new TaskCompletionSource<HttpResponseMessage>();
        var client = new DohClient(new HttpClient(new StubHandler((_, _) => gate.Task)), Options());
        using var cts = new CancellationTokenSource();
        var cancelled = client.QueryAsync("example.com", DnsRecordType.TXT, cts.Token);
        var other = client.QueryAsync("example.com", DnsRecordType.TXT);
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled);
        gate.SetResult(StubHandler.Json(TxtAnswer));
        Assert.Equal(2, (await other).Records.Count);
    }

    [Fact]
    public async Task CacheIsFlushedWhenFull()
    {
        var client = new DohClient(
            new HttpClient(new StubHandler((_, _) => Task.FromResult(StubHandler.Json(TxtAnswer)))),
            new DohOptions { Endpoints = new[] { Primary }, MaxCacheEntries = 2 });
        await client.QueryAsync("a.example", DnsRecordType.TXT);
        await client.QueryAsync("b.example", DnsRecordType.TXT);
        await client.QueryAsync("c.example", DnsRecordType.TXT);
        await client.QueryAsync("a.example", DnsRecordType.TXT);
        Assert.Equal(4, client.RequestCount);
    }

    [Fact]
    public void ValidatesInput()
    {
        var client = new DohClient(new HttpClient(new StubHandler((_, _) => Task.FromResult(StubHandler.Json("{}")))));
        Assert.Throws<ArgumentException>(() => { _ = client.QueryAsync("", DnsRecordType.A); });
        Assert.Throws<ArgumentException>(() => { _ = client.QueryAsync("a b.example", DnsRecordType.A); });
        Assert.Throws<ArgumentNullException>(() => { _ = client.QueryAsync(null!, DnsRecordType.A); });
        Assert.Throws<ArgumentNullException>(() => new DohClient(null!));
        Assert.Throws<ArgumentException>(() => new DohClient(new HttpClient(), new DohOptions { Endpoints = Array.Empty<Uri>() }));
    }

    [Fact]
    public void MapSkipsIncompleteRecords()
    {
        var answer = DohClient.Map(new DohResponse
        {
            Status = 0,
            Answer = new List<DohAnswer>
            {
                new() { Name = "x.example.", Type = 1, Ttl = 5, Data = "192.0.2.1" },
                new() { Name = null, Type = 1, Data = "192.0.2.2" },
                new() { Name = "x.example.", Type = 1, Data = null },
            },
        });
        Assert.Single(answer.Records);
        Assert.Empty(DohClient.Map(new DohResponse { Status = 3 }).Records);
    }
}
