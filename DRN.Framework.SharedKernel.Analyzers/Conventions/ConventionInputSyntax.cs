using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace DRN.Framework.SharedKernel.Analyzers.Conventions;

// Shared source keeps IDE generation and diagnostics on the same data-only input contract.
internal static class ConventionInputSyntax
{
    internal static CompilationUnitSyntax? Parse(SourceText text, string path, CancellationToken token, out string problem)
    {
        problem = "malformed";
        if (text.Length > ConventionInputLimits.MaxCharacters)
        {
            problem = "over the input size limit";
            return null;
        }
        var root = (CompilationUnitSyntax)CSharpSyntaxTree.ParseText(text, path: path, cancellationToken: token).GetRoot(token);
        if (root.ContainsDiagnostics) return null;
        // WriteCodeFragment includes using directives. Names below are read literally, never bound or executed.
        if (root.Members.Count != 0 || root.Externs.Count != 0 || root.ContainsDirectives)
        {
            problem = "not a data-only fragment";
            return null;
        }
        var count = 0;
        var versions = 0;
        var unique = new Dictionary<(string Kind, string Identity), Dictionary<string, string>>();
        foreach (var list in root.AttributeLists)
        foreach (var attribute in list.Attributes)
        {
            token.ThrowIfCancellationRequested();
            if (++count > ConventionInputLimits.MaxRecords)
            {
                problem = "over the record limit";
                return null;
            }
            var kind = Kind(attribute);
            if (list.Target?.Identifier.ValueText != "assembly" ||
                kind is not ("Version" or "Path" or "Role" or "Context" or "Ownership" or "Exception" or "Reference" or "Registration" or "Configuration") ||
                attribute.ArgumentList is not { Arguments.Count: > 0 } arguments) return null;
            var first = arguments.Arguments[0];
            var identity = String(first.Expression, token);
            if (identity is null || first.NameEquals is not null || first.NameColon is not null) return null;
            var fields = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var argument in arguments.Arguments.Skip(1))
            {
                if (argument.NameEquals is not { } named || argument.NameColon is not null ||
                    String(argument.Expression, token) is not { } value) return null;
                var field = named.Name.Identifier.ValueText;
                if (kind == "Exception" && field == "SymbolTarget") field = "Target";
                if (!IsField(kind, field) || !fields.TryAdd(field, value)) return null;
            }
            if (kind == "Version")
            {
                if (++versions != 1 || identity != "1" || fields.Count != 0)
                {
                    problem = "missing or conflicting supported version";
                    return null;
                }
            }
            // Other kinds support multiple records for one identity (for example keyed services).
            if (kind is not ("Context" or "Ownership" or "Path")) continue;
            var key = (kind, kind == "Path" ? identity.Replace('\\', '/') : identity);
            if (unique.TryGetValue(key, out var previous) &&
                (previous.Count != fields.Count || previous.Any(field => !fields.TryGetValue(field.Key, out var value) || value != field.Value)))
            {
                problem = "contradictory";
                return null;
            }
            unique[key] = fields;
        }
        if (versions != 1)
        {
            problem = "missing or conflicting supported version";
            return null;
        }
        problem = "";
        return root;
    }

    internal static string Kind(AttributeSyntax attribute)
    {
        var name = attribute.Name.ToString();
        if (name.StartsWith("global::", StringComparison.Ordinal)) name = name[8..];
        const string prefix = "DRN.Conventions.";
        return name.StartsWith(prefix, StringComparison.Ordinal) ? name[prefix.Length..] : "";
    }

    // Analyzer owners retain their precise semantic diagnostics. Generation must reject invalid
    // structural values before interpreting any record as permission to use a conventional home.
    internal static bool SupportsGeneration(CompilationUnitSyntax root, CancellationToken token)
    {
        foreach (var attribute in root.AttributeLists.SelectMany(list => list.Attributes))
        {
            token.ThrowIfCancellationRequested();
            var arguments = attribute.ArgumentList!.Arguments;
            var identity = String(arguments[0].Expression, token)!;
            var fields = arguments.Skip(1).ToDictionary(argument => argument.NameEquals!.Name.Identifier.ValueText,
                argument => String(argument.Expression, token)!, StringComparer.Ordinal);
            string Field(string name) => fields.TryGetValue(name, out var value) ? value : "";
            bool OptionalPath(string name) => Field(name).Length == 0 || IsLogicalPath(Field(name));
            if (string.IsNullOrWhiteSpace(identity)) return false;
            var supported = Kind(attribute) switch
            {
                "Version" => true,
                "Path" => IsLogicalPath(Field("LogicalPath")),
                "Reference" => Field("Stage") is "Project" or "Package" or "Resolved" ||
                    Field("Stage") == "Complete" && identity == "Inventory",
                "Ownership" => SyntaxFacts.IsValidIdentifier(Field("Subdomain")) && Field("Kind") is "" or "Entity" or "Value",
                "Context" => (Field("Subdomain").Length == 0 || SyntaxFacts.IsValidIdentifier(Field("Subdomain"))) &&
                    OptionalPath("Repositories") && OptionalPath("Migrations") &&
                    (string.IsNullOrWhiteSpace(Field("Factory")) && string.IsNullOrWhiteSpace(Field("Provider")) ||
                     !string.IsNullOrWhiteSpace(Field("Reason"))),
                "Role" => identity is "Domain" or "Contract" or "Infra" or "Application" or "Hosted" or "SharedKernel" or "Utils" &&
                    Field("NamespacePrefix").Length > 0 && Field("NamespacePrefix").Split('.').All(SyntaxFacts.IsValidIdentifier) &&
                    IsLogicalPath(Field("LogicalPathPrefix")),
                "Registration" => !string.IsNullOrWhiteSpace(Field("Service")),
                "Configuration" => !string.IsNullOrWhiteSpace(Field("Context")),
                // Semantic target/fingerprint validity remains analyzer-owned. This never authorizes generation.
                "Exception" => identity.StartsWith("DRNCV", StringComparison.Ordinal) && identity.Length == 8 &&
                    identity[5..].All(char.IsAsciiDigit) &&
                    (Field("SymbolTarget").Length > 0 ? Field("SymbolTarget") : Field("Target")) is { Length: > 0 } target &&
                    !string.IsNullOrWhiteSpace(target) && !target.Contains('*') &&
                    !string.IsNullOrWhiteSpace(Field("Reason")) && !string.IsNullOrWhiteSpace(Field("Owner")) &&
                    DateOnly.TryParseExact(Field("Review"), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var review) &&
                    review > DateOnly.FromDateTime(DateTime.UtcNow) &&
                    (Field("Fingerprint").Length == 0 || Field("Fingerprint").Length == 64 && Field("Fingerprint").All(char.IsAsciiHexDigit)),
                _ => false
            };
            if (!supported) return false;
        }
        return true;
    }

    internal static bool IsLogicalPath(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var path = value.Replace('\\', '/');
        return !path.StartsWith('/') && !path.Contains(':') && !path.Contains('\0') && !path.Contains('\r') && !path.Contains('\n') &&
            path.Split('/').All(segment => segment.Length > 0 && segment is not "." and not "..");
    }

    internal static string? String(ExpressionSyntax expression, CancellationToken token)
    {
        var pending = new Stack<ExpressionSyntax>();
        var value = new System.Text.StringBuilder();
        pending.Push(expression);
        while (pending.TryPop(out var current))
        {
            token.ThrowIfCancellationRequested();
            switch (current)
            {
                case LiteralExpressionSyntax literal when literal.IsKind(SyntaxKind.StringLiteralExpression): value.Append(literal.Token.ValueText); break;
                case ParenthesizedExpressionSyntax parenthesized: pending.Push(parenthesized.Expression); break;
                case BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.AddExpression): pending.Push(binary.Right); pending.Push(binary.Left); break;
                default: return null;
            }
        }
        return value.ToString();
    }

    private static bool IsField(string kind, string field) => kind switch
    {
        "Path" => field == "LogicalPath",
        "Role" => field is "NamespacePrefix" or "LogicalPathPrefix" or "Reason",
        "Context" => field is "Subdomain" or "Repositories" or "Migrations" or "MigrationsAssembly" or "Factory" or "Provider" or "Reason",
        "Ownership" => field is "Subdomain" or "Kind" or "Reason",
        "Exception" => field is "Target" or "Entity" or "Context" or "Reason" or "Owner" or "Review" or "Fingerprint",
        "Reference" => field is "Stage" or "Source" or "Project" or "OriginalProject" or "Package",
        "Registration" => field is "Service" or "Key" or "Reason",
        "Configuration" => field is "Context" or "Reason",
        _ => false
    };
}
