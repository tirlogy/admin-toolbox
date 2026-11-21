// SPDX-License-Identifier: MIT
namespace AdminToolbox.Core.Mail;

/// <summary>Traffic-light level of a finding. Ordered by severity.</summary>
public enum Severity
{
    Ok = 0,
    Info = 1,
    Warning = 2,
    Error = 3,
}

/// <summary>
/// A single result line. <see cref="Code"/> is a stable key the UI maps to a localized explanation
/// and suggestion; <see cref="Args"/> are inserted into the text.
/// </summary>
public sealed class Finding
{
    public Finding(string code, Severity severity, params string[] args)
    {
        Code = code;
        Severity = severity;
        Args = args;
    }

    public string Code { get; }

    public Severity Severity { get; }

    public IReadOnlyList<string> Args { get; }

    public override string ToString() => $"{Severity} {Code} [{string.Join(", ", Args)}]";
}

/// <summary>Collects findings and exposes the worst severity.</summary>
public class FindingList
{
    private readonly List<Finding> _findings = new();

    public IReadOnlyList<Finding> Findings => _findings;

    public Severity Severity => _findings.Count == 0 ? Severity.Ok : _findings.Max(f => f.Severity);

    public bool Has(string code) => _findings.Any(f => f.Code == code);

    public void Add(string code, Severity severity, params string[] args) => _findings.Add(new Finding(code, severity, args));

    public void AddRange(IEnumerable<Finding> findings) => _findings.AddRange(findings);
}
