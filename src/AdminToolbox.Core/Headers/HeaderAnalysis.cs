// SPDX-License-Identifier: MIT
using AdminToolbox.Core.Mail;

namespace AdminToolbox.Core.Headers;

public sealed record HeaderField(string Name, string Value);

/// <summary>One relay step from a Received header. Index 1 is the oldest hop.</summary>
public sealed record ReceivedHop(int Index, string? From, string? By, string? With, string? Ip, DateTimeOffset? Date, TimeSpan? Delay, string Raw);

/// <summary>One method result from Authentication-Results, ARC-Authentication-Results or Received-SPF.</summary>
public sealed record AuthResult(string Source, string? AuthServId, string Method, string Result, IReadOnlyDictionary<string, string> Properties);

/// <summary>A spam-filter header with its interpreted verdict.</summary>
public sealed record SpamIndicator(string Header, string Value, Severity Severity);

/// <summary>Result of the mail header analysis.</summary>
public sealed class HeaderAnalysis : FindingList
{
    public IReadOnlyList<HeaderField> Fields { get; internal set; } = Array.Empty<HeaderField>();

    public string? From { get; internal set; }

    public string? To { get; internal set; }

    public string? Subject { get; internal set; }

    public string? Date { get; internal set; }

    public string? MessageId { get; internal set; }

    public string? ReturnPath { get; internal set; }

    public string? ReplyTo { get; internal set; }

    /// <summary>Hops in chronological order (oldest first).</summary>
    public IReadOnlyList<ReceivedHop> Hops { get; internal set; } = Array.Empty<ReceivedHop>();

    public TimeSpan? TotalDelay { get; internal set; }

    public IReadOnlyList<AuthResult> AuthResults { get; internal set; } = Array.Empty<AuthResult>();

    /// <summary>SPF, DKIM and DMARC verdict of the receiving system (topmost Authentication-Results).</summary>
    public IReadOnlyDictionary<string, string> Verdicts { get; internal set; } = new Dictionary<string, string>();

    public IReadOnlyList<SpamIndicator> Spam { get; internal set; } = Array.Empty<SpamIndicator>();
}
