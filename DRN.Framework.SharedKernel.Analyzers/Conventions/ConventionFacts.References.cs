using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace DRN.Framework.SharedKernel.Analyzers.Conventions;

internal sealed partial class ConventionFacts
{
    private readonly Dictionary<IAssemblySymbol, Dictionary<string, ProducerOwnership>> _referenceOwnership = new(SymbolEqualityComparer.Default);

    private string DeclaredKind(INamedTypeSymbol type)
    {
        type = type.OriginalDefinition;
        var producer = IsLocal(type) ? null : ReferenceOwnership(type);
        if (HasInvalidOwner(type)) return "";
        var name = ConventionSymbols.MetadataName(type);
        var local = _ownership.TryGetValue(name, out var input) ? input.Get("Kind") : "";
        var exported = producer?.Kind ?? "";
        if (local.Length > 0 && exported.Length > 0 && local != exported)
        {
            InvalidOwner(type, name, "a consumer redefinition of the producer's entity/value classification", input!.Location);
            return "";
        }
        return exported.Length > 0 ? exported : local;
    }

    private ProducerOwnership? ReferenceOwnership(INamedTypeSymbol type)
    {
        var assembly = type.ContainingAssembly;
        if (!_referenceOwnership.TryGetValue(assembly, out var owners))
        {
            owners = ReadReferenceOwnership(assembly);
            _referenceOwnership.Add(assembly, owners);
        }
        return owners.TryGetValue(ConventionSymbols.MetadataName(type), out var owner) ? owner : null;
    }

    private Dictionary<string, ProducerOwnership> ReadReferenceOwnership(IAssemblySymbol assembly)
    {
        var result = new Dictionary<string, ProducerOwnership>(StringComparer.Ordinal);
        if (ReferenceProject(assembly) is null) return result;
        var records = new Dictionary<(string Kind, string Type), Dictionary<string, string>>();
        var invalid = new HashSet<string>(StringComparer.Ordinal);
        var malformedKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (key, value) in AssemblyMetadata(assembly))
        {
            Cancellation.ThrowIfCancellationRequested();
            var kind = key.StartsWith(ConventionProject.MetadataPrefix + "Ownership.", StringComparison.Ordinal) ? "Ownership" :
                key.StartsWith(ConventionProject.MetadataPrefix + "Context.", StringComparison.Ordinal) ? "Context" : null;
            if (kind is null) continue;
            var suffix = key[(ConventionProject.MetadataPrefix.Length + kind.Length + 1)..];
            var separator = suffix.LastIndexOf('.');
            if (separator <= 0)
            {
                if (malformedKeys.Add(key))
                    Diagnostics.Add(ConventionProject.InputIssue(assembly.Name + ":" + key,
                        "malformed", "typed producer ownership metadata"));
                continue;
            }
            var name = suffix[..separator];
            var field = suffix[(separator + 1)..];
            // An assembly can publish facts about its own declarations, not relabel a dependency for other consumers.
            if (assembly.GetTypeByMetadataName(name) is not { } type ||
                !SymbolEqualityComparer.Default.Equals(type.ContainingAssembly, assembly)) continue;
            if (field is not ("Subdomain" or "Reason" or "Kind") || kind == "Context" && field == "Kind")
            {
                invalid.Add(name);
                continue;
            }
            if (!records.TryGetValue((kind, name), out var fields)) records.Add((kind, name), fields = new(StringComparer.Ordinal));
            if (fields.TryGetValue(field, out var previous) && previous != value) invalid.Add(name);
            else fields[field] = value;
        }

        foreach (var ((kind, name), fields) in records)
        {
            Cancellation.ThrowIfCancellationRequested();
            var type = assembly.GetTypeByMetadataName(name)!;
            fields.TryGetValue("Subdomain", out var subdomain);
            fields.TryGetValue("Reason", out var reason);
            fields.TryGetValue("Kind", out var classification);
            if (subdomain is null || !SyntaxFacts.IsValidIdentifier(subdomain) || classification is not (null or "" or "Entity" or "Value") ||
                classification == "Value" && Symbols.IsPersistenceType(type, Cancellation) ||
                kind == "Context" && Symbols.Context is not null && !Symbols.IsContext(type, Cancellation))
            {
                invalid.Add(name);
                continue;
            }
            var role = Role(type);
            var conventional = InferOwner(type, role.Role, role.Namespace);
            if (subdomain != conventional && string.IsNullOrWhiteSpace(reason)) invalid.Add(name);
            foreach (var attribute in type.GetAttributes())
                if (ConventionSymbols.Same(attribute.AttributeClass, Symbols.OwnershipAttribute) &&
                    attribute.ConstructorArguments is [{ Value: string attributed }] && attributed != subdomain)
                    invalid.Add(name);
            if (!result.TryGetValue(name, out var owner)) result.Add(name, owner = new());
            if (owner.Declarations.Any(declaration => declaration.Subdomain != subdomain)) invalid.Add(name);
            owner.Declarations.Add((subdomain, reason ?? ""));
            if (kind == "Ownership") owner.Kind = classification ?? "";
        }
        foreach (var name in invalid)
        {
            Cancellation.ThrowIfCancellationRequested();
            if (assembly.GetTypeByMetadataName(name) is not { } type) continue;
            result.Remove(name);
            InvalidOwner(type, assembly.Name + ":" + name, "malformed or contradictory producer ownership metadata",
                Microsoft.CodeAnalysis.Location.None);
        }
        return result;
    }

    private sealed class ProducerOwnership
    {
        internal List<(string Subdomain, string Reason)> Declarations { get; } = [];
        internal string Kind { get; set; } = "";
    }
}
