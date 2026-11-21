// SPDX-License-Identifier: MIT
using System.Text.Json.Serialization;

namespace AdminToolbox.Dns;

/// <summary>DoH JSON response (RFC 8427 style, as served by Cloudflare and Google).</summary>
internal sealed class DohResponse
{
    [JsonPropertyName("Status")]
    public int Status { get; set; }

    [JsonPropertyName("Answer")]
    public List<DohAnswer>? Answer { get; set; }
}

internal sealed class DohAnswer
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("type")]
    public int Type { get; set; }

    [JsonPropertyName("TTL")]
    public int Ttl { get; set; }

    [JsonPropertyName("data")]
    public string? Data { get; set; }
}

/// <summary>Source-generated serializer context, keeps the client trim-safe.</summary>
[JsonSerializable(typeof(DohResponse))]
internal sealed partial class DohJsonContext : JsonSerializerContext
{
}
