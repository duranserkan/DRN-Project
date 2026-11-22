using DRN.Framework.CompilerTools;
using Microsoft.CodeAnalysis.CSharp;

namespace DRN.Framework.SharedKernel.Analyzers.Conventions;

// Represents resolved ownership identity by (AssemblyName, Domain, Subdomain) and project layout metadata.
internal sealed record ConventionOwner(string AssemblyName, string Domain, string Subdomain, string Role, string RoleNamespace,
    bool HasFolderDomain = false, bool HasExplicitDomain = false)
{
    internal string NamespacePrefix => ConventionNames.OwnerNamespacePrefix(Domain, Subdomain, HasFolderDomain);
    internal string DisplayName => HasFolderDomain ? Domain + "/" + Subdomain : Subdomain;

    internal bool SameOwner(ConventionOwner other) =>
        ConventionNames.SameOwner(Domain, Subdomain, other.Domain, other.Subdomain);

    internal bool MatchesAcrossLayers(ConventionOwner other) =>
        ConventionNames.MatchesOwnerAcrossLayers(
            new(Domain, Subdomain, HasFolderDomain, HasExplicitDomain),
            new(other.Domain, other.Subdomain, other.HasFolderDomain, other.HasExplicitDomain));
}

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
    private readonly Dictionary<ISymbol, HashSet<ISymbol>> _incompleteContracts = new(SymbolEqualityComparer.Default);
    private readonly Dictionary<INamedTypeSymbol, ImmutableArray<ISymbol>> _publicContractMembers = new(SymbolEqualityComparer.Default);
    private readonly Dictionary<INamedTypeSymbol, ImmutableArray<ISymbol>> _publicTransportMembers = new(SymbolEqualityComparer.Default);
    private readonly Dictionary<INamedTypeSymbol, ImmutableArray<(int StatusCode, ITypeSymbol Type)>> _controllerProducesResponseTypes = new(SymbolEqualityComparer.Default);
    private readonly Dictionary<IMethodSymbol, ImmutableArray<(int StatusCode, ITypeSymbol? Type)>> _methodProducesResponseTypes = new(SymbolEqualityComparer.Default);
    private readonly HashSet<ITypeSymbol>[] _cleanPublicShapeTypes =
    [
        new(SymbolEqualityComparer.Default),
        new(SymbolEqualityComparer.Default),
        new(SymbolEqualityComparer.Default)
    ];
    private bool? _hasGlobalResponseAlias;
    private readonly Dictionary<SyntaxTree, bool> _treeHasResponseAlias = [];
    private bool _hasIncompleteInventory;
    private bool _hasInvalidFolderInput;

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

    internal bool HasDomainRole() => Project.Role == ConventionArchitecture.Domain ||
        _roles.Any(role => role.Identity == ConventionArchitecture.Domain);

    internal void MarkIncompleteInventory() => _hasIncompleteInventory = true;

    internal bool HasInvalidFolderStructureInputs() =>
        _hasIncompleteInventory ||
        _hasInvalidFolderInput ||
        _invalidRoles.Count > 0 ||
        _invalidPaths.Count > 0 ||
        Project.Diagnostics.Any(diagnostic => diagnostic.Descriptor == ConventionDiagnosticDescriptors.InvalidInput) ||
        Inputs.Diagnostics.Any(diagnostic => diagnostic.Descriptor == ConventionDiagnosticDescriptors.InvalidInput);

    internal bool TryGetDomainRelativeDirectory(string directory, out string relativeDirectory)
    {
        foreach (var mapping in _roles)
        {
            var prefix = ConventionProject.NormalizeSeparators(mapping.Get(ConventionInputSyntax.LogicalPathPrefixField));
            if (directory != prefix && !directory.StartsWith(prefix + "/", StringComparison.Ordinal)) continue;
            if (mapping.Identity != ConventionArchitecture.Domain)
            {
                relativeDirectory = "";
                return false;
            }
            relativeDirectory = directory.Length == prefix.Length ? "" : directory[(prefix.Length + 1)..];
            return true;
        }
        if (Project.Role == ConventionArchitecture.Domain)
        {
            relativeDirectory = directory;
            return true;
        }
        relativeDirectory = "";
        return false;
    }

    internal void ReportIncompleteContract(ISymbol reportingSymbol, ISymbol member, Location location)
    {
        if (!_incompleteContracts.TryGetValue(reportingSymbol, out var members))
        {
            members = new(SymbolEqualityComparer.Default);
            _incompleteContracts.Add(reportingSymbol, members);
        }
        if (members.Add(member))
            Diagnostics.Add(Diagnostic.Create(ConventionDiagnosticDescriptors.IncompleteContract, location,
                ConventionBoundaryAnalysis.Properties(reportingSymbol), member.ToDisplayString()));
    }

    internal Stack<ITypeSymbol> PublicShapePending { get; } = [];
    internal HashSet<ITypeSymbol> PublicShapeVisited { get; } = new(SymbolEqualityComparer.Default);

    internal HashSet<ITypeSymbol> CleanPublicShapeTypes(PublicShapeRule rule) => _cleanPublicShapeTypes[(int)rule];

    internal ImmutableArray<ISymbol> GetOrAddPublicMembers(INamedTypeSymbol type, bool transport,
        Func<ConventionFacts, INamedTypeSymbol, bool, ImmutableArray<ISymbol>> factory)
    {
        Cancellation.ThrowIfCancellationRequested();
        var cache = transport ? _publicTransportMembers : _publicContractMembers;
        if (cache.TryGetValue(type, out var cached)) return cached;
        var members = factory(this, type, transport);
        Cancellation.ThrowIfCancellationRequested();
        cache.Add(type, members);
        return members;
    }

    internal ImmutableArray<(int StatusCode, ITypeSymbol Type)> GetOrAddControllerProducesResponseTypes(
        INamedTypeSymbol controller, Func<ConventionFacts, INamedTypeSymbol, ImmutableArray<(int StatusCode, ITypeSymbol Type)>> factory)
    {
        Cancellation.ThrowIfCancellationRequested();
        if (_controllerProducesResponseTypes.TryGetValue(controller, out var cached)) return cached;
        var entries = factory(this, controller);
        Cancellation.ThrowIfCancellationRequested();
        _controllerProducesResponseTypes.Add(controller, entries);
        return entries;
    }

    internal ImmutableArray<(int StatusCode, ITypeSymbol? Type)> GetOrAddMethodProducesResponseTypes(
        IMethodSymbol method, Func<ConventionFacts, IMethodSymbol, ImmutableArray<(int StatusCode, ITypeSymbol? Type)>> factory)
    {
        Cancellation.ThrowIfCancellationRequested();
        if (_methodProducesResponseTypes.TryGetValue(method, out var cached)) return cached;
        var entries = factory(this, method);
        Cancellation.ThrowIfCancellationRequested();
        _methodProducesResponseTypes.Add(method, entries);
        return entries;
    }

    internal bool HasResponseUsingAlias(SyntaxTree tree)
    {
        Cancellation.ThrowIfCancellationRequested();
        if (HasGlobalResponseAlias()) return true;
        if (_treeHasResponseAlias.TryGetValue(tree, out var cached)) return cached;
        var result = TreeHasResponseAlias(tree, Cancellation);
        Cancellation.ThrowIfCancellationRequested();
        _treeHasResponseAlias.Add(tree, result);
        return result;
    }

    private bool HasGlobalResponseAlias()
    {
        Cancellation.ThrowIfCancellationRequested();
        if (_hasGlobalResponseAlias is { } cached) return cached;
        var result = AnyGlobalResponseAlias(Compilation, Cancellation);
        Cancellation.ThrowIfCancellationRequested();
        _hasGlobalResponseAlias = result;
        return result;
    }

    [SuppressMessage("SonarQube", "S3267", Justification = "Iterating SyntaxList Usings directly avoids boxing the struct list and allocating a predicate delegate per syntax tree.")]
    private static bool AnyGlobalResponseAlias(Compilation compilation, CancellationToken cancellation)
    {
        foreach (var tree in compilation.SyntaxTrees)
        {
            cancellation.ThrowIfCancellationRequested();
            if (tree.GetRoot(cancellation) is not CompilationUnitSyntax root) continue;
            foreach (var directive in root.Usings)
            {
                if (directive.GlobalKeyword.IsKind(SyntaxKind.GlobalKeyword) && IsResponseAlias(directive))
                    return true;
            }
        }
        return false;
    }

    private static bool TreeHasResponseAlias(SyntaxTree tree, CancellationToken cancellation)
    {
        if (tree.GetRoot(cancellation) is not CompilationUnitSyntax root) return false;
        foreach (var node in root.DescendantNodes(static current =>
                     current is CompilationUnitSyntax or BaseNamespaceDeclarationSyntax))
        {
            cancellation.ThrowIfCancellationRequested();
            if (node is UsingDirectiveSyntax directive && IsResponseAlias(directive))
                return true;
        }
        return false;
    }

    private static bool IsResponseAlias(UsingDirectiveSyntax directive) =>
        directive.Alias is not null &&
        !directive.Alias.Name.Identifier.ValueText.EndsWith("Response", StringComparison.Ordinal) &&
        RightmostTypeIdentifier(directive.NamespaceOrType) is { } target &&
        target.EndsWith("Response", StringComparison.Ordinal);

    internal static string? RightmostTypeIdentifier(TypeSyntax type) => type switch
    {
        SimpleNameSyntax simple => simple.Identifier.ValueText,
        QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
        AliasQualifiedNameSyntax alias => alias.Name.Identifier.ValueText,
        _ => null
    };

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

    internal bool HasLogicalPath(string logicalPath)
    {
        var normalized = ConventionProject.NormalizeSeparators(logicalPath);
        return _paths.Values.Any(input => string.Equals(
            ConventionProject.NormalizeSeparators(input.Get(ConventionInputSyntax.LogicalPathField)),
            normalized, StringComparison.Ordinal));
    }

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
        var conventional = project is null ? null : InferOwner(type, role.Role, role.Namespace, project);
        var (declarations, hasDirectAttribute, hasProducerDeclaration) = CollectDeclaredOwners(type, local, ref project, conventional?.Domain ?? "");

        var nameKey = ConventionSymbols.MetadataName(type);
        ValidateDeclaredOwners(type, nameKey, project, conventional, declarations);
        if (IsInvalidConsumerRedefinition(type, local, conventional, nameKey, declarations, hasDirectAttribute, hasProducerDeclaration))
            InvalidOwner(type, nameKey, "a consumer redefinition of referenced ownership", declarations[0].Location);

        _ = DeclaredKind(type);

        if (_invalidOwners.Contains(type)) return null;
        if (project is null) return null;
        if (declarations.Count > 0)
        {
            var chosen = declarations[0];
            var hasFolderDomain = project.DomainLayout == ConventionProject.FoldersLayout;
            var hasExplicitDomain = project.HasExplicitDomain || declarations.Any(declaration => declaration.ExplicitDomain);
            return new(type.ContainingAssembly.Name, chosen.EffectiveDomain, chosen.Name, role.Role, role.Namespace,
                hasFolderDomain, hasExplicitDomain);
        }
        if (conventional is not { } inferred) return null;
        return new(type.ContainingAssembly.Name, inferred.Domain, inferred.Subdomain, role.Role, role.Namespace,
            inferred.HasFolderDomain, project.HasExplicitDomain);
    }

    private readonly record struct DeclaredOwnerEntry(string EffectiveDomain, string Name, string OwnershipReason,
        Location Location, bool ConsumerOverride, bool ExplicitDomain);

    private static DeclaredOwnerEntry CreateDeclaredOwner(string declaredDomain, string defaultDomain, string name,
        string reason, Location location, bool consumerOverride)
    {
        var explicitDomain = declaredDomain.Length > 0;
        return new(explicitDomain ? declaredDomain : defaultDomain, name, reason, location, consumerOverride, explicitDomain);
    }

    private (List<DeclaredOwnerEntry> Declarations, bool HasDirectAttribute, bool HasProducerDeclaration) CollectDeclaredOwners(
        INamedTypeSymbol type, bool local, ref ConventionProject? project, string conventionalDomain)
    {
        var declarations = new List<DeclaredOwnerEntry>();
        var defaultDomain = project?.DomainLayout == ConventionProject.FoldersLayout ? conventionalDomain : project?.DefaultDomain ?? "";
        var hasDirectAttribute = CollectAttributeOwners(type, defaultDomain, declarations);
        var producer = local ? null : ReferenceOwnership(type);
        var hasProducerDeclaration = producer is { Declarations.Count: > 0 };
        if (producer is not null)
        {
            foreach (var declaration in producer.Declarations)
                declarations.Add(new(declaration.EffectiveDomain, declaration.Subdomain, declaration.Reason,
                    Microsoft.CodeAnalysis.Location.None, ConsumerOverride: false, declaration.ExplicitDomain));
        }

        var nameKey = ConventionSymbols.MetadataName(type);
        if (_contexts.TryGetValue(nameKey, out var context) && context.Get(ConventionInputSyntax.SubdomainField).Length > 0)
            declarations.Insert(0, CreateDeclaredOwner(context.Get(ConventionInputSyntax.DomainField), defaultDomain,
                context.Get(ConventionInputSyntax.SubdomainField), context.Get(ConventionInputSyntax.ReasonField),
                context.Location, consumerOverride: !local));
        if (_ownership.TryGetValue(nameKey, out var ownership))
        {
            declarations.Insert(0, CreateDeclaredOwner(ownership.Get(ConventionInputSyntax.DomainField), defaultDomain,
                ownership.Get(ConventionInputSyntax.SubdomainField), ownership.Get(ConventionInputSyntax.ReasonField),
                ownership.Location, consumerOverride: !local));
            project ??= Project;
        }

        // A consumer may fill a missing external fact, but cannot redefine an established owner.
        if (_externalOwners.TryGetValue(type, out var external))
        {
            declarations.Add(CreateDeclaredOwner(Domain(external), defaultDomain, (string)external.ConstructorArguments[1].Value!,
                Reason(external), AttributeLocation(external, type), consumerOverride: true));
            project ??= Project;
        }

        return (declarations, hasDirectAttribute, hasProducerDeclaration);
    }

    private bool CollectAttributeOwners(INamedTypeSymbol type, string defaultDomain, List<DeclaredOwnerEntry> declarations)
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
            declarations.Add(CreateDeclaredOwner(Domain(attribute), defaultDomain, name, Reason(attribute),
                AttributeLocation(attribute, type), consumerOverride: false));
        }
        return hasDirectAttribute;
    }

    private void ValidateDeclaredOwners(INamedTypeSymbol type, string nameKey, ConventionProject? project,
        (string Domain, string Subdomain, bool HasFolderDomain)? conventional, List<DeclaredOwnerEntry> declarations)
    {
        if (declarations.Select(item => (item.EffectiveDomain, item.Name)).Distinct().Skip(1).Any())
            InvalidOwner(type, nameKey, "contradictory", declarations[0].Location, declarations.Select(item => item.Location));
        var requireDomain = project?.DomainLayout == ConventionProject.FoldersLayout;
        foreach (var declaration in declarations)
        {
            var invalidDomain = (requireDomain || declaration.ExplicitDomain) &&
                !Microsoft.CodeAnalysis.CSharp.SyntaxFacts.IsValidIdentifier(declaration.EffectiveDomain);
            var differsFromConventional = conventional is null ||
                declaration.Name != conventional.Value.Subdomain ||
                declaration.EffectiveDomain != conventional.Value.Domain;
            if (invalidDomain || !Microsoft.CodeAnalysis.CSharp.SyntaxFacts.IsValidIdentifier(declaration.Name) ||
                (differsFromConventional || declaration.ConsumerOverride) && string.IsNullOrWhiteSpace(declaration.OwnershipReason))
                InvalidOwner(type, nameKey, "malformed or missing its exceptional reason", declaration.Location);
        }
    }

    private bool IsInvalidConsumerRedefinition(INamedTypeSymbol type, bool local,
        (string Domain, string Subdomain, bool HasFolderDomain)? conventional, string nameKey,
        List<DeclaredOwnerEntry> declarations, bool hasDirectAttribute, bool hasProducerDeclaration) =>
        !local && conventional is { } inferred && !hasDirectAttribute && !hasProducerDeclaration &&
        (_ownership.ContainsKey(nameKey) || _contexts.ContainsKey(nameKey) || _externalOwners.ContainsKey(type)) &&
        declarations.Any(declaration => declaration.ConsumerOverride &&
            (declaration.Name != inferred.Subdomain || declaration.EffectiveDomain != inferred.Domain));

    internal bool UsesOwnSubdomain(INamedTypeSymbol consumer, INamedTypeSymbol entity)
    {
        if (Owner(consumer) is not { } owner || Owner(entity) is not { } entityOwner) return false;
        if (SymbolEqualityComparer.Default.Equals(consumer.ContainingAssembly, entity.ContainingAssembly))
            return owner.SameOwner(entityOwner);
        if (!owner.MatchesAcrossLayers(entityOwner)) return false;
        // A referencing layer can implement a Domain subdomain without sharing its project name.
        // Separate Domain assemblies retain distinct ownership even when subdomain names coincide.
        if (owner.Role is not ("Infra" or "Application" or "Hosted") || entityOwner.Role != "Domain") return false;
        return ReachableAssemblies(consumer.ContainingAssembly).Contains(entity.ContainingAssembly);
    }

    private HashSet<IAssemblySymbol> ReachableAssemblies(IAssemblySymbol root)
    {
        if (_reachableAssemblies.TryGetValue(root, out var reachable)) return reachable;
        reachable = new(SymbolEqualityComparer.Default);
        var pending = new Stack<IAssemblySymbol>();
        pending.Push(root);
        while (pending.TryPop(out var assembly))
        {
            Cancellation.ThrowIfCancellationRequested();
            if (!reachable.Add(assembly)) continue;
            foreach (var module in assembly.Modules)
            {
                foreach (var reference in module.ReferencedAssemblySymbols)
                    pending.Push(reference);
            }
        }
        _reachableAssemblies.Add(root, reachable);
        return reachable;
    }

    internal static int OwnerSegmentOffset(string role, string[] parts) =>
        parts.Length > 0 &&
        (role == ConventionArchitecture.Hosted && parts[0] is "Controllers" or "Pages" ||
         role == ConventionArchitecture.Application && parts[0] == "Services")
            ? 1
            : 0;

    private (string Domain, string Subdomain, bool HasFolderDomain)? InferOwner(
        INamedTypeSymbol type, string role, string rootNamespace, ConventionProject project)
    {
        var ns = type.ContainingNamespace.ToDisplayString();
        if (rootNamespace.Length == 0 || !ns.StartsWith(rootNamespace + ".", StringComparison.Ordinal)) return null;
        var parts = ns[(rootNamespace.Length + 1)..].Split('.');
        var index = OwnerSegmentOffset(role, parts);
        var isFoldersLayout = project.DomainLayout == ConventionProject.FoldersLayout;
        if (ConventionNames.InferNamespaceOwner(parts, index, isFoldersLayout, project.DefaultDomain) is not { } inferred)
            return null;
        if (role == ConventionArchitecture.Domain &&
            ConventionNames.IsMissingAggregateSubdomain(parts, index, isFoldersLayout, type.Name) &&
            Symbols.IsKnown(type, Symbols.Aggregate, Cancellation))
            return null;
        return inferred;
    }

    internal ConventionProject? ReferenceProject(IAssemblySymbol assembly)
    {
        if (_referenceProjects.TryGetValue(assembly, out var existing)) return existing;
        var metadata = AssemblyMetadata(assembly);
        var parsed = ConventionNames.ParseReferenceMetadata(assembly.Name, metadata);
        if (!parsed.Valid)
        {
            Diagnostics.Add(ConventionProject.InputIssue(assembly.Name + ":" + parsed.ErrorProperty,
                parsed.ErrorProblem, parsed.ErrorRemedy));
            _referenceProjects.Add(assembly, null);
            return null;
        }
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["build_property.MSBuildProjectName"] = parsed.ProjectName,
            ["build_property.RootNamespace"] = parsed.RootNamespace
        };
        if (parsed.DomainLayout.Length > 0) values["build_property.DrnDomainLayout"] = parsed.DomainLayout;
        if (parsed.Domain.Length > 0) values["build_property.DrnDomain"] = parsed.Domain;
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
                ConventionNames.TryReadAssemblyMetadata(attribute, out var entry)) metadata.Add(entry);
        }
        var result = metadata.ToImmutable();
        Cancellation.ThrowIfCancellationRequested();
        _assemblyMetadata.Add(assembly, result);
        return result;
    }

    internal static string Reason(AttributeData attribute) =>
        attribute.NamedArguments.FirstOrDefault(pair => pair.Key == "Reason").Value.Value as string ?? "";

    internal static string Domain(AttributeData attribute) =>
        attribute.NamedArguments.FirstOrDefault(pair => pair.Key == "Domain").Value.Value as string ?? "";

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
