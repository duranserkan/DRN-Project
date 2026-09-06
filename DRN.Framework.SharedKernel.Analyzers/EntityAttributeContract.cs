using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DRN.Framework.SharedKernel.Analyzers;

internal static class EntityAttributeContract
{
    internal static bool IsSupported(INamedTypeSymbol? attributeType, Compilation compilation, CancellationToken cancellationToken)
    {
        var genericAttribute = compilation.GetTypeByMetadataName(EntityAnalyzerHelper.EntityTypeAttributeMetadataName + "`1");
        for (var current = attributeType; current != null; current = current.BaseType)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, genericAttribute))
                return true;

            // Metadata contains signatures, not constructor bodies. Producers must run this
            // analyzer to validate forwarding; never execute referenced constructors here.
            if (!HasValidForwardingContract(current, compilation, cancellationToken))
                return false;
        }

        return false;
    }

    private static bool HasValidForwardingContract(INamedTypeSymbol current, Compilation compilation, CancellationToken cancellationToken)
    {
        // A single forwarding constructor makes the metadata contract unambiguous.
        if (current.InstanceConstructors.Length != 1)
            return false;

        var constructor = current.InstanceConstructors[0];
        if (constructor.Parameters.Length != 1 || !IsIdentityType(constructor.Parameters[0].Type))
            return false;

        return ForwardsParameterToInitializer(constructor.OriginalDefinition.Parameters[0], compilation, cancellationToken);
    }

    private static bool ForwardsParameterToInitializer(
        IParameterSymbol parameter,
        Compilation compilation,
        CancellationToken cancellationToken)
    {
        foreach (var syntaxReference in parameter.DeclaringSyntaxReferences)
        {
            // The parameter's owner is either a primary-constructor class or an ordinary constructor.
            var syntax = syntaxReference.GetSyntax(cancellationToken).Parent?.Parent;
            if (syntax == null)
                return false;
            if (!compilation.ContainsSyntaxTree(syntax.SyntaxTree))
                continue; // Compilation references have the same producer-validation boundary as metadata.

            if (!IsArgumentForwarded(syntax, parameter, compilation, cancellationToken))
                return false;
        }

        return true;
    }

    private static SeparatedSyntaxList<ArgumentSyntax>? GetInitializerArguments(SyntaxNode syntax) => syntax switch
    {
        ClassDeclarationSyntax declaration => declaration.BaseList?.Types
            .OfType<PrimaryConstructorBaseTypeSyntax>().SingleOrDefault()?.ArgumentList.Arguments,
        ConstructorDeclarationSyntax declaration => declaration.Initializer?.ArgumentList.Arguments,
        _ => null
    };

    private static bool IsArgumentForwarded(
        SyntaxNode syntax,
        IParameterSymbol parameter,
        Compilation compilation,
        CancellationToken cancellationToken)
    {
        var arguments = GetInitializerArguments(syntax);
        if (arguments is not { Count: 1 })
            return false;

        var model = compilation.GetSemanticModel(syntax.SyntaxTree);
        var expression = UnwrapForwardedExpression(arguments.Value[0].Expression, model, cancellationToken);

        return SymbolEqualityComparer.Default.Equals(
            model.GetSymbolInfo(expression, cancellationToken).Symbol,
            parameter);
    }

    private static ExpressionSyntax UnwrapForwardedExpression(
        ExpressionSyntax expression,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (expression is ParenthesizedExpressionSyntax parentheses)
            {
                expression = parentheses.Expression;
            }
            else if (expression is CastExpressionSyntax cast && IsIdentityCast(cast, model, cancellationToken))
            {
                expression = cast.Expression;
            }
            else
            {
                return expression;
            }
        }
    }

    private static bool IsIdentityCast(CastExpressionSyntax cast, SemanticModel model, CancellationToken cancellationToken) =>
        model.GetTypeInfo(cast.Type, cancellationToken).Type is { } castType && IsIdentityType(castType);

    private static bool IsIdentityType(ITypeSymbol type) => type.SpecialType == SpecialType.System_Byte ||
        type is INamedTypeSymbol { TypeKind: TypeKind.Enum, EnumUnderlyingType.SpecialType: SpecialType.System_Byte };
}
