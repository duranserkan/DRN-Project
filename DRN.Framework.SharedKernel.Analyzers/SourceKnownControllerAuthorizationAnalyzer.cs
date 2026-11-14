using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DRN.Framework.SharedKernel.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SourceKnownControllerAuthorizationAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        [DiagnosticDescriptors.SourceKnownControllerPolicyRequired];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            var foundation = start.Compilation.GetTypeByMetadataName("DRN.Framework.Hosting.Controllers.SourceKnownControllerBase`2");
            var authorize = start.Compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Authorization.AuthorizeAttribute");
            var anonymous = start.Compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Authorization.IAllowAnonymous");
            var nonAction = start.Compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Mvc.NonActionAttribute");
            var nonController = start.Compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Mvc.NonControllerAttribute");
            var attributeUsage = start.Compilation.GetTypeByMetadataName("System.AttributeUsageAttribute");
            var authorizeData = start.Compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Authorization.IAuthorizeData");
            var policyProperty = authorizeData?.GetMembers("Policy").OfType<IPropertySymbol>().FirstOrDefault();
            var conditional = start.Compilation.GetTypeByMetadataName("System.Diagnostics.ConditionalAttribute");
            if (foundation is null || authorize is null || anonymous is null || nonAction is null ||
                nonController is null || attributeUsage is null || policyProperty is null || conditional is null)
                return;

            var disposable = start.Compilation.GetTypeByMetadataName("System.IDisposable");
            var contract = new Contract(foundation, authorize, anonymous, nonAction, nonController, attributeUsage,
                policyProperty, conditional, disposable);
            start.RegisterSymbolAction(symbol => AnalyzeController(symbol, contract), SymbolKind.NamedType);
        });
    }

    private static void AnalyzeController(SymbolAnalysisContext context, Contract contract)
    {
        var controller = (INamedTypeSymbol)context.Symbol;
        if (controller.TypeKind != TypeKind.Class || controller.IsAbstract || !IsPublicClosedType(controller) ||
            !DerivesFrom(controller, contract.Foundation))
            return;

        var controllerAttributes = EffectiveAttributes(controller, contract).ToArray();
        if (controllerAttributes.Any(attribute => DerivesFrom(attribute.AttributeClass, contract.NonController)))
            return;

        var classAllowsAnonymous = controllerAttributes.Any(attribute => AllowsAnonymous(attribute, contract));
        var classHasPolicy = controllerAttributes.Any(attribute => HasNamedPolicy(attribute, contract));
        foreach (var method in EffectivePublicMethods(controller, context.CancellationToken))
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            if (!IsAction(method, contract))
                continue;

            var methodAttributes = EffectiveAttributes(method, contract).ToArray();
            if (methodAttributes.Any(attribute => DerivesFrom(attribute.AttributeClass, contract.NonAction)))
                continue;

            var allowsAnonymous = classAllowsAnonymous || methodAttributes.Any(attribute => AllowsAnonymous(attribute, contract));
            if (!allowsAnonymous && (classHasPolicy || methodAttributes.Any(attribute => HasNamedPolicy(attribute, contract))))
                continue;

            var owner = SymbolEqualityComparer.Default.Equals(method.ContainingType, controller) ? (ISymbol)method : controller;
            var location = owner.Locations.FirstOrDefault(item => item.IsInSource) ?? Location.None;
            context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.SourceKnownControllerPolicyRequired,
                location, controller.Name, method.Name,
                allowsAnonymous ? "anonymous access bypasses authorization" : "no explicit nonblank named policy is declared"));
        }
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

        var dispose = contract.Disposable?.GetMembers("Dispose").OfType<IMethodSymbol>().FirstOrDefault();
        return dispose is null || !SymbolEqualityComparer.Default.Equals(
            root.ContainingType.FindImplementationForInterfaceMember(dispose), root);
    }

    private static IEnumerable<IMethodSymbol> EffectivePublicMethods(INamedTypeSymbol controller, CancellationToken cancellationToken)
    {
        var seen = new List<IMethodSymbol>();
        var overridden = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        for (var current = controller; current is not null; current = current.BaseType)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var method in current.GetMembers().OfType<IMethodSymbol>())
            {
                if (method.DeclaredAccessibility != Accessibility.Public || method.MethodKind != MethodKind.Ordinary)
                    continue;
                if (overridden.Contains(method.OriginalDefinition) || seen.Any(derived => SameRuntimeSignature(derived, method)))
                    continue;

                seen.Add(method);
                for (var parent = method.OverriddenMethod; parent is not null; parent = parent.OverriddenMethod)
                    overridden.Add(parent.OriginalDefinition);
                yield return method;
            }
        }
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

    private static IEnumerable<AttributeData> EffectiveAttributes(ISymbol symbol, Contract contract)
    {
        var seen = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        var inherited = false;
        for (ISymbol? current = symbol; current is not null; current = AttributeParent(current))
        {
            foreach (var attribute in current.GetAttributes())
            {
                if (attribute.AttributeClass is not { } attributeType)
                    continue;
                var usage = AttributeUsage(attributeType, contract);
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

    private static (bool Inherited, bool AllowMultiple) AttributeUsage(INamedTypeSymbol attributeType, Contract contract)
    {
        for (var current = attributeType; current is not null; current = current.BaseType)
        {
            var usage = current.GetAttributes().FirstOrDefault(attribute =>
                SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, contract.AttributeUsage));
            if (usage is null)
                continue;
            var inherited = true;
            var multiple = false;
            foreach (var argument in usage.NamedArguments)
            {
                if (argument.Key == "Inherited" && argument.Value.Value is bool inherits) inherited = inherits;
                if (argument.Key == "AllowMultiple" && argument.Value.Value is bool allowsMultiple) multiple = allowsMultiple;
            }
            return (inherited, multiple);
        }
        return (true, false);
    }

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

    private sealed record Contract(INamedTypeSymbol Foundation, INamedTypeSymbol Authorize, INamedTypeSymbol Anonymous,
        INamedTypeSymbol NonAction, INamedTypeSymbol NonController, INamedTypeSymbol AttributeUsage,
        IPropertySymbol PolicyProperty, INamedTypeSymbol Conditional, INamedTypeSymbol? Disposable);
}
