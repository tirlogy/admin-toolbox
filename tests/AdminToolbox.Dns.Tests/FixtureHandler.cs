// SPDX-License-Identifier: MIT
using System.Net;
using System.Text;
using System.Web;

namespace AdminToolbox.Dns.Tests;

/// <summary>
/// Replays recorded DoH responses from Fixtures/. With RECORD_DOH=1 it forwards to the real
/// Cloudflare endpoint and stores the answers (used once to create the fixtures).
/// </summary>
internal sealed class FixtureHandler : HttpMessageHandler
{
    private static readonly string SourceDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../Fixtures"));
    private static readonly string OutputDir = Path.Combine(AppContext.BaseDirectory, "Fixtures");
    private readonly bool _record = Environment.GetEnvironmentVariable("RECORD_DOH") == "1";
    private readonly HttpClient? _real;

    public FixtureHandler()
    {
        if (_record)
        {
            _real = new HttpClient();
        }
    }

    public List<string> Missing { get; } = new();

    public static string FileName(string name, string type) => name.ToLowerInvariant().TrimEnd('.') + "_" + type + ".json";

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var query = HttpUtility.ParseQueryString(request.RequestUri!.Query);
        var file = FileName(query["name"]!, query["type"]!);
        if (_record)
        {
            var real = new HttpRequestMessage(HttpMethod.Get, request.RequestUri);
            real.Headers.Accept.ParseAdd("application/dns-json");
            var response = await _real!.SendAsync(real, cancellationToken).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            await File.WriteAllTextAsync(Path.Combine(SourceDir, file), body, cancellationToken).ConfigureAwait(false);
            return Json(body);
        }

        var path = Path.Combine(OutputDir, file);
        if (!File.Exists(path))
        {
            Missing.Add(file);
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        return Json(await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false));
    }

    private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/dns-json"),
    };

    protected override void Dispose(bool disposing)
    {
        _real?.Dispose();
        base.Dispose(disposing);
    }
}

/// <summary>Programmable handler for error and fallback tests.</summary>
internal sealed class StubHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _respond;

    public StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) => _respond = respond;

    public List<Uri> Requests { get; } = new();

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        lock (Requests)
        {
            Requests.Add(request.RequestUri!);
        }

        return _respond(request, cancellationToken);
    }

    public static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/dns-json"),
    };
}
