using Microsoft.CodeAnalysis.Operations;

namespace DRN.Framework.SharedKernel.Analyzers.Conventions;

internal static class ConventionRegistrationAnalysis
{
    internal static void AnalyzeAll(ConventionFacts facts, bool requireModule = false)
    {
        var inputs = ReadInputs(facts, requireModule);
        AnalyzeAll(facts, inputs, new ConventionCallSites(facts));
    }

    internal static void AnalyzeAll(ConventionFacts facts, Inputs inputs, ConventionCallSites calls)
    {
        var lifetimeFailures = AnalyzeLifetimeAttributes(facts, inputs.Implementations);
        Analyze(facts, inputs, calls, lifetimeFailures);
    }

    internal sealed record Inputs(INamedTypeSymbol[] Implementations, ConventionInput[] Demands, bool NeedsModule)
    {
        internal bool NeedsComposition => Implementations.Length != 0 || Demands.Length != 0 || NeedsModule;
    }

    internal static Inputs ReadInputs(ConventionFacts facts, bool requireModule)
    {
        var implementations = ReadImplementations(facts);
        var demands = facts.Records.Where(input => input.Kind == ConventionInputSyntax.RegistrationKind).ToArray();
        var needsModule = requireModule && facts.Project.Role is (ConventionArchitecture.Hosted or
            ConventionArchitecture.Application or ConventionArchitecture.Infra or ConventionArchitecture.Utils);
        return new(implementations, demands, needsModule);
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
        var semantic = facts.SemanticModel(reference.SyntaxTree);
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

    internal static void Analyze(ConventionFacts facts)
    {
        var inputs = ReadInputs(facts, requireModule: false);
        Analyze(facts, inputs, new ConventionCallSites(facts));
    }

    private static void Analyze(ConventionFacts facts, Inputs inputs, ConventionCallSites calls,
        Dictionary<INamedTypeSymbol, HashSet<INamedTypeSymbol>>? lifetimeFailures = null)
    {
        facts.Cancellation.ThrowIfCancellationRequested();
        if (!inputs.NeedsComposition) return;
        var evidence = calls.ReadComposition();
        if (inputs.NeedsModule && !evidence.ModuleScan)
            facts.Diagnostics.Add(Diagnostic.Create(ConventionDiagnosticDescriptors.ModuleScan, Location.None,
                ImmutableDictionary<string, string?>.Empty.Add(ConventionInputSyntax.TargetField,
                    facts.Project.AssemblyName + ":Module"), facts.Project.Name));
        if (inputs.Implementations.Length == 0 && inputs.Demands.Length == 0) return;

        var session = new RegistrationSession(facts, inputs.Implementations, lifetimeFailures, evidence);
        IndexConstructorDemands(facts, session);
        AnalyzeExplicitDemands(facts, inputs.Demands, session);
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
            var key = input.Get(ConventionInputSyntax.KeyField) is { Length: > 0 } selectedKey
                ? ConventionCompositionReader.KeyIdentity(selectedKey) : "";
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
        Dictionary<INamedTypeSymbol, HashSet<INamedTypeSymbol>>? lifetimeFailures, ConventionCompositionReader.Composition evidence)
    {
        private readonly Dictionary<INamedTypeSymbol, INamedTypeSymbol?> _serviceImplementations = new(SymbolEqualityComparer.Default);
        private readonly Dictionary<IAssemblySymbol, bool> _unknownModules = new(SymbolEqualityComparer.Default);

        internal INamedTypeSymbol[] Implementations { get; } = implementations;
        internal ConventionCompositionReader.Composition Evidence { get; } = evidence;
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

        private bool? AttributeRegistration(INamedTypeSymbol implementation,
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
                string text => ConventionCompositionReader.KeyIdentity(text) == key,
                _ => null
            };
        }

        private bool HasUnknownModule(IAssemblySymbol assembly)
        {
            // A custom module can register arbitrary services. Its presence prevents an absence proof.
            if (!SymbolEqualityComparer.Default.Equals(assembly, facts.Compilation.Assembly)) return true;
            return facts.Types.Any(type => facts.Symbols.EffectiveAttributes(type, facts.Symbols.RegistrationAttribute,
                facts.Cancellation).Any(attribute =>
                    !ConventionSymbols.Same(attribute.AttributeClass, facts.Symbols.ContextRegistrationAttribute)));
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
                unknownModule = HasUnknownModule(implementation.ContainingAssembly);
                _unknownModules.Add(implementation.ContainingAssembly, unknownModule);
            }
            if (unknownModule) return;
            if (AttributeRegistration(implementation, service, key) is not false) return;
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
        var keyedAttribute = facts.Symbols.FromKeyedServicesAttribute;
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
            else if (keyed.ConstructorArguments is [{ Value: string key }])
                result.Add((service, ConventionCompositionReader.KeyIdentity(key)));
            // Inherited/ambient and non-string keys are unsupported, not evidence for an unkeyed demand.
        }
    }

    private sealed class ObservedServices
    {
        internal List<(INamedTypeSymbol Service, string Key)> Demands { get; } = [];
        internal HashSet<INamedTypeSymbol> Services { get; } = new(SymbolEqualityComparer.Default);
    }
}
