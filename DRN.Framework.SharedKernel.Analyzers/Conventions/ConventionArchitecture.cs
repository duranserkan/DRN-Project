using System.Collections.Immutable;

namespace DRN.Framework.SharedKernel.Analyzers.Conventions;

internal static class ConventionArchitecture
{
    private sealed record Dependencies(ImmutableArray<string> Allowed, ImmutableArray<string> Required);

    // Required roles have minimum cardinality one. Sharing and additional allowed edges are unrestricted.
    private static readonly Dictionary<string, Dependencies> Rules = new(StringComparer.Ordinal)
    {
        ["SharedKernel"] = new([], []),
        ["Contract"] = new(["SharedKernel"], []),
        ["Utils"] = new(["SharedKernel"], []),
        ["Domain"] = new(["Contract", "SharedKernel", "Utils"], ["Contract"]),
        ["Infra"] = new(["Domain", "Contract", "SharedKernel", "Utils"], ["Domain"]),
        ["Application"] = new(["Domain", "Contract", "Infra", "SharedKernel", "Utils"], []),
        ["Hosted"] = new(["Domain", "Contract", "Infra", "Application", "SharedKernel", "Utils"], ["Application", "Infra"])
    };

    internal static bool IsRole(string role) => Rules.ContainsKey(role);

    internal static bool Allows(string from, string to) =>
        Rules.TryGetValue(from, out var rule) && rule.Allowed.Contains(to);

    internal static ImmutableArray<string> Required(string role) =>
        Rules.TryGetValue(role, out var rule) ? rule.Required : [];
}
