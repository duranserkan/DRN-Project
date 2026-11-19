namespace DRN.Framework.SharedKernel.Analyzers.Conventions;

// Shared by diagnostics and IDE actions so project identity cannot drift between them.
internal static class ConventionNames
{
    internal static string DtoFolder(string stem)
    {
        if (stem.Length > 1 && stem.EndsWith('y') && !"aeiouAEIOU".Contains(stem[^2]))
            return stem[..^1] + "ies";
        if (stem.EndsWith('s') || stem.EndsWith('x') || stem.EndsWith('z') ||
            stem.EndsWith("ch", StringComparison.Ordinal) || stem.EndsWith("sh", StringComparison.Ordinal))
            return stem + "es";
        return stem + "s";
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
            var candidate = parts[index].ToUpperInvariant() switch
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
            if (candidate.Length == 0) continue;
            if (position >= 0) return ("", "ambiguous role tokens");
            position = index;
            role = candidate;
        }
        return (role, "");
    }
}
