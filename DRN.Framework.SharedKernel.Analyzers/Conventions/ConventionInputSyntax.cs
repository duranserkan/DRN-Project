using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace DRN.Framework.SharedKernel.Analyzers.Conventions;

// Shared source keeps IDE generation and diagnostics on the same data-only input contract.
internal static class ConventionInputSyntax
{
    internal const string VersionKind = "Version";
    internal const string PathKind = "Path";
    internal const string RoleKind = "Role";
    internal const string ContextKind = "Context";
    internal const string OwnershipKind = "Ownership";
    internal const string ExceptionKind = "Exception";
    internal const string ReferenceKind = "Reference";
    internal const string RegistrationKind = "Registration";
    internal const string ConfigurationKind = "Configuration";

    internal const string LogicalPathField = "LogicalPath";
    internal const string NamespacePrefixField = "NamespacePrefix";
    internal const string LogicalPathPrefixField = "LogicalPathPrefix";
    internal const string ReasonField = "Reason";
    internal const string SubdomainField = "Subdomain";
    internal const string RepositoriesField = "Repositories";
    internal const string MigrationsField = "Migrations";
    internal const string MigrationsAssemblyField = "MigrationsAssembly";
    internal const string FactoryField = "Factory";
    internal const string ProviderField = "Provider";
    internal const string KindField = "Kind";
    internal const string TargetField = "Target";
    internal const string SymbolTargetField = "SymbolTarget";
    internal const string EntityField = "Entity";
    internal const string OwnerField = "Owner";
    internal const string ReviewField = "Review";
    internal const string FingerprintField = "Fingerprint";
    internal const string StageField = "Stage";
    internal const string SourceField = "Source";
    internal const string ProjectField = "Project";
    internal const string OriginalProjectField = "OriginalProject";
    internal const string PackageField = "Package";
    internal const string ServiceField = "Service";
    internal const string KeyField = "Key";

    internal const string CompleteStage = "Complete";
    internal const string InventoryIdentity = "Inventory";

    private static readonly string[] ConventionRulePrefixes =
    [
        "DRNARCHITECTURE", "DRNOWNERSHIP", "DRNSTRUCTURE", "DRNPERSISTENCE",
        "DRNREPOSITORY", "DRNDEPENDENCYINJECTION", "DRNCONVENTIONINPUT", "DRNCONVENTIONEXCEPTION"
    ];

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
        {
            if (!TryParseAttributeList(list, unique, ref count, ref versions, token, ref problem))
                return null;
        }
        if (versions != 1)
        {
            problem = "missing or conflicting supported version";
            return null;
        }
        problem = "";
        return root;
    }

    private static bool TryParseAttributeList(AttributeListSyntax list,
        Dictionary<(string Kind, string Identity), Dictionary<string, string>> unique,
        ref int count, ref int versions, CancellationToken token, ref string problem)
    {
        var isAssembly = list.Target?.Identifier.ValueText == "assembly";
        foreach (var attribute in list.Attributes)
        {
            token.ThrowIfCancellationRequested();
            if (++count > ConventionInputLimits.MaxRecords)
            {
                problem = "over the record limit";
                return false;
            }
            if (!isAssembly || !TryReadAttributeRecord(attribute, token, out var kind, out var identity, out var fields))
                return false;
            if (!ValidateVersionRecord(kind, identity, fields, ref versions, ref problem))
                return false;
            if (HasContradictoryRecord(unique, kind, identity, fields))
            {
                problem = "contradictory";
                return false;
            }
        }
        return true;
    }

    private static bool TryReadAttributeRecord(AttributeSyntax attribute, CancellationToken token,
        out string kind, out string identity, out Dictionary<string, string> fields)
    {
        kind = Kind(attribute);
        identity = "";
        fields = new(StringComparer.Ordinal);
        if (!IsSupportedKind(kind) || attribute.ArgumentList is not { Arguments.Count: > 0 } arguments)
            return false;
        var first = arguments.Arguments[0];
        var parsedIdentity = String(first.Expression, token);
        if (parsedIdentity is null || first.NameEquals is not null || first.NameColon is not null)
            return false;
        identity = parsedIdentity;
        foreach (var argument in arguments.Arguments.Skip(1))
        {
            if (!TryAddAttributeField(kind, argument, fields, token))
                return false;
        }
        return true;
    }

    private static bool TryAddAttributeField(string kind, AttributeArgumentSyntax argument,
        Dictionary<string, string> fields, CancellationToken token)
    {
        if (argument.NameEquals is not { } named || argument.NameColon is not null ||
            String(argument.Expression, token) is not { } value)
            return false;
        var field = named.Name.Identifier.ValueText;
        if (kind == ExceptionKind && field == SymbolTargetField) field = TargetField;
        return IsField(kind, field) && fields.TryAdd(field, value);
    }

    private static bool ValidateVersionRecord(string kind, string identity, Dictionary<string, string> fields,
        ref int versions, ref string problem)
    {
        if (kind != VersionKind) return true;
        versions++;
        if (versions == 1 && identity == "1" && fields.Count == 0) return true;
        problem = "missing or conflicting supported version";
        return false;
    }

    private static bool HasContradictoryRecord(
        Dictionary<(string Kind, string Identity), Dictionary<string, string>> unique,
        string kind, string identity, Dictionary<string, string> fields)
    {
        // Other kinds support multiple records for one identity (for example keyed services).
        if (kind is not (ContextKind or OwnershipKind or PathKind)) return false;
        var key = (kind, kind == PathKind ? identity.Replace('\\', '/') : identity);
        if (unique.TryGetValue(key, out var previous) && !SameFields(previous, fields))
            return true;
        unique[key] = fields;
        return false;
    }

    private static bool SameFields(Dictionary<string, string> left, Dictionary<string, string> right) =>
        left.Count == right.Count && left.All(field => right.TryGetValue(field.Key, out var value) && value == field.Value);

    private static bool IsSupportedKind(string kind) => kind is VersionKind or PathKind or RoleKind or ContextKind or
        OwnershipKind or ExceptionKind or ReferenceKind or RegistrationKind or ConfigurationKind;

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
            if (!SupportsAttributeRecord(attribute, token)) return false;
        }
        return true;
    }

    private static bool SupportsAttributeRecord(AttributeSyntax attribute, CancellationToken token)
    {
        var arguments = attribute.ArgumentList!.Arguments;
        var identity = String(arguments[0].Expression, token)!;
        if (string.IsNullOrWhiteSpace(identity)) return false;
        var fields = arguments.Skip(1).ToDictionary(argument => argument.NameEquals!.Name.Identifier.ValueText,
            argument => String(argument.Expression, token)!, StringComparer.Ordinal);
        return Kind(attribute) switch
        {
            VersionKind => true,
            PathKind => IsLogicalPath(Field(fields, LogicalPathField)),
            ReferenceKind => SupportsReferenceRecord(identity, fields),
            OwnershipKind => SyntaxFacts.IsValidIdentifier(Field(fields, SubdomainField)) &&
                Field(fields, KindField) is "" or EntityField or "Value",
            ContextKind => SupportsContextRecord(fields),
            RoleKind => SupportsRoleRecord(identity, fields),
            RegistrationKind => !string.IsNullOrWhiteSpace(Field(fields, ServiceField)),
            ConfigurationKind => !string.IsNullOrWhiteSpace(Field(fields, ContextKind)),
            // Semantic target/fingerprint validity remains analyzer-owned. This never authorizes generation.
            ExceptionKind => SupportsExceptionRecord(identity, fields),
            _ => false
        };
    }

    private static string Field(Dictionary<string, string> fields, string name) =>
        fields.TryGetValue(name, out var value) ? value : "";

    private static bool OptionalLogicalPath(Dictionary<string, string> fields, string name)
    {
        var value = Field(fields, name);
        return value.Length == 0 || IsLogicalPath(value);
    }

    private static bool SupportsReferenceRecord(string identity, Dictionary<string, string> fields)
    {
        var stage = Field(fields, StageField);
        return stage is ProjectField or PackageField or "Resolved" ||
               stage == CompleteStage && identity == InventoryIdentity;
    }

    private static bool SupportsContextRecord(Dictionary<string, string> fields)
    {
        var subdomain = Field(fields, SubdomainField);
        if (subdomain.Length != 0 && !SyntaxFacts.IsValidIdentifier(subdomain)) return false;
        if (!OptionalLogicalPath(fields, RepositoriesField) || !OptionalLogicalPath(fields, MigrationsField)) return false;
        return string.IsNullOrWhiteSpace(Field(fields, FactoryField)) &&
               string.IsNullOrWhiteSpace(Field(fields, ProviderField)) ||
               !string.IsNullOrWhiteSpace(Field(fields, ReasonField));
    }

    private static bool SupportsRoleRecord(string identity, Dictionary<string, string> fields) =>
        identity is "Domain" or "Contract" or "Infra" or "Application" or "Hosted" or "SharedKernel" or "Utils" &&
        Field(fields, NamespacePrefixField) is { Length: > 0 } namespacePrefix &&
        namespacePrefix.Split('.').All(SyntaxFacts.IsValidIdentifier) &&
        IsLogicalPath(Field(fields, LogicalPathPrefixField));

    internal static string? ConventionRulePrefix(string id)
    {
        if (!id.StartsWith("DRN", StringComparison.Ordinal)) return null;
        foreach (var prefix in ConventionRulePrefixes)
            if (id.StartsWith(prefix, StringComparison.Ordinal)) return prefix;
        return null;
    }

    private static bool SupportsExceptionRecord(string identity, Dictionary<string, string> fields)
    {
        var prefix = ConventionRulePrefix(identity);
        if (prefix is null || identity.Length != prefix.Length + 3 || !identity[^3..].All(char.IsAsciiDigit))
            return false;
        var symbolTarget = Field(fields, SymbolTargetField);
        var target = symbolTarget.Length > 0 ? symbolTarget : Field(fields, TargetField);
        if (string.IsNullOrWhiteSpace(target) || target.Contains('*'))
            return false;
        if (string.IsNullOrWhiteSpace(Field(fields, ReasonField)) || string.IsNullOrWhiteSpace(Field(fields, OwnerField)))
            return false;
        if (!DateOnly.TryParseExact(Field(fields, ReviewField), "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var review) || review <= DateOnly.FromDateTime(DateTime.UtcNow))
            return false;
        var fingerprint = Field(fields, FingerprintField);
        return fingerprint.Length == 0 || fingerprint.Length == 64 && fingerprint.All(char.IsAsciiHexDigit);
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

    internal static bool IsField(string kind, string field) => kind switch
    {
        PathKind => field == LogicalPathField,
        RoleKind => field is NamespacePrefixField or LogicalPathPrefixField or ReasonField,
        ContextKind => field is SubdomainField or RepositoriesField or MigrationsField or MigrationsAssemblyField or FactoryField or ProviderField or ReasonField,
        OwnershipKind => field is SubdomainField or KindField or ReasonField,
        ExceptionKind => field is TargetField or EntityField or ContextKind or ReasonField or OwnerField or ReviewField or FingerprintField,
        ReferenceKind => field is StageField or SourceField or ProjectField or OriginalProjectField or PackageField,
        RegistrationKind => field is ServiceField or KeyField or ReasonField,
        ConfigurationKind => field is ContextKind or ReasonField,
        _ => false
    };
}
