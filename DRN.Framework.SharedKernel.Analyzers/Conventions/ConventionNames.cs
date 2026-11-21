using Microsoft.CodeAnalysis.CSharp;

namespace DRN.Framework.SharedKernel.Analyzers.Conventions;

// Shared by diagnostics and IDE actions so project identity cannot drift between them.
internal static class ConventionNames
{
    internal const string ProjectLayout = "Project";
    internal const string FoldersLayout = "Folders";

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

    internal static bool SameOwner(string leftDomain, string leftSubdomain, string rightDomain, string rightSubdomain) =>
        string.Equals(leftDomain, rightDomain, StringComparison.Ordinal) &&
        string.Equals(leftSubdomain, rightSubdomain, StringComparison.Ordinal);

    internal static bool MatchesOwnerAcrossLayers(string leftDomain, string leftSubdomain, bool leftFolderDomain, bool leftExplicitDomain,
        string rightDomain, string rightSubdomain, bool rightFolderDomain, bool rightExplicitDomain) =>
        leftFolderDomain || rightFolderDomain || leftExplicitDomain || rightExplicitDomain
            ? SameOwner(leftDomain, leftSubdomain, rightDomain, rightSubdomain)
            : string.Equals(leftSubdomain, rightSubdomain, StringComparison.Ordinal);

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
            return ("", "", false, "DrnDomainLayout", "malformed", "layout value of 'Project' or 'Folders'");
        if (layout == FoldersLayout)
            return rawDomain.Length > 0
                ? (FoldersLayout, "", false, "DrnDomain", "contradictory with Folders layout", "project-level DrnDomain only with Project layout")
                : (FoldersLayout, "", false, "", "", "");
        if (rawDomain.Length > 0)
            return SyntaxFacts.IsValidIdentifier(rawDomain)
                ? (ProjectLayout, rawDomain, true, "", "", "")
                : (ProjectLayout, "", true, "DrnDomain", "malformed", "valid identifier for project domain");
        var inferred = DefaultDomain(projectName);
        if (inferred.Length > 0 && SyntaxFacts.IsValidIdentifier(inferred))
            return (ProjectLayout, inferred, false, "", "", "");
        if (role == "Domain")
            return (ProjectLayout, "", false, "DrnDomain", "missing for ambiguous Domain project name", "DrnDomain or DrnDomainLayout='Folders'");
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
