// SPDX-License-Identifier: MIT
using System.Globalization;
using System.Numerics;
using AdminToolbox.Core.Mail;

namespace AdminToolbox.Tools.Helpers;

/// <summary>Display helpers shared by the tool pages.</summary>
public static class Format
{
    public static string Css(Severity s) => s switch
    {
        Severity.Ok => "ok",
        Severity.Info => "info",
        Severity.Warning => "warn",
        _ => "error",
    };

    public static string Number(BigInteger n) => n.ToString("N0", CultureInfo.CurrentCulture);

    public static string Time(DateTimeOffset? t) =>
        t is null ? "–" : t.Value.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture);

    public static string Duration(TimeSpan? d)
    {
        if (d is null)
        {
            return "–";
        }

        var v = d.Value;
        var sign = v < TimeSpan.Zero ? "-" : "+";
        v = v.Duration();
        return v.TotalHours >= 1
            ? string.Create(CultureInfo.InvariantCulture, $"{sign}{(int)v.TotalHours} h {v.Minutes} min")
            : v.TotalMinutes >= 1
                ? string.Create(CultureInfo.InvariantCulture, $"{sign}{(int)v.TotalMinutes} min {v.Seconds} s")
                : string.Create(CultureInfo.InvariantCulture, $"{sign}{v.Seconds} s");
    }

    public static object[] Args(Finding f) => f.Args.Cast<object>().ToArray();
}
