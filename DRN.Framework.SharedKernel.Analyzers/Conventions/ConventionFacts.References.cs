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
        if (type.TypeKind == TypeKind.Error || type.ContainingAssembly is not { } assembly) return null;
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
            if (field is not (ConventionInputSyntax.DomainField or ConventionInputSyntax.SubdomainField or ConventionInputSyntax.ReasonField or ConventionInputSyntax.KindField) ||
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
            fields.TryGetValue(ConventionInputSyntax.DomainField, out var domain);
            fields.TryGetValue(ConventionInputSyntax.SubdomainField, out var subdomain);
            fields.TryGetValue(ConventionInputSyntax.ReasonField, out var reason);
            fields.TryGetValue(ConventionInputSyntax.KindField, out var classification);
            if (!IsValidProducerRecord(type, kind, domain, subdomain, reason, classification, out var effectiveDomain))
            {
                invalid.Add(name);
                continue;
            }
            if (!result.TryGetValue(name, out var owner))
            {
                owner = new();
                result.Add(name, owner);
            }
            if (owner.Declarations.Any(declaration => declaration.Subdomain != subdomain || declaration.EffectiveDomain != effectiveDomain))
                invalid.Add(name);
            owner.Declarations.Add((effectiveDomain, !string.IsNullOrEmpty(domain), subdomain!, reason ?? ""));
            if (kind == ConventionInputSyntax.OwnershipKind) owner.Kind = classification ?? "";
        }
    }

    private bool IsValidProducerRecord(INamedTypeSymbol type, string kind, string? domain, string? subdomain,
        string? reason, string? classification, out string effectiveDomain)
    {
        effectiveDomain = "";
        if (!HasValidProducerFields(type, kind, domain, subdomain, classification) ||
            ReferenceProject(type.ContainingAssembly) is not { } project)
            return false;
        var role = Role(type);
        var conventional = InferOwner(type, role.Role, role.Namespace, project);
        effectiveDomain = ResolveProducerDomain(domain, project, conventional?.Domain);
        if (project.DomainLayout == ConventionProject.FoldersLayout && effectiveDomain.Length == 0)
            return false;
        var differs = conventional is null || subdomain != conventional.Value.Subdomain || effectiveDomain != conventional.Value.Domain;
        if (differs && string.IsNullOrWhiteSpace(reason)) return false;
        return !HasConflictingOwnershipAttribute(type, subdomain!, effectiveDomain);
    }

    private bool HasValidProducerFields(INamedTypeSymbol type, string kind, string? domain, string? subdomain, string? classification)
    {
        if (subdomain is null || !SyntaxFacts.IsValidIdentifier(subdomain)) return false;
        if (!string.IsNullOrEmpty(domain) && !SyntaxFacts.IsValidIdentifier(domain)) return false;
        if (classification is not (null or "" or ConventionInputSyntax.EntityField or "Value")) return false;
        if (classification == "Value" && Symbols.IsPersistenceType(type, Cancellation)) return false;
        return kind != ConventionInputSyntax.ContextKind || Symbols.Context is null || Symbols.IsContext(type, Cancellation);
    }

    private static string ResolveProducerDomain(string? domain, ConventionProject project, string? conventionalDomain)
    {
        if (!string.IsNullOrEmpty(domain)) return domain;
        return project.DomainLayout == ConventionProject.FoldersLayout
            ? conventionalDomain ?? ""
            : project.DefaultDomain;
    }

    [SuppressMessage("SonarQube", "S3267", Justification = "Scanning the immutable attribute array directly avoids boxing ImmutableArray and allocating a predicate capturing subdomain and resolvedDomain.")]
    private bool HasConflictingOwnershipAttribute(INamedTypeSymbol type, string subdomain, string resolvedDomain)
    {
        foreach (var attribute in type.GetAttributes())
        {
            if (ConventionSymbols.Same(attribute.AttributeClass, Symbols.OwnershipAttribute) &&
                attribute.ConstructorArguments is [{ Value: string attributed }] &&
                (attributed != subdomain || Domain(attribute) is { Length: > 0 } attrDomain && attrDomain != resolvedDomain))
                return true;
        }
        return false;
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
        internal List<(string EffectiveDomain, bool ExplicitDomain, string Subdomain, string Reason)> Declarations { get; } = [];
        internal string Kind { get; set; } = "";
    }
}
