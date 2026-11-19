using DRN.Framework.CompilerTools;

namespace DRN.Framework.SharedKernel.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SourceKnownControllerAuthorizationAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        [DiagnosticDescriptors.SourceKnownControllerPolicyRequired];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            if (Contract.TryCreate(start.Compilation) is not { } contract)
                return;
            start.RegisterSymbolAction(symbol => AnalyzeController(symbol, contract), SymbolKind.NamedType);
        });
    }

    private static void AnalyzeController(SymbolAnalysisContext context, Contract contract)
    {
        var controller = (INamedTypeSymbol)context.Symbol;
        if (controller.TypeKind != TypeKind.Class || controller.IsAbstract || !IsPublicClosedType(controller) ||
            !DerivesFrom(controller, contract.Foundation))
            return;

        var classAccess = ReadAuthorization(controller, contract.NonController, contract, context.CancellationToken);
        if (classAccess.Excluded) return;
        foreach (var method in EffectivePublicMethods(controller, context.CancellationToken))
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            if (!IsAction(method, contract))
                continue;

            AnalyzeAction(context, contract, controller, method, classAccess);
        }
    }

    private static void AnalyzeAction(SymbolAnalysisContext context, Contract contract, INamedTypeSymbol controller,
        IMethodSymbol method, AuthorizationFacts classAccess)
    {
        var declaredHere = SymbolEqualityComparer.Default.Equals(method.ContainingType, controller);
        var methodAccess = declaredHere ? ReadAuthorization(method, contract.NonAction, contract, context.CancellationToken)
            : contract.InheritedAction(method, context.CancellationToken);
        var allowsAnonymous = classAccess.Anonymous || methodAccess.Anonymous;
        if (methodAccess.Excluded || !allowsAnonymous && (classAccess.NamedPolicy || methodAccess.NamedPolicy)) return;

        var owner = declaredHere ? (ISymbol)method : controller;
        var location = owner.Locations.FirstOrDefault(item => item.IsInSource) ?? Location.None;
        context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.SourceKnownControllerPolicyRequired,
            location, controller.Name, method.Name,
            allowsAnonymous ? "anonymous access bypasses authorization" : "no explicit nonblank named policy is declared"));
    }

    private readonly record struct AuthorizationFacts(bool Excluded, bool Anonymous, bool NamedPolicy);

    private static AuthorizationFacts ReadAuthorization(ISymbol symbol, INamedTypeSymbol exclusion, Contract contract, CancellationToken cancellation)
    {
        var anonymous = false;
        var policy = false;
        foreach (var attribute in EffectiveAttributes(symbol, contract, cancellation))
        {
            if (DerivesFrom(attribute.AttributeClass, exclusion)) return new(true, false, false);
            anonymous |= AllowsAnonymous(attribute, contract);
            if (!anonymous && !policy) policy = HasNamedPolicy(attribute, contract);
        }
        return new(false, anonymous, policy);
    }

    private static bool IsPublicClosedType(INamedTypeSymbol type)
    {
        for (var current = type; current is not null; current = current.ContainingType)
            if (current.DeclaredAccessibility != Accessibility.Public || current.Arity != 0)
                return false;
        return true;
    }

    private static bool IsAction(IMethodSymbol method, Contract contract)
    {
        if (method.MethodKind != MethodKind.Ordinary || method.IsStatic || method.IsAbstract || method.IsGenericMethod)
            return false;

        var root = method;
        while (root.OverriddenMethod is { } overridden)
            root = overridden;
        if (root.ContainingType.SpecialType == SpecialType.System_Object)
            return false;

        return contract.Dispose is null || !SymbolEqualityComparer.Default.Equals(
            root.ContainingType.FindImplementationForInterfaceMember(contract.Dispose), root);
    }

    private static IEnumerable<IMethodSymbol> EffectivePublicMethods(INamedTypeSymbol controller, CancellationToken cancellationToken)
    {
        var seen = new Dictionary<string, List<IMethodSymbol>>(StringComparer.Ordinal);
        var overridden = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        for (var current = controller; current is not null; current = current.BaseType)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var method in current.GetMembers().OfType<IMethodSymbol>())
            {
                if (method.DeclaredAccessibility != Accessibility.Public || method.MethodKind != MethodKind.Ordinary)
                    continue;
                seen.TryGetValue(method.Name, out var overloads);
                if (overridden.Contains(method.OriginalDefinition) || overloads?.Any(derived => SameRuntimeSignature(derived, method)) == true)
                    continue;

                if (overloads == null)
                {
                    overloads = [];
                    seen.Add(method.Name, overloads);
                }
                overloads.Add(method);
                RecordOverriddenMethods(method, overridden);
                yield return method;
            }
        }
    }

    private static void RecordOverriddenMethods(IMethodSymbol method, HashSet<ISymbol> overridden)
    {
        for (var parent = method.OverriddenMethod; parent is not null; parent = parent.OverriddenMethod)
            overridden.Add(parent.OriginalDefinition);
    }

    private static bool SameRuntimeSignature(IMethodSymbol left, IMethodSymbol right)
    {
        if (left.Name != right.Name || left.Arity != right.Arity || left.IsStatic != right.IsStatic ||
            left.IsVararg != right.IsVararg || left.Parameters.Length != right.Parameters.Length ||
            (left.ReturnsByRef || left.ReturnsByRefReadonly) != (right.ReturnsByRef || right.ReturnsByRefReadonly) ||
            !SameType(left.ReturnType, right.ReturnType) ||
            !SameModifiers(left.ReturnTypeCustomModifiers, right.ReturnTypeCustomModifiers) ||
            !SameModifiers(left.RefCustomModifiers, right.RefCustomModifiers))
            return false;

        for (var index = 0; index < left.Parameters.Length; index++)
        {
            var first = left.Parameters[index];
            var second = right.Parameters[index];
            if ((first.RefKind == RefKind.None) != (second.RefKind == RefKind.None) ||
                !SameType(first.Type, second.Type) || !SameModifiers(first.CustomModifiers, second.CustomModifiers) ||
                !SameModifiers(first.RefCustomModifiers, second.RefCustomModifiers))
                return false;
        }
        return true;
    }

    private static bool SameType(ITypeSymbol left, ITypeSymbol right)
    {
        if (SymbolEqualityComparer.Default.Equals(left, right))
            return true;
        if ((left.TypeKind == TypeKind.Dynamic || left.SpecialType == SpecialType.System_Object) &&
            (right.TypeKind == TypeKind.Dynamic || right.SpecialType == SpecialType.System_Object))
            return true;
        if (left is IArrayTypeSymbol firstArray && right is IArrayTypeSymbol secondArray)
            return firstArray.Rank == secondArray.Rank && SameType(firstArray.ElementType, secondArray.ElementType);
        if (left is INamedTypeSymbol first && right is INamedTypeSymbol second)
        {
            first = first.TupleUnderlyingType ?? first;
            second = second.TupleUnderlyingType ?? second;
            return SymbolEqualityComparer.Default.Equals(first.OriginalDefinition, second.OriginalDefinition) &&
                   (first.ContainingType is null ? second.ContainingType is null :
                       second.ContainingType is not null && SameType(first.ContainingType, second.ContainingType)) &&
                   first.TypeArguments.Length == second.TypeArguments.Length &&
                   first.TypeArguments.Zip(second.TypeArguments).All(pair => SameType(pair.First, pair.Second));
        }
        return false;
    }

    private static bool SameModifiers(ImmutableArray<CustomModifier> left, ImmutableArray<CustomModifier> right)
        => left.Length == right.Length && left.Zip(right).All(pair => pair.First.IsOptional == pair.Second.IsOptional &&
            SymbolEqualityComparer.Default.Equals(pair.First.Modifier, pair.Second.Modifier));

    private static IEnumerable<AttributeData> EffectiveAttributes(ISymbol symbol, Contract contract, CancellationToken cancellationToken)
    {
        var seen = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        var inherited = false;
        for (ISymbol? current = symbol; current is not null; current = AttributeParent(current))
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var attribute in current.GetAttributes())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (attribute.AttributeClass is not { } attributeType)
                    continue;
                var usage = contract.AttributeUsage.Get(attributeType, cancellationToken);
                if (inherited && !usage.Inherited)
                    continue;
                if (!seen.Add(attributeType) && !usage.AllowMultiple)
                    continue;
                yield return attribute;
            }
            inherited = true;
        }
    }

    private static ISymbol? AttributeParent(ISymbol symbol) => symbol switch
    {
        INamedTypeSymbol type => type.BaseType,
        IMethodSymbol method => method.OverriddenMethod,
        _ => null
    };

    private static bool HasNamedPolicy(AttributeData attribute, Contract contract)
    {
        if (!DerivesFrom(attribute.AttributeClass, contract.Authorize) || IsConditionalAttribute(attribute.AttributeClass!, contract))
            return false;
        foreach (var argument in attribute.NamedArguments)
        {
            if (argument.Key != "Policy")
                continue;
            // A derived attribute may hide Policy with a different member. That does not set IAuthorizeData.Policy.
            var property = FindPolicyMember(attribute.AttributeClass!);
            return property is IPropertySymbol && SymbolEqualityComparer.Default.Equals(property.ContainingType, contract.Authorize) &&
                   UsesFrameworkPolicy(attribute.AttributeClass!, property, contract) &&
                   argument.Value.Value is string namedPolicy && !string.IsNullOrWhiteSpace(namedPolicy);
        }
        return SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, contract.Authorize) &&
               attribute.ConstructorArguments.Length == 1 && attribute.ConstructorArguments[0].Value is string policy &&
               !string.IsNullOrWhiteSpace(policy);
    }

    private static bool IsConditionalAttribute(INamedTypeSymbol attributeType, Contract contract)
    {
        // Conditional attributes may be omitted from runtime metadata at individual application sites.
        for (var current = attributeType; current is not null; current = current.BaseType)
            if (current.GetAttributes().Any(attribute =>
                    SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, contract.Conditional)))
                return true;
        return false;
    }

    private static bool UsesFrameworkPolicy(INamedTypeSymbol attributeType, ISymbol property, Contract contract)
    {
        return SymbolEqualityComparer.Default.Equals(
            attributeType.FindImplementationForInterfaceMember(contract.PolicyProperty), property);
    }

    private static ISymbol? FindPolicyMember(INamedTypeSymbol attributeType)
    {
        for (var current = attributeType; current is not null; current = current.BaseType)
            if (current.GetMembers("Policy").FirstOrDefault() is { } member)
                return member;
        return null;
    }

    private static bool AllowsAnonymous(AttributeData attribute, Contract contract)
        => attribute.AttributeClass?.AllInterfaces.Any(type => SymbolEqualityComparer.Default.Equals(type, contract.Anonymous)) == true;

    private static bool DerivesFrom(INamedTypeSymbol? type, INamedTypeSymbol expected)
    {
        for (var current = type; current is not null; current = current.BaseType)
            if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, expected))
                return true;
        return false;
    }

    private sealed class Contract
    {
        private readonly SymbolFactCache<IMethodSymbol, AuthorizationFacts> _inheritedActions = new(capacity: 31);
        private readonly Func<IMethodSymbol, CancellationToken, AuthorizationFacts> _readAction;

        private Contract(INamedTypeSymbol nonAction)
        {
            NonAction = nonAction;
            _readAction = (method, token) => ReadAuthorization(method, NonAction, this, token);
        }

        internal required INamedTypeSymbol Foundation { get; init; }
        internal required INamedTypeSymbol Authorize { get; init; }
        internal required INamedTypeSymbol Anonymous { get; init; }
        internal INamedTypeSymbol NonAction { get; }
        internal required INamedTypeSymbol NonController { get; init; }
        internal required AttributeUsageFacts.Cache AttributeUsage { get; init; }
        internal required IPropertySymbol PolicyProperty { get; init; }
        internal required INamedTypeSymbol Conditional { get; init; }
        internal required IMethodSymbol? Dispose { get; init; }

        internal static Contract? TryCreate(Compilation compilation)
        {
            var foundation = compilation.GetTypeByMetadataName("DRN.Framework.Hosting.Controllers.SourceKnownControllerBase`2");
            if (foundation is null) return null;
            var authorize = compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Authorization.AuthorizeAttribute");
            var anonymous = compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Authorization.IAllowAnonymous");
            var nonAction = compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Mvc.NonActionAttribute");
            var nonController = compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Mvc.NonControllerAttribute");
            var attributeUsage = AttributeUsageFacts.Cache.For(compilation);
            var authorizeData = compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Authorization.IAuthorizeData");
            var policyProperty = authorizeData?.GetMembers("Policy").OfType<IPropertySymbol>().FirstOrDefault();
            var conditional = compilation.GetTypeByMetadataName("System.Diagnostics.ConditionalAttribute");
            if (authorize is null || anonymous is null || nonAction is null ||
                nonController is null || attributeUsage.UsageAttribute is null || policyProperty is null || conditional is null)
                return null;

            var dispose = compilation.GetTypeByMetadataName("System.IDisposable")
                ?.GetMembers("Dispose").OfType<IMethodSymbol>().FirstOrDefault();
            return new Contract(nonAction)
            {
                Foundation = foundation,
                Authorize = authorize,
                Anonymous = anonymous,
                NonController = nonController,
                AttributeUsage = attributeUsage,
                PolicyProperty = policyProperty,
                Conditional = conditional,
                Dispose = dispose
            };
        }

        internal AuthorizationFacts InheritedAction(IMethodSymbol method, CancellationToken cancellation) =>
            _inheritedActions.GetOrAdd(method, _readAction, cancellation);
    }
}
