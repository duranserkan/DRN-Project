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

    internal static bool OwnershipCompatible(CompilationUnitSyntax root, string typeName, string owner, CancellationToken token)
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
        }
        return true;
    }

    internal static string? NamespaceOwner(INamedTypeSymbol type, string rootNamespace)
    {
        if (type.ContainingNamespace.IsGlobalNamespace) return null;
        var ns = type.ContainingNamespace.ToDisplayString();
        return ns.StartsWith(rootNamespace + ".", StringComparison.Ordinal) ? ns[(rootNamespace.Length + 1)..].Split('.')[0] : null;
    }

    internal static string? DeclaredSubdomain(INamedTypeSymbol type, string? conventional, Compilation compilation)
    {
        var ownership = compilation.GetTypeByMetadataName("DRN.Framework.SharedKernel.Attributes.SourceKnownSubdomainAttribute");
        var declarations = type.GetAttributes().Where(attribute => Same(attribute.AttributeClass, ownership)).ToArray();
        if (declarations.Length == 0) return conventional;
        if (declarations.Length != 1 || declarations[0].ConstructorArguments is not [{ Value: string owner }] ||
            !ConventionGenerationPreflight.ValidNamespaceSegment(owner)) return null;
        var reason = declarations[0].NamedArguments.FirstOrDefault(pair => pair.Key == "Reason").Value.Value as string;
        return owner == conventional || !string.IsNullOrWhiteSpace(reason) ? owner : null;
    }
}
