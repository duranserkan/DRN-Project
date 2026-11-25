using Microsoft.CodeAnalysis.CSharp;

namespace DRN.Framework.SharedKernel.Analyzers;

internal static class RoslynSymbolHelpers
{
    [return: NotNullIfNotNull(nameof(type))]
    internal static ITypeSymbol? UnwrapNullable(ITypeSymbol? type) =>
        type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T, TypeArguments: [var underlying] }
            ? underlying
            : type;

    internal static ISymbol UnwrapNullableSymbol(ISymbol symbol) =>
        symbol is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T, TypeArguments: [var underlying] }
            ? underlying
            : symbol;

    [SuppressMessage("SonarQube", "S3267", Justification = "Scanning the immutable attribute array directly avoids boxing ImmutableArray and allocating a predicate capturing target.")]
    internal static AttributeData? FindAttribute(ISymbol symbol, INamedTypeSymbol? target)
    {
        if (target is null) return null;
        foreach (var attribute in symbol.GetAttributes())
        {
            if (attribute.AttributeClass is { } attributeClass &&
                SymbolEqualityComparer.Default.Equals(attributeClass.OriginalDefinition, target.OriginalDefinition))
                return attribute;
        }
        return null;
    }

    internal static bool HasAttribute(ISymbol symbol, INamedTypeSymbol? target) =>
        FindAttribute(symbol, target) is not null;

    internal static bool OverridesOrEquals(IPropertySymbol property, IPropertySymbol ancestor)
    {
        for (var current = property; current is not null; current = current.OverriddenProperty)
        {
            if (SymbolEqualityComparer.Default.Equals(current, ancestor))
                return true;
        }
        return false;
    }

    internal static bool MatchesAccessedProperty(IPropertySymbol assigned, IPropertySymbol accessed)
    {
        if (OverridesOrEquals(assigned, accessed))
            return true;
        if (accessed.ContainingType.TypeKind != TypeKind.Interface)
            return false;
        var implementation = assigned.ContainingType.FindImplementationForInterfaceMember(accessed);
        return SymbolEqualityComparer.Default.Equals(implementation, assigned);
    }

    internal static bool IsDynamicDispatchProperty(IPropertySymbol property, INamedTypeSymbol? receiver)
    {
        if (receiver is not null) return false;
        var isSealed = property.IsSealed || property.ContainingType.IsSealed;
        if (isSealed) return false;
        return property.IsVirtual || property.IsAbstract || property.IsOverride ||
               property.ContainingType.TypeKind == TypeKind.Interface;
    }

    internal static bool IsCompilerGenerated(ISymbol symbol, INamedTypeSymbol? compilerGeneratedAttribute) =>
        symbol.IsImplicitlyDeclared || HasAttribute(symbol, compilerGeneratedAttribute);

    internal static IMethodSymbol RootOverriddenMethod(IMethodSymbol method)
    {
        var root = method;
        while (root.OverriddenMethod is { } overridden)
            root = overridden;
        return root;
    }

    internal static bool IsObjectOrDisposeMethod(IMethodSymbol method, IMethodSymbol? disposeMethod)
    {
        var root = RootOverriddenMethod(method);
        if (root.ContainingType.SpecialType == SpecialType.System_Object)
            return true;
        if (disposeMethod is null)
            return false;
        var implementation = root.ContainingType.FindImplementationForInterfaceMember(disposeMethod);
        return SymbolEqualityComparer.Default.Equals(implementation, root);
    }

    internal static bool IsNamespace(INamespaceSymbol? ns, string leaf, string root) =>
        ns is { Name: var n1 } && n1 == leaf &&
        ns.ContainingNamespace is { Name: var n2, ContainingNamespace.IsGlobalNamespace: true } &&
        n2 == root;

    internal static bool IsNamespace(INamespaceSymbol? ns, string leaf, string middle, string root) =>
        ns is { Name: var n1 } && n1 == leaf &&
        ns.ContainingNamespace is { Name: var n2 } && n2 == middle &&
        ns.ContainingNamespace.ContainingNamespace is { Name: var n3, ContainingNamespace.IsGlobalNamespace: true } &&
        n3 == root;

    internal static bool IsImplicitUpcastOrBoxing(Compilation compilation, ITypeSymbol from, ITypeSymbol to)
    {
        if (compilation is CSharpCompilation csharp)
        {
            var conversion = csharp.ClassifyConversion(from, to);
            return conversion.IsImplicit && (conversion.IsReference || conversion.IsBoxing || conversion.IsIdentity);
        }
        var common = compilation.ClassifyCommonConversion(from, to);
        return common.IsImplicit && (common.IsReference || common.IsIdentity);
    }

    internal static INamedTypeSymbol? SealedType(ITypeSymbol? type) =>
        type is INamedTypeSymbol { IsSealed: true } named ? named : null;

    internal static int TupleElementOrdinal(IFieldSymbol field)
    {
        if (field.ContainingType is not { IsTupleType: true } tuple)
            return -1;
        var underlying = field.CorrespondingTupleField ?? field;
        var elements = tuple.TupleElements;
        for (var i = 0; i < elements.Length; i++)
        {
            var element = elements[i];
            if (SymbolEqualityComparer.Default.Equals(element.CorrespondingTupleField ?? element, underlying))
                return i;
        }
        return -1;
    }
}
