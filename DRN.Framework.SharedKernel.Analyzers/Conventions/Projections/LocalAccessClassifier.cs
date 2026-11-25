using Microsoft.CodeAnalysis.Operations;

namespace DRN.Framework.SharedKernel.Analyzers.Conventions;

internal static class LocalAccessClassifier
{
    internal static ExpansionCursor CreateAccessCursor(ProjectionNode entry, IOperation receiver, ConventionSymbols symbols)
    {
        if (!entry.IsInput) return new(LocalAccessMode.Full);
        var mode = ClassifyLocalAccess(receiver, symbols, out var properties);
        return new(mode, properties);
    }

    internal static bool TryResolveConditionalPlaceholderAccess(
        IConditionalAccessInstanceOperation placeholder, ConventionSymbols symbols, CancellationToken cancellation,
        out IOperation receiver, out ExpansionCursor cursor)
    {
        receiver = null!;
        cursor = new(LocalAccessMode.Full);
        var suffixMode = ClassifyLocalAccess(placeholder, symbols, out var suffix);
        if (suffix.IsDefaultOrEmpty) return false;

        var prefixSegments = new List<IPropertySymbol>();
        var current = RoslynOperationHelpers.ConditionalReceiver(placeholder, cancellation);
        while (current is not null)
        {
            cancellation.ThrowIfCancellationRequested();
            current = RoslynOperationHelpers.UnwrapTransparent(current);
            var isRootLocalOrTupleField = current is ILocalReferenceOperation ||
                (current is IFieldReferenceOperation field && RoslynSymbolHelpers.TupleElementOrdinal(field.Field) >= 0);
            if (isRootLocalOrTupleField)
            {
                receiver = current!;
                break;
            }
            if (current is IPropertyReferenceOperation { Property.IsIndexer: false } prop)
            {
                prefixSegments.Add(prop.Property);
                current = prop.Instance;
                continue;
            }
            if (current is IConditionalAccessInstanceOperation outerPlaceholder)
            {
                current = RoslynOperationHelpers.ConditionalReceiver(outerPlaceholder, cancellation);
                continue;
            }
            return false;
        }
        if (receiver is null) return false;

        var builder = ImmutableArray.CreateBuilder<IPropertySymbol>(prefixSegments.Count + suffix.Length);
        for (var i = prefixSegments.Count - 1; i >= 0; i--) builder.Add(prefixSegments[i]);
        builder.AddRange(suffix);
        cursor = new(suffixMode, builder.MoveToImmutable());
        return true;
    }

    private static LocalAccessMode ClassifyLocalAccess(
        IOperation receiver, ConventionSymbols symbols, out ImmutableArray<IPropertySymbol> chain)
    {
        ImmutableArray<IPropertySymbol>.Builder? properties = null;
        var current = receiver;
        while (current.Parent is { } parent)
        {
            if (parent is IParenthesizedOperation or IConversionOperation { Conversion.IsUserDefined: false })
            {
                current = parent;
                continue;
            }
            if (parent is IConditionalAccessOperation cond && ReferenceEquals(cond.WhenNotNull, current))
            {
                current = cond;
                continue;
            }
            if (parent is IPropertyReferenceOperation { Property.IsIndexer: false } property &&
                ReferenceEquals(property.Instance, current))
            {
                (properties ??= ImmutableArray.CreateBuilder<IPropertySymbol>()).Add(property.Property);
                current = property;
                continue;
            }
            break;
        }
        chain = properties is null ? default : properties.ToImmutable();
        var nullCheckMode = ClassifyPureNullCheckUse(current, symbols);
        if (nullCheckMode != LocalAccessMode.Full) return nullCheckMode;
        return chain.IsDefaultOrEmpty ? LocalAccessMode.Full : LocalAccessMode.PropertyChain;
    }

    private static LocalAccessMode ClassifyPureNullCheckUse(IOperation current, ConventionSymbols symbols)
    {
        var parent = current.Parent;
        if (parent is IBinaryOperation { OperatorKind: BinaryOperatorKind.Equals or BinaryOperatorKind.NotEquals } binary)
        {
            var other = ReferenceEquals(binary.LeftOperand, current) ? binary.RightOperand : binary.LeftOperand;
            if (!RoslynOperationHelpers.IsNullLiteral(other)) return LocalAccessMode.Full;
            if (binary.OperatorMethod is null) return LocalAccessMode.NullCheckOnly;
            return IsSynthesizedRecordEqualityOperator(binary.OperatorMethod, symbols)
                ? LocalAccessMode.RecordNullCheck
                : LocalAccessMode.Full;
        }
        if (parent is IIsPatternOperation isPattern && ReferenceEquals(isPattern.Value, current) &&
            RoslynOperationHelpers.IsNullPattern(isPattern.Pattern))
            return LocalAccessMode.NullCheckOnly;
        if (parent is IConditionalAccessOperation conditional &&
            ReferenceEquals(conditional.Operation, current) &&
            IsPropertyOnlyConditionalAccess(conditional))
            return LocalAccessMode.NullCheckOnly;
        return LocalAccessMode.Full;
    }

    private static bool IsSynthesizedRecordEqualityOperator(IMethodSymbol operatorMethod, ConventionSymbols symbols) =>
        operatorMethod.ContainingType is { IsRecord: true } &&
        RoslynSymbolHelpers.IsCompilerGenerated(operatorMethod, symbols.CompilerGeneratedAttribute);

    private static bool IsPropertyOnlyConditionalAccess(IConditionalAccessOperation conditional)
    {
        var current = conditional.WhenNotNull;
        var sawProperty = false;
        for (var depth = 0; current is not null && depth < 32; depth++)
        {
            current = RoslynOperationHelpers.UnwrapTransparent(current);
            if (current is IConditionalAccessInstanceOperation) return sawProperty;
            if (current is IPropertyReferenceOperation { Property.IsIndexer: false } property)
            {
                sawProperty = true;
                current = property.Instance;
                continue;
            }
            if (current is IConditionalAccessOperation nested)
            {
                sawProperty = false;
                current = nested.Operation;
                continue;
            }
            if (current is IInvocationOperation invocation)
            {
                sawProperty = false;
                current = RoslynOperationHelpers.TryGetExtensionReceiver(invocation);
                continue;
            }
            if (current is IArrayElementReferenceOperation or IPropertyReferenceOperation { Property.IsIndexer: true })
            {
                sawProperty = false;
                current = CollectionMutationFacts.MutationReceiver(current);
                continue;
            }
            return false;
        }
        return false;
    }
}
