using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace DRN.Framework.SharedKernel.Analyzers.Conventions;

internal static class ConventionRegistrationAnalysis
{
    internal static void AnalyzeAll(ConventionFacts facts, bool requireModule = false)
    {
        var implementations = ReadImplementations(facts);
        var lifetimeFailures = AnalyzeLifetimeAttributes(facts, implementations);
        Analyze(facts, implementations, lifetimeFailures, requireModule);
    }

    private static INamedTypeSymbol[] ReadImplementations(ConventionFacts facts)
    {
        facts.Cancellation.ThrowIfCancellationRequested();
        return facts.Types.Where(type =>
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            return IsServiceImplementation(facts, type);
        }).ToArray();
    }

    internal static void AnalyzeLifetimeAttributes(ConventionFacts facts) =>
        AnalyzeLifetimeAttributes(facts, ReadImplementations(facts));

    private static Dictionary<INamedTypeSymbol, HashSet<INamedTypeSymbol>>? AnalyzeLifetimeAttributes(ConventionFacts facts, INamedTypeSymbol[] implementations)
    {
        Dictionary<INamedTypeSymbol, HashSet<INamedTypeSymbol>>? failures = null;
        var matched = new List<INamedTypeSymbol>();
        foreach (var implementation in implementations)
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            matched.Clear();
            foreach (var service in implementation.AllInterfaces)
            {
                facts.Cancellation.ThrowIfCancellationRequested();
                if (service.Name.Length != implementation.Name.Length + 1 || service.Name[0] != 'I' ||
                    !service.Name.AsSpan(1).SequenceEqual(implementation.Name.AsSpan()) ||
                    facts.Role(service).Role.Length == 0) continue;
                matched.Add(service);
            }
            if (matched.Count == 0) continue;
            var registeredService = GetLifetime(facts, implementation).Service;
            if (matched.Any(service => SymbolEqualityComparer.Default.Equals(registeredService, service))) continue;
            var location = facts.Location(implementation);
            if (location != Location.None)
            {
                facts.Diagnostics.Add(Diagnostic.Create(ConventionDiagnosticDescriptors.LifetimeRequired, location,
                    ImmutableDictionary<string, string?>.Empty.Add("Target", implementation.GetDocumentationCommentId()),
                    implementation.Name, matched[0].ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
                (failures ??= new(SymbolEqualityComparer.Default)).Add(implementation, new(matched, SymbolEqualityComparer.Default));
            }
        }
        return failures;
    }

    internal readonly record struct LifetimeAnalysis(AttributeData? Attribute, INamedTypeSymbol? Service, bool Ambiguous);

    internal static LifetimeAnalysis GetLifetime(ConventionFacts facts, INamedTypeSymbol implementation)
    {
        facts.Cancellation.ThrowIfCancellationRequested();
        if (facts.Lifetimes.TryGetValue(implementation, out var cached)) return cached;
        var result = ReadLifetime(facts, implementation);
        facts.Cancellation.ThrowIfCancellationRequested();
        facts.Lifetimes.Add(implementation, result);
        return result;
    }

    private static LifetimeAnalysis ReadLifetime(ConventionFacts facts, INamedTypeSymbol implementation)
    {
        AttributeData? selected = null;
        foreach (var attribute in implementation.GetAttributes())
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            if (attribute.AttributeClass is not { } type ||
                !facts.Symbols.IsKnown(type, facts.Symbols.LifetimeAttribute, facts.Cancellation)) continue;
            if (selected is not null) return new(null, null, true);
            selected = attribute;
        }
        // Runtime scanning prefers direct lifetime attributes over inherited ones.
        if (selected is null)
            foreach (var attribute in facts.Symbols.EffectiveAttributes(implementation, facts.Symbols.LifetimeAttribute, facts.Cancellation))
            {
                facts.Cancellation.ThrowIfCancellationRequested();
                if (selected is not null) return new(null, null, true);
                selected = attribute;
            }
        for (var type = selected?.AttributeClass; type is not null; type = type.BaseType)
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            if ((ConventionSymbols.Same(type, facts.Symbols.GenericLifetimeAttribute) ||
                 ConventionSymbols.Same(type, facts.Symbols.KeyedLifetimeAttribute)) &&
                type.TypeArguments is [INamedTypeSymbol service]) return new(selected, service, false);
        }
        return new(selected, selected is null ? null : ReadCustomService(facts, selected), false);
    }

    private static INamedTypeSymbol? ReadCustomService(ConventionFacts facts, AttributeData attribute)
    {
        var constructor = attribute.AttributeConstructor;
        if (constructor is null) return null;
        var values = new Dictionary<IParameterSymbol, INamedTypeSymbol?>(SymbolEqualityComparer.Default);
        for (var i = 0; i < constructor.Parameters.Length && i < attribute.ConstructorArguments.Length; i++)
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            values[constructor.Parameters[i]] = attribute.ConstructorArguments[i].Value as INamedTypeSymbol;
        }

        // Bound malformed constructor cycles and keep arbitrary expressions outside the proof.
        for (var depth = 0; depth < 32; depth++)
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            if (ConventionSymbols.Same(constructor.ContainingType, facts.Symbols.LifetimeAttribute))
            {
                foreach (var parameter in constructor.Parameters)
                    if (parameter.Name == "serviceType") return values.GetValueOrDefault(parameter);
                return null;
            }
            if (constructor.DeclaringSyntaxReferences is not [var reference] ||
                !facts.Compilation.ContainsSyntaxTree(reference.SyntaxTree)) return null;
            var syntax = reference.GetSyntax(facts.Cancellation);
            SyntaxNode? initializer = syntax is ConstructorDeclarationSyntax declaration ? declaration.Initializer :
                syntax is TypeDeclarationSyntax type ? type.BaseList?.Types.OfType<PrimaryConstructorBaseTypeSyntax>().FirstOrDefault() : null;
            if (initializer is null) return null;
            var semantic = facts.Compilation.GetSemanticModel(reference.SyntaxTree);
            if (semantic.GetSymbolInfo(initializer, facts.Cancellation).Symbol is not IMethodSymbol next) return null;
            var arguments = initializer is ConstructorInitializerSyntax ordinary ? ordinary.ArgumentList.Arguments :
                ((PrimaryConstructorBaseTypeSyntax)initializer).ArgumentList.Arguments;
            var forwarded = new Dictionary<IParameterSymbol, INamedTypeSymbol?>(SymbolEqualityComparer.Default);
            foreach (var argument in arguments)
            {
                facts.Cancellation.ThrowIfCancellationRequested();
                if (semantic.GetOperation(argument, facts.Cancellation) is not IArgumentOperation { Parameter: { } parameter } operation)
                    return null;
                var value = operation.Value;
                while (value is IConversionOperation { OperatorMethod: null } conversion)
                {
                    facts.Cancellation.ThrowIfCancellationRequested();
                    value = conversion.Operand;
                }
                forwarded[parameter] = value switch
                {
                    ITypeOfOperation { TypeOperand: INamedTypeSymbol service } => service,
                    IParameterReferenceOperation forwardedParameter => values.GetValueOrDefault(forwardedParameter.Parameter),
                    _ => null
                };
            }
            constructor = next;
            values = forwarded;
        }
        return null;
    }

    internal static void Analyze(ConventionFacts facts) => Analyze(facts, ReadImplementations(facts));

    private static void Analyze(ConventionFacts facts, INamedTypeSymbol[] implementations,
        Dictionary<INamedTypeSymbol, HashSet<INamedTypeSymbol>>? lifetimeFailures = null, bool requireModule = false)
    {
        facts.Cancellation.ThrowIfCancellationRequested();
        var demands = facts.Records.Where(input => input.Kind == "Registration").ToArray();
        var needsModule = requireModule && facts.Project.Role is ("Hosted" or "Application" or "Infra" or "Utils");
        if (implementations.Length == 0 && demands.Length == 0 && !needsModule) return;
        var evidence = ReadComposition(facts);
        if (needsModule && !evidence.ModuleScan)
            facts.Diagnostics.Add(Diagnostic.Create(ConventionDiagnosticDescriptors.ModuleScan, Location.None,
                ImmutableDictionary<string, string?>.Empty.Add("Target", facts.Project.AssemblyName + ":Module"), facts.Project.Name));
        if (implementations.Length == 0 && demands.Length == 0) return;
        var serviceImplementations = new Dictionary<INamedTypeSymbol, INamedTypeSymbol?>(SymbolEqualityComparer.Default);
        var observedByImplementation = new Dictionary<INamedTypeSymbol, ObservedServices>(SymbolEqualityComparer.Default);
        foreach (var demand in ReadConstructorServices(facts))
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            var implementation = UniqueImplementation(demand.Service);
            if (implementation is null || evidence.HasConflictingManual(demand.Service, demand.Key, implementation)) continue;
            if (!observedByImplementation.TryGetValue(implementation, out var observed))
                observedByImplementation.Add(implementation, observed = new());
            observed.Demands.Add(demand);
            observed.Services.Add(demand.Service);
        }
        var unknownModules = new Dictionary<IAssemblySymbol, bool>(SymbolEqualityComparer.Default);
        var seen = new HashSet<(string Implementation, string Service, string Key)>();
        var explicitPairs = new HashSet<(string Implementation, string Service)>();
        foreach (var input in demands)
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            var key = input.Get("Key") is { Length: > 0 } selectedKey ? KeyIdentity(selectedKey) : "";
            if (!seen.Add((input.Identity, input.Get("Service"), key))) continue;
            var implementation = facts.Compilation.GetTypeByMetadataName(input.Identity);
            var service = facts.Compilation.GetTypeByMetadataName(input.Get("Service"));
            explicitPairs.Add((input.Identity, input.Get("Service")));
            if (implementation is null || service is null || implementation.IsAbstract ||
                facts.Symbols.IsOpen(implementation, facts.Cancellation) ||
                !facts.Compilation.ClassifyCommonConversion(implementation, service).IsImplicit)
            {
                facts.Diagnostics.Add(ConventionProject.InputIssue(input.Identity, "an invalid registration relationship",
                    "concrete implementation assignable to the declared service", input.Location));
                continue;
            }
            if (facts.Symbols.IsTransport(implementation, facts.Cancellation)) continue;
            var role = facts.Role(implementation).Role;
            if (role is not ("Infra" or "Application")) continue;
            // Reasoned selections describe an external/custom path, whose runtime behavior is not certified here.
            if (!string.IsNullOrWhiteSpace(input.Get("Reason"))) continue;
            AnalyzeDemand(implementation, service, key, input.Location);
        }

        foreach (var implementation in implementations)
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            observedByImplementation.TryGetValue(implementation, out var observed);
            if (observed is not null)
            {
                foreach (var demand in observed.Demands)
                {
                    var identity = ConventionSymbols.MetadataName(implementation);
                    var serviceName = demand.Service.IsGenericType ? demand.Service.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) :
                        ConventionSymbols.MetadataName(demand.Service);
                    if (!seen.Add((identity, serviceName, demand.Key))) continue;
                    var location = facts.Location(implementation);
                    if (location != Location.None) AnalyzeDemand(implementation, demand.Service, demand.Key, location);
                }
            }
            foreach (var service in ServiceContracts(facts, implementation))
            {
                if (explicitPairs.Contains((ConventionSymbols.MetadataName(implementation), ConventionSymbols.MetadataName(service))) ||
                    observed?.Services.Contains(service) is true) continue;
                var location = facts.Location(implementation);
                if (location != Location.None) AnalyzeDemand(implementation, service, null, location);
            }
        }

        INamedTypeSymbol? UniqueImplementation(INamedTypeSymbol service)
        {
            if (serviceImplementations.TryGetValue(service, out var cached)) return cached;
            INamedTypeSymbol? match = null;
            foreach (var implementation in implementations)
            {
                facts.Cancellation.ThrowIfCancellationRequested();
                if (!facts.Compilation.ClassifyCommonConversion(implementation, service).IsImplicit) continue;
                if (match is not null)
                {
                    match = null;
                    break;
                }
                match = implementation;
            }
            serviceImplementations.Add(service, match);
            return match;
        }

        void AnalyzeDemand(INamedTypeSymbol implementation, INamedTypeSymbol service, string? key, Location location)
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            if (lifetimeFailures is not null && lifetimeFailures.TryGetValue(implementation, out var failedServices) &&
                failedServices.Contains(service)) return;
            if (evidence.HasManual(service, key, implementation)) return;
            if (!evidence.Scanned.Contains(implementation.ContainingAssembly) || evidence.UnknownComposition) return;
            if (!unknownModules.TryGetValue(implementation.ContainingAssembly, out var unknownModule))
                unknownModules.Add(implementation.ContainingAssembly, unknownModule = HasUnknownModule(facts, implementation.ContainingAssembly));
            if (unknownModule) return;
            var registration = AttributeRegistration(facts, implementation, service, key);
            if (registration is not false) return;
            facts.Diagnostics.Add(Diagnostic.Create(ConventionDiagnosticDescriptors.Registration, location,
                ImmutableDictionary<string, string?>.Empty.Add("Target", implementation.GetDocumentationCommentId()),
                implementation.Name, service.ToDisplayString()));
        }
    }

    internal static bool IsServiceImplementation(ConventionFacts facts, INamedTypeSymbol type)
    {
        if (type.TypeKind != TypeKind.Class || type.IsAbstract || type.IsStatic ||
            facts.Symbols.IsOpen(type, facts.Cancellation) || facts.Role(type).Role is not ("Application" or "Infra") ||
            facts.IsEntity(type) || facts.IsNeutralValue(type) || facts.Symbols.IsTransport(type, facts.Cancellation) ||
            facts.Symbols.IsContext(type, facts.Cancellation)) return false;
        return !facts.Symbols.IsKnown(type, facts.Symbols.Dto, facts.Cancellation) &&
               !facts.Symbols.IsKnown(type, facts.Symbols.Configuration, facts.Cancellation) &&
               !facts.Symbols.EffectiveAttributes(type, facts.Symbols.ConfigAttribute, facts.Cancellation).Any();
    }

    private static IEnumerable<INamedTypeSymbol> ServiceContracts(ConventionFacts facts, INamedTypeSymbol implementation)
    {
        // A paired, behavioral interface is the repository's ordinary service convention.
        // Framework interfaces such as IDisposable and data-only interfaces do not establish service intent.
        foreach (var service in implementation.AllInterfaces)
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            if (service.Name != "I" + implementation.Name ||
                facts.Role(service).Role.Length == 0) continue;
            var behavioral = service.GetMembers().OfType<IMethodSymbol>().Any(method => !method.IsStatic && method.MethodKind == MethodKind.Ordinary) ||
                             service.AllInterfaces.Any(parent => parent.GetMembers().OfType<IMethodSymbol>()
                                 .Any(method => !method.IsStatic && method.MethodKind == MethodKind.Ordinary));
            if (behavioral || facts.Symbols.IsKnown(service, facts.Symbols.RepositoryContract, facts.Cancellation) ||
                facts.Symbols.IsKnown(service, facts.Symbols.ServiceContract, facts.Cancellation)) yield return service;
        }
    }

    private static List<(INamedTypeSymbol Service, string Key)> ReadConstructorServices(ConventionFacts facts)
    {
        var result = new List<(INamedTypeSymbol Service, string Key)>();
        var keyedAttribute = facts.Compilation.GetTypeByMetadataName("Microsoft.Extensions.DependencyInjection.FromKeyedServicesAttribute");
        foreach (var consumer in facts.Types)
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            var serviceConsumer = IsServiceImplementation(facts, consumer) &&
                                  (ServiceContracts(facts, consumer).Any() || facts.Symbols.EffectiveAttributes(consumer,
                                      facts.Symbols.LifetimeAttribute, facts.Cancellation).Any());
            if (!serviceConsumer && !facts.Symbols.IsTransport(consumer, facts.Cancellation)) continue;
            var constructors = consumer.InstanceConstructors.Where(constructor => constructor.DeclaredAccessibility == Accessibility.Public).ToArray();
            if (constructors is not [var constructor]) continue;
            foreach (var parameter in constructor.Parameters)
            {
                if (parameter.IsOptional || parameter.HasExplicitDefaultValue ||
                    parameter.Type is not INamedTypeSymbol service || service.SpecialType != SpecialType.None ||
                    facts.Symbols.IsOpen(service, facts.Cancellation)) continue;
                var keyed = parameter.GetAttributes().FirstOrDefault(attribute => ConventionSymbols.Same(attribute.AttributeClass, keyedAttribute));
                if (keyed is null) result.Add((service, ""));
                else if (keyed.ConstructorArguments is [{ Value: string key }]) result.Add((service, KeyIdentity(key)));
                // Inherited/ambient and non-string keys are unsupported, not evidence for an unkeyed demand.
            }
        }
        return result;
    }

    private static string KeyIdentity(string key) => "key:" + key;

    private static bool? AttributeRegistration(ConventionFacts facts, INamedTypeSymbol implementation,
        INamedTypeSymbol service, string? key)
    {
        for (var containing = implementation; containing is not null; containing = containing.ContainingType)
            if (containing.DeclaredAccessibility != Accessibility.Public) return false;
        var lifetime = GetLifetime(facts, implementation);
        if (lifetime.Ambiguous) return false;
        if (lifetime.Attribute is not { } attribute) return false;
        if (attribute.AttributeClass is null) return null;
        if (facts.Symbols.IsKnown(attribute.AttributeClass, facts.Symbols.HostedServiceAttribute, facts.Cancellation))
        {
            return facts.Symbols.IsKnown(implementation, facts.Symbols.HostedService, facts.Cancellation) &&
                   SymbolEqualityComparer.Default.Equals(service, facts.Symbols.HostedService) && (key is null || key.Length == 0);
        }
        if (lifetime.Service is not { } registered) return null;
        if (!SymbolEqualityComparer.Default.Equals(registered, service)) return false;

        // Only framework constructors have known key semantics. Arbitrary derived attributes may transform arguments.
        if (!SymbolEqualityComparer.Default.Equals(attribute.AttributeClass.ContainingAssembly, facts.Symbols.LifetimeAttribute?.ContainingAssembly))
            return null;
        if (key is null) return true;
        var keyParameter = attribute.AttributeConstructor?.Parameters.FirstOrDefault(parameter => parameter.Name == "key");
        if (keyParameter is null) return key.Length == 0;
        if (attribute.ConstructorArguments.Length <= keyParameter.Ordinal) return null;
        var registeredKey = attribute.ConstructorArguments[keyParameter.Ordinal].Value;
        return registeredKey is null ? key.Length == 0 : registeredKey is string text ? KeyIdentity(text) == key : null;
    }

    private static bool HasUnknownModule(ConventionFacts facts, IAssemblySymbol assembly)
    {
        // A custom module can register arbitrary services. Its presence prevents an absence proof.
        if (!SymbolEqualityComparer.Default.Equals(assembly, facts.Compilation.Assembly)) return true;
        return facts.Types.Any(type => facts.Symbols.EffectiveAttributes(type, facts.Symbols.RegistrationAttribute,
            facts.Cancellation).Any(attribute =>
                !ConventionSymbols.Same(attribute.AttributeClass, facts.Symbols.ContextRegistrationAttribute)));
    }

    private static Composition ReadComposition(ConventionFacts facts)
    {
        var evidence = new Composition();
        var drnExtensions = facts.Compilation.GetTypeByMetadataName("DRN.Framework.Utils.DependencyInjection.ServiceCollectionExtensions");
        var diExtensions = facts.Compilation.GetTypeByMetadataName("Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions");
        var collection = facts.Compilation.GetTypeByMetadataName("Microsoft.Extensions.DependencyInjection.IServiceCollection");
        var scanScopes = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        var unknownScopes = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        foreach (var tree in facts.Compilation.SyntaxTrees)
        {
            SemanticModel? semantic = null;
            foreach (var node in tree.GetRoot(facts.Cancellation).DescendantNodes())
            {
                facts.Cancellation.ThrowIfCancellationRequested();
                if (node is BaseObjectCreationExpressionSyntax { ArgumentList.Arguments.Count: > 0 } creation)
                {
                    semantic ??= facts.Compilation.GetSemanticModel(tree);
                    if (creation.ArgumentList.Arguments.Any(argument =>
                            semantic.GetTypeInfo(argument.Expression, facts.Cancellation).Type is INamedTypeSymbol argumentType &&
                            ConventionSymbols.Is(argumentType, collection, facts.Cancellation)))
                        evidence.UnknownComposition = true;
                    continue;
                }
                if (node is not InvocationExpressionSyntax invocation) continue;
                semantic ??= facts.Compilation.GetSemanticModel(tree);
                if (semantic.GetSymbolInfo(invocation, facts.Cancellation).Symbol is not IMethodSymbol method)
                {
                    evidence.UnknownComposition = true;
                    continue;
                }
                var original = method.ReducedFrom ?? method;
                var scope = semantic.GetEnclosingSymbol(invocation.SpanStart, facts.Cancellation);
                if (ConventionSymbols.Same(original.ContainingType, drnExtensions) && method.Name == "AddServicesWithAttributes")
                {
                    if (scope is not null) scanScopes.Add(scope);
                    IParameterSymbol? receiverParameter = null;
                    IAssemblySymbol? scannedAssembly = original.Parameters.Length == 1 ? facts.Compilation.Assembly : null;
                    if (semantic.GetOperation(invocation, facts.Cancellation) is IInvocationOperation scan)
                    {
                        foreach (var argument in scan.Arguments)
                        {
                            facts.Cancellation.ThrowIfCancellationRequested();
                            var value = argument.Value;
                            while (value is IConversionOperation { IsImplicit: true, OperatorMethod: null } conversion)
                            {
                                facts.Cancellation.ThrowIfCancellationRequested();
                                value = conversion.Operand;
                            }
                            if (argument.Parameter?.Name == "sc" && value is IParameterReferenceOperation scanReceiver)
                                receiverParameter = scanReceiver.Parameter;
                            else if (argument.Parameter?.Name == "assembly" && value is IPropertyReferenceOperation
                                { Property.Name: "Assembly", Instance: ITypeOfOperation { TypeOperand: INamedTypeSymbol marker } })
                                scannedAssembly = marker.ContainingAssembly;
                        }
                    }
                    if (scannedAssembly is not null) evidence.Scanned.Add(scannedAssembly);
                    var localScan = SymbolEqualityComparer.Default.Equals(scannedAssembly, facts.Compilation.Assembly);
                    if (localScan && !evidence.ModuleScan && scope is IMethodSymbol
                        { MethodKind: MethodKind.Ordinary, IsStatic: true, IsGenericMethod: false,
                            DeclaredAccessibility: Accessibility.Public, ContainingType: { IsStatic: true, IsGenericType: false,
                                DeclaredAccessibility: Accessibility.Public, ContainingType: null } module } moduleMethod &&
                        module.Name.EndsWith("Module", StringComparison.Ordinal) &&
                        SymbolEqualityComparer.Default.Equals(moduleMethod.ReturnType, collection))
                    {
                        if (receiverParameter is not null &&
                            SymbolEqualityComparer.Default.Equals(receiverParameter.ContainingSymbol, moduleMethod) &&
                            SymbolEqualityComparer.Default.Equals(receiverParameter.Type, collection)) evidence.ModuleScan = true;
                    }
                    continue;
                }
                if (!ConventionSymbols.Same(original.ContainingType, diExtensions) ||
                    method.Name is not ("AddScoped" or "AddTransient" or "AddSingleton" or "AddKeyedScoped" or "AddKeyedTransient" or "AddKeyedSingleton"))
                {
                    var receivesCollection = invocation.ArgumentList.Arguments.Any(argument =>
                        semantic.GetTypeInfo(argument.Expression, facts.Cancellation).Type is INamedTypeSymbol argumentType &&
                        ConventionSymbols.Is(argumentType, collection, facts.Cancellation));
                    var collectionReceiver = invocation.Expression is MemberAccessExpressionSyntax access &&
                        semantic.GetTypeInfo(access.Expression, facts.Cancellation).Type is INamedTypeSymbol receiver &&
                        ConventionSymbols.Is(receiver, collection, facts.Cancellation);
                    var returnsCollection = method.ReturnType is INamedTypeSymbol returnType &&
                        ConventionSymbols.Is(returnType, collection, facts.Cancellation);
                    if (scope is not null && (receivesCollection || collectionReceiver || returnsCollection ||
                        SymbolEqualityComparer.Default.Equals(method.ContainingType, scope.ContainingType))) unknownScopes.Add(scope);
                    evidence.UnknownComposition |= receivesCollection || collectionReceiver;
                    continue;
                }
                INamedTypeSymbol service;
                INamedTypeSymbol implementation;
                if (method.TypeArguments is [INamedTypeSymbol declaredService, INamedTypeSymbol declaredImplementation])
                {
                    service = declaredService;
                    implementation = declaredImplementation;
                }
                else if (method.TypeArguments is [INamedTypeSymbol self] && self.TypeKind == TypeKind.Class && !self.IsAbstract)
                {
                    service = implementation = self;
                }
                else
                {
                    evidence.UnknownComposition = true;
                    continue;
                }
                var key = "";
                var argumentOffset = method.ReducedFrom is null ? 1 : 0;
                var keyed = method.Name.StartsWith("AddKeyed", StringComparison.Ordinal);
                if (invocation.ArgumentList.Arguments.Count != argumentOffset + (keyed ? 1 : 0))
                {
                    evidence.UnknownComposition = true;
                    continue;
                }
                if (keyed)
                {
                    var constant = semantic.GetConstantValue(invocation.ArgumentList.Arguments[argumentOffset].Expression, facts.Cancellation);
                    if (!constant.HasValue || constant.Value is not string value)
                    {
                        evidence.UnknownComposition = true;
                        continue;
                    }
                    key = KeyIdentity(value);
                }
                evidence.AddManual(service, key, implementation);
            }
        }
        evidence.UnknownComposition |= scanScopes.Overlaps(unknownScopes);
        return evidence;
    }

    private sealed class ObservedServices
    {
        internal List<(INamedTypeSymbol Service, string Key)> Demands { get; } = [];
        internal HashSet<INamedTypeSymbol> Services { get; } = new(SymbolEqualityComparer.Default);
    }

    private sealed class Composition
    {
        internal bool ModuleScan { get; set; }
        internal HashSet<IAssemblySymbol> Scanned { get; } = new(SymbolEqualityComparer.Default);
        private readonly Dictionary<INamedTypeSymbol, ManualBindings> _manual = new(SymbolEqualityComparer.Default);
        internal bool UnknownComposition { get; set; }

        internal void AddManual(INamedTypeSymbol service, string key, INamedTypeSymbol implementation)
        {
            if (!_manual.TryGetValue(service, out var bindings)) _manual.Add(service, bindings = new());
            if (!bindings.ByKey.TryGetValue(key, out var implementations))
                bindings.ByKey.Add(key, implementations = new(SymbolEqualityComparer.Default));
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
