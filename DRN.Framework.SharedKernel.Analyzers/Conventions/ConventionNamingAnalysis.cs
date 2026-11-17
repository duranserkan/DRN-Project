using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace DRN.Framework.SharedKernel.Analyzers.Conventions;

internal static class ConventionNamingAnalysis
{
    internal static void Analyze(ConventionFacts facts)
    {
        foreach (var type in facts.Types)
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            if (type.IsImplicitlyDeclared || facts.Symbols.IsOpen(type, facts.Cancellation) ||
                type.TypeKind != TypeKind.Interface && type.IsAbstract) continue;
            var location = facts.Location(type);
            if (location == Location.None) continue;
            AnalyzeTypeNaming(facts, type, location);
        }
    }

    private static void AnalyzeTypeNaming(ConventionFacts facts, INamedTypeSymbol type, Location location)
    {
        var aggregate = facts.Symbols.IsKnown(type, facts.Symbols.Aggregate, facts.Cancellation);
        var suffix = RequiredSuffix(facts, type, aggregate);
        if (suffix is null) return;
        var name = type.Name;
        var stemLength = name.Length - suffix.Length;
        var prefixLength = type.TypeKind == TypeKind.Interface && name.StartsWith('I') ? 1 : 0;
        if (stemLength <= prefixLength || !name.EndsWith(suffix, StringComparison.Ordinal))
        {
            facts.Diagnostics.Add(Diagnostic.Create(ConventionDiagnosticDescriptors.RoleSuffix, location,
                Properties(type), name, suffix));
            return;
        }
        if (aggregate) CheckAggregatePlacement(facts, type, location, name, stemLength);
    }

    private static void CheckAggregatePlacement(ConventionFacts facts, INamedTypeSymbol type, Location location,
        string name, int stemLength)
    {
        var stem = name[..stemLength];
        var namespaceConflict = type.ContainingNamespace.Name != stem;
        List<Location>? locations = null;
        foreach (var reference in type.DeclaringSyntaxReferences)
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            var tree = reference.SyntaxTree;
            if (facts.IsGenerated(tree) || facts.HasInvalidPath(tree)) continue;
            var directory = facts.LogicalDirectory(tree);
            if (directory is null) continue; // Ownership analysis owns missing placement evidence.
            if (namespaceConflict || directory.Value.Name != stem)
                (locations ??= []).Add(reference.GetSyntax(facts.Cancellation).GetLocation());
        }
        if (namespaceConflict || locations is { Count: > 0 })
            facts.Diagnostics.Add(Diagnostic.Create(ConventionDiagnosticDescriptors.AggregatePlacement, location,
                additionalLocations: locations, properties: Properties(type), messageArgs: [name, stem]));
    }

    private static string? RequiredSuffix(ConventionFacts facts, INamedTypeSymbol type, bool aggregate)
    {
        if (aggregate) return "Entity";
        if (facts.Symbols.IsKnown(type, facts.Symbols.Configuration, facts.Cancellation)) return "Config";
        if (facts.Symbols.IsKnown(type, facts.Symbols.RepositoryContract, facts.Cancellation)) return "Repository";
        if (facts.Symbols.IsKnown(type, facts.Symbols.Controller, facts.Cancellation)) return "Controller";
        if (facts.Symbols.IsKnown(type, facts.Symbols.ServiceContract, facts.Cancellation)) return "Service";
        if (facts.Role(type).Role != ConventionArchitecture.Application) return null;
        var service = ConventionOwnershipAnalysis.HasEntityBinding(facts, type) ||
            ConventionRegistrationAnalysis.IsServiceImplementation(facts, type) &&
            facts.Symbols.EffectiveAttributes(type, facts.Symbols.LifetimeAttribute, facts.Cancellation).Any();
        return service ? "Service" : null;
    }

    private static ImmutableDictionary<string, string?> Properties(INamedTypeSymbol type) =>
        ImmutableDictionary<string, string?>.Empty.Add("Target", type.GetDocumentationCommentId());
}
