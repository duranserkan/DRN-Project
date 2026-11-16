using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static DRN.Framework.SharedKernel.CodeFixes.EntityConfigurationRefactoringProvider;

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
        var drn = compilation.GetTypeByMetadataName("DRN.Framework.EntityFramework.Context.DrnContext`1");
        var identity = compilation.GetTypeByMetadataName("DRN.Framework.EntityFramework.Context.Identity.DrnContextIdentity`2");
        var dbContext = compilation.GetTypeByMetadataName("Microsoft.EntityFrameworkCore.DbContext");
        var builder = compilation.GetTypeByMetadataName("Microsoft.EntityFrameworkCore.ModelBuilder");
        var runtimeType = compilation.GetTypeByMetadataName("System.Type");
        bool OverridesEfModel(IMethodSymbol method)
        {
            for (var overridden = method.OverriddenMethod; overridden is not null; overridden = overridden.OverriddenMethod)
            {
                token.ThrowIfCancellationRequested();
                if (Same(overridden.ContainingType, dbContext)) return true;
            }
            return false;
        }
        List<Scope>? Walk(INamedTypeSymbol? current)
        {
            token.ThrowIfCancellationRequested();
            if (current is null) return null;
            if (Same(current, drn) || Same(current, identity)) return [new(context.ContainingNamespace.ToDisplayString(), true)];
            if (Same(current, dbContext)) return [];
            var methods = current.GetMembers("OnModelCreating").OfType<IMethodSymbol>()
                .Where(method => method.Parameters is [{ Type: INamedTypeSymbol parameter }] && Same(parameter, builder) && OverridesEfModel(method)).ToArray();
            if (methods.Length == 0) return Walk(current.BaseType);
            if (methods is not [var method] || method.DeclaringSyntaxReferences is not [var reference] ||
                !compilation.ContainsSyntaxTree(reference.SyntaxTree) || reference.GetSyntax(token) is not MethodDeclarationSyntax declaration) return null;
            var model = compilation.GetSemanticModel(declaration.SyntaxTree);
            if (model.GetDiagnostics(declaration.Span, token).Any(item => item.Severity == DiagnosticSeverity.Error)) return null;
            ExpressionSyntax[] expressions;
            if (declaration.ExpressionBody is { } expressionBody) expressions = [expressionBody.Expression];
            else if (declaration.Body is { } body && body.Statements.All(statement => statement is ExpressionStatementSyntax))
                expressions = body.Statements.Cast<ExpressionStatementSyntax>().Select(statement => statement.Expression).ToArray();
            else return null;
            var result = new List<Scope>();
            foreach (var expression in expressions)
            {
                token.ThrowIfCancellationRequested();
                if (expression is not InvocationExpressionSyntax invocation || invocation.Expression is not MemberAccessExpressionSyntax access ||
                    model.GetSymbolInfo(invocation, token).Symbol is not IMethodSymbol called) return null;
                if (access.Expression is BaseExpressionSyntax && SymbolEqualityComparer.Default.Equals(called, method.OverriddenMethod) &&
                    invocation.ArgumentList.Arguments is [var argument] &&
                    SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(argument.Expression, token).Symbol, method.Parameters[0]))
                {
                    var inherited = Walk(current.BaseType);
                    if (inherited is null) return null;
                    result.AddRange(inherited);
                    continue;
                }
                if (called.Name != "ApplyConfigurationsFromAssembly" || !Same(called.ContainingType, builder) ||
                    !SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(access.Expression, token).Symbol, method.Parameters[0]) ||
                    invocation.ArgumentList.Arguments is not [var assembly, var predicate] ||
                    assembly.NameColon is not null || predicate.NameColon is not null ||
                    assembly.Expression is not MemberAccessExpressionSyntax { Expression: TypeOfExpressionSyntax typeOf } assemblyAccess ||
                    model.GetSymbolInfo(assemblyAccess, token).Symbol is not IPropertySymbol assemblyProperty || assemblyProperty.Name != "Assembly" ||
                    !Same(assemblyProperty.ContainingType, runtimeType) ||
                    model.GetTypeInfo(typeOf.Type, token).Type is not INamedTypeSymbol scannedType ||
                    !SymbolEqualityComparer.Default.Equals(scannedType.ContainingAssembly, compilation.Assembly)) return null;
                var lambda = predicate.Expression as LambdaExpressionSyntax;
                var parameterSyntax = lambda switch
                {
                    SimpleLambdaExpressionSyntax simple => simple.Parameter,
                    ParenthesizedLambdaExpressionSyntax { ParameterList.Parameters: [var single] } => single,
                    _ => null
                };
                if (parameterSyntax is null || lambda!.Body is not BinaryExpressionSyntax comparison ||
                    !comparison.IsKind(SyntaxKind.EqualsExpression)) return null;
                var parameterSymbol = model.GetDeclaredSymbol(parameterSyntax, token);
                bool NamespaceProperty(ExpressionSyntax node) => node is MemberAccessExpressionSyntax member &&
                    model.GetSymbolInfo(member, token).Symbol is IPropertySymbol property && property.Name == "Namespace" &&
                    Same(property.ContainingType, runtimeType) &&
                    SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(member.Expression, token).Symbol, parameterSymbol);
                var constant = NamespaceProperty(comparison.Left) ? model.GetConstantValue(comparison.Right, token) :
                    NamespaceProperty(comparison.Right) ? model.GetConstantValue(comparison.Left, token) : default;
                if (!constant.HasValue || constant.Value is not string ns || ns.Length == 0) return null;
                result.Add(new(ns, false));
            }
            return result;
        }
        return Walk(context);
    }
}
