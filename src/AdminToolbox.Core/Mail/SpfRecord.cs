// SPDX-License-Identifier: MIT
using System.Net;
using System.Net.Sockets;

namespace AdminToolbox.Core.Mail;

public enum SpfQualifier
{
    Pass,
    Fail,
    SoftFail,
    Neutral,
}

public enum SpfTermKind
{
    All,
    Include,
    A,
    Mx,
    Ptr,
    Ip4,
    Ip6,
    Exists,
    Redirect,
    Exp,
    UnknownModifier,
}

/// <summary>One mechanism or modifier of an SPF record.</summary>
public sealed record SpfTerm(SpfQualifier Qualifier, SpfTermKind Kind, string? Value, string Raw)
{
    /// <summary>Terms that need a DNS lookup and count against the limit of 10 (RFC 7208, 4.6.4).</summary>
    public bool CountsAsLookup => Kind is SpfTermKind.Include or SpfTermKind.A or SpfTermKind.Mx
        or SpfTermKind.Ptr or SpfTermKind.Exists or SpfTermKind.Redirect;

    /// <summary>Domain this term points to, without CIDR suffix. Null when the current domain is meant.</summary>
    public string? TargetDomain
    {
        get
        {
            if (Value is null || Kind is SpfTermKind.Ip4 or SpfTermKind.Ip6 or SpfTermKind.All or SpfTermKind.Exp or SpfTermKind.UnknownModifier)
            {
                return null;
            }

            var slash = Value.IndexOf('/');
            var d = slash >= 0 ? Value[..slash] : Value;
            return d.Length == 0 ? null : d.TrimEnd('.').ToLowerInvariant();
        }
    }
}

/// <summary>Parsed SPF record (RFC 7208).</summary>
public sealed class SpfRecord
{
    private SpfRecord(string text, IReadOnlyList<SpfTerm> terms, IReadOnlyList<string> errors)
    {
        Text = text;
        Terms = terms;
        Errors = errors;
    }

    public string Text { get; }

    public IReadOnlyList<SpfTerm> Terms { get; }

    /// <summary>Raw terms that could not be parsed.</summary>
    public IReadOnlyList<string> Errors { get; }

    public static bool IsSpf(string txt) =>
        txt.Equals("v=spf1", StringComparison.OrdinalIgnoreCase) || txt.StartsWith("v=spf1 ", StringComparison.OrdinalIgnoreCase);

    public static SpfRecord Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var terms = new List<SpfTerm>();
        var errors = new List<string>();
        var tokens = text.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        for (var i = 1; i < tokens.Length; i++)
        {
            var term = ParseTerm(tokens[i]);
            if (term is null)
            {
                errors.Add(tokens[i]);
            }
            else
            {
                terms.Add(term);
            }
        }

        return new SpfRecord(text, terms, errors);
    }

    private static SpfTerm? ParseTerm(string raw)
    {
        var eq = raw.IndexOf('=');
        var colon = raw.IndexOf(':');
        var slash = raw.IndexOf('/');
        if (eq > 0 && (colon < 0 || eq < colon) && (slash < 0 || eq < slash))
        {
            var name = raw[..eq].ToLowerInvariant();
            var value = raw[(eq + 1)..];
            return name switch
            {
                "redirect" => value.Length > 0 ? new SpfTerm(SpfQualifier.Pass, SpfTermKind.Redirect, value, raw) : null,
                "exp" => value.Length > 0 ? new SpfTerm(SpfQualifier.Pass, SpfTermKind.Exp, value, raw) : null,
                _ => IsModifierName(name) ? new SpfTerm(SpfQualifier.Pass, SpfTermKind.UnknownModifier, value, raw) : null,
            };
        }

        var qualifier = SpfQualifier.Pass;
        var body = raw;
        switch (raw[0])
        {
            case '+': qualifier = SpfQualifier.Pass; body = raw[1..]; break;
            case '-': qualifier = SpfQualifier.Fail; body = raw[1..]; break;
            case '~': qualifier = SpfQualifier.SoftFail; body = raw[1..]; break;
            case '?': qualifier = SpfQualifier.Neutral; body = raw[1..]; break;
        }

        var sep = body.IndexOfAny(new[] { ':', '/' });
        var mech = (sep >= 0 ? body[..sep] : body).ToLowerInvariant();
        string? arg = sep >= 0 ? body[sep..] : null;
        string? domainArg = arg is not null && arg.StartsWith(':') ? arg[1..] : arg;

        switch (mech)
        {
            case "all":
                return arg is null ? new SpfTerm(qualifier, SpfTermKind.All, null, raw) : null;
            case "include":
            case "exists":
                if (arg is null || !arg.StartsWith(':') || domainArg!.Length == 0)
                {
                    return null;
                }

                return new SpfTerm(qualifier, mech == "include" ? SpfTermKind.Include : SpfTermKind.Exists, domainArg, raw);
            case "a":
            case "mx":
                if (arg is not null && !ValidDualCidr(arg))
                {
                    return null;
                }

                return new SpfTerm(qualifier, mech == "a" ? SpfTermKind.A : SpfTermKind.Mx, domainArg, raw);
            case "ptr":
                if (arg is not null && (!arg.StartsWith(':') || domainArg!.Length == 0))
                {
                    return null;
                }

                return new SpfTerm(qualifier, SpfTermKind.Ptr, domainArg, raw);
            case "ip4":
                return arg is not null && arg.StartsWith(':') && ValidIp(domainArg!, AddressFamily.InterNetwork, 32)
                    ? new SpfTerm(qualifier, SpfTermKind.Ip4, domainArg, raw) : null;
            case "ip6":
                return arg is not null && arg.StartsWith(':') && ValidIp(domainArg!, AddressFamily.InterNetworkV6, 128)
                    ? new SpfTerm(qualifier, SpfTermKind.Ip6, domainArg, raw) : null;
            default:
                return null;
        }
    }

    private static bool IsModifierName(string name) =>
        name.Length > 0 && char.IsAsciiLetter(name[0]) && name.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.');

    private static bool ValidDualCidr(string arg)
    {
        // forms: ":domain", ":domain/24", "/24", "//64", ":domain/24//64"
        var s = arg.StartsWith(':') ? arg[1..] : arg;
        var slash = s.IndexOf('/');
        var domain = slash >= 0 ? s[..slash] : s;
        if (arg.StartsWith(':') && domain.Length == 0)
        {
            return false;
        }

        if (slash < 0)
        {
            return true;
        }

        var cidr = s[slash..];
        var parts = cidr.Split("//");
        if (parts[0].Length > 0 && !ValidPrefix(parts[0].TrimStart('/'), 32))
        {
            return false;
        }

        return parts.Length < 2 || ValidPrefix(parts[1], 128);
    }

    private static bool ValidPrefix(string s, int max) =>
        int.TryParse(s, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var p) && p >= 0 && p <= max;

    private static bool ValidIp(string value, AddressFamily family, int maxPrefix)
    {
        var slash = value.IndexOf('/');
        var ip = slash >= 0 ? value[..slash] : value;
        if (slash >= 0 && !ValidPrefix(value[(slash + 1)..], maxPrefix))
        {
            return false;
        }

        if (family == AddressFamily.InterNetwork && ip.Split('.').Length != 4)
        {
            return false;
        }

        return IPAddress.TryParse(ip, out var addr) && addr.AddressFamily == family;
    }
}
