using Microsoft.CodeAnalysis.Operations;

namespace DRN.Framework.SharedKernel.Analyzers.Conventions;

internal sealed class ConventionCompositionReader(ConventionFacts facts)
{
    private readonly Composition _evidence = new();
    private readonly HashSet<ISymbol> _scanScopes = new(SymbolEqualityComparer.Default);
    private readonly HashSet<ISymbol> _unknownScopes = new(SymbolEqualityComparer.Default);

    internal static string KeyIdentity(string key) => "key:" + key;

    internal Composition Complete()
    {
        _evidence.UnknownComposition |= _scanScopes.Overlaps(_unknownScopes);
        return _evidence;
    }

    internal void InspectCreation(SemanticModel semantic, BaseObjectCreationExpressionSyntax creation)
    {
        if (CreatesServiceCollectionConsumer(facts, semantic, creation))
            _evidence.UnknownComposition = true;
    }

    private static bool CreatesServiceCollectionConsumer(ConventionFacts facts, SemanticModel semantic,
        BaseObjectCreationExpressionSyntax creation) =>
        creation.ArgumentList!.Arguments.Any(argument =>
            semantic.GetTypeInfo(argument.Expression, facts.Cancellation).Type is INamedTypeSymbol argumentType &&
            ConventionSymbols.Is(argumentType, facts.Symbols.ServiceCollection, facts.Cancellation));

    internal TypeInfo? InspectInvocation(SemanticModel semantic, InvocationExpressionSyntax invocation, IMethodSymbol? method, ISymbol? scope)
    {
        if (method is null)
        {
            _evidence.UnknownComposition = true;
            return null;
        }
        var original = method.ReducedFrom ?? method;
        if (ConventionSymbols.Same(original.ContainingType, facts.Symbols.DrnDiServiceCollectionExtensions) &&
            method.Name == "AddServicesWithAttributes")
        {
            RecordAttributeScanInvocation(facts, semantic, invocation, original, scope, _evidence, _scanScopes);
            return null;
        }
        if (!ConventionSymbols.Same(original.ContainingType, facts.Symbols.DiServiceCollectionExtensions) ||
            method.Name is not ("AddScoped" or "AddTransient" or "AddSingleton" or "AddKeyedScoped" or "AddKeyedTransient" or "AddKeyedSingleton"))
            return RecordNonManualInvocation(facts, semantic, invocation, method, scope, _evidence, _unknownScopes);
        RecordManualRegistration(facts, semantic, invocation, method, _evidence);
        return null;
    }

    private static void RecordAttributeScanInvocation(ConventionFacts facts, SemanticModel semantic,
        InvocationExpressionSyntax invocation, IMethodSymbol original, ISymbol? scope,
        Composition evidence, HashSet<ISymbol> scanScopes)
    {
        if (scope is not null) scanScopes.Add(scope);
        var (receiverParameter, scannedAssembly) = ReadAttributeScanArguments(facts, semantic, invocation, original);
        if (scannedAssembly is not null) evidence.Scanned.Add(scannedAssembly);
        var localScan = SymbolEqualityComparer.Default.Equals(scannedAssembly, facts.Compilation.Assembly);
        if (localScan && !evidence.ModuleScan && IsProjectModuleScan(scope, receiverParameter, facts.Symbols.ServiceCollection))
            evidence.ModuleScan = true;
    }

    private static (IParameterSymbol? ReceiverParameter, IAssemblySymbol? ScannedAssembly) ReadAttributeScanArguments(
        ConventionFacts facts, SemanticModel semantic, InvocationExpressionSyntax invocation, IMethodSymbol original)
    {
        IParameterSymbol? receiverParameter = null;
        IAssemblySymbol? scannedAssembly = original.Parameters.Length == 1 ? facts.Compilation.Assembly : null;
        if (semantic.GetOperation(invocation, facts.Cancellation) is not IInvocationOperation scan)
            return (receiverParameter, scannedAssembly);
        foreach (var argument in scan.Arguments)
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            var value = UnwrapImplicitConversions(facts, argument.Value);
            if (argument.Parameter?.Name == "sc" && value is IParameterReferenceOperation scanReceiver)
                receiverParameter = scanReceiver.Parameter;
            else if (argument.Parameter?.Name == "assembly" && value is IPropertyReferenceOperation
                { Property.Name: "Assembly", Instance: ITypeOfOperation { TypeOperand: INamedTypeSymbol marker } })
                scannedAssembly = marker.ContainingAssembly;
        }
        return (receiverParameter, scannedAssembly);
    }

    private static IOperation? UnwrapImplicitConversions(ConventionFacts facts, IOperation? value)
    {
        while (value is IConversionOperation { IsImplicit: true, OperatorMethod: null } conversion)
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            value = conversion.Operand;
        }
        return value;
    }

    private static bool IsProjectModuleScan(ISymbol? scope, IParameterSymbol? receiverParameter, INamedTypeSymbol? collection)
    {
        if (scope is not IMethodSymbol
            {
                MethodKind: MethodKind.Ordinary, IsStatic: true, IsGenericMethod: false,
                DeclaredAccessibility: Accessibility.Public,
                ContainingType:
                {
                    IsStatic: true, IsGenericType: false, DeclaredAccessibility: Accessibility.Public, ContainingType: null
                } module
            } moduleMethod) return false;
        return module.Name.EndsWith("Module", StringComparison.Ordinal) &&
               SymbolEqualityComparer.Default.Equals(moduleMethod.ReturnType, collection) &&
               receiverParameter is not null &&
               SymbolEqualityComparer.Default.Equals(receiverParameter.ContainingSymbol, moduleMethod) &&
               SymbolEqualityComparer.Default.Equals(receiverParameter.Type, collection);
    }

    private static TypeInfo? RecordNonManualInvocation(ConventionFacts facts, SemanticModel semantic,
        InvocationExpressionSyntax invocation, IMethodSymbol method, ISymbol? scope,
        Composition evidence, HashSet<ISymbol> unknownScopes)
    {
        var collection = facts.Symbols.ServiceCollection;
        var receivesCollection = invocation.ArgumentList.Arguments.Any(argument =>
            semantic.GetTypeInfo(argument.Expression, facts.Cancellation).Type is INamedTypeSymbol argumentType &&
            ConventionSymbols.Is(argumentType, collection, facts.Cancellation));
        TypeInfo? boundReceiver = invocation.Expression is MemberAccessExpressionSyntax access
            ? semantic.GetTypeInfo(access.Expression, facts.Cancellation) : null;
        var collectionReceiver = boundReceiver?.Type is INamedTypeSymbol receiver &&
            ConventionSymbols.Is(receiver, collection, facts.Cancellation);
        var returnsCollection = method.ReturnType is INamedTypeSymbol returnType &&
            ConventionSymbols.Is(returnType, collection, facts.Cancellation);
        if (scope is not null && (receivesCollection || collectionReceiver || returnsCollection ||
                SymbolEqualityComparer.Default.Equals(method.ContainingType, scope.ContainingType)))
            unknownScopes.Add(scope);
        evidence.UnknownComposition |= receivesCollection || collectionReceiver;
        return boundReceiver;
    }

    private static void RecordManualRegistration(ConventionFacts facts, SemanticModel semantic,
        InvocationExpressionSyntax invocation, IMethodSymbol method, Composition evidence)
    {
        if (!TryGetManualServicePair(method, out var service, out var implementation))
        {
            evidence.UnknownComposition = true;
            return;
        }
        var key = "";
        var argumentOffset = method.ReducedFrom is null ? 1 : 0;
        var keyed = method.Name.StartsWith("AddKeyed", StringComparison.Ordinal);
        if (invocation.ArgumentList.Arguments.Count != argumentOffset + (keyed ? 1 : 0))
        {
            evidence.UnknownComposition = true;
            return;
        }
        if (keyed)
        {
            var operation = semantic.GetOperation(invocation, facts.Cancellation) as IInvocationOperation;
            var argument = operation?.Arguments.FirstOrDefault(item => item.Parameter?.Name == "serviceKey");
            if (UnwrapImplicitConversions(facts, argument?.Value) is not { ConstantValue: { HasValue: true, Value: string value } })
            {
                evidence.UnknownComposition = true;
                return;
            }
            key = KeyIdentity(value);
        }
        evidence.AddManual(service, key, implementation);
    }

    private static bool TryGetManualServicePair(IMethodSymbol method,
        [NotNullWhen(true)] out INamedTypeSymbol? service,
        [NotNullWhen(true)] out INamedTypeSymbol? implementation)
    {
        if (method.TypeArguments is [INamedTypeSymbol declaredService, INamedTypeSymbol declaredImplementation])
        {
            service = declaredService;
            implementation = declaredImplementation;
            return true;
        }
        if (method.TypeArguments is [INamedTypeSymbol self] && self.TypeKind == TypeKind.Class && !self.IsAbstract)
        {
            service = implementation = self;
            return true;
        }
        service = null;
        implementation = null;
        return false;
    }

    internal sealed class Composition
    {
        internal bool ModuleScan { get; set; }
        internal HashSet<IAssemblySymbol> Scanned { get; } = new(SymbolEqualityComparer.Default);
        private readonly Dictionary<INamedTypeSymbol, ManualBindings> _manual = new(SymbolEqualityComparer.Default);
        internal bool UnknownComposition { get; set; }

        internal void AddManual(INamedTypeSymbol service, string key, INamedTypeSymbol implementation)
        {
            if (!_manual.TryGetValue(service, out var bindings))
            {
                bindings = new();
                _manual.Add(service, bindings);
            }
            if (!bindings.ByKey.TryGetValue(key, out var implementations))
            {
                implementations = new(SymbolEqualityComparer.Default);
                bindings.ByKey.Add(key, implementations);
            }
            implementations.Add(implementation);
            bindings.All.Add(implementation);
        }

        internal bool HasManual(INamedTypeSymbol service, string? key, INamedTypeSymbol implementation) =>
            _manual.TryGetValue(service, out var bindings) &&
            (key is null ? bindings.All.Contains(implementation) :
                bindings.ByKey.TryGetValue(key, out var implementations) && implementations.Contains(implementation));

        internal bool HasConflictingManual(INamedTypeSymbol service, string key, INamedTypeSymbol implementation) =>
            _manual.TryGetValue(service, out var bindings) && bindings.ByKey.TryGetValue(key, out var implementations) &&
            (implementations.Count > 1 || !implementations.Contains(implementation));

        private sealed class ManualBindings
        {
            internal Dictionary<string, HashSet<INamedTypeSymbol>> ByKey { get; } = new(StringComparer.Ordinal);
            internal HashSet<INamedTypeSymbol> All { get; } = new(SymbolEqualityComparer.Default);
        }
    }
}
