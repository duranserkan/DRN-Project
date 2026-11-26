using Microsoft.CodeAnalysis.Operations;

namespace DRN.Framework.SharedKernel.Analyzers.Conventions;

internal static class LinqSequenceFacts
{
    internal static bool IsLinqOwner(INamedTypeSymbol? owner, ConventionSymbols symbols) =>
        ConventionSymbols.Same(owner, symbols.Enumerable) ||
        ConventionSymbols.Same(owner, symbols.Queryable) ||
        ConventionSymbols.Same(owner, symbols.ImmutableArrayExtensions);

    internal static bool IsLinqSelect(IMethodSymbol method, ConventionSymbols symbols) =>
        method.Name == "Select" && IsLinqOwner(method.ContainingType, symbols);

    internal static IAnonymousFunctionOperation? SelectLambda(IInvocationOperation call)
    {
        var selector = RoslynOperationHelpers.TryGetArgumentValueByOrdinal(call, 1);
        return RoslynOperationHelpers.UnwrapDelegateCreation(selector) as IAnonymousFunctionOperation;
    }

    internal static IInvocationOperation? FindEnclosingSelectCall(IAnonymousFunctionOperation lambda, ConventionSymbols symbols)
    {
        var call = FindEnclosingLinqCall(lambda, symbols);
        return call is not null && IsLinqSelect(call.TargetMethod, symbols) &&
               IsArgumentAtOrdinal(lambda, call, 1)
            ? call
            : null;
    }

    internal static IInvocationOperation? FindEnclosingLinqCall(IAnonymousFunctionOperation lambda, ConventionSymbols symbols)
    {
        var parent = UnwrapLambdaParent(lambda);
        var isLinqArgument = parent is IArgumentOperation { Parameter.Ordinal: > 0, Parent: IInvocationOperation { Instance: null } call } &&
            IsLinqOwner(call.TargetMethod.ContainingType, symbols);
        return isLinqArgument ? (IInvocationOperation)parent!.Parent! : null;
    }

    private static bool IsArgumentAtOrdinal(IAnonymousFunctionOperation lambda, IInvocationOperation call, int ordinal) =>
        UnwrapLambdaParent(lambda) is IArgumentOperation { Parameter.Ordinal: var actual, Parent: var parent } &&
        actual == ordinal &&
        ReferenceEquals(parent, call);

    private static IOperation? UnwrapLambdaParent(IAnonymousFunctionOperation lambda)
    {
        var parent = lambda.Parent;
        while (parent is IDelegateCreationOperation or IConversionOperation or IParenthesizedOperation)
            parent = parent.Parent;
        return parent;
    }

    internal static bool TryGetPreservedSequenceArgumentCount(
        IMethodSymbol method, ConventionSymbols symbols, out int preservedCount)
    {
        preservedCount = 0;
        if (!method.IsStatic) return false;
        var owner = method.ContainingType;
        if (ConventionSymbols.Same(owner, symbols.ImmutableArrayFactory))
        {
            preservedCount = method.Name == "ToImmutableArray" ? 1 : 0;
            return preservedCount > 0;
        }
        if (ConventionSymbols.Same(owner, symbols.ImmutableListFactory))
        {
            preservedCount = method.Name == "ToImmutableList" ? 1 : 0;
            return preservedCount > 0;
        }
        if (!IsLinqOwner(owner, symbols)) return false;
        preservedCount = method.Name switch
        {
            "Concat" or "Union" or "UnionBy" or "Append" or "Prepend" => 2,
            "ToArray" or "ToList" or "ToHashSet" or "AsEnumerable" or "AsQueryable" or "Where" or
            "Take" or "TakeLast" or "TakeWhile" or "Skip" or "SkipLast" or "SkipWhile" or
            "Order" or "OrderDescending" or "OrderBy" or "OrderByDescending" or "ThenBy" or "ThenByDescending" or
            "Reverse" or "Distinct" or "DistinctBy" or "OfType" or "Cast" or "DefaultIfEmpty" or
            "Except" or "ExceptBy" or "Intersect" or "IntersectBy" or
            "First" or "FirstOrDefault" or "Single" or "SingleOrDefault" or "Last" or "LastOrDefault" or
            "ElementAt" or "ElementAtOrDefault" => 1,
            _ => 0
        };
        return preservedCount > 0;
    }

    internal static ITypeParameterSymbol? FallbackElementTypeParameter(IMethodSymbol method)
    {
        var preservesFallback = method.Name is "DefaultIfEmpty" or "FirstOrDefault" or "SingleOrDefault" or "LastOrDefault";
        if (!preservesFallback || method.OriginalDefinition.TypeParameters.Length == 0)
            return null;
        return method.OriginalDefinition.TypeParameters[0];
    }

    internal static bool PreservesSequenceArgument(
        IArgumentOperation argument, int preservedCount, ITypeParameterSymbol? sourceTypeParameter)
    {
        if (argument.Parameter is not { } parameter) return false;
        if (parameter.Ordinal < preservedCount) return true;
        return sourceTypeParameter is not null &&
               SymbolEqualityComparer.Default.Equals(parameter.OriginalDefinition.Type, sourceTypeParameter);
    }

    private static bool IsLengthPreservingSequenceCall(IMethodSymbol method, ConventionSymbols symbols)
    {
        if (IsLinqSelect(method, symbols)) return true;
        var keepsLength = method.Name is "ToArray" or "ToList" or "AsEnumerable" or "AsQueryable" or "Reverse" or
            "OrderBy" or "OrderByDescending" or "ThenBy" or "ThenByDescending";
        return keepsLength && TryGetPreservedSequenceArgumentCount(method, symbols, out _);
    }

    internal static IOperation? SequenceControllingIteration(IOperation? source, ConventionSymbols symbols, CancellationToken cancellation)
    {
        source = RoslynOperationHelpers.UnwrapInstance(source, cancellation);
        while (source is IInvocationOperation call && IsLengthPreservingSequenceCall(call.TargetMethod, symbols))
        {
            cancellation.ThrowIfCancellationRequested();
            source = RoslynOperationHelpers.UnwrapInstance(RoslynOperationHelpers.TryGetArgumentValueByOrdinal(call, 0), cancellation);
        }
        // Values stored in a fixed-length literal do not affect whether it has an element.
        if (source is IArrayCreationOperation { Initializer: not null } ||
            source is ICollectionExpressionOperation { Type: IArrayTypeSymbol } literal && !literal.Elements.Any(e => e is ISpreadOperation))
            return null;
        return source;
    }

    internal static ImmutableArray<IOperation> LambdaIterationConditions(
        IInvocationOperation call, IAnonymousFunctionOperation lambda, ConventionSymbols symbols, CancellationToken cancellation)
    {
        if (UnwrapLambdaParent(lambda) is not IArgumentOperation { Parameter.OriginalDefinition.Type: INamedTypeSymbol callback })
            return [];
        if (callback.TypeKind != TypeKind.Delegate &&
            callback.TypeArguments is [INamedTypeSymbol { TypeKind: TypeKind.Delegate } expressionDelegate])
            callback = expressionDelegate;
        if (callback.DelegateInvokeMethod is not { } invoke) return [];

        // Outer and inner sequences can have the same concrete element type at the call site.
        ImmutableArray<IOperation> conditions = [];
        foreach (var argument in call.Arguments)
        {
            if (argument.Parameter is not { } parameter ||
                parameter.OriginalDefinition.Type is not INamedTypeSymbol { TypeArguments: [ITypeParameterSymbol element] } sequenceType)
                continue;
            if (parameter.Ordinal != 0 && sequenceType.OriginalDefinition.SpecialType != SpecialType.System_Collections_Generic_IEnumerable_T)
                continue;
            if (invoke.Parameters.Any(input => SymbolEqualityComparer.Default.Equals(input.Type, element)) &&
                SequenceControllingIteration(argument.Value, symbols, cancellation) is { } sequence)
                conditions = conditions.Add(sequence);
        }
        return conditions;
    }

    internal static bool IsMaterializingCollectionCreation(IObjectCreationOperation creation, ConventionSymbols symbols) =>
        creation.Type is INamedTypeSymbol type &&
        (ConventionSymbols.Same(type, symbols.ListGeneric) ||
         ConventionSymbols.Same(type, symbols.HashSetGeneric));

    internal static bool IsMaterializingLinqCall(
        IMethodSymbol method, IArgumentOperation argument, ConventionSymbols symbols)
    {
        if (!method.IsStatic || argument.Parameter is not { } parameter) return false;
        var owner = method.ContainingType;
        if (ConventionSymbols.Same(owner, symbols.ImmutableArrayFactory))
            return parameter.Ordinal == 0 && method.Name == "ToImmutableArray";
        if (ConventionSymbols.Same(owner, symbols.ImmutableListFactory))
            return parameter.Ordinal == 0 && method.Name == "ToImmutableList";
        if (!IsLinqOwner(owner, symbols)) return false;
        if (method.Name == "SequenceEqual")
            return parameter.Ordinal is 0 or 1;
        // Even parameterless Any can enumerate when the source has no cheap count.
        return parameter.Ordinal == 0 && method.Name is
            "ToArray" or "ToList" or "ToHashSet" or "ToDictionary" or "ToLookup" or
            "First" or "FirstOrDefault" or "Single" or "SingleOrDefault" or
            "Last" or "LastOrDefault" or "ElementAt" or "ElementAtOrDefault" or
            "Count" or "LongCount" or
            "Any" or "All" or "Contains" or
            "Max" or "MaxBy" or "Min" or "MinBy" or
            "Sum" or "Average" or "Aggregate";
    }

    internal static bool PreservesSequenceFlow(
        IMethodSymbol method, IArgumentOperation argument, ConventionSymbols symbols)
    {
        if (argument.Parameter is not { } parameter || parameter.OriginalDefinition.Type is ITypeParameterSymbol)
            return false;
        if (IsLinqSelect(method, symbols))
            return parameter.Ordinal == 0;
        if (TryGetPreservedSequenceArgumentCount(method, symbols, out var preservedCount) &&
            PreservesSequenceArgument(argument, preservedCount, FallbackElementTypeParameter(method)))
            return true;
        if (!method.IsStatic || !IsLinqOwner(method.ContainingType, symbols))
            return false;
        return method.Name switch
        {
            "SelectMany" or "GroupBy" or "Chunk" => parameter.Ordinal == 0,
            "Join" or "GroupJoin" or "Zip" => parameter.Ordinal is 0 or 1,
            _ => false
        };
    }

    internal static bool IsTryGetNonEnumeratedCount(IMethodSymbol method, IArgumentOperation argument, ConventionSymbols symbols) =>
        method.Name == "TryGetNonEnumeratedCount" &&
        argument.Parameter?.Ordinal == 0 &&
        ConventionSymbols.Same(method.ContainingType, symbols.Enumerable);
}
