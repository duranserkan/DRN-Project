using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static DRN.Framework.SharedKernel.CodeFixes.CodeFixSymbolFacts;

namespace DRN.Framework.SharedKernel.CodeFixes;

internal static class ConfigurationDiscovery
{
    internal sealed record Scope(string Namespace, bool Children)
    {
        internal bool Contains(string value) => value == Namespace || Children && value.StartsWith(Namespace + ".", StringComparison.Ordinal);
    }

    // Only complete, straight-line implementations establish absence of another scan.
    internal static List<Scope>? Read(INamedTypeSymbol context, Compilation compilation, CancellationToken token)
    {
        var discovery = DiscoveryContext.Create(context, compilation, token);
        return WalkContextHierarchy(discovery, context);
    }

    private sealed record DiscoveryContext(Compilation Compilation, INamedTypeSymbol? Drn, INamedTypeSymbol? Identity,
        INamedTypeSymbol? DbContext, INamedTypeSymbol? Builder, INamedTypeSymbol? RuntimeType,
        INamedTypeSymbol RootContext, CancellationToken Token)
    {
        internal static DiscoveryContext Create(INamedTypeSymbol context, Compilation compilation, CancellationToken token) => new(
            compilation,
            compilation.GetTypeByMetadataName("DRN.Framework.EntityFramework.Context.DrnContext`1"),
            compilation.GetTypeByMetadataName("DRN.Framework.EntityFramework.Context.Identity.DrnContextIdentity`2"),
            compilation.GetTypeByMetadataName("Microsoft.EntityFrameworkCore.DbContext"),
            compilation.GetTypeByMetadataName("Microsoft.EntityFrameworkCore.ModelBuilder"),
            compilation.GetTypeByMetadataName("System.Type"),
            context,
            token);
    }

    private static List<Scope>? WalkContextHierarchy(DiscoveryContext discovery, INamedTypeSymbol? current)
    {
        discovery.Token.ThrowIfCancellationRequested();
        if (current is null) return null;
        if (Same(current, discovery.Drn) || Same(current, discovery.Identity))
            return [new(discovery.RootContext.ContainingNamespace.ToDisplayString(), true)];
        if (Same(current, discovery.DbContext)) return [];
        var methods = current.GetMembers("OnModelCreating").OfType<IMethodSymbol>()
            .Where(method => method.Parameters is [{ Type: INamedTypeSymbol parameter }] &&
                             Same(parameter, discovery.Builder) && OverridesEfModel(discovery, method)).ToArray();
        if (methods.Length == 0) return WalkContextHierarchy(discovery, current.BaseType);
        if (methods is not [var method]) return null;
        return TryReadOnModelCreatingScopes(discovery, current, method);
    }

    private static bool OverridesEfModel(DiscoveryContext discovery, IMethodSymbol method)
    {
        for (var overridden = method.OverriddenMethod; overridden is not null; overridden = overridden.OverriddenMethod)
        {
            discovery.Token.ThrowIfCancellationRequested();
            if (Same(overridden.ContainingType, discovery.DbContext)) return true;
        }
        return false;
    }

    private static List<Scope>? TryReadOnModelCreatingScopes(DiscoveryContext discovery, INamedTypeSymbol current, IMethodSymbol method)
    {
        if (method.DeclaringSyntaxReferences is not [var reference] ||
            !discovery.Compilation.ContainsSyntaxTree(reference.SyntaxTree) ||
            reference.GetSyntax(discovery.Token) is not MethodDeclarationSyntax declaration) return null;
        var model = discovery.Compilation.GetSemanticModel(declaration.SyntaxTree);
        if (model.GetDiagnostics(declaration.Span, discovery.Token).Any(item => item.Severity == DiagnosticSeverity.Error)) return null;
        if (!TryGetMethodExpressions(declaration, out var expressions)) return null;
        var result = new List<Scope>();
        foreach (var expression in expressions)
        {
            discovery.Token.ThrowIfCancellationRequested();
            if (!TryAddInvocationScope(discovery, current, method, model, expression, result)) return null;
        }
        return result;
    }

    private static bool TryGetMethodExpressions(MethodDeclarationSyntax declaration, out ExpressionSyntax[] expressions)
    {
        if (declaration.ExpressionBody is { } expressionBody)
        {
            expressions = [expressionBody.Expression];
            return true;
        }
        if (declaration.Body is { } body && body.Statements.All(statement => statement is ExpressionStatementSyntax))
        {
            expressions = body.Statements.Cast<ExpressionStatementSyntax>().Select(statement => statement.Expression).ToArray();
            return true;
        }
        expressions = [];
        return false;
    }

    private static bool TryAddInvocationScope(DiscoveryContext discovery, INamedTypeSymbol current, IMethodSymbol method,
        SemanticModel model, ExpressionSyntax expression, List<Scope> result)
    {
        if (expression is not InvocationExpressionSyntax invocation || invocation.Expression is not MemberAccessExpressionSyntax access ||
            model.GetSymbolInfo(invocation, discovery.Token).Symbol is not IMethodSymbol called) return false;
        if (access.Expression is BaseExpressionSyntax && SymbolEqualityComparer.Default.Equals(called, method.OverriddenMethod) &&
            invocation.ArgumentList.Arguments is [var argument] &&
            SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(argument.Expression, discovery.Token).Symbol, method.Parameters[0]))
        {
            var inherited = WalkContextHierarchy(discovery, current.BaseType);
            if (inherited is null) return false;
            result.AddRange(inherited);
            return true;
        }
        if (!IsLocalConfigurationScan(discovery, method, model, invocation, access, called, out var predicate) ||
            !TryReadPredicateNamespace(discovery, model, predicate, out var ns)) return false;
        result.Add(new(ns, false));
        return true;
    }

    private static bool IsLocalConfigurationScan(DiscoveryContext discovery, IMethodSymbol method, SemanticModel model,
        InvocationExpressionSyntax invocation, MemberAccessExpressionSyntax access, IMethodSymbol called,
        [NotNullWhen(true)] out ArgumentSyntax? predicate)
    {
        predicate = null;
        if (called.Name != "ApplyConfigurationsFromAssembly" || !Same(called.ContainingType, discovery.Builder) ||
            !SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(access.Expression, discovery.Token).Symbol, method.Parameters[0]) ||
            invocation.ArgumentList.Arguments is not [var assembly, var candidatePredicate] ||
            assembly.NameColon is not null || candidatePredicate.NameColon is not null) return false;
        if (assembly.Expression is not MemberAccessExpressionSyntax { Expression: TypeOfExpressionSyntax typeOf } assemblyAccess ||
            model.GetSymbolInfo(assemblyAccess, discovery.Token).Symbol is not IPropertySymbol assemblyProperty ||
            assemblyProperty.Name != "Assembly" || !Same(assemblyProperty.ContainingType, discovery.RuntimeType) ||
            model.GetTypeInfo(typeOf.Type, discovery.Token).Type is not INamedTypeSymbol scannedType ||
            !SymbolEqualityComparer.Default.Equals(scannedType.ContainingAssembly, discovery.Compilation.Assembly)) return false;
        predicate = candidatePredicate;
        return true;
    }

    private static bool TryReadPredicateNamespace(DiscoveryContext discovery, SemanticModel model, ArgumentSyntax predicate,
        [NotNullWhen(true)] out string? ns)
    {
        ns = null;
        var lambda = predicate.Expression as LambdaExpressionSyntax;
        var parameterSyntax = lambda switch
        {
            SimpleLambdaExpressionSyntax simple => simple.Parameter,
            ParenthesizedLambdaExpressionSyntax { ParameterList.Parameters: [var single] } => single,
            _ => null
        };
        if (parameterSyntax is null || lambda!.Body is not BinaryExpressionSyntax comparison ||
            !comparison.IsKind(SyntaxKind.EqualsExpression)) return false;
        var parameterSymbol = model.GetDeclaredSymbol(parameterSyntax, discovery.Token);
        var constant = TryResolveNamespaceComparison(discovery, model, comparison, parameterSymbol);
        if (!constant.HasValue || constant.Value is not string resolved || resolved.Length == 0) return false;
        ns = resolved;
        return true;
    }

    private static Optional<object?> TryResolveNamespaceComparison(DiscoveryContext discovery, SemanticModel model,
        BinaryExpressionSyntax comparison, ISymbol? parameterSymbol)
    {
        if (IsNamespacePropertyAccess(discovery, model, comparison.Left, parameterSymbol))
            return model.GetConstantValue(comparison.Right, discovery.Token);
        if (IsNamespacePropertyAccess(discovery, model, comparison.Right, parameterSymbol))
            return model.GetConstantValue(comparison.Left, discovery.Token);
        return default;
    }

    private static bool IsNamespacePropertyAccess(DiscoveryContext discovery, SemanticModel model,
        ExpressionSyntax node, ISymbol? parameterSymbol) =>
        node is MemberAccessExpressionSyntax member &&
        model.GetSymbolInfo(member, discovery.Token).Symbol is IPropertySymbol property &&
        property.Name == "Namespace" && Same(property.ContainingType, discovery.RuntimeType) &&
        SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(member.Expression, discovery.Token).Symbol, parameterSymbol);
}
