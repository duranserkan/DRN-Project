using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Operations;

namespace DRN.Framework.SharedKernel.Analyzers.Conventions;

internal static class CollectionMutationFacts
{
    internal const string AddMethodName = "Add";

    private const string SystemNamespace = "System";
    private const string TextNamespace = "Text";
    private const string CollectionsNamespace = "Collections";
    private const string ImmutableNamespace = "Immutable";
    private const string GenericNamespace = "Generic";
    private const string ConcurrentNamespace = "Concurrent";

    private const string StringBuilderTypeName = "StringBuilder";
    private const string BuilderTypeName = "Builder";
    private const string ListMetadataName = "List`1";
    private const string InterfaceListMetadataName = "IList`1";
    private const string QueueMetadataName = "Queue`1";
    private const string StackMetadataName = "Stack`1";
    private const string DictionaryMetadataName = "Dictionary`2";
    private const string InterfaceDictionaryMetadataName = "IDictionary`2";
    private const string ConcurrentDictionaryMetadataName = "ConcurrentDictionary`2";
    private const string ConcurrentQueueMetadataName = "ConcurrentQueue`1";
    private const string ConcurrentStackMetadataName = "ConcurrentStack`1";

    private const string AppendMethodName = "Append";
    private const string AppendLineMethodName = "AppendLine";
    private const string AppendFormatMethodName = "AppendFormat";
    private const string AppendJoinMethodName = "AppendJoin";
    private const string ReplaceMethodName = "Replace";
    private const string AddRangeMethodName = "AddRange";
    private const string TryAddMethodName = "TryAdd";
    private const string InsertMethodName = "Insert";
    private const string InsertRangeMethodName = "InsertRange";
    private const string InsertItemMethodName = "InsertItem";
    private const string EnqueueMethodName = "Enqueue";
    private const string TryEnqueueMethodName = "TryEnqueue";
    private const string PushMethodName = "Push";
    private const string TryPushMethodName = "TryPush";
    private const string SetMethodName = "Set";
    private const string SetItemMethodName = "SetItem";
    private const string SetValueMethodName = "SetValue";
    private const string AddOrUpdateMethodName = "AddOrUpdate";
    private const string GetOrAddMethodName = "GetOrAdd";
    private const string UnionWithMethodName = "UnionWith";

    internal static bool IsStringBuilder(INamedTypeSymbol? type) =>
        type is { Name: StringBuilderTypeName } &&
        RoslynSymbolHelpers.IsNamespace(type.ContainingNamespace, TextNamespace, SystemNamespace);

    internal static IOperation? MutationReceiver(IOperation operation) => RoslynOperationHelpers.UnwrapTransparent(operation switch
    {
        IPropertyReferenceOperation property => property.Instance,
        IFieldReferenceOperation field => field.Instance,
        IArrayElementReferenceOperation element => element.ArrayReference,
        IInvocationOperation { Instance: { } builder } call when
            IsStringBuilder(call.TargetMethod.ContainingType) &&
            SymbolEqualityComparer.Default.Equals(call.TargetMethod.ReturnType, call.TargetMethod.ContainingType) => builder,
        _ => null
    });

    internal static bool CanStoreArgument(IMethodSymbol method)
    {
        if (method.ContainingType is not { } owner) return false;
        var isImmutableType = owner.SpecialType == SpecialType.System_String ||
            owner is { IsValueType: true, IsReadOnly: true } ||
            (owner.Name != BuilderTypeName &&
             RoslynSymbolHelpers.IsNamespace(owner.ContainingNamespace, ImmutableNamespace, CollectionsNamespace, SystemNamespace));
        if (isImmutableType) return false;

        return method.Name switch
        {
            AppendMethodName or AppendLineMethodName or AppendFormatMethodName or AppendJoinMethodName or ReplaceMethodName => IsStringBuilder(owner),
            AddMethodName or AddRangeMethodName or TryAddMethodName or InsertMethodName or InsertRangeMethodName or
            EnqueueMethodName or TryEnqueueMethodName or PushMethodName or TryPushMethodName or
            SetMethodName or SetItemMethodName or SetValueMethodName or AddOrUpdateMethodName or GetOrAddMethodName or UnionWithMethodName => true,
            _ => false
        };
    }

    internal static bool StoresProjectedElement(
        IInvocationOperation call, IArgumentOperation argument, SelectorScope? locals, ConventionFacts facts)
    {
        if (call.Instance?.Type is not INamedTypeSymbol collection || argument.Parameter is not { } parameter)
            return false;
        var method = call.TargetMethod;
        if (method.Name == AddMethodName && method.Parameters.Length == 1)
        {
            var exactDefault = IsExactDefaultCollectionReceiver(call, method, collection, locals, facts);
            return IsStorageAdd(method, collection, facts, exactDefault);
        }
        if (method.DeclaringSyntaxReferences.Length != 0)
            return false;
        return IsKnownRuntimeCollectionStorage(method, parameter);
    }

    private static bool IsExactDefaultCollectionReceiver(
        IInvocationOperation call, IMethodSymbol method, INamedTypeSymbol collection, SelectorScope? locals, ConventionFacts facts) =>
        ConventionSymbols.Same(method.ContainingType, facts.Symbols.CollectionGeneric) &&
        RoslynOperationHelpers.UnwrapTransparent(call.Instance) is ILocalReferenceOperation receiver &&
        locals is not null && locals.TryGetValues(receiver, out var bindings) &&
        HasOnlyDefaultAllocations(bindings, collection);

    private static bool IsKnownRuntimeCollectionStorage(IMethodSymbol method, IParameterSymbol parameter)
    {
        var owner = method.ContainingType.OriginalDefinition;
        if (RoslynSymbolHelpers.IsNamespace(owner.ContainingNamespace, GenericNamespace, CollectionsNamespace, SystemNamespace))
        {
            return (owner.MetadataName, method.Name, parameter.Ordinal) switch
            {
                (ListMetadataName, AddRangeMethodName, 0) or
                (ListMetadataName or InterfaceListMetadataName, InsertMethodName, 1) or
                (ListMetadataName, InsertRangeMethodName, 1) or
                (QueueMetadataName, EnqueueMethodName, 0) or
                (StackMetadataName, PushMethodName, 0) or
                (DictionaryMetadataName or InterfaceDictionaryMetadataName, AddMethodName or TryAddMethodName, 1) => true,
                _ => false
            };
        }
        if (RoslynSymbolHelpers.IsNamespace(owner.ContainingNamespace, ConcurrentNamespace, CollectionsNamespace, SystemNamespace))
        {
            return (owner.MetadataName, method.Name, parameter.Ordinal) switch
            {
                (ConcurrentDictionaryMetadataName, TryAddMethodName, 1) or
                (ConcurrentQueueMetadataName, EnqueueMethodName, 0) or
                (ConcurrentStackMetadataName, PushMethodName, 0) => true,
                _ => false
            };
        }
        return false;
    }

    private static bool HasOnlyDefaultAllocations(ImmutableArray<LocalBinding> bindings, INamedTypeSymbol collection)
    {
        var found = false;
        foreach (var binding in bindings)
        {
            if (!binding.IsBaseValueBinding) continue;
            var allocation = RoslynOperationHelpers.UnwrapTransparent(binding.Value);
            var isMatchingDefaultAllocation = binding.Kind == LocalBindingKind.Value &&
                binding.TuplePath.IsDefaultOrEmpty &&
                IsDefaultAllocation(allocation) &&
                SymbolEqualityComparer.Default.Equals(allocation?.Type, collection);
            if (!isMatchingDefaultAllocation)
                return false;
            found = true;
        }
        return found;
    }

    internal static bool IsDefaultAllocation(IOperation? operation) => operation is
        IObjectCreationOperation { Arguments.Length: 0 } or
        ICollectionExpressionOperation { ConstructMethod.MethodKind: MethodKind.Constructor, ConstructArguments.Length: 0 };

    internal static bool BindsToStorageAdd(
        SemanticModel model, INamedTypeSymbol collection, IOperation element, ConventionFacts facts,
        bool knownExactDefaultCollection)
    {
        // Bracket expressions expose the argument but omit the implicit Add invocation.
        var argument = element.Syntax as ExpressionSyntax;
        if (element is ISpreadOperation { ElementType: { } itemType }) argument = DefaultValue(itemType);
        if (argument is null) return false;
        var add = SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, DefaultValue(collection),
            SyntaxFactory.IdentifierName(AddMethodName));
        var invocation = SyntaxFactory.InvocationExpression(add,
            SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(SyntaxFactory.Argument(argument))));
        return model.GetSpeculativeSymbolInfo(element.Syntax.SpanStart, invocation, SpeculativeBindingOption.BindAsExpression).Symbol
            is IMethodSymbol method && IsStorageAdd(method, collection, facts, knownExactDefaultCollection);

        static ExpressionSyntax DefaultValue(ITypeSymbol type) => SyntaxFactory.DefaultExpression(
            SyntaxFactory.ParseTypeName(type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
    }

    internal static bool IsStorageAdd(
        IMethodSymbol method, INamedTypeSymbol collection, ConventionFacts facts,
        bool knownExactDefaultCollection)
    {
        if (method.Name != AddMethodName || method.DeclaringSyntaxReferences.Length != 0)
            return false;
        if (CanTransformCollectionInsert(method, collection, facts, knownExactDefaultCollection))
            return false;
        // Collection contracts identify a stored element. Other Add methods can consume arbitrary helper inputs.
        if (MatchesStorageAddContract(method, collection, collection, facts.Symbols))
            return true;
        foreach (var contract in collection.AllInterfaces)
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            if (MatchesStorageAddContract(method, collection, contract, facts.Symbols))
                return true;
        }
        return false;
    }

    [SuppressMessage("SonarQube", "S3267", Justification = "Scanning member and constructor ImmutableArrays directly avoids boxing and predicate delegate allocations.")]
    private static bool CanTransformCollectionInsert(
        IMethodSymbol method, INamedTypeSymbol collection, ConventionFacts facts, bool knownExactDefaultCollection)
    {
        if (!ConventionSymbols.Same(method.ContainingType, facts.Symbols.CollectionGeneric))
            return false;
        // Collection<T>.Add delegates to protected virtual InsertItem and its backing IList<T>.
        if (!knownExactDefaultCollection && !collection.IsSealed)
            return true;
        for (var current = collection;
             current is not null && !ConventionSymbols.Same(current, facts.Symbols.CollectionGeneric);
             current = current.BaseType)
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            foreach (var member in current.GetMembers(InsertItemMethodName))
            {
                if (member is IMethodSymbol { IsOverride: true, Parameters.Length: 2 })
                    return true;
            }
            foreach (var ctor in current.InstanceConstructors)
            {
                if (!ctor.IsImplicitlyDeclared)
                    return true;
            }
        }
        return false;
    }

    [SuppressMessage("SonarQube", "S3267", Justification = "Scanning Add members directly avoids boxing ImmutableArray and capturing method and collection in a predicate delegate.")]
    private static bool MatchesStorageAddContract(
        IMethodSymbol method, INamedTypeSymbol collection, INamedTypeSymbol contract, ConventionSymbols symbols)
    {
        if (!IsElementContract(contract, symbols)) return false;
        // A derived Add can reimplement the storage contract even when the DTO assembly is metadata-only.
        if (BaseImplementsInterface(method.ContainingType.BaseType, contract)) return false;
        foreach (var member in contract.GetMembers(AddMethodName))
        {
            if (SymbolEqualityComparer.Default.Equals(method, member) ||
                SymbolEqualityComparer.Default.Equals(method, collection.FindImplementationForInterfaceMember(member)))
                return true;
        }
        return false;
    }

    [SuppressMessage("SonarQube", "S3267", Justification = "Scanning AllInterfaces directly avoids boxing ImmutableArray and capturing contract in a predicate delegate.")]
    private static bool BaseImplementsInterface(INamedTypeSymbol? baseType, INamedTypeSymbol contract)
    {
        if (baseType is null) return false;
        foreach (var inherited in baseType.AllInterfaces)
        {
            if (SymbolEqualityComparer.Default.Equals(inherited, contract))
                return true;
        }
        return false;
    }

    private static bool IsElementContract(INamedTypeSymbol contract, ConventionSymbols symbols) =>
        contract.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_ICollection_T ||
        ConventionSymbols.Same(contract, symbols.SetGeneric) ||
        ConventionSymbols.Same(contract, symbols.NonGenericList);
}
