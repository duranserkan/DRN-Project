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
        if (TrySelectLifetimeAttribute(facts, implementation, out var selected)) return new(null, null, true);
        // Runtime hosted-service registration uses IHostedService rather than the attribute's object argument.
        if (selected?.AttributeClass is { } selectedType &&
            facts.Symbols.IsKnown(selectedType, facts.Symbols.HostedServiceAttribute, facts.Cancellation))
            return new(selected, facts.Symbols.IsKnown(implementation, facts.Symbols.HostedService, facts.Cancellation)
                ? facts.Symbols.HostedService : null, false);
        return new(selected, ResolveLifetimeService(facts, selected), false);
    }

    private static bool TrySelectLifetimeAttribute(ConventionFacts facts, INamedTypeSymbol implementation, out AttributeData? selected)
    {
        selected = null;
        foreach (var attribute in implementation.GetAttributes())
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            if (attribute.AttributeClass is not { } type ||
                !facts.Symbols.IsKnown(type, facts.Symbols.LifetimeAttribute, facts.Cancellation)) continue;
            if (selected is not null) return true;
            selected = attribute;
        }
        // Runtime scanning prefers direct lifetime attributes over inherited ones.
        if (selected is not null) return false;
        foreach (var attribute in facts.Symbols.EffectiveAttributes(implementation, facts.Symbols.LifetimeAttribute, facts.Cancellation))
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            if (selected is not null) return true;
            selected = attribute;
        }
        return false;
    }

    private static INamedTypeSymbol? ResolveLifetimeService(ConventionFacts facts, AttributeData? selected)
    {
        if (selected is null) return null;
        for (var type = selected.AttributeClass; type is not null; type = type.BaseType)
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            if ((ConventionSymbols.Same(type, facts.Symbols.GenericLifetimeAttribute) ||
                 ConventionSymbols.Same(type, facts.Symbols.KeyedLifetimeAttribute)) &&
                type.TypeArguments is [INamedTypeSymbol service]) return service;
        }
        return ReadCustomService(facts, selected);
    }

    private static INamedTypeSymbol? ReadCustomService(ConventionFacts facts, AttributeData attribute)
    {
        var constructor = attribute.AttributeConstructor;
        if (constructor is null) return null;
        var values = ReadInitialConstructorValues(facts, constructor, attribute);

        // Bound malformed constructor cycles and keep arbitrary expressions outside the proof.
        for (var depth = 0; depth < 32; depth++)
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            if (ConventionSymbols.Same(constructor.ContainingType, facts.Symbols.LifetimeAttribute))
                return FindServiceTypeArgument(constructor, values);
            if (!TryReadForwardedConstructor(facts, constructor, values, out var next, out var forwarded))
                return null;
            constructor = next;
            values = forwarded;
        }
        return null;
    }

    private static Dictionary<IParameterSymbol, INamedTypeSymbol?> ReadInitialConstructorValues(
        ConventionFacts facts, IMethodSymbol constructor, AttributeData attribute)
    {
        var values = new Dictionary<IParameterSymbol, INamedTypeSymbol?>(SymbolEqualityComparer.Default);
        for (var i = 0; i < constructor.Parameters.Length && i < attribute.ConstructorArguments.Length; i++)
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            values[constructor.Parameters[i]] = attribute.ConstructorArguments[i].Value as INamedTypeSymbol;
        }
        return values;
    }

    private static INamedTypeSymbol? FindServiceTypeArgument(IMethodSymbol constructor,
        Dictionary<IParameterSymbol, INamedTypeSymbol?> values)
    {
        foreach (var parameter in constructor.Parameters)
            if (parameter.Name == "serviceType") return values.GetValueOrDefault(parameter);
        return null;
    }

    private static bool TryReadForwardedConstructor(ConventionFacts facts, IMethodSymbol constructor,
        Dictionary<IParameterSymbol, INamedTypeSymbol?> values,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out IMethodSymbol? next,
        out Dictionary<IParameterSymbol, INamedTypeSymbol?> forwarded)
    {
        next = null;
        forwarded = new(SymbolEqualityComparer.Default);
        if (constructor.DeclaringSyntaxReferences is not [var reference] ||
            !facts.Compilation.ContainsSyntaxTree(reference.SyntaxTree)) return false;
        var initializer = GetConstructorInitializer(reference.GetSyntax(facts.Cancellation));
        if (initializer is null) return false;
        var semantic = facts.Compilation.GetSemanticModel(reference.SyntaxTree);
        if (semantic.GetSymbolInfo(initializer, facts.Cancellation).Symbol is not IMethodSymbol resolved) return false;
        var arguments = initializer is ConstructorInitializerSyntax ordinary
            ? ordinary.ArgumentList.Arguments
            : ((PrimaryConstructorBaseTypeSyntax)initializer).ArgumentList.Arguments;
        foreach (var argument in arguments)
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            if (semantic.GetOperation(argument, facts.Cancellation) is not IArgumentOperation { Parameter: { } parameter } operation)
                return false;
            forwarded[parameter] = ResolveForwardedArgumentValue(facts, operation.Value, values);
        }
        next = resolved;
        return true;
    }

    private static SyntaxNode? GetConstructorInitializer(SyntaxNode syntax) => syntax switch
    {
        ConstructorDeclarationSyntax declaration => declaration.Initializer,
        TypeDeclarationSyntax type => type.BaseList?.Types.OfType<PrimaryConstructorBaseTypeSyntax>().FirstOrDefault(),
        _ => null
    };

    private static INamedTypeSymbol? ResolveForwardedArgumentValue(ConventionFacts facts, IOperation? value,
        Dictionary<IParameterSymbol, INamedTypeSymbol?> values)
    {
        while (value is IConversionOperation { OperatorMethod: null } conversion)
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            value = conversion.Operand;
        }
        return value switch
        {
            ITypeOfOperation { TypeOperand: INamedTypeSymbol service } => service,
            IParameterReferenceOperation forwardedParameter => values.GetValueOrDefault(forwardedParameter.Parameter),
            _ => null
        };
    }

    internal static void Analyze(ConventionFacts facts) => Analyze(facts, ReadImplementations(facts));

    private static void Analyze(ConventionFacts facts, INamedTypeSymbol[] implementations,
        Dictionary<INamedTypeSymbol, HashSet<INamedTypeSymbol>>? lifetimeFailures = null, bool requireModule = false)
    {
        facts.Cancellation.ThrowIfCancellationRequested();
        var demands = facts.Records.Where(input => input.Kind == ConventionInputSyntax.RegistrationKind).ToArray();
        var needsModule = requireModule && facts.Project.Role is (ConventionArchitecture.Hosted or
            ConventionArchitecture.Application or ConventionArchitecture.Infra or ConventionArchitecture.Utils);
        if (implementations.Length == 0 && demands.Length == 0 && !needsModule) return;
        var evidence = ReadComposition(facts);
        if (needsModule && !evidence.ModuleScan)
            facts.Diagnostics.Add(Diagnostic.Create(ConventionDiagnosticDescriptors.ModuleScan, Location.None,
                ImmutableDictionary<string, string?>.Empty.Add(ConventionInputSyntax.TargetField,
                    facts.Project.AssemblyName + ":Module"), facts.Project.Name));
        if (implementations.Length == 0 && demands.Length == 0) return;

        var session = new RegistrationSession(facts, implementations, lifetimeFailures, evidence);
        IndexConstructorDemands(facts, session);
        AnalyzeExplicitDemands(facts, demands, session);
        AnalyzeImplementationDemands(facts, session);
    }

    private static void IndexConstructorDemands(ConventionFacts facts, RegistrationSession session)
    {
        foreach (var demand in ReadConstructorServices(facts))
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            var implementation = session.UniqueImplementation(demand.Service);
            if (implementation is null || session.Evidence.HasConflictingManual(demand.Service, demand.Key, implementation)) continue;
            if (!session.ObservedByImplementation.TryGetValue(implementation, out var observed))
            {
                observed = new();
                session.ObservedByImplementation.Add(implementation, observed);
            }
            observed.Demands.Add(demand);
            observed.Services.Add(demand.Service);
        }
    }

    private static void AnalyzeExplicitDemands(ConventionFacts facts, ConventionInput[] demands, RegistrationSession session)
    {
        foreach (var input in demands)
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            var key = input.Get(ConventionInputSyntax.KeyField) is { Length: > 0 } selectedKey ? KeyIdentity(selectedKey) : "";
            var serviceName = input.Get(ConventionInputSyntax.ServiceField);
            if (!session.Seen.Add((input.Identity, serviceName, key))) continue;
            var implementation = facts.Compilation.GetTypeByMetadataName(input.Identity);
            var service = facts.Compilation.GetTypeByMetadataName(serviceName);
            session.ExplicitPairs.Add((input.Identity, serviceName));
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
            if (role is not (ConventionArchitecture.Infra or ConventionArchitecture.Application)) continue;
            // Reasoned selections describe an external/custom path, whose runtime behavior is not certified here.
            if (!string.IsNullOrWhiteSpace(input.Get(ConventionInputSyntax.ReasonField))) continue;
            session.AnalyzeDemand(implementation, service, key, input.Location);
        }
    }

    private static void AnalyzeImplementationDemands(ConventionFacts facts, RegistrationSession session)
    {
        foreach (var implementation in session.Implementations)
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            var location = facts.Location(implementation);
            if (location == Location.None) continue;
            session.ObservedByImplementation.TryGetValue(implementation, out var observed);
            if (observed is not null) AnalyzeObservedImplementationDemands(implementation, observed, location, session);
            foreach (var service in ServiceContracts(facts, implementation))
            {
                if (session.ExplicitPairs.Contains((ConventionSymbols.MetadataName(implementation), ConventionSymbols.MetadataName(service))) ||
                    observed?.Services.Contains(service) is true) continue;
                session.AnalyzeDemand(implementation, service, null, location);
            }
        }
    }

    private static void AnalyzeObservedImplementationDemands(INamedTypeSymbol implementation, ObservedServices observed,
        Location location, RegistrationSession session)
    {
        var identity = ConventionSymbols.MetadataName(implementation);
        foreach (var demand in observed.Demands)
        {
            var serviceName = demand.Service.IsGenericType
                ? demand.Service.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                : ConventionSymbols.MetadataName(demand.Service);
            if (session.Seen.Add((identity, serviceName, demand.Key)))
                session.AnalyzeDemand(implementation, demand.Service, demand.Key, location);
        }
    }

    private sealed class RegistrationSession(ConventionFacts facts, INamedTypeSymbol[] implementations,
        Dictionary<INamedTypeSymbol, HashSet<INamedTypeSymbol>>? lifetimeFailures, Composition evidence)
    {
        private readonly Dictionary<INamedTypeSymbol, INamedTypeSymbol?> _serviceImplementations = new(SymbolEqualityComparer.Default);
        private readonly Dictionary<IAssemblySymbol, bool> _unknownModules = new(SymbolEqualityComparer.Default);

        internal INamedTypeSymbol[] Implementations { get; } = implementations;
        internal Composition Evidence { get; } = evidence;
        internal Dictionary<INamedTypeSymbol, ObservedServices> ObservedByImplementation { get; } = new(SymbolEqualityComparer.Default);
        internal HashSet<(string Implementation, string Service, string Key)> Seen { get; } = [];
        internal HashSet<(string Implementation, string Service)> ExplicitPairs { get; } = [];

        internal INamedTypeSymbol? UniqueImplementation(INamedTypeSymbol service)
        {
            if (_serviceImplementations.TryGetValue(service, out var cached)) return cached;
            INamedTypeSymbol? match = null;
            foreach (var implementation in Implementations)
            {
                facts.Cancellation.ThrowIfCancellationRequested();
                if (!facts.Compilation.ClassifyCommonConversion(implementation, service).IsImplicit) continue;
                if (match is not null) { match = null; break; }
                match = implementation;
            }
            _serviceImplementations.Add(service, match);
            return match;
        }

        internal void AnalyzeDemand(INamedTypeSymbol implementation, INamedTypeSymbol service, string? key, Location location)
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            if (lifetimeFailures is not null && lifetimeFailures.TryGetValue(implementation, out var failedServices) &&
                failedServices.Contains(service)) return;
            if (Evidence.HasManual(service, key, implementation) ||
                !Evidence.Scanned.Contains(implementation.ContainingAssembly) || Evidence.UnknownComposition) return;
            if (!_unknownModules.TryGetValue(implementation.ContainingAssembly, out var unknownModule))
            {
                unknownModule = HasUnknownModule(facts, implementation.ContainingAssembly);
                _unknownModules.Add(implementation.ContainingAssembly, unknownModule);
            }
            if (unknownModule) return;
            if (AttributeRegistration(facts, implementation, service, key) is not false) return;
            facts.Diagnostics.Add(Diagnostic.Create(ConventionDiagnosticDescriptors.Registration, location,
                ImmutableDictionary<string, string?>.Empty.Add(ConventionInputSyntax.TargetField, implementation.GetDocumentationCommentId()),
                implementation.Name, service.ToDisplayString()));
        }
    }

    internal static bool IsServiceImplementation(ConventionFacts facts, INamedTypeSymbol type)
    {
        if (type.TypeKind != TypeKind.Class || type.IsAbstract || type.IsStatic ||
            facts.Symbols.IsOpen(type, facts.Cancellation) ||
            facts.Role(type).Role is not (ConventionArchitecture.Application or ConventionArchitecture.Infra) ||
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
            if (!IsServiceConsumer(facts, consumer)) continue;
            var constructors = consumer.InstanceConstructors.Where(constructor => constructor.DeclaredAccessibility == Accessibility.Public).ToArray();
            if (constructors is [var constructor]) ReadConstructorParameterDemands(facts, constructor, keyedAttribute, result);
        }
        return result;
    }

    private static bool IsServiceConsumer(ConventionFacts facts, INamedTypeSymbol consumer) =>
        facts.Symbols.IsTransport(consumer, facts.Cancellation) ||
        IsServiceImplementation(facts, consumer) &&
        (ServiceContracts(facts, consumer).Any() ||
         facts.Symbols.EffectiveAttributes(consumer, facts.Symbols.LifetimeAttribute, facts.Cancellation).Any());

    private static void ReadConstructorParameterDemands(ConventionFacts facts, IMethodSymbol constructor,
        INamedTypeSymbol? keyedAttribute, List<(INamedTypeSymbol Service, string Key)> result)
    {
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

    private static string KeyIdentity(string key) => "key:" + key;

    private static bool? AttributeRegistration(ConventionFacts facts, INamedTypeSymbol implementation,
        INamedTypeSymbol service, string? key)
    {
        if (!IsPublicTypeHierarchy(implementation)) return false;
        var lifetime = GetLifetime(facts, implementation);
        if (lifetime.Ambiguous || lifetime.Attribute is not { } attribute) return false;
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
        return MatchesRegisteredKey(attribute, key);
    }

    private static bool IsPublicTypeHierarchy(INamedTypeSymbol type)
    {
        for (var containing = type; containing is not null; containing = containing.ContainingType)
            if (containing.DeclaredAccessibility != Accessibility.Public) return false;
        return true;
    }

    private static bool? MatchesRegisteredKey(AttributeData attribute, string? key)
    {
        if (key is null) return true;
        var keyParameter = attribute.AttributeConstructor?.Parameters.FirstOrDefault(parameter => parameter.Name == "key");
        if (keyParameter is null) return key.Length == 0;
        if (attribute.ConstructorArguments.Length <= keyParameter.Ordinal) return null;
        return attribute.ConstructorArguments[keyParameter.Ordinal].Value switch
        {
            null => key.Length == 0,
            string text => KeyIdentity(text) == key,
            _ => null
        };
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
        var symbols = CompositionSymbols.Create(facts.Compilation);
        var scanScopes = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        var unknownScopes = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        foreach (var tree in facts.Compilation.SyntaxTrees)
            InspectSyntaxTree(facts, tree, symbols, evidence, scanScopes, unknownScopes);
        evidence.UnknownComposition |= scanScopes.Overlaps(unknownScopes);
        return evidence;
    }

    private static void InspectSyntaxTree(ConventionFacts facts, SyntaxTree tree, CompositionSymbols symbols,
        Composition evidence, HashSet<ISymbol> scanScopes, HashSet<ISymbol> unknownScopes)
    {
        SemanticModel? semantic = null;
        foreach (var node in tree.GetRoot(facts.Cancellation).DescendantNodes())
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            if (node is BaseObjectCreationExpressionSyntax { ArgumentList.Arguments.Count: > 0 } creation)
            {
                semantic ??= facts.Compilation.GetSemanticModel(tree);
                if (CreatesServiceCollectionConsumer(facts, semantic, creation, symbols.Collection))
                    evidence.UnknownComposition = true;
                continue;
            }
            if (node is not InvocationExpressionSyntax invocation) continue;
            semantic ??= facts.Compilation.GetSemanticModel(tree);
            InspectInvocation(facts, semantic, invocation, symbols, evidence, scanScopes, unknownScopes);
        }
    }

    private static bool CreatesServiceCollectionConsumer(ConventionFacts facts, SemanticModel semantic,
        BaseObjectCreationExpressionSyntax creation, INamedTypeSymbol? collection) =>
        creation.ArgumentList!.Arguments.Any(argument =>
            semantic.GetTypeInfo(argument.Expression, facts.Cancellation).Type is INamedTypeSymbol argumentType &&
            ConventionSymbols.Is(argumentType, collection, facts.Cancellation));

    private static void InspectInvocation(ConventionFacts facts, SemanticModel semantic, InvocationExpressionSyntax invocation,
        CompositionSymbols symbols, Composition evidence, HashSet<ISymbol> scanScopes, HashSet<ISymbol> unknownScopes)
    {
        if (semantic.GetSymbolInfo(invocation, facts.Cancellation).Symbol is not IMethodSymbol method)
        {
            evidence.UnknownComposition = true;
            return;
        }
        var original = method.ReducedFrom ?? method;
        var scope = semantic.GetEnclosingSymbol(invocation.SpanStart, facts.Cancellation);
        if (ConventionSymbols.Same(original.ContainingType, symbols.DrnExtensions) && method.Name == "AddServicesWithAttributes")
        {
            RecordAttributeScanInvocation(facts, semantic, invocation, original, scope, symbols.Collection, evidence, scanScopes);
            return;
        }
        if (!ConventionSymbols.Same(original.ContainingType, symbols.DiExtensions) ||
            method.Name is not ("AddScoped" or "AddTransient" or "AddSingleton" or "AddKeyedScoped" or "AddKeyedTransient" or "AddKeyedSingleton"))
        {
            RecordNonManualInvocation(facts, semantic, invocation, method, scope, symbols.Collection, evidence, unknownScopes);
            return;
        }
        RecordManualRegistration(facts, semantic, invocation, method, evidence);
    }

    private static void RecordAttributeScanInvocation(ConventionFacts facts, SemanticModel semantic,
        InvocationExpressionSyntax invocation, IMethodSymbol original, ISymbol? scope,
        INamedTypeSymbol? collection, Composition evidence, HashSet<ISymbol> scanScopes)
    {
        if (scope is not null) scanScopes.Add(scope);
        var (receiverParameter, scannedAssembly) = ReadAttributeScanArguments(facts, semantic, invocation, original);
        if (scannedAssembly is not null) evidence.Scanned.Add(scannedAssembly);
        var localScan = SymbolEqualityComparer.Default.Equals(scannedAssembly, facts.Compilation.Assembly);
        if (localScan && !evidence.ModuleScan && IsProjectModuleScan(scope, receiverParameter, collection))
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

    private static void RecordNonManualInvocation(ConventionFacts facts, SemanticModel semantic,
        InvocationExpressionSyntax invocation, IMethodSymbol method, ISymbol? scope, INamedTypeSymbol? collection,
        Composition evidence, HashSet<ISymbol> unknownScopes)
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
                SymbolEqualityComparer.Default.Equals(method.ContainingType, scope.ContainingType)))
            unknownScopes.Add(scope);
        evidence.UnknownComposition |= receivesCollection || collectionReceiver;
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
            var constant = semantic.GetConstantValue(invocation.ArgumentList.Arguments[argumentOffset].Expression, facts.Cancellation);
            if (!constant.HasValue || constant.Value is not string value)
            {
                evidence.UnknownComposition = true;
                return;
            }
            key = KeyIdentity(value);
        }
        evidence.AddManual(service, key, implementation);
    }

    private static bool TryGetManualServicePair(IMethodSymbol method,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out INamedTypeSymbol? service,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out INamedTypeSymbol? implementation)
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

    private sealed record CompositionSymbols(INamedTypeSymbol? DrnExtensions, INamedTypeSymbol? DiExtensions,
        INamedTypeSymbol? Collection)
    {
        internal static CompositionSymbols Create(Compilation compilation) => new(
            compilation.GetTypeByMetadataName("DRN.Framework.Utils.DependencyInjection.ServiceCollectionExtensions"),
            compilation.GetTypeByMetadataName("Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions"),
            compilation.GetTypeByMetadataName("Microsoft.Extensions.DependencyInjection.IServiceCollection"));
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
