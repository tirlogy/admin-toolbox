// SPDX-License-Identifier: MIT
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace AdminToolbox.Core.Helpers;

public sealed record PasswordOptions(int Length = 20, bool Upper = true, bool Lower = true, bool Digits = true, bool Symbols = true, bool ExcludeAmbiguous = false);

public sealed record PassphraseOptions(int Words = 5, string Separator = "-", bool Capitalize = true, bool AppendNumber = true);

public enum Strength
{
    VeryWeak,
    Weak,
    Fair,
    Strong,
    VeryStrong,
}

/// <summary>Password and passphrase generator using a cryptographically secure RNG.</summary>
public static class PasswordGenerator
{
    public const int MinLength = 4;
    public const int MaxLength = 256;
    public const int MinWords = 3;
    public const int MaxWords = 12;

    private const string UpperChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private const string LowerChars = "abcdefghijklmnopqrstuvwxyz";
    private const string DigitChars = "0123456789";
    private const string SymbolChars = "!#$%&()*+,-./:;<=>?@[]^_{|}~";
    private const string Ambiguous = "Il1O0o|";

    private static readonly Lazy<IReadOnlyList<string>> WordList = new(LoadWords);

    /// <summary>EFF short word list (1296 words, CC BY 3.0 US).</summary>
    public static IReadOnlyList<string> Words => WordList.Value;

    public static string Generate(PasswordOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.Length is < MinLength or > MaxLength)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "length");
        }

        var classes = Classes(options);
        if (classes.Count == 0)
        {
            throw new ArgumentException("No character class selected", nameof(options));
        }

        var pool = string.Concat(classes);
        var chars = new char[options.Length];
        var i = 0;
        // one character from every selected class, the rest from the whole pool
        foreach (var cls in classes)
        {
            chars[i++] = cls[RandomNumberGenerator.GetInt32(cls.Length)];
        }

        for (; i < chars.Length; i++)
        {
            chars[i] = pool[RandomNumberGenerator.GetInt32(pool.Length)];
        }

        for (var j = chars.Length - 1; j > 0; j--)
        {
            var k = RandomNumberGenerator.GetInt32(j + 1);
            (chars[j], chars[k]) = (chars[k], chars[j]);
        }

        return new string(chars);
    }

    public static double EntropyBits(PasswordOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var pool = Classes(options).Sum(c => c.Length);
        return pool == 0 ? 0 : options.Length * Math.Log2(pool);
    }

    public static string GeneratePassphrase(PassphraseOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.Words is < MinWords or > MaxWords)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "words");
        }

        var words = Words;
        var parts = new List<string>(options.Words + 1);
        for (var i = 0; i < options.Words; i++)
        {
            var w = words[RandomNumberGenerator.GetInt32(words.Count)];
            parts.Add(options.Capitalize ? char.ToUpperInvariant(w[0]) + w[1..] : w);
        }

        if (options.AppendNumber)
        {
            parts.Add(RandomNumberGenerator.GetInt32(100).ToString(CultureInfo.InvariantCulture));
        }

        return string.Join(options.Separator ?? string.Empty, parts);
    }

    public static double PassphraseEntropyBits(PassphraseOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return (options.Words * Math.Log2(Words.Count)) + (options.AppendNumber ? Math.Log2(100) : 0);
    }

    /// <summary>Rating thresholds in bits, similar to common password managers.</summary>
    public static Strength Rate(double bits) => bits switch
    {
        < 28 => Strength.VeryWeak,
        < 36 => Strength.Weak,
        < 60 => Strength.Fair,
        < 80 => Strength.Strong,
        _ => Strength.VeryStrong,
    };

    private static List<string> Classes(PasswordOptions o)
    {
        var list = new List<string>();
        void AddClass(bool on, string chars)
        {
            if (!on)
            {
                return;
            }

            var filtered = o.ExcludeAmbiguous ? new string(chars.Where(c => !Ambiguous.Contains(c)).ToArray()) : chars;
            list.Add(filtered);
        }

        AddClass(o.Upper, UpperChars);
        AddClass(o.Lower, LowerChars);
        AddClass(o.Digits, DigitChars);
        AddClass(o.Symbols, SymbolChars);
        return list;
    }

    private static IReadOnlyList<string> LoadWords()
    {
        using var stream = typeof(PasswordGenerator).Assembly.GetManifestResourceStream("AdminToolbox.Core.wordlist.txt")
            ?? throw new InvalidOperationException("word list missing");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var words = new List<string>(1296);
        while (reader.ReadLine() is { } line)
        {
            if (line.Length > 0)
            {
                words.Add(line);
            }
        }

        return words;
    }
}
