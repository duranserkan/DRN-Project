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
        var parent = lambda.Parent;
        while (parent is IDelegateCreationOperation or IConversionOperation or IParenthesizedOperation)
            parent = parent.Parent;
        var isSelectArgument = parent is IArgumentOperation { Parameter.Ordinal: 1, Parent: IInvocationOperation call } &&
            IsLinqSelect(call.TargetMethod, symbols);
        return isSelectArgument ? (IInvocationOperation)parent!.Parent! : null;
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

    internal static bool IsLengthPreservingSequenceCall(IMethodSymbol method, ConventionSymbols symbols)
    {
        if (IsLinqSelect(method, symbols)) return true;
        var keepsLength = method.Name is "ToArray" or "ToList" or "AsEnumerable" or "AsQueryable" or "Reverse" or
            "OrderBy" or "OrderByDescending" or "ThenBy" or "ThenByDescending";
        return keepsLength && TryGetPreservedSequenceArgumentCount(method, symbols, out _);
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
