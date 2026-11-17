using System.Collections.Immutable;

namespace DRN.Framework.SharedKernel.Analyzers.Conventions;

internal static class ConventionArchitecture
{
    internal const string SharedKernel = "SharedKernel";
    internal const string Contract = "Contract";
    internal const string Utils = "Utils";
    internal const string Domain = "Domain";
    internal const string Infra = "Infra";
    internal const string Application = "Application";
    internal const string Hosted = "Hosted";

    private sealed record Dependencies(ImmutableArray<string> AllowedRoles, ImmutableArray<string> RequiredRoles);

    // Required roles have minimum cardinality one. Sharing and additional allowed edges are unrestricted.
    private static readonly Dictionary<string, Dependencies> Rules = new(StringComparer.Ordinal)
    {
        [SharedKernel] = new([], []),
        [Contract] = new([SharedKernel], []),
        [Utils] = new([SharedKernel], []),
        [Domain] = new([Contract, SharedKernel, Utils], [Contract]),
        [Infra] = new([Domain, Contract, SharedKernel, Utils], [Domain]),
        [Application] = new([Domain, Contract, Infra, SharedKernel, Utils], []),
        [Hosted] = new([Domain, Contract, Infra, Application, SharedKernel, Utils], [Application, Infra])
    };

    internal static bool IsRole(string role) => Rules.ContainsKey(role);

    internal static bool Allows(string from, string to) =>
        Rules.TryGetValue(from, out var rule) && rule.AllowedRoles.Contains(to);

    internal static ImmutableArray<string> Required(string role) =>
        Rules.TryGetValue(role, out var rule) ? rule.RequiredRoles : [];
}
