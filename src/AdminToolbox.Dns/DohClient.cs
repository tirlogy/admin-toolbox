// SPDX-License-Identifier: MIT
using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using AdminToolbox.Core.Dns;

namespace AdminToolbox.Dns;

/// <summary>Options of the DoH client.</summary>
public sealed class DohOptions
{
    /// <summary>JSON API endpoints, tried in order. Both send CORS headers (see docs/adr/0002).</summary>
    public IReadOnlyList<Uri> Endpoints { get; init; } = new[]
    {
        new Uri("https://cloudflare-dns.com/dns-query"),
        new Uri("https://dns.google/resolve"),
    };

    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(5);

    public TimeSpan MinCacheTtl { get; init; } = TimeSpan.FromSeconds(5);

    public TimeSpan MaxCacheTtl { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>Cache time for answers without records (NXDOMAIN, NODATA).</summary>
    public TimeSpan NegativeCacheTtl { get; init; } = TimeSpan.FromSeconds(30);

    public int MaxCacheEntries { get; init; } = 1000;
}

/// <summary>
/// DNS-over-HTTPS resolver using the JSON API. Identical concurrent queries share one request,
/// answers are cached by TTL, and the next endpoint is tried on errors, SERVFAIL or timeouts.
/// </summary>
public sealed class DohClient : IDnsResolver
{
    private readonly HttpClient _http;
    private readonly DohOptions _options;
    private readonly Func<DateTimeOffset> _clock;
    private readonly Dictionary<(string, DnsRecordType), CacheEntry> _cache = new();
    private readonly object _lock = new();

    public DohClient(HttpClient http, DohOptions? options = null, Func<DateTimeOffset>? clock = null)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _options = options ?? new DohOptions();
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
        if (_options.Endpoints.Count == 0)
        {
            throw new ArgumentException("At least one endpoint is required", nameof(options));
        }
    }

    /// <summary>Number of HTTP requests sent (for diagnostics and tests).</summary>
    public int RequestCount { get; private set; }

    public Task<DnsAnswer> QueryAsync(string name, DnsRecordType type, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(name);
        var normalized = DnsText.NormalizeName(name);
        if (normalized.Length is 0 or > 253 || normalized.Any(c => c <= ' ' || c > '~'))
        {
            throw new ArgumentException("Invalid DNS name", nameof(name));
        }

        var key = (normalized, type);
        CacheEntry entry;
        lock (_lock)
        {
            if (!_cache.TryGetValue(key, out entry!) || entry.Expires <= _clock())
            {
                if (_cache.Count >= _options.MaxCacheEntries)
                {
                    _cache.Clear(); // shortcut: no LRU, a full flush is fine for a browser session
                }

                entry = new CacheEntry();
                entry.Task = FetchAsync(key, entry);
                _cache[key] = entry;
            }
        }

        return entry.Task.WaitAsync(cancellationToken);
    }

    private async Task<DnsAnswer> FetchAsync((string Name, DnsRecordType Type) key, CacheEntry entry)
    {
        await Task.Yield(); // make sure the entry is stored before a synchronous failure removes it
        try
        {
            var answer = await QueryUncachedAsync(key.Name, key.Type).ConfigureAwait(false);
            entry.Expires = _clock() + CacheTime(answer);
            return answer;
        }
        catch
        {
            lock (_lock)
            {
                if (_cache.TryGetValue(key, out var current) && ReferenceEquals(current, entry))
                {
                    _cache.Remove(key);
                }
            }

            throw;
        }
    }

    private TimeSpan CacheTime(DnsAnswer answer)
    {
        if (answer.Records.Count == 0)
        {
            return _options.NegativeCacheTtl;
        }

        var ttl = TimeSpan.FromSeconds(answer.Records.Min(r => r.Ttl));
        return ttl < _options.MinCacheTtl ? _options.MinCacheTtl : ttl > _options.MaxCacheTtl ? _options.MaxCacheTtl : ttl;
    }

    private async Task<DnsAnswer> QueryUncachedAsync(string name, DnsRecordType type)
    {
        Exception? last = null;
        foreach (var endpoint in _options.Endpoints)
        {
            using var cts = new CancellationTokenSource(_options.Timeout);
            try
            {
                var url = new Uri(endpoint, "?name=" + Uri.EscapeDataString(name) + "&type=" + ((int)type).ToString(CultureInfo.InvariantCulture));
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/dns-json"));
                RequestCount++;
                using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseContentRead, cts.Token).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    last = new DnsQueryException(endpoint.Host + ": HTTP " + ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture));
                    continue;
                }

                var stream = await response.Content.ReadAsStreamAsync(cts.Token).ConfigureAwait(false);
                var dto = await JsonSerializer.DeserializeAsync(stream, DohJsonContext.Default.DohResponse, cts.Token).ConfigureAwait(false);
                if (dto is null || dto.Status == 2)
                {
                    last = new DnsQueryException(endpoint.Host + ": SERVFAIL");
                    continue;
                }

                return Map(dto);
            }
            catch (OperationCanceledException ex)
            {
                last = new DnsQueryException(endpoint.Host + ": timeout", ex);
            }
            catch (HttpRequestException ex)
            {
                last = new DnsQueryException(endpoint.Host + ": " + ex.Message, ex);
            }
            catch (JsonException ex)
            {
                last = new DnsQueryException(endpoint.Host + ": invalid JSON", ex);
            }
        }

        throw last as DnsQueryException ?? new DnsQueryException("DNS query failed", last);
    }

    internal static DnsAnswer Map(DohResponse dto)
    {
        var records = new List<DnsRecord>();
        foreach (var a in dto.Answer ?? new List<DohAnswer>())
        {
            if (a.Name is null || a.Data is null)
            {
                continue;
            }

            var type = (DnsRecordType)a.Type;
            var data = type == DnsRecordType.TXT ? DnsText.UnquoteTxt(a.Data) : a.Data;
            records.Add(new DnsRecord(DnsText.NormalizeName(a.Name), type, a.Ttl, data));
        }

        return new DnsAnswer(dto.Status, records);
    }

    private sealed class CacheEntry
    {
        public Task<DnsAnswer> Task { get; set; } = null!;

        public DateTimeOffset Expires { get; set; } = DateTimeOffset.MaxValue;
    }
}
