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
            ConventionInputSyntax.Kind(attribute) == ConventionInputSyntax.RoleKind);

    internal static bool ValidNamespace(string ns) =>
        !string.IsNullOrEmpty(ns) && ns.Split('.').All(ConventionGenerationPreflight.ValidNamespaceSegment);

    internal const string OwnershipAttributeMetadataName = "DRN.Framework.SharedKernel.Attributes.SourceKnownSubdomainAttribute";

    internal sealed record OwnerKey(string Domain, string Subdomain, bool HasFolderDomain, bool HasExplicitDomain)
    {
        internal string NamespacePrefix => ConventionNames.OwnerNamespacePrefix(Domain, Subdomain, HasFolderDomain);
        internal string? DomainFilter => Domain.Length > 0 ? Domain : null;

        internal OwnerKey WithExplicitDomain(bool explicitDomain) =>
            explicitDomain && !HasExplicitDomain ? this with { HasExplicitDomain = true } : this;

        internal bool SameOwner(OwnerKey other) =>
            ConventionNames.SameOwner(Domain, Subdomain, other.Domain, other.Subdomain);

        internal bool MatchesAcrossLayers(OwnerKey other) =>
            ConventionNames.MatchesOwnerAcrossLayers(
                new(Domain, Subdomain, HasFolderDomain, HasExplicitDomain),
                new(other.Domain, other.Subdomain, other.HasFolderDomain, other.HasExplicitDomain));
    }

    internal static bool TryReadSingleOptionalField(SeparatedSyntaxList<AttributeArgumentSyntax> arguments,
        string name, CancellationToken token, out string value)
    {
        value = "";
        var found = false;
        foreach (var argument in arguments)
        {
            if (argument.NameEquals?.Name.Identifier.ValueText != name) continue;
            if (found || ConventionInputSyntax.String(argument.Expression, token) is not { } parsed) return false;
            value = parsed;
            found = true;
        }
        return true;
    }

    internal static bool OwnershipCompatible(CompilationUnitSyntax root, string typeName, string owner, CancellationToken token,
        out bool hasExplicitDomain, string? domain = null)
    {
        hasExplicitDomain = false;
        foreach (var attribute in root.AttributeLists.SelectMany(list => list.Attributes))
        {
            token.ThrowIfCancellationRequested();
            if (!IsOwnershipAttributeCompatible(attribute, typeName, owner, domain, token, out var explicitDomain))
                return false;
            hasExplicitDomain |= explicitDomain;
        }
        return true;
    }

    private static bool IsOwnershipAttributeCompatible(AttributeSyntax attribute, string typeName, string owner,
        string? domain, CancellationToken token, out bool hasExplicitDomain)
    {
        hasExplicitDomain = false;
        if (ConventionInputSyntax.Kind(attribute) != ConventionInputSyntax.OwnershipKind)
            return true;
        if (attribute.ArgumentList is not { Arguments.Count: > 0 } arguments ||
            arguments.Arguments[0].NameEquals is not null || arguments.Arguments[0].NameColon is not null ||
            ConventionInputSyntax.String(arguments.Arguments[0].Expression, token) is not { } target)
            return false;
        if (target != typeName) return true;
        if (!TryReadSingleOptionalField(arguments.Arguments, ConventionInputSyntax.SubdomainField, token, out var declaredOwner) ||
            declaredOwner != owner)
            return false;
        if (!TryReadSingleOptionalField(arguments.Arguments, ConventionInputSyntax.DomainField, token, out var declaredDomain))
            return false;
        if (declaredDomain.Length > 0)
        {
            if (declaredDomain != domain) return false;
            hasExplicitDomain = true;
        }
        return true;
    }

    internal static OwnerKey? ResolveNamespaceOwnerKey(INamedTypeSymbol type, string rootNamespace,
        ConventionGenerationPreflight.ProjectLayout layout)
    {
        if (!layout.Valid || type.ContainingNamespace.IsGlobalNamespace || rootNamespace.Length == 0) return null;
        var ns = type.ContainingNamespace.ToDisplayString();
        if (!ns.StartsWith(rootNamespace + ".", StringComparison.Ordinal)) return null;
        return ResolveOwnerKey(type, ns[(rootNamespace.Length + 1)..].Split('.'), layout);
    }

    internal static OwnerKey? ResolveSuffixOwnerKey(INamedTypeSymbol type, string suffix,
        ConventionGenerationPreflight.ProjectLayout layout) =>
        layout.Valid ? ResolveOwnerKey(type, suffix.Split('.'), layout) : null;

    private static OwnerKey? ResolveOwnerKey(INamedTypeSymbol type, string[] parts,
        ConventionGenerationPreflight.ProjectLayout layout)
    {
        if (ConventionNames.IsMissingAggregateSubdomain(parts, 0, layout.IsFoldersLayout, type.Name) &&
            Bases(type).Any(current => MetadataName(current) == "DRN.Framework.SharedKernel.Domain.AggregateRoot"))
            return null;
        if (ConventionNames.InferNamespaceOwner(parts, 0, layout.IsFoldersLayout, layout.DefaultDomain) is not { } owner)
            return null;
        if (!ConventionGenerationPreflight.ValidNamespaceSegment(owner.Subdomain) ||
            owner.HasFolderDomain && !ConventionGenerationPreflight.ValidNamespaceSegment(owner.Domain))
            return null;
        return new(owner.Domain, owner.Subdomain, owner.HasFolderDomain, layout.HasExplicitDomain);
    }

    internal static bool TryGetOptionalStringArgument(AttributeData attribute, string name, out string? value)
    {
        value = null;
        if (attribute.AttributeConstructor is null) return false;
        foreach (var argument in attribute.ConstructorArguments)
            if (argument.Kind == TypedConstantKind.Error) return false;
        foreach (var pair in attribute.NamedArguments)
        {
            if (pair.Value.Kind == TypedConstantKind.Error) return false;
            if (pair.Key != name) continue;
            if (pair.Value.IsNull)
            {
                value = null;
                continue;
            }
            if (pair.Value.Value is not string text) return false;
            value = text;
        }
        return true;
    }

    internal static bool HasReason(AttributeData attribute) =>
        TryGetOptionalStringArgument(attribute, ConventionInputSyntax.ReasonField, out var reason) &&
        !string.IsNullOrWhiteSpace(reason);

    internal static bool MatchesSubdomainAttribute(AttributeData attribute, OwnerKey owner) =>
        MatchesSubdomainAttribute(attribute, owner, out _);

    internal static bool MatchesSubdomainAttribute(AttributeData attribute, OwnerKey owner, out bool hasExplicitDomain)
    {
        hasExplicitDomain = false;
        if (!TryGetOptionalStringArgument(attribute, ConventionInputSyntax.DomainField, out var domain)) return false;
        if (attribute.ConstructorArguments is not [{ Value: string declared }] || declared != owner.Subdomain) return false;
        if (!string.IsNullOrEmpty(domain))
        {
            if (domain != owner.Domain) return false;
            hasExplicitDomain = true;
        }
        return true;
    }

    internal static bool AttributesPreserveOwner(INamedTypeSymbol type, OwnerKey owner, Compilation compilation, out bool hasExplicitDomain)
    {
        hasExplicitDomain = false;
        var ownership = compilation.GetTypeByMetadataName(OwnershipAttributeMetadataName);
        foreach (var attribute in type.GetAttributes())
        {
            if (!Same(attribute.AttributeClass, ownership)) continue;
            if (!MatchesSubdomainAttribute(attribute, owner, out var attrExplicitDomain)) return false;
            hasExplicitDomain |= attrExplicitDomain;
        }
        return true;
    }

    internal static bool ContextOwnerCompatible(SeparatedSyntaxList<AttributeArgumentSyntax> arguments,
        OwnerKey owner, CancellationToken token, out bool hasDeclaredSubdomain, out bool hasExplicitDomain)
    {
        hasDeclaredSubdomain = false;
        hasExplicitDomain = false;
        if (!TryReadSingleOptionalField(arguments, ConventionInputSyntax.DomainField, token, out var declaredDomain) ||
            declaredDomain.Length > 0 && declaredDomain != owner.Domain)
            return false;
        if (!TryReadSingleOptionalField(arguments, ConventionInputSyntax.SubdomainField, token, out var declaredSubdomain) ||
            declaredSubdomain.Length > 0 && declaredSubdomain != owner.Subdomain)
            return false;
        if (declaredDomain.Length > 0 && declaredSubdomain.Length == 0) return false;
        hasDeclaredSubdomain = declaredSubdomain.Length > 0;
        hasExplicitDomain = declaredDomain.Length > 0;
        return true;
    }

    internal static OwnerKey? DeclaredOwner(INamedTypeSymbol type, OwnerKey? conventional, Compilation compilation)
    {
        var ownership = compilation.GetTypeByMetadataName(OwnershipAttributeMetadataName);
        var declarations = type.GetAttributes().Where(attribute => Same(attribute.AttributeClass, ownership)).ToArray();
        if (declarations.Length == 0) return conventional;
        if (declarations.Length != 1 ||
            !TryGetOptionalStringArgument(declarations[0], ConventionInputSyntax.DomainField, out var declaredDomain) ||
            declarations[0].ConstructorArguments is not [{ Value: string subdomain }] ||
            !ConventionGenerationPreflight.ValidNamespaceSegment(subdomain))
            return null;
        if (!string.IsNullOrEmpty(declaredDomain) && !ConventionGenerationPreflight.ValidNamespaceSegment(declaredDomain))
            return null;
        var hasExplicitDomain = !string.IsNullOrEmpty(declaredDomain);
        var effectiveDomain = hasExplicitDomain ? declaredDomain! : conventional?.Domain ?? "";
        if (conventional is { HasFolderDomain: true } && !ConventionGenerationPreflight.ValidNamespaceSegment(effectiveDomain))
            return null;
        var resolved = new OwnerKey(effectiveDomain, subdomain,
            conventional?.HasFolderDomain ?? false,
            (conventional?.HasExplicitDomain ?? false) || hasExplicitDomain);
        return conventional is not null && resolved.SameOwner(conventional) ? resolved : null;
    }
}
