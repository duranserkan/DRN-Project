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

            // A single forwarding constructor makes the metadata contract unambiguous.
            if (current.InstanceConstructors.Length != 1)
                return false;

            var constructor = current.InstanceConstructors[0];
            if (constructor.Parameters.Length != 1 || !IsIdentityType(constructor.Parameters[0].Type))
                return false;

            foreach (var syntaxReference in constructor.OriginalDefinition.Parameters[0].DeclaringSyntaxReferences)
            {
                // The parameter's owner is either a primary-constructor class or an ordinary constructor.
                var syntax = syntaxReference.GetSyntax(cancellationToken).Parent?.Parent;
                if (syntax == null)
                    return false;
                if (!compilation.ContainsSyntaxTree(syntax.SyntaxTree))
                    continue; // Compilation references have the same producer-validation boundary as metadata.

                SeparatedSyntaxList<ArgumentSyntax>? arguments = syntax switch
                {
                    ClassDeclarationSyntax declaration => declaration.BaseList?.Types
                        .OfType<PrimaryConstructorBaseTypeSyntax>().SingleOrDefault()?.ArgumentList.Arguments,
                    ConstructorDeclarationSyntax declaration => declaration.Initializer?.ArgumentList.Arguments,
                    _ => null
                };

                if (arguments is not { Count: 1 })
                    return false;

                var model = compilation.GetSemanticModel(syntax.SyntaxTree);
                ExpressionSyntax expression = arguments.Value[0].Expression;
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (expression is ParenthesizedExpressionSyntax parentheses)
                        expression = parentheses.Expression;
                    else if (expression is CastExpressionSyntax cast &&
                             model.GetTypeInfo(cast.Type, cancellationToken).Type is { } castType && IsIdentityType(castType))
                        expression = cast.Expression;
                    else
                        break;
                }

                if (!SymbolEqualityComparer.Default.Equals(
                        model.GetSymbolInfo(expression, cancellationToken).Symbol, constructor.OriginalDefinition.Parameters[0]))
                    return false;
            }
            // Metadata contains signatures, not constructor bodies. Producers must run this
            // analyzer to validate forwarding; never execute referenced constructors here.
        }

        return false;
    }

    private static bool IsIdentityType(ITypeSymbol type) => type.SpecialType == SpecialType.System_Byte ||
        type is INamedTypeSymbol { TypeKind: TypeKind.Enum, EnumUnderlyingType.SpecialType: SpecialType.System_Byte };
}
