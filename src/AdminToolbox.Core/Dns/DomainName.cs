// SPDX-License-Identifier: MIT
using System.Globalization;

namespace AdminToolbox.Core.Dns;

/// <summary>Validation and normalization of user-entered domain names.</summary>
public static class DomainName
{
    // shortcut: small static list instead of the full Public Suffix List; upgrade if users report wrong org domains.
    private static readonly HashSet<string> MultiLabelSuffixes = new(StringComparer.Ordinal)
    {
        "co.uk", "org.uk", "ac.uk", "gov.uk", "me.uk", "ltd.uk", "plc.uk",
        "com.tr", "net.tr", "org.tr", "gov.tr", "edu.tr", "gen.tr", "biz.tr", "info.tr", "k12.tr", "av.tr", "bel.tr",
        "com.au", "net.au", "org.au", "edu.au", "gov.au",
        "co.at", "or.at", "gv.at", "ac.at",
        "co.jp", "ne.jp", "or.jp", "ac.jp",
        "com.br", "net.br", "org.br",
        "co.nz", "org.nz", "co.za", "com.cn", "com.mx", "co.in", "co.il", "com.sg", "com.hk",
    };

    /// <summary>
    /// Accepts a domain, an e-mail address or a URL and returns the lower-case ASCII domain.
    /// </summary>
    public static bool TryNormalize(string? input, out string domain)
    {
        domain = string.Empty;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var s = input.Trim();
        var scheme = s.IndexOf("://", StringComparison.Ordinal);
        if (scheme >= 0)
        {
            s = s[(scheme + 3)..];
        }

        var at = s.LastIndexOf('@');
        if (at >= 0)
        {
            s = s[(at + 1)..];
        }

        var cut = s.IndexOfAny(new[] { '/', '?', '#', ':', ' ' });
        if (cut >= 0)
        {
            s = s[..cut];
        }

        s = s.TrimEnd('.');
        if (s.Length == 0)
        {
            return false;
        }

        if (s.Any(c => c > 127))
        {
            try
            {
                s = new IdnMapping().GetAscii(s);
            }
            catch (Exception ex) when (ex is ArgumentException or PlatformNotSupportedException)
            {
                return false;
            }
        }

        s = s.ToLowerInvariant();
        if (s.Length > 253)
        {
            return false;
        }

        var labels = s.Split('.');
        if (labels.Length < 2)
        {
            return false;
        }

        foreach (var label in labels)
        {
            if (label.Length is 0 or > 63 || label[0] == '-' || label[^1] == '-')
            {
                return false;
            }

            if (!label.All(c => c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-' or '_'))
            {
                return false;
            }
        }

        domain = s;
        return true;
    }

    /// <summary>Approximates the organizational domain (registrable domain) of a host name.</summary>
    public static string OrganizationalDomain(string name)
    {
        var n = DnsText.NormalizeName(name);
        var labels = n.Split('.');
        if (labels.Length <= 2)
        {
            return n;
        }

        var lastTwo = labels[^2] + "." + labels[^1];
        var take = MultiLabelSuffixes.Contains(lastTwo) ? 3 : 2;
        return string.Join('.', labels[^take..]);
    }
}
