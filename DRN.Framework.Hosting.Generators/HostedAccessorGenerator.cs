using System.Collections.Immutable;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace DRN.Framework.Hosting.Generators;

[Generator(LanguageNames.CSharp)]
public sealed class HostedAccessorGenerator : IIncrementalGenerator
{
    private const string AttributeNamespace = "DRN.Framework.Hosting.Endpoints.";
    private static readonly DiagnosticDescriptor Invalid = new("DRNHG001", "Invalid hosted accessor declaration",
        "{0}", "Hosting", DiagnosticSeverity.Error, true);
    private static readonly SymbolDisplayFormat BindingTypeFormat = SymbolDisplayFormat.FullyQualifiedFormat.WithMiscellaneousOptions(
        SymbolDisplayFormat.FullyQualifiedFormat.MiscellaneousOptions & ~SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);
    private static readonly Regex RazorComments = new(@"@\*[\s\S]*?\*@", RegexOptions.NonBacktracking);
    private static readonly Regex PageDirective = new(@"^[\t ]*@page(?:[\t ]|\r?$)", RegexOptions.Multiline | RegexOptions.NonBacktracking);
    private static readonly PageInput DisabledPage = new(null, false);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var hostedProject = context.AnalyzerConfigOptionsProvider.Select(static (options, _) =>
            options.GlobalOptions.TryGetValue("build_property.UsingMicrosoftNETSdkWeb", out var value) &&
            string.Equals(value, "true", StringComparison.OrdinalIgnoreCase));
        var programs = context.SyntaxProvider.CreateSyntaxProvider(
                static (node, _) => node is ClassDeclarationSyntax { BaseList: not null } declaration &&
                    declaration.Identifier.ValueText.EndsWith("Program", StringComparison.Ordinal),
                static (syntax, cancellation) => FindProgram(syntax, cancellation))
            .Where(static program => program != null)
            .Select(static (program, _) => program!)
            .Collect();
        var pagesEnabled = programs.Select(static (candidates, _) => !candidates.IsEmpty)
            .Combine(hostedProject).Select(static (input, _) => input.Left && input.Right);
        var pages = context.AdditionalTextsProvider
            .Where(static file => file.Path.EndsWith(".cshtml", StringComparison.OrdinalIgnoreCase))
            .Combine(context.AnalyzerConfigOptionsProvider)
            .Combine(pagesEnabled)
            .Select(static (input, cancellation) =>
            {
                if (!input.Right) return DisabledPage;
                var file = input.Left.Left;
                var configuration = input.Left.Right;
                var options = configuration.GetOptions(file);
                options.TryGetValue("build_metadata.AdditionalFiles.TargetPath", out var target);
                configuration.GlobalOptions.TryGetValue("build_property.MSBuildProjectDirectory", out var directory);
                if (!string.IsNullOrEmpty(target) && !target.EndsWith(".cshtml", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(target));
                        if (decoded.EndsWith(".cshtml", StringComparison.OrdinalIgnoreCase)) target = decoded;
                    }
                    catch (FormatException) { /* Plain TargetPath values are supported too. */ }
                }
                target ??= directory == null ? null : Path.GetRelativePath(directory, file.Path);
                cancellation.ThrowIfCancellationRequested();
                var content = RazorComments.Replace(file.GetText(cancellation)?.ToString() ?? "", "");
                cancellation.ThrowIfCancellationRequested();
                var isPage = PageDirective.IsMatch(content);
                cancellation.ThrowIfCancellationRequested();
                return new PageInput(target?.Replace('\\', '/'), isPage);
            }).WithTrackingName("HostedAccessorPageIdentity").Collect();
        var inputs = context.CompilationProvider.Combine(programs).Combine(pages).Combine(hostedProject)
            .WithTrackingName("HostedAccessorInputs");
        context.RegisterSourceOutput(inputs, static (output, input) =>
        {
            if (!input.Right) return;
            var compilation = input.Left.Left.Left;
            var candidates = input.Left.Left.Right;
            if (candidates.IsEmpty) return;
            // The common single-program case needs neither a deduplication set nor a sort.
            var hosts = candidates;
            if (candidates.Length > 1)
                hosts = candidates.Distinct<INamedTypeSymbol>(SymbolEqualityComparer.Default)
                    .OrderBy(static program => program.ToDisplayString(), StringComparer.Ordinal).ToImmutableArray();
            var generatedSources = new Dictionary<string, string>(StringComparer.Ordinal);
            CompilationFacts? facts = null;
            foreach (var program in hosts)
            {
                output.CancellationToken.ThrowIfCancellationRequested();
                try
                {
                    facts ??= new CompilationFacts(compilation, output.CancellationToken);
                    new Emitter(compilation, facts, output, program, input.Left.Right, generatedSources).Emit();
                }
                catch (AccessorException exception)
                {
                    output.ReportDiagnostic(Diagnostic.Create(Invalid, program.Locations[0], exception.Message));
                }
            }
        });
    }

    private static INamedTypeSymbol? FindProgram(GeneratorSyntaxContext syntax, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        if (syntax.SemanticModel.GetDeclaredSymbol(syntax.Node, cancellation) is not INamedTypeSymbol program ||
            program.IsAbstract || program.Arity != 0 || program.ContainingType != null || program.IsFileLocal ||
            program.DeclaredAccessibility != Accessibility.Public)
            return null;
        var definition = syntax.SemanticModel.Compilation.GetTypeByMetadataName(DrnProgramConvention.BaseMetadataName);
        if (definition == null) return null;
        var programBase = DrnProgramConvention.FindBase(program, definition);
        return programBase != null && SymbolEqualityComparer.Default.Equals(programBase.TypeArguments[0], program)
            ? program : null;
    }

    private sealed record PageInput(string? Path, bool IsPage);
    private sealed class AccessorException(string message) : Exception(message);
    private static IEnumerable<AttributeData> Attributes(ISymbol symbol, string name) =>
        symbol.GetAttributes().Where(attribute => attribute.AttributeClass?.ToDisplayString() == AttributeNamespace + name);
    private static string? Text(AttributeData? attribute, string name) =>
        attribute?.NamedArguments.FirstOrDefault(pair => pair.Key == name).Value.Value as string;
    private static bool Flag(AttributeData? attribute, string name) =>
        attribute?.NamedArguments.FirstOrDefault(pair => pair.Key == name).Value.Value is true;
    private static string Literal(string value) => SymbolDisplay.FormatLiteral(value, true);
    private static string ArgumentText(AttributeData attribute, int index) =>
        attribute.ConstructorArguments.Length > index && attribute.ConstructorArguments[index].Value is string { Length: > 0 } text
            ? text : throw new AccessorException($"{attribute.AttributeClass?.Name} requires a nonempty path or type name.");
    private static string Identifier(string value)
    {
        if (SyntaxFacts.GetKeywordKind(value) != SyntaxKind.None || SyntaxFacts.GetContextualKeywordKind(value) != SyntaxKind.None)
            return "@" + value;
        if (!SyntaxFacts.IsValidIdentifier(value)) throw new AccessorException($"'{value}' is not a C# identifier. Supply an accessor alias.");
        return value;
    }
    private static bool CanUseIdentifier(string value) => SyntaxFacts.IsValidIdentifier(value) ||
        SyntaxFacts.GetKeywordKind(value) != SyntaxKind.None || SyntaxFacts.GetContextualKeywordKind(value) != SyntaxKind.None;
    private static string TypeName(ITypeSymbol type) => type.TypeKind == TypeKind.Dynamic ? "global::System.Object" :
        type.ToDisplayString(BindingTypeFormat);

    private sealed class CompilationFacts
    {
        private static readonly string[] FrameworkNames =
            ["ApiEndpoint", "EndpointAction", "EndpointActionAttribute", "ControllerForBase", "EndpointCollectionBase", "PageCollectionBase", "PageForBase"];
        private readonly Compilation _compilation;
        private readonly CancellationToken _cancellation;
        private readonly HashSet<string> _nameConflicts = new(StringComparer.Ordinal);
        private readonly Dictionary<string, HashSet<string>> _namespaceConflicts = new(StringComparer.Ordinal);
        private readonly Dictionary<ISymbol, Dictionary<string, List<AttributeData>>> _programAttributes = new(SymbolEqualityComparer.Default);
        private readonly Dictionary<ITypeSymbol, List<string>> _endpointCollections = new(SymbolEqualityComparer.Default);

        public CompilationFacts(Compilation compilation, CancellationToken cancellation)
        {
            _compilation = compilation;
            _cancellation = cancellation;
            cancellation.ThrowIfCancellationRequested();
            var controllers = new SortedDictionary<string, INamedTypeSymbol>(StringComparer.Ordinal);
            var collectionBase = compilation.GetTypeByMetadataName("DRN.Framework.Hosting.Endpoints.EndpointCollection`1");
            foreach (var type in Types(compilation.Assembly.GlobalNamespace, cancellation))
            {
                if (IsControllerCandidate(type)) controllers.TryAdd(type.ToDisplayString(), type);
                if (collectionBase == null) continue;
                foreach (var nested in SelfAndNestedTypes(type, cancellation))
                for (var current = nested; current != null; current = current.BaseType)
                {
                    cancellation.ThrowIfCancellationRequested();
                    if (!SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, collectionBase)) continue;
                    var owner = current.TypeArguments[0];
                    if (!_endpointCollections.TryGetValue(owner, out var collections))
                        _endpointCollections.Add(owner, collections = []);
                    collections.Add(nested.ToDisplayString());
                    break;
                }
            }
            Controllers = controllers.ToArray();
            foreach (var attribute in compilation.Assembly.GetAttributes())
            {
                cancellation.ThrowIfCancellationRequested();
                var name = attribute.AttributeClass?.Name;
                if (name is not ("AccessorControllerAttribute" or "AccessorGroupAttribute" or "AccessorMemberAttribute") ||
                    attribute.AttributeClass!.ToDisplayString() != AttributeNamespace + name ||
                    attribute.ConstructorArguments.FirstOrDefault().Value is not INamedTypeSymbol program) continue;
                if (!_programAttributes.TryGetValue(program, out var attributes))
                    _programAttributes.Add(program, attributes = new(StringComparer.Ordinal));
                if (!attributes.TryGetValue(name, out var declarations)) attributes.Add(name, declarations = []);
                declarations.Add(attribute);
                // Programs can share a generated namespace. Their aliases must not shadow each other's contracts.
                if (name == "AccessorGroupAttribute" && attribute.ConstructorArguments.ElementAtOrDefault(2).Value is string groupName &&
                    Array.IndexOf(FrameworkNames, groupName) >= 0)
                    _nameConflicts.Add(groupName);
                if (name == "AccessorControllerAttribute" && Text(attribute, "TypeName") is { } typeName && Array.IndexOf(FrameworkNames, typeName) >= 0)
                    _nameConflicts.Add(typeName);
            }
            foreach (var name in FrameworkNames)
            {
                cancellation.ThrowIfCancellationRequested();
                if (compilation.GetSymbolsWithName(name, SymbolFilter.Type | SymbolFilter.Namespace, cancellation).Any()) _nameConflicts.Add(name);
            }
            foreach (var tree in compilation.SyntaxTrees)
            {
                cancellation.ThrowIfCancellationRequested();
                var root = (CompilationUnitSyntax)tree.GetRoot(cancellation);
                SemanticModel? model = null;
                // Valid global imports occur only at compilation-unit scope. Method bodies are irrelevant.
                foreach (var directive in root.Usings)
                {
                    if (!directive.GlobalKeyword.IsKind(SyntaxKind.GlobalKeyword)) continue;
                    cancellation.ThrowIfCancellationRequested();
                    if (directive.Alias != null)
                    {
                        var alias = directive.Alias.Name.Identifier.ValueText;
                        if (Array.IndexOf(FrameworkNames, alias) >= 0) _nameConflicts.Add(alias);
                        continue;
                    }
                    if (directive.Name == null) continue;
                    model ??= compilation.GetSemanticModel(tree);
                    var imported = model.GetSymbolInfo(directive.Name, cancellation).Symbol;
                    foreach (var name in FrameworkNames)
                    {
                        if (_nameConflicts.Contains(name)) continue;
                        var frameworkName = name == "EndpointAction" ? "EndpointActionAttribute" : name;
                        var conflict = imported switch
                        {
                            INamespaceSymbol importedNamespace => importedNamespace.GetMembers(name).Any(symbol =>
                                symbol is not INamedTypeSymbol named || named.Name != frameworkName || named.ContainingNamespace.ToDisplayString() != AttributeNamespace[..^1]),
                            INamedTypeSymbol importedType => importedType.GetTypeMembers(name).Length > 0,
                            _ => false
                        };
                        if (conflict) _nameConflicts.Add(name);
                    }
                }
            }
        }

        public KeyValuePair<string, INamedTypeSymbol>[] Controllers { get; }

        public bool HasOtherEndpointCollection(INamedTypeSymbol program, string rootName)
        {
            if (!_endpointCollections.TryGetValue(program, out var collections)) return false;
            foreach (var collection in collections)
                if (collection != rootName) return true;
            return false;
        }

        public IEnumerable<AttributeData> ForProgram(INamedTypeSymbol program, string name) =>
            _programAttributes.TryGetValue(program, out var attributes) && attributes.TryGetValue(name, out var declarations) ? declarations : [];

        public bool Conflicts(string ns, string name)
        {
            if (_nameConflicts.Contains(name)) return true;
            if (!_namespaceConflicts.TryGetValue(ns, out var conflicts))
            {
                conflicts = new(StringComparer.Ordinal);
                var scope = _compilation.GlobalNamespace;
                AddConflicts(scope);
                foreach (var segment in ns.Replace("@", "").Split('.'))
                {
                    _cancellation.ThrowIfCancellationRequested();
                    var nested = scope.GetNamespaceMembers().FirstOrDefault(candidate => candidate.Name == segment);
                    if (nested == null) break;
                    scope = nested;
                    AddConflicts(scope);
                }
                _namespaceConflicts.Add(ns, conflicts);

                void AddConflicts(INamespaceSymbol current)
                {
                    foreach (var candidate in FrameworkNames)
                        if (current.GetMembers(candidate).Any()) conflicts.Add(candidate);
                }
            }
            return conflicts.Contains(name);
        }
    }

    private static IEnumerable<INamedTypeSymbol> Types(INamespaceSymbol ns, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        foreach (var type in ns.GetTypeMembers())
        {
            cancellation.ThrowIfCancellationRequested();
            yield return type;
        }
        foreach (var child in ns.GetNamespaceMembers())
        foreach (var type in Types(child, cancellation)) yield return type;
    }

    private static IEnumerable<INamedTypeSymbol> SelfAndNestedTypes(INamedTypeSymbol type, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        yield return type;
        foreach (var child in type.GetTypeMembers())
        foreach (var nested in SelfAndNestedTypes(child, cancellation)) yield return nested;
    }

    private sealed class Node(string path, string name)
    {
        public string Path { get; } = path;
        public string Name { get; } = name;
        public SortedDictionary<string, Node> Children { get; } = new(StringComparer.Ordinal);
        public SortedDictionary<string, string> Members { get; } = new(StringComparer.Ordinal);
        public INamedTypeSymbol? Controller { get; set; }
        public string Route { get; set; } = "";
        public string? Area { get; set; }
    }

    private sealed class Emitter(Compilation compilation, CompilationFacts facts, SourceProductionContext output, INamedTypeSymbol program,
        ImmutableArray<PageInput> pages, Dictionary<string, string> generatedSources)
    {
        private readonly Dictionary<string, AttributeData> _members = new(StringComparer.Ordinal);
        private readonly HashSet<string> _usedMembers = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _groups = new(StringComparer.Ordinal);
        private readonly HashSet<string> _usedGroups = new(StringComparer.Ordinal);

        public void Emit()
        {
            var ns = program.ContainingNamespace.IsGlobalNamespace ? "Helpers" : program.ContainingNamespace.ToDisplayString() + ".Helpers";
            var stem = program.Name == "Program" ? "App" : program.Name[..^7];
            Identifier(stem);
            foreach (var attribute in ForProgram("AccessorMemberAttribute"))
                if (!_members.TryAdd(ArgumentText(attribute, 1), attribute))
                    throw new AccessorException("Duplicate AccessorMember path. Remove or combine the declarations.");
            foreach (var attribute in ForProgram("AccessorGroupAttribute"))
                if (!_groups.TryAdd(ArgumentText(attribute, 1), ArgumentText(attribute, 2)))
                    throw new AccessorException("Duplicate AccessorGroup path. Remove or combine the declarations.");

            var endpoints = new Node("", stem + "EndpointFor");
            if (facts.HasOtherEndpointCollection(program, ns.Replace("@", "") + ".EndpointFor." + endpoints.Name))
                throw new AccessorException($"'{program}' already has another endpoint collection. Adopt that root explicitly instead of generating a second collection.");
            AddControllers(endpoints);
            var pageRoot = new Node("", stem + "PageFor");
            AddPages(pageRoot);
            foreach (var path in _members.Keys.Except(_usedMembers).Order(StringComparer.Ordinal))
                throw new AccessorException($"AccessorMember '{path}' does not identify an eligible action/page. Remove stale configuration.");
            foreach (var path in _groups.Keys.Except(_usedGroups).Order(StringComparer.Ordinal))
                throw new AccessorException($"AccessorGroup '{path}' does not identify a generated group.");

            // Validate the entire host before publishing any source for it.
            var sources = new SortedDictionary<string, string>(StringComparer.Ordinal);
            Render(endpoints, ns + ".EndpointFor", false, true, sources);
            Render(pageRoot, ns + ".PageFor", true, true, sources);
            var metadataNamespace = ns.Replace("@", "");
            var endpointRootName = metadataNamespace + ".EndpointFor." + endpoints.Name;
            var pageRootName = metadataNamespace + ".PageFor." + pageRoot.Name;
            foreach (var source in sources)
                if (generatedSources.TryGetValue(source.Key, out var previous) &&
                    (source.Key == endpointRootName || source.Key == pageRootName || previous != source.Value))
                    throw new AccessorException($"Generated type '{source.Key}' collides with another program's accessors.");
            var pageNamespace = metadataNamespace + ".PageFor.";
            foreach (var source in sources)
            {
                output.CancellationToken.ThrowIfCancellationRequested();
                // Shared leaf types still have separate instances under each program's singleton root.
                if (!generatedSources.TryAdd(source.Key, source.Value)) continue;
                var folder = source.Key.StartsWith(pageNamespace, StringComparison.Ordinal) ? "PageFor/" : "EndpointFor/";
                output.AddSource("Helpers/" + folder + source.Key + ".g.cs", SourceText.From(source.Value, Encoding.UTF8));
            }
        }

        private IEnumerable<AttributeData> ForProgram(string name) => facts.ForProgram(program, name);

        private void AddControllers(Node root)
        {
            var mappings = new Dictionary<string, AttributeData>(StringComparer.Ordinal);
            foreach (var mapping in ForProgram("AccessorControllerAttribute"))
            {
                output.CancellationToken.ThrowIfCancellationRequested();
                var controller = mapping.ConstructorArguments.Length > 1 ? mapping.ConstructorArguments[1].Value as INamedTypeSymbol : null;
                if (controller == null) throw new AccessorException("AccessorController requires a controller type.");
                if (!mappings.TryAdd(controller.ToDisplayString(), mapping))
                    throw new AccessorException($"Controller '{controller}' has duplicate mappings for '{program}'.");
            }
            IEnumerable<KeyValuePair<string, INamedTypeSymbol>> controllers = facts.Controllers;
            if (mappings.Count > 0)
                controllers = controllers.Concat(mappings.Select(static mapping =>
                        new KeyValuePair<string, INamedTypeSymbol>(mapping.Key, (INamedTypeSymbol)mapping.Value.ConstructorArguments[1].Value!)))
                    .DistinctBy(static controller => controller.Key, StringComparer.Ordinal)
                    .OrderBy(static controller => controller.Key, StringComparer.Ordinal);
            foreach (var candidate in controllers)
            {
                output.CancellationToken.ThrowIfCancellationRequested();
                var controller = candidate.Value;
                mappings.TryGetValue(candidate.Key, out var mapping);
                if (Flag(mapping, "Exclude")) continue;
                if (!IsController(controller)) throw new AccessorException($"'{controller}' is not an eligible MVC controller. Exclude it or correct the mapping.");
                var controllerName = controller.Name.EndsWith("Controller", StringComparison.Ordinal) ? controller.Name[..^10] : controller.Name;
                var prefix = program.ContainingNamespace + ".Controllers";
                var controllerNamespace = controller.ContainingNamespace.ToDisplayString();
                var group = controllerNamespace.StartsWith(prefix + ".", StringComparison.Ordinal) ? controllerNamespace[(prefix.Length + 1)..] : "";
                var path = mapping == null || mapping.ConstructorArguments.Length == 2
                    ? (group.Length == 0 ? controllerName : group + "." + controllerName)
                    : ArgumentText(mapping, 2);
                var parts = path.Split('.');
                // Endpoint collection binding starts at the first group and traverses at most five group levels.
                if (parts.Length > 6)
                    throw new AccessorException($"Accessor '{path}' exceeds the runtime discovery limit of five groups. Supply a shorter controller alias.");
                var parent = Group(root, parts[..^1], false);
                var leaf = new Node(path, Text(mapping, "TypeName") ?? controllerName + "For")
                {
                    Controller = controller, Route = Text(mapping, "RouteTemplate") ?? ControllerRouteTemplate(controller)
                };
                if (!parent.Children.TryAdd(parts[^1], leaf)) throw new AccessorException($"Accessor '{path}' collides. Supply a controller alias.");
                foreach (var method in Actions(controller, output.CancellationToken))
                {
                    var member = MemberName("Endpoint." + path + "." + method.Name, method.Name);
                    if (member == null) continue;
                    if (method.Parameters.Any(parameter => parameter.RefKind != RefKind.None || parameter.Type.TypeKind is TypeKind.Pointer or TypeKind.FunctionPointer))
                        throw new AccessorException($"'{controller}.{method.Name}' has unsupported parameters. Exclude its accessor and bind it manually.");
                    var binding = $"[global::{AttributeNamespace}EndpointAction({Literal(method.Name)}{string.Concat(method.Parameters.Select(parameter => ", typeof(" + TypeName(parameter.Type) + ")"))})]";
                    var declaration = binding + "\n    public global::DRN.Framework.Hosting.Endpoints.ApiEndpoint " + Identifier(member) + " { get; private set; } = null!;";
                    if (!leaf.Members.TryAdd(member, declaration))
                        throw new AccessorException($"Action accessor '{path}.{member}' collides, possibly due to overloads. Exclude that method name and provide manual bindings.");
                }
            }
        }

        private string? MemberName(string path, string fallback)
        {
            if (!_members.TryGetValue(path, out var mapping)) return fallback;
            _usedMembers.Add(path);
            return Flag(mapping, "Exclude") ? null : Text(mapping, "Name") ?? fallback;
        }

        private void AddPages(Node root)
        {
            foreach (var page in pages.OrderBy(page => page.Path, StringComparer.Ordinal))
            {
                output.CancellationToken.ThrowIfCancellationRequested();
                if (!page.IsPage) continue;
                var path = page.Path?.TrimStart('/');
                if (path == null || path.Split('/').Contains(".."))
                    throw new AccessorException("A Razor page has no valid logical TargetPath. Supply AdditionalFiles TargetPath metadata.");
                string? area = null;
                string relative;
                if (path.StartsWith("Pages/", StringComparison.Ordinal)) relative = path[6..^7];
                else if (path.StartsWith("Areas/", StringComparison.Ordinal))
                {
                    var parts = path.Split('/');
                    if (parts.Length < 4 || parts[2] != "Pages") throw new AccessorException($"Unsupported Razor location '{path}'. Use Pages or Areas/<area>/Pages.");
                    area = parts[1];
                    relative = string.Join("/", parts.Skip(3))[..^7];
                }
                else throw new AccessorException($"Unsupported Razor location '{path}'. Use explicit logical TargetPath metadata.");
                var segments = relative.Split('/');
                var groups = segments.Length == 1 ? new[] { "Root" } : segments[..^1];
                if (area != null) groups = ["Areas", area, .. groups];
                var member = MemberName("Page." + string.Join(".", groups.Append(segments[^1])), segments[^1]);
                if (member == null && groups.Any(segment => !CanUseIdentifier(segment))) continue;
                // Valid excluded groups still host handwritten partial replacements.
                var node = Group(root, groups, true);
                node.Area = area;
                if (member == null) continue;
                var declaration = $"public string {Identifier(member)} {{ get; init; }} = {Literal("/" + relative)};";
                if (!node.Members.TryAdd(member, declaration)) throw new AccessorException($"Page accessor '{path}' collides. Supply a member alias or exclusion.");
            }
        }

        private Node Group(Node root, string[] segments, bool page)
        {
            var current = root;
            foreach (var segment in segments)
            {
                Identifier(segment);
                var path = current.Path.Length == 0 ? segment : current.Path + "." + segment;
                if (!current.Children.TryGetValue(segment, out var child))
                {
                    var name = path.Replace(".", "") + (page ? "PageFor" : "ApiFor");
                    if (!page && _groups.TryGetValue(path, out var configured)) { name = configured; _usedGroups.Add(path); }
                    child = new Node(path, name);
                    current.Children.Add(segment, child);
                }
                if (child.Controller != null) throw new AccessorException($"Group '{path}' collides with a controller accessor.");
                current = child;
            }
            return current;
        }

        private void Render(Node node, string ns, bool page, bool root, SortedDictionary<string, string> sources)
        {
            output.CancellationToken.ThrowIfCancellationRequested();
            var frameworkTypes = new Dictionary<string, string>(StringComparer.Ordinal);
            var usesFrameworkImport = false;
            string FrameworkType(string typeName)
            {
                if (frameworkTypes.TryGetValue(typeName, out var resolved)) return resolved;
                // Authored and generated names must not shadow imported framework contracts.
                var shortName = typeName == "EndpointActionAttribute" ? "EndpointAction" : typeName;
                bool Conflicts(string candidate) =>
                    facts.Conflicts(ns, candidate) || node.Name == candidate || node.Members.ContainsKey(candidate) || node.Children.ContainsKey(candidate);
                var collision = Conflicts(typeName) || shortName != typeName && Conflicts(shortName);
                usesFrameworkImport |= !collision;
                return frameworkTypes[typeName] = collision ? "global::" + AttributeNamespace + typeName : shortName;
            }

            var name = Identifier(node.Name);
            var fullName = ns.Replace("@", "") + "." + node.Name;
            var existing = compilation.GetTypeByMetadataName(fullName);
            if (existing != null && (existing.DeclaringSyntaxReferences.Length == 0 || existing.DeclaringSyntaxReferences.Any(reference =>
                    reference.GetSyntax(output.CancellationToken) is not ClassDeclarationSyntax declaration || !declaration.Modifiers.Any(SyntaxKind.PartialKeyword))))
                throw new AccessorException($"'{fullName}' already exists and must be a partial class to customize generated accessors.");
            if (root && !page && existing?.GetMembers("EndpointCollection").Length > 0)
                throw new AccessorException($"'{fullName}.EndpointCollection' is reserved for the inherited typed singleton. Remove or rename the authored member.");
            var members = new SortedDictionary<string, string>(node.Members, StringComparer.Ordinal);
            if (node.Area != null && (members.ContainsKey("Area") || existing?.GetMembers("Area").Length > 0))
                throw new AccessorException($"'{fullName}.Area' is reserved for the destination area. Rename the page member.");
            foreach (var child in node.Children)
                if (!members.TryAdd(child.Key, $"public {Identifier(child.Value.Name)} {Identifier(child.Key)} {{ get; }} = new();"))
                    throw new AccessorException($"'{fullName}.{child.Key}' is both a group and a member. Supply an alias.");
            foreach (var member in members.Keys)
                if (member == node.Name || existing?.GetMembers(member).Length > 0 ||
                    (root && member is "All" or "PageCollection" or "EndpointCollection" or "EndpointSource" or "Endpoints" or "ApiEndpoints" or "PageEndpoints") ||
                    (page && member is "PathSegments" or "GetPages") ||
                    (node.Controller != null && member is "Endpoints" or "ControllerRoute" or "Controller" or "ControllerName" or "ControllerClassName"))
                    throw new AccessorException($"'{fullName}.{member}' collides with an authored member. Explicitly exclude the generated member or rename it.");
            var baseType = root
                ? page ? $"{FrameworkType("PageCollectionBase")}<{name}>" : $"{FrameworkType("EndpointCollectionBase")}<{TypeName(program)}, {name}>"
                : page ? FrameworkType("PageForBase") : node.Controller != null ? $"{FrameworkType("ControllerForBase")}<{TypeName(node.Controller)}>" : null;
            var declarations = members.Values.Select(member => node.Controller == null ? member : member
                .Replace("[global::" + AttributeNamespace + "EndpointAction(", "[" + FrameworkType("EndpointActionAttribute") + "(", StringComparison.Ordinal)
                .Replace("public global::" + AttributeNamespace + "ApiEndpoint ", "public " + FrameworkType("ApiEndpoint") + " ", StringComparison.Ordinal)).ToArray();
            var source = new StringBuilder("// <auto-generated/>\n#nullable enable\n");
            if (usesFrameworkImport) source.Append("using global::DRN.Framework.Hosting.Endpoints;\n\n");
            source.Append("namespace ").Append(ns).Append(";\n\npublic partial class ").Append(name);
            if (baseType != null) source.Append(" : ").Append(baseType);
            source.Append("\n{\n");
            if (node.Controller != null) source.Append("    public ").Append(name).Append("() : base(").Append(Literal(node.Route)).Append(") { }\n");
            if (page && !root) source.Append("    protected override string[] PathSegments { get; } = [];\n");
            if (node.Area != null) source.Append("    public const string Area = ").Append(Literal(node.Area)).Append(";\n");
            foreach (var declaration in declarations)
            {
                output.CancellationToken.ThrowIfCancellationRequested();
                source.Append("    ").Append(declaration).Append('\n');
            }
            source.Append("}\n");
            if (!sources.TryAdd(fullName, source.ToString())) throw new AccessorException($"Generated type '{fullName}' collides. Supply a TypeName or group alias.");
            foreach (var child in node.Children.Values) Render(child, ns, page, false, sources);
        }

        private static bool IsController(INamedTypeSymbol type) => !type.IsAbstract && type.Arity == 0 && type.ContainingType == null &&
            type.DeclaredAccessibility == Accessibility.Public && !HasAttribute(type, "Microsoft.AspNetCore.Mvc.NonControllerAttribute") &&
            Bases(type).Any(candidate => candidate.ToDisplayString() == "Microsoft.AspNetCore.Mvc.ControllerBase");

        private static IEnumerable<IMethodSymbol> Actions(INamedTypeSymbol type, CancellationToken cancellation)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var current in Bases(type))
            foreach (var method in current.GetMembers().OfType<IMethodSymbol>().OrderBy(method => method.ToDisplayString(), StringComparer.Ordinal))
            {
                cancellation.ThrowIfCancellationRequested();
                if (method.MethodKind != MethodKind.Ordinary || method.DeclaredAccessibility != Accessibility.Public) continue;
                var signature = method.Name + "`" + method.Arity + "(" + string.Join(",", method.Parameters.Select(parameter => parameter.RefKind + ":" + TypeName(parameter.Type))) + ")";
                if (!seen.Add(signature) || method.IsStatic || method.IsGenericMethod || method.IsAbstract ||
                    HasAttribute(method, "Microsoft.AspNetCore.Mvc.NonActionAttribute")) continue;
                var original = method;
                while (original.OverriddenMethod != null) original = original.OverriddenMethod;
                if (original.ContainingType.SpecialType == SpecialType.System_Object) continue;
                if (method.Name == "Dispose" && method.Parameters.Length == 0 && type.AllInterfaces.Any(item => item.ToDisplayString() == "System.IDisposable")) continue;
                yield return method;
            }
        }

        private static string ControllerRouteTemplate(INamedTypeSymbol controller)
        {
            var routes = Bases(controller).SelectMany(type => type.GetAttributes())
                .Where(attribute => attribute.AttributeClass?.AllInterfaces.Any(type =>
                    type.ToDisplayString() == "Microsoft.AspNetCore.Mvc.Routing.IRouteTemplateProvider") == true)
                .ToArray();
            // Custom providers and multiple templates require an explicit legacy metadata override.
            if (routes.Any(attribute => attribute.AttributeClass!.ToDisplayString() != "Microsoft.AspNetCore.Mvc.RouteAttribute"))
                return "";
            var templates = routes.Select(attribute => attribute.ConstructorArguments.FirstOrDefault().Value as string)
                .Distinct(StringComparer.Ordinal).ToArray();
            return templates.Length == 1 ? templates[0] ?? "" : "";
        }
    }

    private static bool IsControllerCandidate(INamedTypeSymbol type) => type.TypeKind == TypeKind.Class && !type.IsAbstract &&
        type.Arity == 0 && type.DeclaredAccessibility == Accessibility.Public &&
        !HasAttribute(type, "Microsoft.AspNetCore.Mvc.NonControllerAttribute") &&
        (type.Name.EndsWith("Controller", StringComparison.OrdinalIgnoreCase) || HasAttribute(type, "Microsoft.AspNetCore.Mvc.ControllerAttribute"));

    private static bool HasAttribute(ISymbol symbol, string name)
    {
        for (ISymbol? current = symbol; current != null; current = current switch
             { INamedTypeSymbol type => type.BaseType, IMethodSymbol method => method.OverriddenMethod, _ => null })
            if (current.GetAttributes().Any(attribute => attribute.AttributeClass != null && Bases(attribute.AttributeClass).Any(type => type.ToDisplayString() == name))) return true;
        return false;
    }

    private static IEnumerable<INamedTypeSymbol> Bases(INamedTypeSymbol type)
    {
        for (var current = type; current != null; current = current.BaseType) yield return current;
    }
}
