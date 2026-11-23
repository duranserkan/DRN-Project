using Microsoft.CodeAnalysis.CSharp;

namespace DRN.Framework.SharedKernel.Analyzers.Conventions;

// Shared by diagnostics and IDE actions so project identity cannot drift between them.
internal static class ConventionNames
{
    internal const string ProjectLayout = "Project";
    internal const string FoldersLayout = "Folders";
    private const string DomainLayoutProperty = "DrnDomainLayout";
    private const string DomainProperty = "DrnDomain";

    internal readonly record struct OwnerIdentity(string Domain, string Subdomain, bool HasFolderDomain, bool HasExplicitDomain);

    internal readonly record struct ReferenceAssemblyMetadata(
        string ProjectName,
        string RootNamespace,
        bool HasExplicitRootNamespace,
        string DomainLayout,
        string Domain,
        string ErrorProperty,
        string ErrorProblem,
        string ErrorRemedy)
    {
        internal bool Valid => ErrorProperty.Length == 0;
    }

    internal static string OwnerNamespacePrefix(string domain, string subdomain, bool hasFolderDomain) =>
        hasFolderDomain ? domain + "." + subdomain : subdomain;

    internal static bool IsMissingAggregateSubdomain(string[] parts, int offset, bool isFoldersLayout, string typeName) =>
        isFoldersLayout
            ? parts.Length == offset + 2 && parts[offset + 1] == EntityStem(typeName)
            : parts.Length == offset + 1 && parts[offset] == EntityStem(typeName);

    internal static (string Domain, string Subdomain, bool HasFolderDomain)? InferNamespaceOwner(
        string[] parts, int offset, bool isFoldersLayout, string defaultDomain)
    {
        if (isFoldersLayout)
            return offset + 1 < parts.Length ? (parts[offset], parts[offset + 1], true) : null;
        return offset < parts.Length ? (defaultDomain, parts[offset], false) : null;
    }

    internal static bool TryReadAssemblyMetadata(AttributeData attribute, out KeyValuePair<string, string> entry)
    {
        if (attribute.ConstructorArguments is [{ Value: string key }, var value] &&
            value.Kind != TypedConstantKind.Error &&
            (value.Value is string || value.IsNull))
        {
            entry = new(key, value.Value as string ?? "");
            return true;
        }
        entry = default;
        return false;
    }

    internal static ReferenceAssemblyMetadata ParseReferenceMetadata(
        string assemblyName, IEnumerable<KeyValuePair<string, string>> metadata)
    {
        var projectName = assemblyName;
        var rootNamespace = assemblyName;
        var domainLayout = "";
        var domain = "";
        var hasProjectName = false;
        var hasRootNamespace = false;
        var hasDomainLayout = false;
        var hasDomain = false;
        foreach (var (key, value) in metadata)
        {
            var property = key switch
            {
                "DRN.Conventions.ProjectName" => "MSBuildProjectName",
                "DRN.Conventions.RootNamespace" => "RootNamespace",
                "DRN.Conventions.DomainLayout" => DomainLayoutProperty,
                "DRN.Conventions.Domain" => DomainProperty,
                _ => null
            };
            if (property is null) continue;
            if (string.IsNullOrWhiteSpace(value))
                return new("", "", false, "", "", property, "incomplete", "reference-visible project identity");

            var conflict = property switch
            {
                "MSBuildProjectName" => hasProjectName && projectName != value,
                "RootNamespace" => hasRootNamespace && rootNamespace != value,
                DomainLayoutProperty => hasDomainLayout && domainLayout != value,
                DomainProperty => hasDomain && domain != value,
                _ => false
            };
            if (conflict)
                return new("", "", false, "", "", property, "contradictory", "reference-visible ownership fact");

            switch (property)
            {
                case "MSBuildProjectName":
                    hasProjectName = true;
                    projectName = value;
                    break;
                case "RootNamespace":
                    hasRootNamespace = true;
                    rootNamespace = value;
                    break;
                case DomainLayoutProperty:
                    hasDomainLayout = true;
                    domainLayout = value;
                    break;
                case DomainProperty:
                    hasDomain = true;
                    domain = value;
                    break;
            }
        }
        if (!hasRootNamespace) rootNamespace = projectName;
        return new(projectName, rootNamespace, hasRootNamespace, domainLayout, domain, "", "", "");
    }

    internal static string DtoFolder(string stem)
    {
        if (stem.Length > 1 && stem.EndsWith('y') && !"aeiouAEIOU".Contains(stem[^2]))
            return stem[..^1] + "ies";
        if (stem.EndsWith('s') || stem.EndsWith('x') || stem.EndsWith('z') ||
            stem.EndsWith("ch", StringComparison.Ordinal) || stem.EndsWith("sh", StringComparison.Ordinal))
            return stem + "es";
        return stem + "s";
    }

    internal static string EntityStem(string name) =>
        name.Length > "Entity".Length && name.EndsWith("Entity", StringComparison.Ordinal) ? name[..^"Entity".Length] : name;

    internal static string MetadataName(INamedTypeSymbol type)
    {
        var typeParts = new Stack<string>();
        INamespaceSymbol? outermostNamespace = null;
        for (var current = type; current is not null; current = current.ContainingType)
        {
            typeParts.Push(current.MetadataName);
            if (current.ContainingType is null) outermostNamespace = current.ContainingNamespace;
        }
        var namespaceParts = new Stack<string>();
        for (var ns = outermostNamespace; ns is { IsGlobalNamespace: false }; ns = ns.ContainingNamespace)
            namespaceParts.Push(ns.MetadataName);
        var qualifiedType = string.Join("+", typeParts);
        return namespaceParts.Count == 0 ? qualifiedType : string.Join(".", namespaceParts) + "." + qualifiedType;
    }

    internal static bool SameOwner(string leftDomain, string leftSubdomain, string rightDomain, string rightSubdomain) =>
        string.Equals(leftDomain, rightDomain, StringComparison.Ordinal) &&
        string.Equals(leftSubdomain, rightSubdomain, StringComparison.Ordinal);

    internal static bool MatchesOwnerAcrossLayers(OwnerIdentity left, OwnerIdentity right) =>
        left.HasFolderDomain || right.HasFolderDomain || left.HasExplicitDomain || right.HasExplicitDomain
            ? SameOwner(left.Domain, left.Subdomain, right.Domain, right.Subdomain)
            : string.Equals(left.Subdomain, right.Subdomain, StringComparison.Ordinal);

    internal static bool IsConventionalContextName(string contextName, string domain, string subdomain, bool hasFolderDomain)
    {
        const string suffix = "Context";
        if (!contextName.EndsWith(suffix, StringComparison.Ordinal) || contextName.Length <= suffix.Length) return false;
        var stem = contextName.AsSpan(0, contextName.Length - suffix.Length);
        if (stem.Equals(subdomain.AsSpan(), StringComparison.Ordinal)) return true;
        return hasFolderDomain && stem.Length == domain.Length + subdomain.Length &&
               stem.StartsWith(domain.AsSpan(), StringComparison.Ordinal) &&
               stem.EndsWith(subdomain.AsSpan(), StringComparison.Ordinal);
    }

    internal static (string Layout, string Domain, bool ExplicitDomain, string ErrorKey, string ErrorProblem, string ErrorRemedy)
        ParseDomainLayout(string projectName, string role, string rawLayout, string rawDomain)
    {
        var layout = rawLayout.Length == 0 ? ProjectLayout : rawLayout;
        if (layout is not (ProjectLayout or FoldersLayout))
            return ("", "", false, DomainLayoutProperty, "malformed", "layout value of 'Project' or 'Folders'");
        if (layout == FoldersLayout)
            return rawDomain.Length > 0
                ? (FoldersLayout, "", false, DomainProperty, "contradictory with Folders layout", "project-level DrnDomain only with Project layout")
                : (FoldersLayout, "", false, "", "", "");
        if (rawDomain.Length > 0)
            return SyntaxFacts.IsValidIdentifier(rawDomain)
                ? (ProjectLayout, rawDomain, true, "", "", "")
                : (ProjectLayout, "", true, DomainProperty, "malformed", "valid identifier for project domain");
        var inferred = DefaultDomain(projectName);
        if (inferred.Length > 0 && SyntaxFacts.IsValidIdentifier(inferred))
            return (ProjectLayout, inferred, false, "", "", "");
        if (role == "Domain")
            return (ProjectLayout, "", false, DomainProperty, "missing for ambiguous Domain project name", "DrnDomain or DrnDomainLayout='Folders'");
        return (ProjectLayout, "", false, "", "", "");
    }

    internal static (string Role, string Problem) Read(string name)
    {
        var parts = name.Split('.');
        if (parts.Any(part => part.ToUpperInvariant() is "FRAMEWORK" or "TEST" or "TESTS" or "TESTING" or
                "ANALYZER" or "ANALYZERS" or "CODEFIXES" or "GENERATOR" or "GENERATORS")) return ("", "");
        var role = "";
        var position = -1;
        for (var index = 0; index < parts.Length; index++)
        {
            var candidate = RoleToken(parts[index]);
            if (candidate.Length == 0) continue;
            if (position >= 0) return ("", "ambiguous role tokens");
            position = index;
            role = candidate;
        }
        return (role, "");
    }

    // Infers the project stem from the dot-delimited token adjacent to the project role token.
    internal static string DefaultDomain(string name)
    {
        var parts = name.Split('.', StringSplitOptions.RemoveEmptyEntries);
        var roleIndex = -1;
        for (var index = 0; index < parts.Length; index++)
        {
            if (RoleToken(parts[index]).Length == 0) continue;
            roleIndex = index;
            break;
        }
        if (roleIndex < 0) return "";
        if (roleIndex > 0) return parts[roleIndex - 1];
        return roleIndex + 1 < parts.Length ? parts[roleIndex + 1] : "";
    }

    private static string RoleToken(string part) => part.ToUpperInvariant() switch
    {
        "INFRA" or "INFRASTRUCTURE" => "Infra",
        "HOST" or "HOSTED" or "HOSTING" => "Hosted",
        "CONTRACT" or "CONTRACTS" => "Contract",
        "APPLICATION" => "Application",
        "DOMAIN" => "Domain",
        "SHAREDKERNEL" => "SharedKernel",
        "UTILS" => "Utils",
        _ => ""
    };
}
