using System.Collections.Immutable;
using DRN.Framework.CompilerTools;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DRN.Framework.SharedKernel.Analyzers.Conventions;

internal sealed record ConventionOwner(string AssemblyName, string Subdomain, string Role, string RoleNamespace);

internal sealed partial class ConventionFacts
{
    private readonly Dictionary<SyntaxTree, SemanticModel> _semanticModels = [];
    private readonly Dictionary<SyntaxTree, bool> _generatedTrees = [];
    private readonly Dictionary<IAssemblySymbol, ImmutableArray<KeyValuePair<string, string>>> _assemblyMetadata = new(SymbolEqualityComparer.Default);
    private readonly Dictionary<INamedTypeSymbol, bool> _subdomainBoundaries = new(SymbolEqualityComparer.Default);
    private readonly Dictionary<SyntaxTree, (string Directory, string Name)?> _directories = [];
    private readonly Dictionary<ISymbol, Location> _locations = new(SymbolEqualityComparer.Default);
    private readonly Dictionary<INamedTypeSymbol, (string Role, string Namespace, string PathPrefix)> _typeRoles = new(SymbolEqualityComparer.Default);
    private readonly Dictionary<IAssemblySymbol, ConventionProject?> _referenceProjects = new(SymbolEqualityComparer.Default);
    private readonly Dictionary<IAssemblySymbol, HashSet<IAssemblySymbol>> _reachableAssemblies = new(SymbolEqualityComparer.Default);
    private readonly Dictionary<INamedTypeSymbol, ConventionOwner?> _owners = new(SymbolEqualityComparer.Default);
    private readonly HashSet<INamedTypeSymbol> _invalidOwners = new(SymbolEqualityComparer.Default);
    private readonly HashSet<INamedTypeSymbol> _reportedInvalidOwners = new(SymbolEqualityComparer.Default);
    private readonly Dictionary<string, ConventionInput> _ownership = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ConventionInput> _contexts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ConventionInput> _paths = new(StringComparer.Ordinal);
    private readonly Dictionary<INamedTypeSymbol, AttributeData> _externalOwners = new(SymbolEqualityComparer.Default);
    private readonly List<ConventionInput> _roles = [];
    private readonly HashSet<ConventionInput> _invalidRoles = [];
    private readonly HashSet<string> _invalidOwnershipNames = new(StringComparer.Ordinal);
    private readonly HashSet<string> _invalidContextNames = new(StringComparer.Ordinal);
    private readonly HashSet<string> _invalidPaths = new(StringComparer.Ordinal);

    internal ConventionFacts(Compilation compilation, ConventionProject project, ConventionInputs inputs,
        AnalyzerConfigOptionsProvider options, CancellationToken cancellation)
    {
        Compilation = compilation;
        Project = project;
        Inputs = inputs;
        Options = options;
        Cancellation = cancellation;
        Symbols = new(compilation);
        Types = ConventionSymbols.SourceTypes(compilation, cancellation);
        Diagnostics.AddRange(project.Diagnostics);
        Diagnostics.AddRange(inputs.Diagnostics);
        PrepareInputs();
    }

    internal Compilation Compilation { get; }
    internal ConventionProject Project { get; }
    internal ConventionInputs Inputs { get; }
    internal AnalyzerConfigOptionsProvider Options { get; }
    internal CancellationToken Cancellation { get; }
    internal ConventionSymbols Symbols { get; }
    internal ImmutableArray<INamedTypeSymbol> Types { get; }
    internal List<Diagnostic> Diagnostics { get; } = [];
    internal List<ConventionInput> Records { get; } = [];
    internal Dictionary<INamedTypeSymbol, ConventionPersistenceAnalysis.ContextModel> ContextModels { get; } = new(SymbolEqualityComparer.Default);
    internal Dictionary<IMethodSymbol, bool> DefaultModelScans { get; } = new(SymbolEqualityComparer.Default);
    internal Dictionary<INamedTypeSymbol, ConventionRegistrationAnalysis.LifetimeAnalysis> Lifetimes { get; } = new(SymbolEqualityComparer.Default);

    internal bool IsLocal(INamedTypeSymbol type) => SymbolEqualityComparer.Default.Equals(type.ContainingAssembly, Compilation.Assembly);

    internal bool HasInvalidOwner(INamedTypeSymbol type) => _invalidOwners.Contains(type.OriginalDefinition) ||
        _invalidOwnershipNames.Contains(ConventionSymbols.MetadataName(type)) || HasInvalidContext(type);

    internal bool HasInvalidContext(INamedTypeSymbol type) => _invalidContextNames.Contains(ConventionSymbols.MetadataName(type));

    internal bool HasInvalidPath(SyntaxTree tree) => _invalidPaths.Contains(ConventionProject.NormalizeSeparators(tree.FilePath));

    internal ConventionInput? ContextInput(INamedTypeSymbol type) =>
        _contexts.TryGetValue(ConventionSymbols.MetadataName(type), out var input) ? input : null;

    internal bool IsEntity(INamedTypeSymbol type) => Symbols.IsEntity(type, Cancellation) || DeclaredKind(type) == "Entity";

    internal bool HasSubdomainBoundary(INamedTypeSymbol type)
    {
        Cancellation.ThrowIfCancellationRequested();
        if (_subdomainBoundaries.TryGetValue(type, out var cached)) return cached;
        var result = ResolveSubdomainBoundary(type);
        Cancellation.ThrowIfCancellationRequested();
        _subdomainBoundaries.Add(type, result);
        return result;
    }

    private bool ResolveSubdomainBoundary(INamedTypeSymbol type)
    {
        if (Symbols.IsKnown(type, Symbols.PageModel, Cancellation)) return false;
        var role = Role(type).Role;
        if (role == "Domain" || IsEntity(type) || Symbols.IsContext(type, Cancellation) ||
            Symbols.IsKnown(type, Symbols.Configuration, Cancellation) ||
            Symbols.IsKnown(type, Symbols.RepositoryContract, Cancellation) ||
            Symbols.IsKnown(type, Symbols.ServiceContract, Cancellation) ||
            Symbols.IsKnown(type, Symbols.SourceKnownController, Cancellation)) return true;
        if (role is "Application" or "Infra" &&
            (ConventionOwnershipAnalysis.HasEntityBinding(this, type) ||
             ConventionRegistrationAnalysis.IsServiceImplementation(this, type) &&
             Symbols.EffectiveAttributes(type, Symbols.LifetimeAttribute, Cancellation).Any())) return true;
        return type.ContainingType is { } containing && HasSubdomainBoundary(containing);
    }

    internal bool IsNeutralValue(INamedTypeSymbol type) => !Symbols.IsPersistenceType(type, Cancellation) && DeclaredKind(type) == "Value";

    internal string? LogicalPath(SyntaxTree tree) =>
        _paths.TryGetValue(ConventionProject.NormalizeSeparators(tree.FilePath), out var input) ? input.Get("LogicalPath") : null;

    internal (string Directory, string Name)? LogicalDirectory(SyntaxTree tree)
    {
        Cancellation.ThrowIfCancellationRequested();
        if (_directories.TryGetValue(tree, out var cached)) return cached;
        (string Directory, string Name)? result = null;
        if (LogicalPath(tree) is { } path)
        {
            path = ConventionProject.NormalizeSeparators(path);
            var separator = path.LastIndexOf('/');
            var directory = separator < 0 ? "" : path[..separator];
            result = (directory, directory[(directory.LastIndexOf('/') + 1)..]);
        }
        Cancellation.ThrowIfCancellationRequested();
        _directories.Add(tree, result);
        return result;
    }

    internal bool IsGenerated(SyntaxTree tree)
    {
        Cancellation.ThrowIfCancellationRequested();
        if (_generatedTrees.TryGetValue(tree, out var cached)) return cached;
        var result = GeneratedSource.IsGenerated(tree, Compilation, Options, Cancellation);
        Cancellation.ThrowIfCancellationRequested();
        _generatedTrees.Add(tree, result);
        return result;
    }

    internal SemanticModel SemanticModel(SyntaxTree tree)
    {
        Cancellation.ThrowIfCancellationRequested();
        if (_semanticModels.TryGetValue(tree, out var cached)) return cached;
        var model = Compilation.GetSemanticModel(tree);
        Cancellation.ThrowIfCancellationRequested();
        _semanticModels.Add(tree, model);
        return model;
    }

    internal Location Location(ISymbol symbol)
    {
        Cancellation.ThrowIfCancellationRequested();
        if (_locations.TryGetValue(symbol, out var cached)) return cached;
        var result = Microsoft.CodeAnalysis.Location.None;
        string? selectedPath = null;
        foreach (var location in symbol.Locations)
        {
            Cancellation.ThrowIfCancellationRequested();
            if (!location.IsInSource || location.SourceTree is not { } tree ||
                !Compilation.ContainsSyntaxTree(tree) || IsGenerated(tree)) continue;
            var path = LogicalPath(tree) ?? tree.FilePath;
            var order = StringComparer.Ordinal.Compare(path, selectedPath);
            if (result != Microsoft.CodeAnalysis.Location.None &&
                (order > 0 || order == 0 && location.SourceSpan.Start >= result.SourceSpan.Start)) continue;
            result = location;
            selectedPath = path;
        }
        Cancellation.ThrowIfCancellationRequested();
        _locations.Add(symbol, result);
        return result;
    }

    internal (string Role, string Namespace, string PathPrefix) Role(INamedTypeSymbol type)
    {
        Cancellation.ThrowIfCancellationRequested();
        if (_typeRoles.TryGetValue(type, out var cached)) return cached;
        var result = ResolveRole(type);
        Cancellation.ThrowIfCancellationRequested();
        _typeRoles.Add(type, result);
        return result;
    }

    private (string Role, string Namespace, string PathPrefix) ResolveRole(INamedTypeSymbol type)
    {
        var project = IsLocal(type) ? Project : ReferenceProject(type.ContainingAssembly);
        if (project is null || !ConventionProject.IsRole(project.Role) ||
            !ConventionProject.IsNamespace(project.RootNamespace)) return ("", "", "");
        var ns = type.ContainingNamespace.ToDisplayString();
        return IsLocal(type) ? ResolveLocalRole(ns, project) : ResolveReferencedRole(type.ContainingAssembly, ns, project);
    }

    private (string Role, string Namespace, string PathPrefix) ResolveLocalRole(string ns, ConventionProject project)
    {
        if (_invalidRoles.Any(item => ConventionProject.IsNamespaceWithin(ns, item.Get(ConventionInputSyntax.NamespacePrefixField))))
            return ("", "", "");
        var mapping = _roles.FirstOrDefault(item => ConventionProject.IsNamespaceWithin(ns, item.Get(ConventionInputSyntax.NamespacePrefixField)));
        if (mapping is null) return (project.Role, project.RootNamespace, "");
        return (mapping.Identity, mapping.Get(ConventionInputSyntax.NamespacePrefixField),
            ConventionProject.NormalizeSeparators(mapping.Get(ConventionInputSyntax.LogicalPathPrefixField)));
    }

    private (string Role, string Namespace, string PathPrefix) ResolveReferencedRole(
        IAssemblySymbol assembly, string ns, ConventionProject project)
    {
        foreach (var metadata in AssemblyMetadata(assembly))
        {
            if (!metadata.Key.StartsWith(ConventionProject.MetadataPrefix + ConventionInputSyntax.RoleKind + ".", StringComparison.Ordinal))
                continue;
            var role = metadata.Key[(ConventionProject.MetadataPrefix.Length + 5)..];
            if (ConventionProject.IsRole(role) && ConventionProject.IsNamespaceWithin(ns, metadata.Value))
                return (role, metadata.Value, "");
        }
        return (project.Role, project.RootNamespace, "");
    }

    internal ConventionOwner? Owner(INamedTypeSymbol type)
    {
        type = type.OriginalDefinition;
        if (_owners.TryGetValue(type, out var cached)) return cached;
        var result = ResolveOwner(type);
        _owners.Add(type, result);
        return result;
    }

    private ConventionOwner? ResolveOwner(INamedTypeSymbol type)
    {
        if (HasInvalidOwner(type)) return null;
        var local = IsLocal(type);
        var project = local ? Project : ReferenceProject(type.ContainingAssembly);
        var role = Role(type);
        var conventional = project is null ? null : InferOwner(type, role.Role, role.Namespace);
        var (declarations, hasDirectAttribute, hasProducerDeclaration) = CollectDeclaredOwners(type, local, ref project);

        var nameKey = ConventionSymbols.MetadataName(type);
        ValidateDeclaredOwners(type, nameKey, conventional, declarations);
        if (IsInvalidConsumerRedefinition(type, local, conventional, nameKey, declarations, hasDirectAttribute, hasProducerDeclaration))
            InvalidOwner(type, nameKey, "a consumer redefinition of referenced ownership", declarations[0].Location);

        _ = DeclaredKind(type);

        if (_invalidOwners.Contains(type)) return null;
        var owner = declarations.Count > 0 ? declarations[0].Name : conventional;
        if (owner is null || project is null) return null;
        return new(type.ContainingAssembly.Name, owner, role.Role, role.Namespace);
    }

    private (List<(string Name, string Reason, Location Location, bool ConsumerOverride)> Declarations,
        bool HasDirectAttribute, bool HasProducerDeclaration) CollectDeclaredOwners(
        INamedTypeSymbol type, bool local, ref ConventionProject? project)
    {
        var declarations = new List<(string Name, string Reason, Location Location, bool ConsumerOverride)>();
        var hasDirectAttribute = CollectAttributeOwners(type, declarations);
        var producer = local ? null : ReferenceOwnership(type);
        var hasProducerDeclaration = producer is { Declarations.Count: > 0 };
        if (producer is not null)
        {
            foreach (var declaration in producer.Declarations)
                declarations.Add((declaration.Subdomain, declaration.Reason, Microsoft.CodeAnalysis.Location.None, false));
        }

        var nameKey = ConventionSymbols.MetadataName(type);
        if (_contexts.TryGetValue(nameKey, out var context) && context.Get(ConventionInputSyntax.SubdomainField).Length > 0)
            declarations.Insert(0, (context.Get(ConventionInputSyntax.SubdomainField),
                context.Get(ConventionInputSyntax.ReasonField), context.Location, !local));
        if (_ownership.TryGetValue(nameKey, out var ownership))
        {
            declarations.Insert(0, (ownership.Get(ConventionInputSyntax.SubdomainField),
                ownership.Get(ConventionInputSyntax.ReasonField), ownership.Location, !local));
            project ??= Project;
        }

        // A consumer may fill a missing external fact, but cannot redefine an established owner.
        if (_externalOwners.TryGetValue(type, out var external))
        {
            var externalName = (string)external.ConstructorArguments[1].Value!;
            declarations.Add((externalName, Reason(external), AttributeLocation(external, type), true));
            project ??= Project;
        }

        return (declarations, hasDirectAttribute, hasProducerDeclaration);
    }

    private bool CollectAttributeOwners(INamedTypeSymbol type,
        List<(string Name, string Reason, Location Location, bool ConsumerOverride)> declarations)
    {
        var hasDirectAttribute = false;
        foreach (var attribute in type.GetAttributes())
        {
            if (!ConventionSymbols.Same(attribute.AttributeClass, Symbols.OwnershipAttribute)) continue;
            hasDirectAttribute = true;
            if (attribute.AttributeConstructor is null || attribute.ConstructorArguments.Any(argument => argument.Kind == TypedConstantKind.Error))
            {
                _invalidOwners.Add(type);
                continue;
            }
            if (attribute.ConstructorArguments is not [{ Value: string name }])
            {
                InvalidOwner(type, "type ownership", "not a single subdomain declaration", AttributeLocation(attribute, type));
                continue;
            }
            declarations.Add((name, Reason(attribute), AttributeLocation(attribute, type), false));
        }
        return hasDirectAttribute;
    }

    private void ValidateDeclaredOwners(INamedTypeSymbol type, string nameKey, string? conventional,
        List<(string Name, string Reason, Location Location, bool ConsumerOverride)> declarations)
    {
        if (declarations.Select(item => item.Name).Distinct(StringComparer.Ordinal).Skip(1).Any())
            InvalidOwner(type, nameKey, "contradictory", declarations[0].Location, declarations.Select(item => item.Location));
        foreach (var declaration in declarations)
        {
            if (!Microsoft.CodeAnalysis.CSharp.SyntaxFacts.IsValidIdentifier(declaration.Name) ||
                (declaration.Name != conventional || declaration.ConsumerOverride) && string.IsNullOrWhiteSpace(declaration.Reason))
                InvalidOwner(type, nameKey, "malformed or missing its exceptional reason", declaration.Location);
        }
    }

    private bool IsInvalidConsumerRedefinition(INamedTypeSymbol type, bool local, string? conventional, string nameKey,
        List<(string Name, string Reason, Location Location, bool ConsumerOverride)> declarations,
        bool hasDirectAttribute, bool hasProducerDeclaration) =>
        !local && conventional is not null && !hasDirectAttribute && !hasProducerDeclaration &&
        (_ownership.ContainsKey(nameKey) || _contexts.ContainsKey(nameKey) || _externalOwners.ContainsKey(type)) &&
        declarations.Any(declaration => declaration.ConsumerOverride && declaration.Name != conventional);

    internal bool UsesOwnSubdomain(INamedTypeSymbol consumer, INamedTypeSymbol entity)
    {
        if (Owner(consumer) is not { } owner || Owner(entity) is not { } entityOwner ||
            owner.Subdomain != entityOwner.Subdomain) return false;
        if (SymbolEqualityComparer.Default.Equals(consumer.ContainingAssembly, entity.ContainingAssembly)) return true;
        // A referencing layer can implement a Domain subdomain without sharing its project name.
        // Separate Domain assemblies retain distinct ownership even when subdomain names coincide.
        if (owner.Role is not ("Infra" or "Application" or "Hosted") || entityOwner.Role != "Domain") return false;
        if (!_reachableAssemblies.TryGetValue(consumer.ContainingAssembly, out var reachable))
        {
            reachable = new(SymbolEqualityComparer.Default);
            var pending = new Stack<IAssemblySymbol>();
            pending.Push(consumer.ContainingAssembly);
            while (pending.TryPop(out var assembly))
            {
                Cancellation.ThrowIfCancellationRequested();
                if (!reachable.Add(assembly)) continue;
                foreach (var reference in assembly.Modules.SelectMany(module => module.ReferencedAssemblySymbols))
                    pending.Push(reference);
            }
            _reachableAssemblies.Add(consumer.ContainingAssembly, reachable);
        }
        return reachable.Contains(entity.ContainingAssembly);
    }

    private static string? InferOwner(INamedTypeSymbol type, string role, string rootNamespace)
    {
        var ns = type.ContainingNamespace.ToDisplayString();
        if (rootNamespace.Length == 0 || !ns.StartsWith(rootNamespace + ".", StringComparison.Ordinal)) return null;
        var parts = ns[(rootNamespace.Length + 1)..].Split('.');
        var prefix = role == ConventionArchitecture.Hosted && parts[0] is "Controllers" or "Pages" ||
                     role == ConventionArchitecture.Application && parts[0] == "Services";
        var index = prefix ? 1 : 0;
        return index < parts.Length ? parts[index] : null;
    }

    internal ConventionProject? ReferenceProject(IAssemblySymbol assembly)
    {
        if (_referenceProjects.TryGetValue(assembly, out var existing)) return existing;
        var metadata = AssemblyMetadata(assembly);
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["build_property.MSBuildProjectName"] = assembly.Name,
            ["build_property.RootNamespace"] = assembly.Name
        };
        var declared = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (key, value) in metadata)
        {
            var property = key switch
            {
                "DRN.Conventions.ProjectName" => "MSBuildProjectName",
                "DRN.Conventions.RootNamespace" => "RootNamespace",
                _ => null
            };
            if (property is null) continue;
            if (string.IsNullOrWhiteSpace(value))
            {
                Diagnostics.Add(ConventionProject.InputIssue(assembly.Name + ":" + property, "incomplete", "reference-visible project identity"));
                _referenceProjects.Add(assembly, null);
                return null;
            }
            if (!declared.Add(property) && values["build_property." + property] != value)
            {
                Diagnostics.Add(ConventionProject.InputIssue(assembly.Name + ":" + property, "contradictory", "reference-visible ownership fact"));
                _referenceProjects.Add(assembly, null);
                return null;
            }
            values["build_property." + property] = value;
        }
        if (!declared.Contains("RootNamespace"))
            values["build_property.RootNamespace"] = values["build_property.MSBuildProjectName"];
        ConventionProject? project = ConventionProject.Read(Compilation, new MetadataOptions(values)) with { AssemblyName = assembly.Name };
        if (!project.Diagnostics.IsEmpty || !ValidReferenceRoles(metadata))
        {
            Diagnostics.Add(ConventionProject.InputIssue(assembly.Name, "incomplete", "reference-visible project identity"));
            project = null;
        }
        else if (!project.Enabled) project = null;
        _referenceProjects.Add(assembly, project);
        return project;
    }

    private bool ValidReferenceRoles(ImmutableArray<KeyValuePair<string, string>> metadata)
    {
        var prefixes = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, value) in metadata)
        {
            Cancellation.ThrowIfCancellationRequested();
            if (!key.StartsWith(ConventionProject.MetadataPrefix + "Role.", StringComparison.Ordinal)) continue;
            var role = key[(ConventionProject.MetadataPrefix.Length + 5)..];
            if (!ConventionProject.IsRole(role) || !ConventionProject.IsNamespace(value)) return false;
            if (prefixes.TryGetValue(value, out var previous) && previous != role) return false;
            prefixes[value] = role;
        }
        foreach (var prefix in prefixes.Keys)
        {
            for (var end = prefix.LastIndexOf('.'); end >= 0; end = end == 0 ? -1 : prefix.LastIndexOf('.', end - 1))
            {
                Cancellation.ThrowIfCancellationRequested();
                if (prefixes.ContainsKey(prefix[..end])) return false;
            }
        }
        return true;
    }

    internal ImmutableArray<KeyValuePair<string, string>> AssemblyMetadata(IAssemblySymbol assembly)
    {
        Cancellation.ThrowIfCancellationRequested();
        if (_assemblyMetadata.TryGetValue(assembly, out var cached)) return cached;
        var metadata = ImmutableArray.CreateBuilder<KeyValuePair<string, string>>();
        foreach (var attribute in assembly.GetAttributes())
        {
            Cancellation.ThrowIfCancellationRequested();
            if (ConventionSymbols.Same(attribute.AttributeClass, Symbols.AssemblyMetadataAttribute) &&
                attribute.ConstructorArguments is [{ Value: string key }, var value] && value.Kind != TypedConstantKind.Error &&
                (value.Value is string || value.IsNull)) metadata.Add(new(key, value.Value as string ?? ""));
        }
        var result = metadata.ToImmutable();
        Cancellation.ThrowIfCancellationRequested();
        _assemblyMetadata.Add(assembly, result);
        return result;
    }

    internal static string Reason(AttributeData attribute) =>
        attribute.NamedArguments.FirstOrDefault(pair => pair.Key == "Reason").Value.Value as string ?? "";

    internal Location AttributeLocation(AttributeData attribute, INamedTypeSymbol fallback) =>
        attribute.ApplicationSyntaxReference is { } reference && Compilation.ContainsSyntaxTree(reference.SyntaxTree)
            ? reference.GetSyntax(Cancellation).GetLocation() : Location(fallback);

    private void InvalidOwner(INamedTypeSymbol type, string key, string problem, Location location, IEnumerable<Location>? evidence = null)
    {
        _invalidOwners.Add(type.OriginalDefinition);
        if (!_reportedInvalidOwners.Add(type.OriginalDefinition)) return;
        Diagnostics.Add(Diagnostic.Create(ConventionDiagnosticDescriptors.InvalidInput, location,
            additionalLocations: evidence?.Where(item => item != Microsoft.CodeAnalysis.Location.None && item != location).Distinct(),
            properties: null, messageArgs: [key, problem, "reasoned consistent subdomain owner"]));
    }

    private sealed class MetadataOptions(Dictionary<string, string> values) : AnalyzerConfigOptions
    {
        public override bool TryGetValue(string key, out string value) => values.TryGetValue(key, out value!);
    }
}
