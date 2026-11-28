using Microsoft.CodeAnalysis.Operations;

namespace DRN.Framework.SharedKernel.Analyzers.Conventions;

internal static class LinqSequenceFacts
{
    internal const string SelectMethodName = "Select";

    private const string ToImmutableArrayMethodName = "ToImmutableArray";
    private const string ToImmutableListMethodName = "ToImmutableList";
    private const string ConcatMethodName = "Concat";
    private const string UnionMethodName = "Union";
    private const string UnionByMethodName = "UnionBy";
    private const string AppendMethodName = "Append";
    private const string PrependMethodName = "Prepend";
    private const string ToArrayMethodName = "ToArray";
    private const string ToListMethodName = "ToList";
    private const string ToHashSetMethodName = "ToHashSet";
    private const string ToDictionaryMethodName = "ToDictionary";
    private const string ToLookupMethodName = "ToLookup";
    private const string AsEnumerableMethodName = "AsEnumerable";
    private const string AsQueryableMethodName = "AsQueryable";
    private const string WhereMethodName = "Where";
    private const string TakeMethodName = "Take";
    private const string TakeLastMethodName = "TakeLast";
    private const string TakeWhileMethodName = "TakeWhile";
    private const string SkipMethodName = "Skip";
    private const string SkipLastMethodName = "SkipLast";
    private const string SkipWhileMethodName = "SkipWhile";
    private const string OrderMethodName = "Order";
    private const string OrderDescendingMethodName = "OrderDescending";
    private const string OrderByMethodName = "OrderBy";
    private const string OrderByDescendingMethodName = "OrderByDescending";
    private const string ThenByMethodName = "ThenBy";
    private const string ThenByDescendingMethodName = "ThenByDescending";
    private const string ReverseMethodName = "Reverse";
    private const string DistinctMethodName = "Distinct";
    private const string DistinctByMethodName = "DistinctBy";
    private const string OfTypeMethodName = "OfType";
    private const string CastMethodName = "Cast";
    private const string DefaultIfEmptyMethodName = "DefaultIfEmpty";
    private const string ExceptMethodName = "Except";
    private const string ExceptByMethodName = "ExceptBy";
    private const string IntersectMethodName = "Intersect";
    private const string IntersectByMethodName = "IntersectBy";
    private const string FirstMethodName = "First";
    private const string FirstOrDefaultMethodName = "FirstOrDefault";
    private const string SingleMethodName = "Single";
    private const string SingleOrDefaultMethodName = "SingleOrDefault";
    private const string LastMethodName = "Last";
    private const string LastOrDefaultMethodName = "LastOrDefault";
    private const string ElementAtMethodName = "ElementAt";
    private const string ElementAtOrDefaultMethodName = "ElementAtOrDefault";
    private const string SequenceEqualMethodName = "SequenceEqual";
    private const string CountMethodName = "Count";
    private const string LongCountMethodName = "LongCount";
    private const string AnyMethodName = "Any";
    private const string AllMethodName = "All";
    private const string ContainsMethodName = "Contains";
    private const string MaxMethodName = "Max";
    private const string MaxByMethodName = "MaxBy";
    private const string MinMethodName = "Min";
    private const string MinByMethodName = "MinBy";
    private const string SumMethodName = "Sum";
    private const string AverageMethodName = "Average";
    private const string AggregateMethodName = "Aggregate";
    private const string SelectManyMethodName = "SelectMany";
    private const string GroupByMethodName = "GroupBy";
    private const string ChunkMethodName = "Chunk";
    private const string JoinMethodName = "Join";
    private const string GroupJoinMethodName = "GroupJoin";
    private const string ZipMethodName = "Zip";
    private const string TryGetNonEnumeratedCountMethodName = "TryGetNonEnumeratedCount";
    private const string EmptyMethodName = "Empty";

    internal static bool IsLinqOwner(INamedTypeSymbol? owner, ConventionSymbols symbols) =>
        ConventionSymbols.Same(owner, symbols.Enumerable) ||
        ConventionSymbols.Same(owner, symbols.Queryable) ||
        ConventionSymbols.Same(owner, symbols.ImmutableArrayExtensions);

    internal static bool IsLinqSelect(IMethodSymbol method, ConventionSymbols symbols) =>
        method.Name == SelectMethodName && IsLinqOwner(method.ContainingType, symbols);

    internal static bool IsSelectOrOrdering(IMethodSymbol method, ConventionSymbols symbols) =>
        method.Name is SelectMethodName or OrderByMethodName or OrderByDescendingMethodName or ThenByMethodName or ThenByDescendingMethodName &&
        IsLinqOwner(method.ContainingType, symbols);

    private static bool IsDirectOrdering(IMethodSymbol method, ConventionSymbols symbols) =>
        method.Name is OrderMethodName or OrderDescendingMethodName &&
        IsLinqOwner(method.ContainingType, symbols);

    private static IOperation? TryGetOrderingComparer(IInvocationOperation call, ConventionSymbols symbols)
    {
        var method = call.TargetMethod;
        if (!IsLinqOwner(method.ContainingType, symbols))
            return null;
        var ordinal = method.Name switch
        {
            OrderMethodName or OrderDescendingMethodName => 1,
            OrderByMethodName or OrderByDescendingMethodName or ThenByMethodName or ThenByDescendingMethodName => 2,
            _ => -1
        };
        return ordinal >= 0 ? RoslynOperationHelpers.TryGetArgumentValueByOrdinal(call, ordinal) : null;
    }

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
            preservedCount = method.Name == ToImmutableArrayMethodName ? 1 : 0;
            return preservedCount > 0;
        }
        if (ConventionSymbols.Same(owner, symbols.ImmutableListFactory))
        {
            preservedCount = method.Name == ToImmutableListMethodName ? 1 : 0;
            return preservedCount > 0;
        }
        if (!IsLinqOwner(owner, symbols)) return false;
        preservedCount = method.Name switch
        {
            ConcatMethodName or UnionMethodName or UnionByMethodName or AppendMethodName or PrependMethodName => 2,
            ToArrayMethodName or ToListMethodName or ToHashSetMethodName or AsEnumerableMethodName or AsQueryableMethodName or WhereMethodName or
            TakeMethodName or TakeLastMethodName or TakeWhileMethodName or SkipMethodName or SkipLastMethodName or SkipWhileMethodName or
            OrderMethodName or OrderDescendingMethodName or OrderByMethodName or OrderByDescendingMethodName or ThenByMethodName or ThenByDescendingMethodName or
            ReverseMethodName or DistinctMethodName or DistinctByMethodName or OfTypeMethodName or CastMethodName or DefaultIfEmptyMethodName or
            ExceptMethodName or ExceptByMethodName or IntersectMethodName or IntersectByMethodName or
            FirstMethodName or FirstOrDefaultMethodName or SingleMethodName or SingleOrDefaultMethodName or LastMethodName or LastOrDefaultMethodName or
            ElementAtMethodName or ElementAtOrDefaultMethodName => 1,
            _ => 0
        };
        return preservedCount > 0;
    }

    internal static ITypeParameterSymbol? FallbackElementTypeParameter(IMethodSymbol method)
    {
        var preservesFallback = method.Name is DefaultIfEmptyMethodName or FirstOrDefaultMethodName or SingleOrDefaultMethodName or LastOrDefaultMethodName;
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

    internal static ImmutableArray<IOperation> SequenceControllingIteration(
        IOperation? source, ConventionSymbols symbols, CancellationToken cancellation)
    {
        ImmutableArray<IOperation> conditions = [];
        ImmutableArray<IOperation> eagerArguments = [];
        IOperation? retainedSource = null;
        source = RoslynOperationHelpers.UnwrapInstance(source, cancellation);
        while (source is IInvocationOperation call)
        {
            cancellation.ThrowIfCancellationRequested();
            var method = call.TargetMethod;
            var hasCallback = IsSelectOrOrdering(method, symbols);
            var isDirectOrdering = IsDirectOrdering(method, symbols);
            if (!hasCallback && !isDirectOrdering &&
                (method.Name is not (ToArrayMethodName or ToListMethodName or AsEnumerableMethodName or AsQueryableMethodName or ReverseMethodName) ||
                 !TryGetPreservedSequenceArgumentCount(method, symbols, out _)))
                break;
            var input = RoslynOperationHelpers.TryGetArgumentValueByOrdinal(call, 0);
            source = RoslynOperationHelpers.UnwrapInstance(input, cancellation);
            if (TryGetOrderingComparer(call, symbols) is { } comparer && !RoslynOperationHelpers.IsNullLiteral(comparer))
            {
                if (!IsKnownNonThrowingComparer(comparer, symbols, cancellation))
                    eagerArguments = eagerArguments.Add(comparer);
                retainedSource ??= call;
            }
            if (isDirectOrdering)
            {
                retainedSource ??= call;
                continue;
            }
            if (!hasCallback) continue;
            if (SelectLambda(call) is not { } lambda)
            {
                if (RoslynOperationHelpers.TryGetArgumentValueByOrdinal(call, 1) is { } selector)
                    eagerArguments = eagerArguments.Add(selector);
                retainedSource ??= call;
                continue;
            }
            if (retainedSource is not null) continue;
            var guard = DirectThrowGuard(lambda.Body, out var directThrow);
            if (RequiresWholeCallback(lambda.Body, directThrow, cancellation))
                retainedSource = call;
            else if (guard is not null)
            {
                conditions = conditions.Add(guard);
                if (lambda.Symbol.Parameters.Length > 0 && ReadsParameter(guard, lambda.Symbol.Parameters[0], cancellation))
                    retainedSource = input;
            }
        }
        if (IsEmptyArraySource(source))
            return eagerArguments;
        if (retainedSource is not null)
            return conditions.Add(retainedSource);
        if (source is null)
            return conditions;
        // A fixed-length literal's cardinality does not change whether the loop runs,
        // but potentially throwing element initializers still execute eagerly.
        if (IsFixedLengthLiteral(source))
            return AddThrowingLiteralElements(source, conditions, cancellation, directThrowOnly: false);
        return conditions.Add(source);
    }

    internal static ImmutableArray<IOperation> LoopHeadThrowingComparers(
        IOperation? source, ConventionSymbols symbols, CancellationToken cancellation)
    {
        ImmutableArray<IOperation> throwing = [];
        var pending = new Stack<IOperation>();
        if (source is not null)
            pending.Push(source);
        while (pending.TryPop(out var current))
        {
            cancellation.ThrowIfCancellationRequested();
            current = RoslynOperationHelpers.UnwrapInstance(current, cancellation);
            if (current is null)
                continue;
            if (current is IInvocationOperation call)
            {
                throwing = InspectLoopHeadInvocation(call, symbols, pending, throwing, cancellation);
                continue;
            }
            if (IsFixedLengthLiteral(current))
                throwing = AddThrowingLiteralElements(current, throwing, cancellation, directThrowOnly: true);
            else if (ContainsDirectThrow(current, cancellation))
                throwing = throwing.Add(current);
        }
        return throwing;
    }

    private static ImmutableArray<IOperation> InspectLoopHeadInvocation(
        IInvocationOperation call, ConventionSymbols symbols, Stack<IOperation> pending,
        ImmutableArray<IOperation> throwing, CancellationToken cancellation)
    {
        var method = call.TargetMethod;
        var hasCallback = IsSelectOrOrdering(method, symbols);
        var isPreserved = TryGetPreservedSequenceArgumentCount(method, symbols, out var preservedCount);
        if (!hasCallback && !isPreserved)
            return ContainsDirectThrow(call, cancellation) ? throwing.Add(call) : throwing;

        if (TryGetOrderingComparer(call, symbols) is { } comparer &&
            !RoslynOperationHelpers.IsNullLiteral(comparer) &&
            !IsKnownNonThrowingComparer(comparer, symbols, cancellation))
            throwing = throwing.Add(comparer);
        if (hasCallback && SelectLambda(call) is null &&
            RoslynOperationHelpers.TryGetArgumentValueByOrdinal(call, 1) is { } selector &&
            ContainsDirectThrow(selector, cancellation))
            throwing = throwing.Add(selector);
        if (preservedCount == 2 && RoslynOperationHelpers.TryGetArgumentValueByOrdinal(call, 1) is { } second)
            pending.Push(second);
        if (RoslynOperationHelpers.TryGetArgumentValueByOrdinal(call, 0) is { } input)
            pending.Push(input);
        return throwing;
    }

    private static bool IsEmptyArraySource(IOperation? source) => source is
        IArrayCreationOperation { Initializer.ElementValues.Length: 0 } or
        IArrayCreationOperation { DimensionSizes: [{ ConstantValue: { HasValue: true, Value: 0 } }] } or
        ICollectionExpressionOperation { Type: IArrayTypeSymbol, Elements.Length: 0 } or
        IInvocationOperation
        {
            Instance: null,
            TargetMethod:
            {
                IsStatic: true,
                Name: EmptyMethodName,
                Parameters.Length: 0,
                TypeParameters.Length: 1,
                ContainingType.SpecialType: SpecialType.System_Array
            }
        };

    private static bool IsKnownNonThrowingComparer(
        IOperation comparer, ConventionSymbols symbols, CancellationToken cancellation) =>
        IsSimpleComparerValue(comparer) ||
        IsNonThrowingParameterProperty(comparer, symbols, cancellation) ||
        IsSimpleComparerChoice(comparer, symbols, cancellation);

    private static bool IsSimpleComparerChoice(
        IOperation comparer, ConventionSymbols symbols, CancellationToken cancellation) =>
        UnwrapSafeReturnConversions(comparer) is IConditionalOperation { WhenFalse: { } other } choice &&
        (IsSimpleReturnValue(choice.Condition) || IsNonThrowingParameterProperty(choice.Condition, symbols, cancellation)) &&
        IsSimpleComparerValue(choice.WhenTrue) && IsSimpleComparerValue(other);

    private static bool IsNonThrowingParameterProperty(
        IOperation condition, ConventionSymbols symbols, CancellationToken cancellation) =>
        UnwrapSafeReturnConversions(condition) is IPropertyReferenceOperation
        {
            Instance: IParameterReferenceOperation parameter,
            Property: { IsIndexer: false } property
        } &&
        PropertyAccessFacts.HasKnownNonThrowingGetter(
            property, RoslynSymbolHelpers.SealedType(parameter.Type), symbols, cancellation);

    private static bool IsSimpleComparerValue(IOperation value) =>
        IsSimpleReturnValue(value) || UnwrapSafeReturnConversions(value) is IPropertyReferenceOperation
        {
            Property.IsStatic: true,
            Property.Name: nameof(StringComparer.Ordinal) or nameof(StringComparer.OrdinalIgnoreCase),
            Property.ContainingType: var owner
        } && ConventionSymbols.MetadataName(owner) == "System.StringComparer";

    private static IOperation? DirectThrowGuard(IBlockOperation body, out IThrowOperation? directThrow)
    {
        if (body.Operations is [IReturnOperation { ReturnedValue: { } value }] &&
            UnwrapSafeReturnConversions(value) is IConditionalOperation { WhenFalse: { } other } choice)
        {
            directThrow = UnwrapThrow(choice.WhenTrue);
            var resultArm = directThrow is null ? choice.WhenTrue : other;
            directThrow = IsSimpleReturnValue(resultArm) ? directThrow ?? UnwrapThrow(other) : null;
            return directThrow is not null ? choice.Condition : null;
        }
        if (body.Operations is [IConditionalOperation { WhenFalse: null } check, IReturnOperation { ReturnedValue: { } result }] &&
            IsSimpleReturnValue(result))
        {
            directThrow = UnwrapThrow(check.WhenTrue);
            return directThrow is not null ? check.Condition : null;
        }
        directThrow = null;
        return null;
    }

    private static bool IsSimpleReturnValue(IOperation value) =>
        UnwrapSafeReturnConversions(value) is { ConstantValue.HasValue: true } or IParameterReferenceOperation or ILocalReferenceOperation;

    private static IOperation UnwrapSafeReturnConversions(IOperation value)
    {
        while (true)
        {
            var operand = value switch
            {
                IParenthesizedOperation parenthesized => parenthesized.Operand,
                IConversionOperation { Conversion: { IsIdentity: true } or { IsImplicit: true, IsReference: true } } conversion => conversion.Operand,
                _ => null
            };
            if (operand is null) return value;
            value = operand;
        }
    }

    private static IThrowOperation? UnwrapThrow(IOperation operation)
    {
        if (operation is IBlockOperation { Operations: [var statement] })
            operation = statement;
        return RoslynOperationHelpers.UnwrapTransparent(operation) as IThrowOperation;
    }

    private static bool RequiresWholeCallback(
        IBlockOperation body, IThrowOperation? directThrow, CancellationToken cancellation)
    {
        var walk = new Stack<IOperation>();
        walk.Push(body);
        while (walk.TryPop(out var current))
        {
            cancellation.ThrowIfCancellationRequested();
            if (current is IAnonymousFunctionOperation or ILocalFunctionOperation) continue;
            if (current is IThrowOperation thrown &&
                (!ReferenceEquals(thrown, directThrow) || !IsFixedDependencyFreeThrow(thrown)))
                return true;
            if (current is IInvocationOperation { TargetMethod.MethodKind: MethodKind.LocalFunction or MethodKind.DelegateInvoke })
                return true;
            foreach (var child in current.ChildOperations) walk.Push(child);
        }
        return false;
    }

    private static bool IsFixedDependencyFreeThrow(IThrowOperation thrown)
    {
        if (thrown.Exception is not { } exception)
            return false;
        if (UnwrapSafeReturnConversions(exception) is not IObjectCreationOperation { Constructor: not null, Initializer: null } creation)
            return false;
        for (var i = 0; i < creation.Arguments.Length; i++)
        {
            if (!IsDependencyFreeExceptionArgument(creation.Arguments[i]))
                return false;
        }
        return true;
    }

    private static bool IsDependencyFreeExceptionArgument(IArgumentOperation argument)
    {
        if (argument.Parameter is { RefKind: not RefKind.None })
            return false;
        if (argument.ArgumentKind == ArgumentKind.DefaultValue)
            return true;
        var value = UnwrapSafeReturnConversions(argument.Value);
        if (value.ConstantValue.HasValue || value is IDefaultValueOperation or ITypeOfOperation)
            return true;
        return argument.ArgumentKind is ArgumentKind.ParamArray or ArgumentKind.ParamCollection &&
               value is IArrayCreationOperation { Initializer.ElementValues.Length: 0 } or
                   ICollectionExpressionOperation { Elements.Length: 0 };
    }

    private static bool ContainsDirectThrow(IOperation root, CancellationToken cancellation)
    {
        var walk = new Stack<IOperation>();
        walk.Push(root);
        while (walk.TryPop(out var current))
        {
            cancellation.ThrowIfCancellationRequested();
            if (current is IAnonymousFunctionOperation or ILocalFunctionOperation) continue;
            if (current is IThrowOperation) return true;
            foreach (var child in current.ChildOperations) walk.Push(child);
        }
        return false;
    }

    private static bool ReadsParameter(IOperation guard, IParameterSymbol element, CancellationToken cancellation)
    {
        foreach (var operation in guard.DescendantsAndSelf())
        {
            cancellation.ThrowIfCancellationRequested();
            if (operation is IParameterReferenceOperation reference && SymbolEqualityComparer.Default.Equals(reference.Parameter, element))
                return true;
        }
        return false;
    }

    private static bool IsFixedLengthLiteral(IOperation source)
    {
        var isFixedArrayLiteral = source is IArrayCreationOperation { Initializer: not null };
        var isFixedCollectionLiteral = source is ICollectionExpressionOperation { Type: IArrayTypeSymbol } literal &&
            !literal.Elements.Any(e => e is ISpreadOperation);
        return isFixedArrayLiteral || isFixedCollectionLiteral;
    }

    private static ImmutableArray<IOperation> AddThrowingLiteralElements(
        IOperation literal, ImmutableArray<IOperation> destination,
        CancellationToken cancellation, bool directThrowOnly)
    {
        if (literal is ICollectionExpressionOperation collection)
        {
            var elements = collection.Elements;
            for (var i = 0; i < elements.Length; i++)
            {
                var element = elements[i];
                if (RetainsLiteralElement(element, cancellation, directThrowOnly))
                    destination = destination.Add(element);
            }
            return destination;
        }
        if (literal is not IArrayCreationOperation { Initializer: { } initializer })
            return destination;
        var pending = new Stack<IOperation>();
        pending.Push(initializer);
        while (pending.TryPop(out var current))
        {
            cancellation.ThrowIfCancellationRequested();
            if (current is IArrayInitializerOperation nested)
            {
                for (var i = nested.ElementValues.Length - 1; i >= 0; i--)
                    pending.Push(nested.ElementValues[i]);
                continue;
            }
            if (RetainsLiteralElement(current, cancellation, directThrowOnly))
                destination = destination.Add(current);
        }
        return destination;
    }

    private static bool RetainsLiteralElement(IOperation element, CancellationToken cancellation, bool directThrowOnly) =>
        directThrowOnly
            ? ContainsDirectThrow(element, cancellation)
            : MayThrowFixedLiteralElement(element, cancellation);

    private static bool MayThrowFixedLiteralElement(IOperation root, CancellationToken cancellation)
    {
        var walk = new Stack<IOperation>();
        walk.Push(root);
        while (walk.TryPop(out var current))
        {
            cancellation.ThrowIfCancellationRequested();
            if (current is IAnonymousFunctionOperation or ILocalFunctionOperation) continue;
            if (IsPotentiallyThrowingOperation(current)) return true;
            foreach (var child in current.ChildOperations) walk.Push(child);
        }
        return false;
    }

    private static bool IsPotentiallyThrowingOperation(IOperation operation) => operation switch
    {
        IThrowOperation or
        IInvocationOperation or
        IObjectCreationOperation or
        IArrayElementReferenceOperation or
        IAwaitOperation or
        IRaiseEventOperation or
        ISwitchExpressionOperation or
        IDynamicInvocationOperation or
        IDynamicMemberReferenceOperation or
        IDynamicIndexerAccessOperation or
        IDynamicObjectCreationOperation => true,
        IPropertyReferenceOperation { Property.IsIndexer: true } => true,
        IConversionOperation { IsChecked: true } or
        IConversionOperation { Conversion.IsUserDefined: true } or
        IConversionOperation { IsTryCast: false, Conversion: { IsImplicit: false, IsReference: true } } or
        IConversionOperation { IsTryCast: false, Conversion.IsImplicit: false, Operand.Type.IsReferenceType: true, Type.IsValueType: true } => true,
        IBinaryOperation { IsChecked: true } or
        IBinaryOperation { OperatorKind: BinaryOperatorKind.Divide or BinaryOperatorKind.Remainder } or
        IBinaryOperation { OperatorMethod: not null } => true,
        IUnaryOperation { IsChecked: true } or IUnaryOperation { OperatorMethod: not null } => true,
        _ => false
    };

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
            var isNonEnumerableSecondarySequence = parameter.Ordinal != 0 &&
                sequenceType.OriginalDefinition.SpecialType != SpecialType.System_Collections_Generic_IEnumerable_T;
            if (isNonEnumerableSecondarySequence)
                continue;
            var matchesCallbackInput = invoke.Parameters.Any(input => SymbolEqualityComparer.Default.Equals(input.Type, element));
            if (!matchesCallbackInput)
                continue;
            var sequences = SequenceControllingIteration(argument.Value, symbols, cancellation);
            for (var i = 0; i < sequences.Length; i++)
            {
                if (!conditions.Contains(sequences[i]))
                    conditions = conditions.Add(sequences[i]);
            }
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
            return parameter.Ordinal == 0 && method.Name == ToImmutableArrayMethodName;
        if (ConventionSymbols.Same(owner, symbols.ImmutableListFactory))
            return parameter.Ordinal == 0 && method.Name == ToImmutableListMethodName;
        if (!IsLinqOwner(owner, symbols)) return false;
        if (method.Name == SequenceEqualMethodName)
            return parameter.Ordinal is 0 or 1;
        // Even parameterless Any can enumerate when the source has no cheap count.
        return parameter.Ordinal == 0 && method.Name is
            ToArrayMethodName or ToListMethodName or ToHashSetMethodName or ToDictionaryMethodName or ToLookupMethodName or
            FirstMethodName or FirstOrDefaultMethodName or SingleMethodName or SingleOrDefaultMethodName or
            LastMethodName or LastOrDefaultMethodName or ElementAtMethodName or ElementAtOrDefaultMethodName or
            CountMethodName or LongCountMethodName or
            AnyMethodName or AllMethodName or ContainsMethodName or
            MaxMethodName or MaxByMethodName or MinMethodName or MinByMethodName or
            SumMethodName or AverageMethodName or AggregateMethodName;
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
            SelectManyMethodName or GroupByMethodName or ChunkMethodName => parameter.Ordinal == 0,
            JoinMethodName or GroupJoinMethodName or ZipMethodName => parameter.Ordinal is 0 or 1,
            _ => false
        };
    }

    internal static bool IsTryGetNonEnumeratedCount(IMethodSymbol method, IArgumentOperation argument, ConventionSymbols symbols) =>
        method.Name == TryGetNonEnumeratedCountMethodName &&
        argument.Parameter?.Ordinal == 0 &&
        ConventionSymbols.Same(method.ContainingType, symbols.Enumerable);
}
