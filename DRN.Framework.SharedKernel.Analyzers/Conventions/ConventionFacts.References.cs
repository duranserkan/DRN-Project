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
        var invalid = new HashSet<string>(StringComparer.Ordinal);
        var records = CollectMetadataRecords(assembly, invalid);
        BuildReferenceOwners(assembly, records, result, invalid);
        InvalidateConflictingOwners(assembly, result, invalid);
        return result;
    }

    private Dictionary<(string Kind, string Type), Dictionary<string, string>> CollectMetadataRecords(
        IAssemblySymbol assembly, HashSet<string> invalid)
    {
        var records = new Dictionary<(string Kind, string Type), Dictionary<string, string>>();
        var malformedKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (key, value) in AssemblyMetadata(assembly))
        {
            Cancellation.ThrowIfCancellationRequested();
            if (!TryParseMetadataRecord(assembly, key, malformedKeys, out var kind, out var name, out var field)) continue;
            if (field is not (ConventionInputSyntax.SubdomainField or ConventionInputSyntax.ReasonField or ConventionInputSyntax.KindField) ||
                kind == ConventionInputSyntax.ContextKind && field == ConventionInputSyntax.KindField)
            {
                invalid.Add(name);
                continue;
            }
            if (!records.TryGetValue((kind, name), out var fields))
            {
                fields = new(StringComparer.Ordinal);
                records.Add((kind, name), fields);
            }
            if (fields.TryGetValue(field, out var previous) && previous != value) invalid.Add(name);
            else fields[field] = value;
        }
        return records;
    }

    private bool TryParseMetadataRecord(IAssemblySymbol assembly, string key, HashSet<string> malformedKeys,
        out string kind, out string name, out string field)
    {
        kind = MetadataRecordKind(key) ?? "";
        name = "";
        field = "";
        if (kind.Length == 0) return false;
        var suffix = key[(ConventionProject.MetadataPrefix.Length + kind.Length + 1)..];
        var separator = suffix.LastIndexOf('.');
        if (separator <= 0)
        {
            if (malformedKeys.Add(key))
                Diagnostics.Add(ConventionProject.InputIssue(assembly.Name + ":" + key,
                    "malformed", "typed producer ownership metadata"));
            return false;
        }
        name = suffix[..separator];
        field = suffix[(separator + 1)..];
        // An assembly can publish facts about its own declarations, not relabel a dependency for other consumers.
        return assembly.GetTypeByMetadataName(name) is { } type &&
               SymbolEqualityComparer.Default.Equals(type.ContainingAssembly, assembly);
    }

    private static string? MetadataRecordKind(string key)
    {
        if (key.StartsWith(ConventionProject.MetadataPrefix + ConventionInputSyntax.OwnershipKind + ".", StringComparison.Ordinal))
            return ConventionInputSyntax.OwnershipKind;
        if (key.StartsWith(ConventionProject.MetadataPrefix + ConventionInputSyntax.ContextKind + ".", StringComparison.Ordinal))
            return ConventionInputSyntax.ContextKind;
        return null;
    }

    private void BuildReferenceOwners(IAssemblySymbol assembly,
        Dictionary<(string Kind, string Type), Dictionary<string, string>> records,
        Dictionary<string, ProducerOwnership> result, HashSet<string> invalid)
    {
        foreach (var ((kind, name), fields) in records)
        {
            Cancellation.ThrowIfCancellationRequested();
            var type = assembly.GetTypeByMetadataName(name)!;
            fields.TryGetValue(ConventionInputSyntax.SubdomainField, out var subdomain);
            fields.TryGetValue(ConventionInputSyntax.ReasonField, out var reason);
            fields.TryGetValue(ConventionInputSyntax.KindField, out var classification);
            if (!IsValidProducerRecord(type, kind, subdomain, reason, classification))
            {
                invalid.Add(name);
                continue;
            }
            if (!result.TryGetValue(name, out var owner))
            {
                owner = new();
                result.Add(name, owner);
            }
            if (owner.Declarations.Any(declaration => declaration.Subdomain != subdomain)) invalid.Add(name);
            owner.Declarations.Add((subdomain!, reason ?? ""));
            if (kind == ConventionInputSyntax.OwnershipKind) owner.Kind = classification ?? "";
        }
    }

    private bool IsValidProducerRecord(INamedTypeSymbol type, string kind, string? subdomain,
        string? reason, string? classification)
    {
        if (subdomain is null || !SyntaxFacts.IsValidIdentifier(subdomain) ||
            classification is not (null or "" or ConventionInputSyntax.EntityField or "Value") ||
            classification == "Value" && Symbols.IsPersistenceType(type, Cancellation) ||
            kind == ConventionInputSyntax.ContextKind && Symbols.Context is not null && !Symbols.IsContext(type, Cancellation))
            return false;
        var role = Role(type);
        var conventional = InferOwner(type, role.Role, role.Namespace);
        if (subdomain != conventional && string.IsNullOrWhiteSpace(reason)) return false;
        return !type.GetAttributes().Any(attribute =>
            ConventionSymbols.Same(attribute.AttributeClass, Symbols.OwnershipAttribute) &&
            attribute.ConstructorArguments is [{ Value: string attributed }] && attributed != subdomain);
    }

    private void InvalidateConflictingOwners(IAssemblySymbol assembly, Dictionary<string, ProducerOwnership> result, HashSet<string> invalid)
    {
        foreach (var name in invalid)
        {
            Cancellation.ThrowIfCancellationRequested();
            if (assembly.GetTypeByMetadataName(name) is not { } type) continue;
            result.Remove(name);
            InvalidOwner(type, assembly.Name + ":" + name, "malformed or contradictory producer ownership metadata",
                Microsoft.CodeAnalysis.Location.None);
        }
    }

    private sealed class ProducerOwnership
    {
        internal List<(string Subdomain, string Reason)> Declarations { get; } = [];
        internal string Kind { get; set; } = "";
    }
}
