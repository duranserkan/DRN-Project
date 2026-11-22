using System.Diagnostics.CodeAnalysis;
using DRN.Framework.SharedKernel.Analyzers.Conventions;

namespace DRN.Framework.SharedKernel.CodeFixes;

internal static class CodeFixSymbolFacts
{
    internal static bool Same(INamedTypeSymbol? left, INamedTypeSymbol? right) => left is not null && right is not null &&
        SymbolEqualityComparer.Default.Equals(left.OriginalDefinition, right.OriginalDefinition);

    internal static bool IsOpen(INamedTypeSymbol type)
    {
        for (var current = type; current is not null; current = current.ContainingType)
            if (current.TypeParameters.Length != 0) return true;
        return false;
    }

    internal static bool IsPublicType(INamedTypeSymbol type)
    {
        for (var current = type; current is not null; current = current.ContainingType)
            if (current.DeclaredAccessibility != Accessibility.Public) return false;
        return true;
    }

    internal static IEnumerable<INamedTypeSymbol> Bases(INamedTypeSymbol? type)
    {
        for (var current = type; current is not null; current = current.BaseType) yield return current;
    }

    internal static IEnumerable<INamedTypeSymbol> NonObjectBases(INamedTypeSymbol? type)
    {
        for (var current = type; current is not null && current.SpecialType != SpecialType.System_Object; current = current.BaseType)
            yield return current;
    }

    internal static bool Inherits(INamedTypeSymbol type, INamedTypeSymbol? target) =>
        Bases(type).Any(current => Same(current, target));

    internal static IEnumerable<INamedTypeSymbol> Types(INamespaceOrTypeSymbol container, CancellationToken token)
    {
        var pending = new Stack<INamespaceOrTypeSymbol>();
        pending.Push(container);
        while (pending.TryPop(out var current))
        {
            token.ThrowIfCancellationRequested();
            foreach (var member in current.GetMembers())
            {
                token.ThrowIfCancellationRequested();
                if (member is INamedTypeSymbol type) yield return type;
                if (member is INamespaceOrTypeSymbol nested) pending.Push(nested);
            }
        }
    }

    internal static string MetadataName(INamedTypeSymbol type) => ConventionGenerationPreflight.MetadataName(type);

    internal static string EscapeIdentifier(string name) =>
        SyntaxFacts.GetKeywordKind(name) == SyntaxKind.None ? name : "@" + name;

    internal static string EscapeMarkdown(string? value) => (value ?? "<unavailable metadata>")
        .Replace("&", "&amp;", StringComparison.Ordinal).Replace("<", "&lt;", StringComparison.Ordinal)
        .Replace(">", "&gt;", StringComparison.Ordinal).Replace("\r", "&#13;", StringComparison.Ordinal)
        .Replace("\n", "&#10;", StringComparison.Ordinal).Replace("\\", "&#92;", StringComparison.Ordinal)
        .Replace("`", "&#96;", StringComparison.Ordinal).Replace("[", "&#91;", StringComparison.Ordinal)
        .Replace("]", "&#93;", StringComparison.Ordinal).Replace("*", "&#42;", StringComparison.Ordinal)
        .Replace("_", "&#95;", StringComparison.Ordinal);

    internal static async Task<bool> ArePartsEditableAsync(Project project, INamedTypeSymbol type,
        bool rejectSpanErrors, CancellationToken token)
    {
        foreach (var reference in type.DeclaringSyntaxReferences)
        {
            token.ThrowIfCancellationRequested();
            var document = project.GetDocument(reference.SyntaxTree);
            if (document is null || !await IsEditablePartAsync(document, reference, rejectSpanErrors, token))
                return false;
        }
        return true;
    }

    internal static async Task<Document?> ResolveEditableHomeAsync(Project project, INamedTypeSymbol type,
        bool rejectSpanErrors, CancellationToken token)
    {
        Document? home = null;
        foreach (var reference in type.DeclaringSyntaxReferences)
        {
            token.ThrowIfCancellationRequested();
            var document = project.GetDocument(reference.SyntaxTree);
            if (document?.FilePath is null || !CodeFixDestinationGuard.TryFullPath(document.FilePath, out _) ||
                !await IsEditablePartAsync(document, reference, rejectSpanErrors, token))
                return null;
            if (home is not null && Path.GetDirectoryName(home.FilePath) != Path.GetDirectoryName(document.FilePath))
                return null;
            home = document;
        }
        return home;
    }

    private static async Task<bool> IsEditablePartAsync(Document document, SyntaxReference reference, bool rejectSpanErrors,
        CancellationToken token)
    {
        if (document.GetLinkedDocumentIds().Length != 0 ||
            await ClassificationEdit.FindEditableRoot(document, reference.Span.Start, token) is null) return false;
        if (!rejectSpanErrors) return true;
        var model = await document.GetSemanticModelAsync(token);
        return model is not null && !model.GetDiagnostics(reference.Span, token).Any(item => item.Severity == DiagnosticSeverity.Error);
    }

    [SuppressMessage("SonarQube", "S3267", Justification = "Inspect matching members directly without a filtered sequence or captured predicate per base type.")]
    internal static bool DefaultDiscovery(INamedTypeSymbol context, Compilation compilation, CancellationToken token = default)
    {
        var drn = compilation.GetTypeByMetadataName("DRN.Framework.EntityFramework.Context.DrnContext`1");
        var identity = compilation.GetTypeByMetadataName("DRN.Framework.EntityFramework.Context.Identity.DrnContextIdentity`2");
        for (var current = context; current is not null; current = current.BaseType)
        {
            token.ThrowIfCancellationRequested();
            if (Same(current, drn) || Same(current, identity)) return true;
            foreach (var member in current.GetMembers("OnModelCreating"))
                if (member is IMethodSymbol method && !ForwardsSingleBaseOnModelCreating(method, compilation, token)) return false;
        }
        return false;
    }

    private static bool ForwardsSingleBaseOnModelCreating(IMethodSymbol method, Compilation compilation, CancellationToken token)
    {
        if (method.OverriddenMethod is null || method.Parameters.Length != 1 || method.DeclaringSyntaxReferences.Length != 1 ||
            method.DeclaringSyntaxReferences[0].GetSyntax(token) is not MethodDeclarationSyntax declaration ||
            !compilation.ContainsSyntaxTree(declaration.SyntaxTree)) return false;
        var expression = declaration.ExpressionBody?.Expression ??
            (declaration.Body?.Statements is { Count: 1 } statements && statements[0] is ExpressionStatementSyntax statement
                ? statement.Expression : null);
        if (expression is not InvocationExpressionSyntax
            {
                Expression: MemberAccessExpressionSyntax { Expression: BaseExpressionSyntax },
                ArgumentList.Arguments.Count: 1
            } invocation) return false;
        var model = compilation.GetSemanticModel(declaration.SyntaxTree);
        return SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(invocation, token).Symbol, method.OverriddenMethod) &&
               SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(invocation.ArgumentList.Arguments[0].Expression, token).Symbol,
                   method.Parameters[0]);
    }

    internal static async Task<bool> HasRoleOverrides(Project project, CancellationToken token)
    {
        var root = await ConventionGenerationPreflight.ReadInputs(project, required: false, token);
        return root is null || HasRoleOverride(root);
    }

    internal static async Task<CompilationUnitSyntax?> ReadInputsWithoutRoleOverrideAsync(Project project, bool required, CancellationToken token)
    {
        var root = await ConventionGenerationPreflight.ReadInputs(project, required, token);
        return root is null || HasRoleOverride(root) ? null : root;
    }

    internal static bool HasRoleOverride(CompilationUnitSyntax root) =>
        root.AttributeLists.SelectMany(list => list.Attributes).Any(attribute =>
            attribute.Name.ToString() is "DRN.Conventions.Role" or "global::DRN.Conventions.Role");

    internal static bool ValidNamespace(string ns) =>
        !string.IsNullOrEmpty(ns) && ns.Split('.').All(ConventionGenerationPreflight.ValidNamespaceSegment);

    internal const string OwnershipAttributeMetadataName = "DRN.Framework.SharedKernel.Attributes.SourceKnownSubdomainAttribute";

    internal sealed record OwnerKey(string Domain, string Subdomain, bool HasFolderDomain, bool HasExplicitDomain)
    {
        internal string NamespacePrefix => ConventionNames.OwnerNamespacePrefix(Domain, Subdomain, HasFolderDomain);
        internal string? DomainFilter => Domain.Length > 0 ? Domain : null;

        internal bool SameOwner(OwnerKey other) =>
            ConventionNames.SameOwner(Domain, Subdomain, other.Domain, other.Subdomain);

        internal bool MatchesAcrossLayers(OwnerKey other) =>
            ConventionNames.MatchesOwnerAcrossLayers(Domain, Subdomain, HasFolderDomain, HasExplicitDomain,
                other.Domain, other.Subdomain, other.HasFolderDomain, other.HasExplicitDomain);
    }

    internal static bool OwnershipCompatible(CompilationUnitSyntax root, string typeName, string owner, CancellationToken token,
        string? domain = null)
    {
        foreach (var attribute in root.AttributeLists.SelectMany(list => list.Attributes))
        {
            token.ThrowIfCancellationRequested();
            if (attribute.Name.ToString() is not ("DRN.Conventions.Ownership" or "global::DRN.Conventions.Ownership")) continue;
            if (attribute.ArgumentList is not { Arguments.Count: > 0 } arguments ||
                arguments.Arguments[0].NameEquals is not null || arguments.Arguments[0].NameColon is not null ||
                ConventionInputSyntax.String(arguments.Arguments[0].Expression, token) is not { } target)
                return false;
            if (target != typeName) continue;
            var owners = arguments.Arguments.Where(argument => argument.NameEquals?.Name.Identifier.ValueText == "Subdomain").ToArray();
            if (owners.Length != 1 || ConventionInputSyntax.String(owners[0].Expression, token) != owner) return false;
            var domains = arguments.Arguments.Where(argument => argument.NameEquals?.Name.Identifier.ValueText == "Domain").ToArray();
            if (domains.Length > 1) return false;
            if (domains.Length == 1)
            {
                var declaredDomain = ConventionInputSyntax.String(domains[0].Expression, token);
                if (declaredDomain is null || declaredDomain.Length > 0 && (domain is null || declaredDomain != domain))
                    return false;
            }
        }
        return true;
    }

    internal static OwnerKey? ResolveNamespaceOwnerKey(INamedTypeSymbol type, string rootNamespace,
        ConventionGenerationPreflight.ProjectLayout layout)
    {
        if (!layout.Valid || type.ContainingNamespace.IsGlobalNamespace || rootNamespace.Length == 0) return null;
        var ns = type.ContainingNamespace.ToDisplayString();
        if (!ns.StartsWith(rootNamespace + ".", StringComparison.Ordinal)) return null;
        var parts = ns[(rootNamespace.Length + 1)..].Split('.');
        if (ConventionNames.IsMissingAggregateSubdomain(parts, 0, layout.IsFoldersLayout, type.Name) &&
            Bases(type).Any(current => MetadataName(current) == "DRN.Framework.SharedKernel.Domain.AggregateRoot"))
            return null;
        return ResolveOwnerKey(parts, layout);
    }

    internal static OwnerKey? ResolveSuffixOwnerKey(string suffix, ConventionGenerationPreflight.ProjectLayout layout) =>
        layout.Valid ? ResolveOwnerKey(suffix.Split('.'), layout) : null;

    private static OwnerKey? ResolveOwnerKey(string[] parts, ConventionGenerationPreflight.ProjectLayout layout)
    {
        if (ConventionNames.InferNamespaceOwner(parts, 0, layout.IsFoldersLayout, layout.DefaultDomain) is not { } owner)
            return null;
        if (!ConventionGenerationPreflight.ValidNamespaceSegment(owner.Subdomain) ||
            owner.HasFolderDomain && !ConventionGenerationPreflight.ValidNamespaceSegment(owner.Domain))
            return null;
        return new(owner.Domain, owner.Subdomain, owner.HasFolderDomain, layout.HasExplicitDomain);
    }

    internal static string? AttributeStringArgument(AttributeData attribute, string name) =>
        attribute.NamedArguments.FirstOrDefault(pair => pair.Key == name).Value.Value as string;

    internal static bool HasReason(AttributeData attribute) =>
        !string.IsNullOrWhiteSpace(AttributeStringArgument(attribute, "Reason"));

    internal static bool MatchesSubdomainAttribute(AttributeData attribute, OwnerKey owner) =>
        attribute.ConstructorArguments is [{ Value: string declared }] && declared == owner.Subdomain &&
        (AttributeStringArgument(attribute, "Domain") is not { Length: > 0 } domain || domain == owner.Domain);

    internal static bool AttributesPreserveOwner(INamedTypeSymbol type, OwnerKey owner, Compilation compilation)
    {
        var ownership = compilation.GetTypeByMetadataName(OwnershipAttributeMetadataName);
        return type.GetAttributes().Where(attribute => Same(attribute.AttributeClass, ownership))
            .All(attribute => MatchesSubdomainAttribute(attribute, owner));
    }

    internal static bool ContextOwnerCompatible(SeparatedSyntaxList<AttributeArgumentSyntax> arguments,
        OwnerKey owner, CancellationToken token, out bool hasDeclaredSubdomain)
    {
        hasDeclaredSubdomain = false;
        var domains = arguments.Where(argument => argument.NameEquals?.Name.Identifier.ValueText == "Domain").ToArray();
        if (domains.Length > 1) return false;
        var declaredDomain = domains.Length == 1 ? ConventionInputSyntax.String(domains[0].Expression, token) : "";
        if (declaredDomain is null || declaredDomain.Length > 0 && declaredDomain != owner.Domain) return false;
        var owners = arguments.Where(argument => argument.NameEquals?.Name.Identifier.ValueText == "Subdomain").ToArray();
        if (owners.Length > 1) return false;
        var declaredSubdomain = owners.Length == 1 ? ConventionInputSyntax.String(owners[0].Expression, token) : "";
        if (declaredSubdomain is null || declaredSubdomain.Length > 0 && declaredSubdomain != owner.Subdomain) return false;
        if (declaredDomain.Length > 0 && declaredSubdomain.Length == 0) return false;
        hasDeclaredSubdomain = declaredSubdomain.Length > 0;
        return true;
    }

    internal static OwnerKey? DeclaredOwner(INamedTypeSymbol type, OwnerKey? conventional, Compilation compilation)
    {
        var ownership = compilation.GetTypeByMetadataName(OwnershipAttributeMetadataName);
        var declarations = type.GetAttributes().Where(attribute => Same(attribute.AttributeClass, ownership)).ToArray();
        if (declarations.Length == 0) return conventional;
        if (declarations.Length != 1 || declarations[0].ConstructorArguments is not [{ Value: string subdomain }] ||
            !ConventionGenerationPreflight.ValidNamespaceSegment(subdomain)) return null;
        var declaredDomain = AttributeStringArgument(declarations[0], "Domain");
        if (!string.IsNullOrEmpty(declaredDomain) && !ConventionGenerationPreflight.ValidNamespaceSegment(declaredDomain))
            return null;
        var hasExplicitDomain = !string.IsNullOrEmpty(declaredDomain);
        var effectiveDomain = hasExplicitDomain ? declaredDomain! : conventional?.Domain ?? "";
        if (conventional is { HasFolderDomain: true } && !ConventionGenerationPreflight.ValidNamespaceSegment(effectiveDomain))
            return null;
        var resolved = new OwnerKey(effectiveDomain, subdomain,
            conventional?.HasFolderDomain ?? false,
            (conventional?.HasExplicitDomain ?? false) || hasExplicitDomain);
        var matchesConventional = conventional is not null && resolved.SameOwner(conventional);
        return matchesConventional || HasReason(declarations[0]) ? resolved : null;
    }
}
