// SPDX-License-Identifier: MIT
namespace AdminToolbox.Core.Mail;

/// <summary>Parser for <c>tag=value; tag=value</c> records (DKIM, DMARC, BIMI, MTA-STS, TLS-RPT).</summary>
public sealed class TagList
{
    private readonly Dictionary<string, string> _tags = new(StringComparer.OrdinalIgnoreCase);

    private TagList()
    {
    }

    /// <summary>Tags in record order.</summary>
    public IReadOnlyList<KeyValuePair<string, string>> Ordered { get; private set; } = Array.Empty<KeyValuePair<string, string>>();

    /// <summary>Tags that appeared more than once.</summary>
    public IReadOnlyList<string> Duplicates { get; private set; } = Array.Empty<string>();

    /// <summary>Parts without '=' (syntax errors).</summary>
    public IReadOnlyList<string> Invalid { get; private set; } = Array.Empty<string>();

    public string? this[string tag] => _tags.TryGetValue(tag, out var v) ? v : null;

    public bool Contains(string tag) => _tags.ContainsKey(tag);

    public static TagList Parse(string record)
    {
        ArgumentNullException.ThrowIfNull(record);
        var list = new TagList();
        var ordered = new List<KeyValuePair<string, string>>();
        var duplicates = new List<string>();
        var invalid = new List<string>();
        foreach (var part in record.Split(';'))
        {
            var p = part.Trim();
            if (p.Length == 0)
            {
                continue;
            }

            var eq = p.IndexOf('=');
            if (eq <= 0)
            {
                invalid.Add(p);
                continue;
            }

            var key = p[..eq].Trim();
            // DKIM allows folding whitespace inside values (e.g. split keys); remove it.
            var value = p[(eq + 1)..].Trim();
            if (!list._tags.TryAdd(key, value))
            {
                duplicates.Add(key);
                continue;
            }

            ordered.Add(new KeyValuePair<string, string>(key, value));
        }

        list.Ordered = ordered;
        list.Duplicates = duplicates;
        list.Invalid = invalid;
        return list;
    }
}
