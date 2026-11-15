using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DRN.Framework.SharedKernel.Analyzers.Conventions;

internal static class ConventionRegistrationAnalysis
{
    internal static void Analyze(ConventionFacts facts)
    {
        var demands = facts.Records.Where(input => input.Kind == "Registration").ToArray();
        var evidence = ReadComposition(facts);
        var implementations = facts.Types.Where(type => IsServiceImplementation(facts, type)).ToArray();
        var serviceImplementations = new Dictionary<INamedTypeSymbol, INamedTypeSymbol?>(SymbolEqualityComparer.Default);
        var observedServices = ReadConstructorServices(facts).Where(demand => UniqueImplementation(demand.Service) is not null).ToArray();
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
                ConventionSymbols.IsOpen(implementation, facts.Cancellation) ||
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
            var observed = observedServices.Where(demand => SymbolEqualityComparer.Default.Equals(implementation, serviceImplementations[demand.Service]) &&
                !evidence.Manual.Any(item => SymbolEqualityComparer.Default.Equals(item.Service, demand.Service) && item.Key == demand.Key &&
                    !SymbolEqualityComparer.Default.Equals(item.Implementation, implementation))).ToArray();
            foreach (var demand in observed)
            {
                var identity = ConventionSymbols.MetadataName(implementation);
                var serviceName = demand.Service.IsGenericType ? demand.Service.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) :
                    ConventionSymbols.MetadataName(demand.Service);
                if (!seen.Add((identity, serviceName, demand.Key))) continue;
                var location = facts.Location(implementation);
                if (location != Location.None) AnalyzeDemand(implementation, demand.Service, demand.Key, location);
            }
            foreach (var service in ServiceContracts(facts, implementation))
            {
                if (explicitPairs.Contains((ConventionSymbols.MetadataName(implementation), ConventionSymbols.MetadataName(service))) ||
                    observed.Any(demand => SymbolEqualityComparer.Default.Equals(demand.Service, service))) continue;
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
            if (evidence.Manual.Any(item => SymbolEqualityComparer.Default.Equals(item.Service, service) &&
                    SymbolEqualityComparer.Default.Equals(item.Implementation, implementation) && (key is null || item.Key == key))) return;
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

    private static bool IsServiceImplementation(ConventionFacts facts, INamedTypeSymbol type)
    {
        if (type.TypeKind != TypeKind.Class || type.IsAbstract || type.IsStatic ||
            ConventionSymbols.IsOpen(type, facts.Cancellation) || facts.Role(type).Role is not ("Application" or "Infra") ||
            facts.IsEntity(type) || facts.IsNeutralValue(type) || facts.Symbols.IsTransport(type, facts.Cancellation) ||
            facts.Symbols.IsContext(type, facts.Cancellation)) return false;
        return !ConventionSymbols.Is(type, facts.Symbols.Dto, facts.Cancellation) &&
               !ConventionSymbols.Is(type, facts.Symbols.Configuration, facts.Cancellation) &&
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
                facts.Role(service).Role is not ("Application" or "Domain" or "Infra")) continue;
            var behavioral = service.GetMembers().OfType<IMethodSymbol>().Any(method => !method.IsStatic && method.MethodKind == MethodKind.Ordinary) ||
                             service.AllInterfaces.Any(parent => parent.GetMembers().OfType<IMethodSymbol>()
                                 .Any(method => !method.IsStatic && method.MethodKind == MethodKind.Ordinary));
            if (behavioral || ConventionSymbols.Is(service, facts.Symbols.RepositoryContract, facts.Cancellation) ||
                ConventionSymbols.Is(service, facts.Symbols.ServiceContract, facts.Cancellation)) yield return service;
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
                    ConventionSymbols.IsOpen(service, facts.Cancellation)) continue;
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
        var direct = implementation.GetAttributes().Where(attribute =>
            ConventionSymbols.Is(attribute.AttributeClass, facts.Symbols.LifetimeAttribute, facts.Cancellation)).ToArray();
        var attributes = direct.Length > 0 ? direct : facts.Symbols.EffectiveAttributes(implementation,
            facts.Symbols.LifetimeAttribute, facts.Cancellation).ToArray();
        if (attributes.Length == 0) return false;
        if (attributes.Length != 1) return null;
        var attribute = attributes[0];
        if (attribute.AttributeClass is null) return null;
        var generic = ConventionSymbols.Bindings(attribute.AttributeClass, facts.Symbols.GenericLifetimeAttribute, facts.Cancellation)
            .Concat(ConventionSymbols.Bindings(attribute.AttributeClass, facts.Symbols.KeyedLifetimeAttribute, facts.Cancellation)).FirstOrDefault();
        if (generic?.TypeArguments is not [INamedTypeSymbol registered]) return null;
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
            var semantic = facts.Compilation.GetSemanticModel(tree);
            foreach (var invocation in tree.GetRoot(facts.Cancellation).DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                facts.Cancellation.ThrowIfCancellationRequested();
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
                    var arguments = invocation.ArgumentList.Arguments;
                    var offset = method.ReducedFrom is null ? 1 : 0;
                    if (arguments.Count == offset) evidence.Scanned.Add(facts.Compilation.Assembly);
                    else if (arguments.Count == offset + 1 && arguments[offset].Expression is MemberAccessExpressionSyntax
                             { Expression: TypeOfExpressionSyntax typeOf, Name.Identifier.ValueText: "Assembly" } &&
                             semantic.GetTypeInfo(typeOf.Type, facts.Cancellation).Type is INamedTypeSymbol marker)
                        evidence.Scanned.Add(marker.ContainingAssembly);
                    continue;
                }
                if (!ConventionSymbols.Same(original.ContainingType, diExtensions) ||
                    method.Name is not ("AddScoped" or "AddTransient" or "AddSingleton" or "AddKeyedScoped" or "AddKeyedTransient" or "AddKeyedSingleton"))
                {
                    if (scope is not null) unknownScopes.Add(scope);
                    var receivesCollection = invocation.ArgumentList.Arguments.Any(argument =>
                        semantic.GetTypeInfo(argument.Expression, facts.Cancellation).Type is INamedTypeSymbol argumentType &&
                        ConventionSymbols.Is(argumentType, collection, facts.Cancellation));
                    var collectionReceiver = invocation.Expression is MemberAccessExpressionSyntax access &&
                        semantic.GetTypeInfo(access.Expression, facts.Cancellation).Type is INamedTypeSymbol receiver &&
                        ConventionSymbols.Is(receiver, collection, facts.Cancellation);
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
                evidence.Manual.Add(new(service, implementation, key));
            }
            foreach (var creation in tree.GetRoot(facts.Cancellation).DescendantNodes().OfType<BaseObjectCreationExpressionSyntax>())
            {
                facts.Cancellation.ThrowIfCancellationRequested();
                if (creation.ArgumentList?.Arguments.Any(argument =>
                        semantic.GetTypeInfo(argument.Expression, facts.Cancellation).Type is INamedTypeSymbol argumentType &&
                        ConventionSymbols.Is(argumentType, collection, facts.Cancellation)) is true)
                    evidence.UnknownComposition = true;
            }
        }
        evidence.UnknownComposition |= scanScopes.Overlaps(unknownScopes);
        return evidence;
    }

    private sealed record ManualRegistration(INamedTypeSymbol Service, INamedTypeSymbol Implementation, string Key);

    private sealed class Composition
    {
        internal HashSet<IAssemblySymbol> Scanned { get; } = new(SymbolEqualityComparer.Default);
        internal List<ManualRegistration> Manual { get; } = [];
        internal bool UnknownComposition { get; set; }
    }
}
