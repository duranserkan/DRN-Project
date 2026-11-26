using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Operations;

namespace DRN.Framework.SharedKernel.Analyzers.Conventions;

internal static class CollectionMutationFacts
{
    internal static bool IsStringBuilder(INamedTypeSymbol? type) =>
        type is { Name: "StringBuilder" } &&
        RoslynSymbolHelpers.IsNamespace(type.ContainingNamespace, "Text", "System");

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
            (owner.Name != "Builder" &&
             RoslynSymbolHelpers.IsNamespace(owner.ContainingNamespace, "Immutable", "Collections", "System"));
        if (isImmutableType) return false;

        return method.Name switch
        {
            "Append" or "AppendLine" or "AppendFormat" or "AppendJoin" or "Replace" => IsStringBuilder(owner),
            "Add" or "AddRange" or "TryAdd" or "Insert" or "InsertRange" or
            "Enqueue" or "TryEnqueue" or "Push" or "TryPush" or
            "Set" or "SetItem" or "SetValue" or "AddOrUpdate" or "GetOrAdd" or "UnionWith" => true,
            _ => false
        };
    }

    internal static bool StoresProjectedElement(
        IInvocationOperation call, IArgumentOperation argument, SelectorScope? locals, ConventionFacts facts)
    {
        if (call.Instance?.Type is not INamedTypeSymbol collection || argument.Parameter is not { } parameter)
            return false;
        var method = call.TargetMethod;
        if (method.Name == "Add" && method.Parameters.Length == 1)
        {
            var exactDefault = ConventionSymbols.Same(method.ContainingType, facts.Symbols.CollectionGeneric) &&
                RoslynOperationHelpers.UnwrapTransparent(call.Instance) is ILocalReferenceOperation receiver &&
                locals is not null && locals.TryGetValues(receiver, out var bindings) &&
                HasOnlyDefaultAllocations(bindings, collection);
            return IsStorageAdd(method, collection, facts, exactDefault);
        }
        if (method.DeclaringSyntaxReferences.Length != 0)
            return false;

        var owner = method.ContainingType.OriginalDefinition;
        if (RoslynSymbolHelpers.IsNamespace(owner.ContainingNamespace, "Generic", "Collections", "System"))
        {
            return (owner.MetadataName, method.Name, parameter.Ordinal) switch
            {
                ("List`1", "AddRange", 0) or
                ("List`1" or "IList`1", "Insert", 1) or
                ("List`1", "InsertRange", 1) or
                ("Queue`1", "Enqueue", 0) or
                ("Stack`1", "Push", 0) or
                ("Dictionary`2" or "IDictionary`2", "Add" or "TryAdd", 1) => true,
                _ => false
            };
        }
        if (RoslynSymbolHelpers.IsNamespace(owner.ContainingNamespace, "Concurrent", "Collections", "System"))
        {
            return (owner.MetadataName, method.Name, parameter.Ordinal) switch
            {
                ("ConcurrentDictionary`2", "TryAdd", 1) or
                ("ConcurrentQueue`1", "Enqueue", 0) or
                ("ConcurrentStack`1", "Push", 0) => true,
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
            if (binding.Kind != LocalBindingKind.Value || !binding.TuplePath.IsDefaultOrEmpty ||
                !IsDefaultAllocation(allocation) || !SymbolEqualityComparer.Default.Equals(allocation?.Type, collection))
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
            SyntaxFactory.IdentifierName("Add"));
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
        if (method.Name != "Add" || method.DeclaringSyntaxReferences.Length != 0)
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
            foreach (var member in current.GetMembers("InsertItem"))
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

    private static bool MatchesStorageAddContract(
        IMethodSymbol method, INamedTypeSymbol collection, INamedTypeSymbol contract, ConventionSymbols symbols)
    {
        if (!IsElementContract(contract, symbols)) return false;
        // A derived Add can reimplement the storage contract even when the DTO assembly is metadata-only.
        if (BaseImplementsInterface(method.ContainingType.BaseType, contract)) return false;
        foreach (var member in contract.GetMembers("Add"))
        {
            if (SymbolEqualityComparer.Default.Equals(method, member) ||
                SymbolEqualityComparer.Default.Equals(method, collection.FindImplementationForInterfaceMember(member)))
                return true;
        }
        return false;
    }

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
