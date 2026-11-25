using Microsoft.CodeAnalysis.Operations;

namespace DRN.Framework.SharedKernel.Analyzers;

internal static class RoslynOperationHelpers
{
    internal static IOperation? UnwrapTransparent(IOperation? operation)
    {
        while (operation is not null)
        {
            var next = operation switch
            {
                IParenthesizedOperation parenthesized => parenthesized.Operand,
                IConversionOperation { Conversion.IsUserDefined: false } conversion => conversion.Operand,
                _ => null
            };
            if (next is null) return operation;
            operation = next;
        }
        return null;
    }

    internal static IOperation UnwrapTransparentNonNull(IOperation operation) =>
        UnwrapTransparent(operation) ?? operation;

    internal static IOperation? UnwrapDelegateCreation(IOperation? operation)
    {
        while (operation is not null)
        {
            var next = operation switch
            {
                IParenthesizedOperation parenthesized => parenthesized.Operand,
                IConversionOperation { Conversion.IsUserDefined: false } conversion => conversion.Operand,
                IDelegateCreationOperation delegateCreation => delegateCreation.Target,
                _ => null
            };
            if (next is null) return operation;
            operation = next;
        }
        return null;
    }

    internal static IOperation? ConditionalReceiver(IConditionalAccessInstanceOperation placeholder, CancellationToken cancellation)
    {
        // In a?.B?.C the placeholder for B is in the receiver of the inner conditional and the non-null branch of the outer one.
        IOperation child = placeholder;
        while (child.Parent is { } parent)
        {
            cancellation.ThrowIfCancellationRequested();
            if (parent is IConditionalAccessOperation access && ReferenceEquals(access.WhenNotNull, child))
                return access.Operation;
            child = parent;
        }
        return null;
    }

    internal static IOperation? UnwrapInstance(IOperation? instance, CancellationToken cancellation)
    {
        while (instance is not null)
        {
            cancellation.ThrowIfCancellationRequested();
            var operand = instance switch
            {
                IConditionalAccessInstanceOperation placeholder => ConditionalReceiver(placeholder, cancellation),
                IConversionOperation { Conversion.IsUserDefined: false } conversion => conversion.Operand,
                IParenthesizedOperation parenthesized => parenthesized.Operand,
                _ => null
            };
            if (operand is null) break;
            instance = operand;
        }
        return instance;
    }

    internal static IArgumentOperation? TryGetArgumentByOrdinal(IInvocationOperation call, int ordinal)
    {
        var arguments = call.Arguments;
        for (var i = 0; i < arguments.Length; i++)
        {
            var argument = arguments[i];
            if (argument.Parameter?.Ordinal == ordinal)
                return argument;
        }
        return null;
    }

    internal static IOperation? TryGetArgumentValueByOrdinal(IInvocationOperation call, int ordinal) =>
        TryGetArgumentByOrdinal(call, ordinal)?.Value;

    internal static IOperation? TryGetExtensionReceiver(IInvocationOperation call) =>
        call.Instance ?? (call.TargetMethod.IsExtensionMethod ? TryGetArgumentValueByOrdinal(call, 0) : null);

    internal static bool IsNullLiteral(IOperation operation) =>
        UnwrapTransparent(operation) is { ConstantValue: { HasValue: true, Value: null } };

    internal static bool IsNullPattern(IPatternOperation pattern)
    {
        var current = pattern;
        for (var depth = 0; depth < 8; depth++)
        {
            if (current is IConstantPatternOperation constant)
                return IsNullLiteral(constant.Value);
            if (current is INegatedPatternOperation negated)
            {
                current = negated.Pattern;
                continue;
            }
            return false;
        }
        return false;
    }

    internal static bool IsNullableValueProperty(IPropertyReferenceOperation property) =>
        property.Property is { Name: "Value", ContainingType.OriginalDefinition.SpecialType: SpecialType.System_Nullable_T };

    internal static void CopyChildrenTo(IOperation operation, List<IOperation> buffer)
    {
        buffer.Clear();
        foreach (var child in operation.ChildOperations)
            buffer.Add(child);
    }

    internal static void PushChildrenReversed(IOperation operation, List<IOperation> scratch, Stack<IOperation> target)
    {
        CopyChildrenTo(operation, scratch);
        for (var i = scratch.Count - 1; i >= 0; i--)
            target.Push(scratch[i]);
        scratch.Clear();
    }
}
