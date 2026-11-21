// SPDX-License-Identifier: MIT
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace AdminToolbox.Core.Tests;

/// <summary>Guards that every finding code has a text in DE, EN and TR and that placeholders match.</summary>
public partial class LocalizationTests
{
    private static readonly string Root = FindRoot();

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AdminToolbox.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("repository root not found");
    }

    private static Dictionary<string, string> Load(string relative) =>
        XDocument.Load(Path.Combine(Root, relative)).Root!.Elements("data")
            .ToDictionary(d => d.Attribute("name")!.Value, d => d.Element("value")!.Value);

    [GeneratedRegex(@"""((?:spf|dns|mx|dkim|dmarc|bimi|mtasts|tlsrpt|header|hop|auth|spam|zone|jwt|cert)\.[a-z0-9.]+)""")]
    private static partial Regex CodePattern();

    [GeneratedRegex(@"\{(\d+)\}")]
    private static partial Regex Placeholder();

    [Theory]
    [InlineData("src/AdminToolbox.Tools/ToolText")]
    [InlineData("src/AdminToolbox.Web/AppText")]
    public void AllLanguagesHaveSameKeysAndPlaceholders(string baseName)
    {
        var en = Load(baseName + ".resx");
        foreach (var lang in new[] { ".de", ".tr" })
        {
            var other = Load(baseName + lang + ".resx");
            Assert.Equal(en.Keys.OrderBy(k => k), other.Keys.OrderBy(k => k));
            foreach (var (key, value) in en)
            {
                var a = Placeholder().Matches(value).Select(m => m.Value).Distinct().OrderBy(x => x);
                var b = Placeholder().Matches(other[key]).Select(m => m.Value).Distinct().OrderBy(x => x);
                Assert.True(a.SequenceEqual(b), $"{lang} {key}: placeholders differ");
                Assert.False(string.IsNullOrWhiteSpace(other[key]), $"{lang} {key} is empty");
            }
        }
    }

    [Fact]
    public void EveryFindingCodeIsTranslated()
    {
        var keys = Load("src/AdminToolbox.Tools/ToolText.resx");
        var codes = Directory.GetFiles(Path.Combine(Root, "src/AdminToolbox.Core"), "*.cs", SearchOption.AllDirectories)
            .SelectMany(f => CodePattern().Matches(File.ReadAllText(f)).Select(m => m.Groups[1].Value))
            .Where(c => !c.EndsWith('.') && c != "header.d") // header.d is an Authentication-Results property
            .Distinct()
            .ToList();
        Assert.True(codes.Count > 80, "code scan found too few codes");
        var missing = codes.Where(c => !keys.ContainsKey(c)).ToList();
        Assert.Empty(missing);
    }

    [Fact]
    public void GermanTextsFollowStyleRules()
    {
        foreach (var file in new[] { "src/AdminToolbox.Tools/ToolText.de.resx", "src/AdminToolbox.Web/AppText.de.resx" })
        {
            foreach (var (key, value) in Load(file))
            {
                Assert.DoesNotContain(" – ", value);
                Assert.DoesNotContain(" - ", value);
                Assert.False(Regex.IsMatch(value, @"\bAI\b"), $"{key} uses AI instead of KI");
            }
        }
    }
}
