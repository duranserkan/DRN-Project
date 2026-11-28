using Microsoft.CodeAnalysis.Operations;

namespace DRN.Framework.SharedKernel.Analyzers.Conventions;

internal static class PropertyAccessFacts
{
    private const string EqualsMethodName = nameof(object.Equals);

    internal static IPropertySymbol DispatchedProperty(
        IPropertySymbol property, INamedTypeSymbol? receiver, CancellationToken cancellation) =>
        ClassificationContract.ResolveReceiverProperty(property, receiver, cancellation).Selected;

    internal static IPropertySymbol? DirectWrittenProperty(IOperation? target) =>
        RoslynOperationHelpers.UnwrapTransparent(target) is IPropertyReferenceOperation { Property.IsIndexer: false } property &&
        RoslynOperationHelpers.UnwrapTransparent(property.Instance) is ILocalReferenceOperation
            ? property.Property
            : null;

    internal static INamedTypeSymbol? KnownLocalReceiverType(ReadOnlySpan<LocalBinding> bindings)
    {
        INamedTypeSymbol? known = null;
        foreach (var binding in bindings)
        {
            if (binding.WriteTarget is not null || binding.Kind == LocalBindingKind.Condition) continue;
            if (binding.Kind != LocalBindingKind.Value || !binding.TuplePath.IsDefaultOrEmpty) return null;
            var value = RoslynOperationHelpers.UnwrapTransparent(binding.Value);
            var type = value is IObjectCreationOperation creation
                ? creation.Type as INamedTypeSymbol
                : RoslynSymbolHelpers.SealedType(value?.Type);
            if (type is null) return null;
            if (known is not null && !SymbolEqualityComparer.Default.Equals(known, type)) return null;
            known = type;
        }
        return known;
    }

    internal static bool HasDefinitePropertyReplacement(
        ImmutableArray<LocalBinding> bindings,
        IPropertySymbol accessedProperty,
        INamedTypeSymbol? localType,
        ConventionFacts facts)
    {
        var read = facts.ProjectionFacts.ResolveGetterRead(accessedProperty, localType);
        if (read.MayReadSibling) return false;
        for (var i = 0; i < bindings.Length; i++)
        {
            var binding = bindings[i];
            if (!binding.ReplacesMember || DirectWrittenProperty(binding.WriteTarget) is not { } writtenProperty)
                continue;
            var dispatchedWritten = DispatchedProperty(writtenProperty, localType, facts.Cancellation);
            if (!facts.ProjectionFacts.CanGuaranteeDirectSetter(dispatchedWritten, localType))
                continue;
            if (read.Matches(writtenProperty, facts.Cancellation))
                return true;
        }
        return false;
    }

    internal static bool SameOrMatchingProperty(IPropertySymbol first, IPropertySymbol second) =>
        RoslynSymbolHelpers.MatchesAccessedProperty(first, second) ||
        RoslynSymbolHelpers.MatchesAccessedProperty(second, first);

    internal static bool HasCompilerGeneratedRecordEquals(INamedTypeSymbol type, ConventionFacts facts)
    {
        if (!type.IsRecord) return false;
        for (var current = type; current is { IsRecord: true }; current = current.BaseType)
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            foreach (var member in current.GetMembers(EqualsMethodName))
            {
                if (member is IMethodSymbol { IsStatic: false, Parameters.Length: 1 } equals &&
                    SymbolEqualityComparer.Default.Equals(equals.Parameters[0].Type, current) &&
                    !RoslynSymbolHelpers.IsCompilerGenerated(equals, facts.Symbols.CompilerGeneratedAttribute))
                    return false;
            }
        }
        return true;
    }

    internal static IPropertySymbol? FindVerifiedPositionalRecordProperty(
        INamedTypeSymbol destinationType, IMethodSymbol? constructor, IParameterSymbol? parameter, CancellationToken cancellation)
    {
        if (!destinationType.IsRecord || constructor is null || parameter is null)
            return null;
        if (constructor.DeclaringSyntaxReferences is not [var ctorRef] ||
            ctorRef.GetSyntax(cancellation) is not RecordDeclarationSyntax)
            return null;
        if (parameter.DeclaringSyntaxReferences is not [var paramRef])
            return null;
        foreach (var member in destinationType.GetMembers(parameter.Name))
        {
            if (member is IPropertySymbol { IsStatic: false, IsIndexer: false } property &&
                property.DeclaringSyntaxReferences is [var propRef] &&
                propRef.SyntaxTree == paramRef.SyntaxTree &&
                propRef.Span == paramRef.Span)
                return property;
        }
        return null;
    }

    internal static GetterRead ComputeGetterRead(IPropertySymbol declared, INamedTypeSymbol? receiver, ConventionFacts facts)
    {
        var first = DispatchedProperty(declared, receiver, facts.Cancellation);
        var property = first;
        ImmutableArray<IPropertySymbol>.Builder? forwarded = null;
        for (var depth = 0; depth < ProjectionLimits.MaxExpansionDepth; depth++)
        {
            if (RoslynSymbolHelpers.IsDynamicDispatchProperty(property, receiver))
                break;
            var mayReadSibling = InspectGetter(property, facts, out var next);
            if (next is null)
                return new(first, receiver, forwarded is null ? default : forwarded.ToImmutable(), mayReadSibling);
            property = DispatchedProperty(next, receiver, facts.Cancellation);
            var alreadySeen = SymbolEqualityComparer.Default.Equals(property, first) ||
                ContainsProperty(forwarded, property);
            if (alreadySeen) break;
            (forwarded ??= ImmutableArray.CreateBuilder<IPropertySymbol>()).Add(property);
        }
        return new(first, receiver, forwarded is null ? default : forwarded.ToImmutable(), MayReadSibling: true);
    }

    internal static bool ComputeHasAutoPropertySetter(IPropertySymbol property, ConventionFacts facts)
    {
        var setter = (property.PartialImplementationPart ?? property).SetMethod;
        if (setter is null || setter.IsAbstract || setter.IsExtern) return false;
        if (setter.DeclaringSyntaxReferences is [var reference, ..])
            return reference.GetSyntax(facts.Cancellation) is AccessorDeclarationSyntax { Body: null, ExpressionBody: null };
        return setter.IsImplicitlyDeclared ||
               RoslynSymbolHelpers.HasAttribute(setter, facts.Symbols.CompilerGeneratedAttribute);
    }

    internal static bool HasKnownNonThrowingGetter(
        IPropertySymbol property, INamedTypeSymbol? receiver, ConventionSymbols symbols, CancellationToken cancellation)
    {
        var dispatched = DispatchedProperty(property, receiver, cancellation);
        if (RoslynSymbolHelpers.IsDynamicDispatchProperty(dispatched, receiver))
            return false;
        var implementation = dispatched.PartialImplementationPart ?? dispatched;
        if (implementation.IsPartialDefinition)
            return false;
        var getter = implementation.GetMethod;
        if (getter is null || getter.IsAbstract || getter.IsExtern)
            return false;
        if (getter.DeclaringSyntaxReferences is [var reference, ..])
            return reference.GetSyntax(cancellation) is
                AccessorDeclarationSyntax { Body: null, ExpressionBody: null } or
                ParameterSyntax;
        return getter.IsImplicitlyDeclared ||
               RoslynSymbolHelpers.HasAttribute(getter, symbols.CompilerGeneratedAttribute);
    }

    internal static IPropertySymbol? ComputeDestinationProperty(INamedTypeSymbol destinationType, string parameterName)
    {
        IPropertySymbol? caseInsensitive = null;
        for (var current = destinationType; current is not null; current = current.BaseType)
        {
            if (FindTypeDestinationProperty(current, parameterName, ref caseInsensitive) is { } exact)
                return exact;
        }
        return caseInsensitive;
    }

    private static IPropertySymbol? FindTypeDestinationProperty(
        INamedTypeSymbol type, string parameterName, ref IPropertySymbol? caseInsensitive)
    {
        foreach (var member in type.GetMembers())
        {
            if (member is not IPropertySymbol property) continue;
            var isAccessibleInstanceProperty = !property.IsStatic && !property.IsIndexer &&
                property.DeclaredAccessibility == Accessibility.Public;
            if (!isAccessibleInstanceProperty) continue;
            if (string.Equals(property.Name, parameterName, StringComparison.Ordinal))
                return property;
            if (caseInsensitive is null &&
                string.Equals(property.Name, parameterName, StringComparison.OrdinalIgnoreCase))
                caseInsensitive = property;
        }
        return null;
    }

    internal static INamedTypeSymbol Receiver(IPropertyReferenceOperation property, ConventionFacts facts)
    {
        var instance = property.Instance;
        while (instance is not null)
        {
            var operand = instance switch
            {
                IConditionalAccessInstanceOperation { Type.IsReferenceType: true } placeholder =>
                    RoslynOperationHelpers.ConditionalReceiver(placeholder, facts.Cancellation),
                IConversionOperation { Conversion.IsUserDefined: false, Operand.Type: INamedTypeSymbol from, Type: { } to } conversion
                    when RoslynSymbolHelpers.IsImplicitUpcastOrBoxing(facts.Compilation, from, to) => conversion.Operand,
                _ => null
            };
            if (operand is null) break;
            instance = operand;
        }
        var receiver = instance?.Type;
        if (receiver is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable &&
            !SymbolEqualityComparer.Default.Equals(nullable, property.Property.ContainingType))
            receiver = nullable.TypeArguments[0];
        return receiver as INamedTypeSymbol ?? property.Property.ContainingType;
    }

    private static bool InspectGetter(IPropertySymbol property, ConventionFacts facts, out IPropertySymbol? forwarded)
    {
        forwarded = null;
        var getter = (property.PartialImplementationPart ?? property).GetMethod;
        if (getter is null) return false;
        if (getter.IsAbstract || getter.IsExtern) return true;
        foreach (var reference in getter.DeclaringSyntaxReferences)
        {
            var syntax = reference.GetSyntax(facts.Cancellation);
            var expression = syntax switch
            {
                AccessorDeclarationSyntax { Body.Statements: [ReturnStatementSyntax statement] } => statement.Expression,
                AccessorDeclarationSyntax accessor => accessor.ExpressionBody?.Expression,
                ArrowExpressionClauseSyntax arrow => arrow.Expression,
                PropertyDeclarationSyntax declaration => declaration.ExpressionBody?.Expression,
                _ => null
            };
            if (expression is not null)
            {
                forwarded = ForwardedProperty(expression, property, facts);
                return true;
            }
            if (syntax is AccessorDeclarationSyntax accessorSyntax) return accessorSyntax.Body is not null;
            if (syntax is ParameterSyntax) return false;
        }
        if (getter.IsImplicitlyDeclared) return false;
        return !RoslynSymbolHelpers.HasAttribute(getter, facts.Symbols.CompilerGeneratedAttribute);
    }

    private static IPropertySymbol? ForwardedProperty(ExpressionSyntax expression, IPropertySymbol getter, ConventionFacts facts)
    {
        while (expression is ParenthesizedExpressionSyntax parentheses)
            expression = parentheses.Expression;
        var isSimpleInstancePropertyAccess = expression is IdentifierNameSyntax or
            MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax };
        if (!isSimpleInstancePropertyAccess || !facts.Compilation.ContainsSyntaxTree(expression.SyntaxTree))
            return null;

        var model = facts.SemanticModel(expression.SyntaxTree);
        if (model.GetOperation(expression, facts.Cancellation) is not IPropertyReferenceOperation
            { Instance: IInstanceReferenceOperation { ReferenceKind: InstanceReferenceKind.ContainingTypeInstance } } read)
            return null;
        if (!RoslynSymbolHelpers.IsImplicitUpcastOrBoxing(facts.Compilation, read.Property.Type, getter.OriginalDefinition.Type))
            return null;
        if (!read.Property.ContainingType.IsGenericType)
            return read.Property;

        // The getter body binds to the generic declaration, while writes use the constructed property.
        return ResolveConstructedProperty(getter, read.Property, facts.Cancellation);
    }

    private static IPropertySymbol? ResolveConstructedProperty(
        IPropertySymbol getter, IPropertySymbol readProperty, CancellationToken cancellation)
    {
        foreach (var owner in ConventionSymbols.Bases(getter.ContainingType, cancellation))
        {
            if (!SymbolEqualityComparer.Default.Equals(owner.OriginalDefinition, readProperty.ContainingType.OriginalDefinition))
                continue;
            foreach (var member in owner.GetMembers(readProperty.Name))
            {
                if (member is IPropertySymbol candidate &&
                    SymbolEqualityComparer.Default.Equals(candidate.OriginalDefinition, readProperty.OriginalDefinition))
                    return candidate;
            }
        }
        return null;
    }

    private static bool ContainsProperty(ImmutableArray<IPropertySymbol>.Builder? properties, IPropertySymbol target)
    {
        if (properties is null) return false;
        for (var i = 0; i < properties.Count; i++)
        {
            if (SymbolEqualityComparer.Default.Equals(properties[i], target))
                return true;
        }
        return false;
    }
}
