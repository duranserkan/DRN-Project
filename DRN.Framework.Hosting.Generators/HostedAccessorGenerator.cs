using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace DRN.Framework.Hosting.Generators;

[Generator(LanguageNames.CSharp)]
public sealed partial class HostedAccessorGenerator : IIncrementalGenerator
{
    private const string AttributeNamespace = "DRN.Framework.Hosting.Endpoints.";
    private const string EndpointActionAttributeName = "EndpointActionAttribute";
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
            .Select(static (input, cancellation) => input.Right
                ? ReadPage(input.Left.Left, input.Left.Right, cancellation) : DisabledPage)
            .WithTrackingName("HostedAccessorPageIdentity").Collect();
        var inputs = context.CompilationProvider.Combine(programs).Combine(pages).Combine(hostedProject)
            .WithTrackingName("HostedAccessorInputs");
        var generation = inputs.Select(static (input, cancellation) => input.Right
            ? PreparePrograms(input.Left.Left.Left, input.Left.Left.Right, input.Left.Right, cancellation)
            : new GenerationResult([], []));
        var models = generation.SelectMany(static (result, _) => result.Sources)
            .WithTrackingName("HostedAccessorModels");
        var sources = models.Select(static (model, cancellation) =>
                new GeneratedSource(model.HintName, AccessorRenderer.Render(model, cancellation)))
            .WithTrackingName("HostedAccessorSources");
        context.RegisterSourceOutput(sources, static (output, source) =>
            output.AddSource(source.HintName, SourceText.From(source.Text, Encoding.UTF8)));
        // Locations must follow the current compilation even when source models remain equal.
        context.RegisterSourceOutput(generation.SelectMany(static (result, _) => result.Diagnostics),
            static (output, diagnostic) => output.ReportDiagnostic(diagnostic));
    }

    private static PageInput ReadPage(AdditionalText file, AnalyzerConfigOptionsProvider configuration, CancellationToken cancellation)
    {
        var options = configuration.GetOptions(file);
        options.TryGetValue("build_metadata.AdditionalFiles.TargetPath", out var target);
        configuration.GlobalOptions.TryGetValue("build_property.MSBuildProjectDirectory", out var directory);
        target = DecodeTargetPath(target);
        if (target == null && directory != null) target = Path.GetRelativePath(directory, file.Path);
        cancellation.ThrowIfCancellationRequested();
        var content = RazorComments.Replace(file.GetText(cancellation)?.ToString() ?? "", "");
        cancellation.ThrowIfCancellationRequested();
        var isPage = PageDirective.IsMatch(content);
        cancellation.ThrowIfCancellationRequested();
        return new PageInput(target?.Replace('\\', '/'), isPage);
    }

    private static string? DecodeTargetPath(string? target)
    {
        if (string.IsNullOrEmpty(target) || target.EndsWith(".cshtml", StringComparison.OrdinalIgnoreCase)) return target;
        try
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(target));
            if (decoded.EndsWith(".cshtml", StringComparison.OrdinalIgnoreCase)) return decoded;
        }
        catch (FormatException) { /* Plain TargetPath values are supported too. */ }
        return target;
    }

    private static GenerationResult PreparePrograms(Compilation compilation,
        ImmutableArray<INamedTypeSymbol> candidates, ImmutableArray<PageInput> pages, CancellationToken cancellation)
    {
        if (candidates.IsEmpty) return new([], []);
        // The common single-program case needs neither a deduplication set nor a sort.
        var hosts = candidates;
        if (candidates.Length > 1)
            hosts = candidates.Distinct<INamedTypeSymbol>(SymbolEqualityComparer.Default)
                .OrderBy(static program => program.ToDisplayString(), StringComparer.Ordinal).ToImmutableArray();
        var generatedSources = new Dictionary<string, AccessorModel>(StringComparer.Ordinal);
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
        var orderedPages = pages.OrderBy(static page => page.Path, StringComparer.Ordinal).ToImmutableArray();
        CompilationFacts? facts = null;
        foreach (var program in hosts)
        {
            cancellation.ThrowIfCancellationRequested();
            try
            {
                facts ??= new CompilationFacts(compilation, cancellation, reuseActions: hosts.Length > 1);
                new AccessorModelBuilder(compilation, facts, cancellation, program, orderedPages, generatedSources).Prepare();
            }
            catch (AccessorException exception)
            {
                diagnostics.Add(Diagnostic.Create(Invalid, program.Locations[0], exception.Message));
            }
        }
        return new(generatedSources.Values.OrderBy(static model => model.HintName, StringComparer.Ordinal).ToImmutableArray(),
            diagnostics.ToImmutable());
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
    private sealed record GenerationResult(ImmutableArray<AccessorModel> Sources, ImmutableArray<Diagnostic> Diagnostics);
    private sealed record GeneratedSource(string HintName, string Text);
    [SuppressMessage("SonarQube", "S3871", Justification = "This private generation failure is caught by PreparePrograms and reported as DRNHG001. It never forms part of the generator's public exception contract.")]
    private sealed class AccessorException(string message) : Exception(message);
    private static string? Text(AttributeData? attribute, string name) =>
        attribute?.NamedArguments.FirstOrDefault(pair => pair.Key == name).Value.Value as string;
    private static string Literal(string value) => SymbolDisplay.FormatLiteral(value, true);
    private static string Identifier(string value)
    {
        if (SyntaxFacts.GetKeywordKind(value) != SyntaxKind.None || SyntaxFacts.GetContextualKeywordKind(value) != SyntaxKind.None)
            return "@" + value;
        if (!SyntaxFacts.IsValidIdentifier(value)) throw new AccessorException($"'{value}' is not a C# identifier. Supply an accessor alias.");
        return value;
    }
    private static string TypeName(ITypeSymbol type) => type.TypeKind == TypeKind.Dynamic ? "global::System.Object" :
        type.ToDisplayString(BindingTypeFormat);

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

    private sealed class AccessorModelBuilder(Compilation compilation, CompilationFacts facts, CancellationToken cancellation, INamedTypeSymbol program,
        ImmutableArray<PageInput> pages, Dictionary<string, AccessorModel> generatedSources)
    {
        private readonly Dictionary<string, AttributeData> _members = new(StringComparer.Ordinal);
        private readonly HashSet<string> _usedMembers = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _groups = new(StringComparer.Ordinal);
        private readonly HashSet<string> _usedGroups = new(StringComparer.Ordinal);

        public void Prepare()
        {
            var ns = program.ContainingNamespace.IsGlobalNamespace ? "Helpers" : program.ContainingNamespace.ToDisplayString() + ".Helpers";
            var stem = program.Name == "Program" ? "App" : program.Name[..^7];
            Identifier(stem);
            CollectMappings();
            var endpoints = new Node("", stem + "EndpointFor");
            if (facts.HasOtherEndpointCollection(program, ns.Replace("@", "") + ".EndpointFor." + endpoints.Name))
                throw new AccessorException($"'{program}' already has another endpoint collection. Adopt that root explicitly instead of generating a second collection.");
            AddControllers(endpoints);
            var pageRoot = new Node("", stem + "PageFor");
            AddPages(pageRoot);
            ValidateUsedMappings();

            // Validate the entire host before publishing any source for it.
            var sources = new SortedDictionary<string, AccessorModel>(StringComparer.Ordinal);
            var renderer = new AccessorRenderer(compilation, facts, program, cancellation);
            renderer.Prepare(endpoints, ns + ".EndpointFor", false, true, sources);
            renderer.Prepare(pageRoot, ns + ".PageFor", true, true, sources);
            PublishSources(sources, ns, endpoints.Name, pageRoot.Name);
        }

        [SuppressMessage("SonarQube", "S3267", Justification = "Where predicates would capture and mutate the mapping dictionaries.")]
        private void CollectMappings()
        {
            foreach (var attribute in ForProgram("AccessorMemberAttribute"))
                if (!_members.TryAdd(ArgumentText(attribute, 1), attribute))
                    throw new AccessorException("Duplicate AccessorMember path. Remove or combine the declarations.");
            foreach (var attribute in ForProgram("AccessorGroupAttribute"))
                if (!_groups.TryAdd(ArgumentText(attribute, 1), ArgumentText(attribute, 2)))
                    throw new AccessorException("Duplicate AccessorGroup path. Remove or combine the declarations.");
        }

        private void ValidateUsedMappings()
        {
            var unusedMember = _members.Keys.Except(_usedMembers).Order(StringComparer.Ordinal).FirstOrDefault();
            if (unusedMember != null)
                throw new AccessorException($"AccessorMember '{unusedMember}' does not identify an eligible action/page. Remove stale configuration.");
            var unusedGroup = _groups.Keys.Except(_usedGroups).Order(StringComparer.Ordinal).FirstOrDefault();
            if (unusedGroup != null)
                throw new AccessorException($"AccessorGroup '{unusedGroup}' does not identify a generated group.");
        }

        [SuppressMessage("SonarQube", "S3267", Justification = "A Where predicate would capture generatedSources and both root names.")]
        private void PublishSources(SortedDictionary<string, AccessorModel> sources, string ns, string endpointName, string pageName)
        {
            var metadataNamespace = ns.Replace("@", "");
            var endpointRootName = metadataNamespace + ".EndpointFor." + endpointName;
            var pageRootName = metadataNamespace + ".PageFor." + pageName;
            foreach (var source in sources)
                if (generatedSources.TryGetValue(source.Key, out var previous) &&
                    (source.Key == endpointRootName || source.Key == pageRootName || previous != source.Value))
                    throw new AccessorException($"Generated type '{source.Key}' collides with another program's accessors.");
            foreach (var source in sources)
            {
                cancellation.ThrowIfCancellationRequested();
                // Shared leaf types still have separate instances under each program's singleton root.
                generatedSources.TryAdd(source.Key, source.Value);
            }
        }

        private List<AttributeData> ForProgram(string name) => facts.ForProgram(program, name);

        private static bool Flag(AttributeData? attribute, string name) =>
            attribute?.NamedArguments.FirstOrDefault(pair => pair.Key == name).Value.Value is true;

        private static string ArgumentText(AttributeData attribute, int index) =>
            attribute.ConstructorArguments.Length > index && attribute.ConstructorArguments[index].Value is string { Length: > 0 } text
                ? text : throw new AccessorException($"{attribute.AttributeClass?.Name} requires a nonempty path or type name.");

        private static bool CanUseIdentifier(string value) => SyntaxFacts.IsValidIdentifier(value) ||
            SyntaxFacts.GetKeywordKind(value) != SyntaxKind.None || SyntaxFacts.GetContextualKeywordKind(value) != SyntaxKind.None;

        private void AddControllers(Node root)
        {
            var mappings = ControllerMappings();
            IEnumerable<KeyValuePair<string, INamedTypeSymbol>> controllers = facts.Controllers;
            if (mappings.Count > 0)
                controllers = controllers.Concat(mappings.Select(static mapping =>
                        new KeyValuePair<string, INamedTypeSymbol>(mapping.Key, (INamedTypeSymbol)mapping.Value.ConstructorArguments[1].Value!)))
                    .DistinctBy(static controller => controller.Key, StringComparer.Ordinal)
                    .OrderBy(static controller => controller.Key, StringComparer.Ordinal);
            foreach (var candidate in controllers)
            {
                cancellation.ThrowIfCancellationRequested();
                mappings.TryGetValue(candidate.Key, out var mapping);
                if (!Flag(mapping, "Exclude")) AddController(root, candidate.Value, mapping);
            }
        }

        private Dictionary<string, AttributeData> ControllerMappings()
        {
            var mappings = new Dictionary<string, AttributeData>(StringComparer.Ordinal);
            foreach (var mapping in ForProgram("AccessorControllerAttribute"))
            {
                cancellation.ThrowIfCancellationRequested();
                var controller = mapping.ConstructorArguments.Length > 1 ? mapping.ConstructorArguments[1].Value as INamedTypeSymbol : null;
                if (controller == null) throw new AccessorException("AccessorController requires a controller type.");
                if (!mappings.TryAdd(controller.ToDisplayString(), mapping))
                    throw new AccessorException($"Controller '{controller}' has duplicate mappings for '{program}'.");
            }
            return mappings;
        }

        private void AddController(Node root, INamedTypeSymbol controller, AttributeData? mapping)
        {
            if (!IsController(controller)) throw new AccessorException($"'{controller}' is not an eligible MVC controller. Exclude it or correct the mapping.");
            var controllerName = controller.Name.EndsWith("Controller", StringComparison.Ordinal) ? controller.Name[..^10] : controller.Name;
            var path = ControllerPath(controller, controllerName, mapping);
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
            AddActions(leaf, controller);
        }

        private string ControllerPath(INamedTypeSymbol controller, string controllerName, AttributeData? mapping)
        {
            if (mapping != null && mapping.ConstructorArguments.Length != 2) return ArgumentText(mapping, 2);
            var prefix = program.ContainingNamespace + ".Controllers";
            var controllerNamespace = controller.ContainingNamespace.ToDisplayString();
            var group = controllerNamespace.StartsWith(prefix + ".", StringComparison.Ordinal) ? controllerNamespace[(prefix.Length + 1)..] : "";
            return group.Length == 0 ? controllerName : group + "." + controllerName;
        }

        private void AddActions(Node leaf, INamedTypeSymbol controller)
        {
            foreach (var method in facts.Actions(controller))
            {
                cancellation.ThrowIfCancellationRequested();
                var member = MemberName("Endpoint." + leaf.Path + "." + method.Name, method.Name);
                if (member == null) continue;
                if (method.Parameters.Any(parameter => parameter.RefKind != RefKind.None || parameter.Type.TypeKind is TypeKind.Pointer or TypeKind.FunctionPointer))
                    throw new AccessorException($"'{controller}.{method.Name}' has unsupported parameters. Exclude its accessor and bind it manually.");
                var binding = $"[global::{AttributeNamespace}EndpointAction({Literal(method.Name)}{string.Concat(method.Parameters.Select(parameter => ", typeof(" + TypeName(parameter.Type) + ")"))})]";
                var declaration = binding + "\n    public global::DRN.Framework.Hosting.Endpoints.ApiEndpoint " + Identifier(member) + " { get; private set; } = null!;";
                if (!leaf.Members.TryAdd(member, declaration))
                    throw new AccessorException($"Action accessor '{leaf.Path}.{member}' collides, possibly due to overloads. Exclude that method name and provide manual bindings.");
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
            foreach (var page in pages)
            {
                cancellation.ThrowIfCancellationRequested();
                if (!page.IsPage) continue;
                AddPage(root, page);
            }
        }

        private void AddPage(Node root, PageInput page)
        {
            var path = page.Path?.TrimStart('/');
            if (path == null || path.Split('/').Contains(".."))
                throw new AccessorException("A Razor page has no valid logical TargetPath. Supply AdditionalFiles TargetPath metadata.");
            var (relative, area) = PageLocation(path);
            var segments = relative.Split('/');
            var groups = segments.Length == 1 ? new[] { "Root" } : segments[..^1];
            if (area != null) groups = ["Areas", area, .. groups];
            var member = MemberName("Page." + string.Join(".", groups.Append(segments[^1])), segments[^1]);
            if (member == null && groups.Any(segment => !CanUseIdentifier(segment))) return;
            // Valid excluded groups still host handwritten partial replacements.
            var node = Group(root, groups, true);
            node.Area = area;
            if (member == null) return;
            var declaration = $"public string {Identifier(member)} {{ get; init; }} = {Literal("/" + relative)};";
            if (!node.Members.TryAdd(member, declaration)) throw new AccessorException($"Page accessor '{path}' collides. Supply a member alias or exclusion.");
        }

        private static (string Relative, string? Area) PageLocation(string path)
        {
            if (path.StartsWith("Pages/", StringComparison.Ordinal)) return (path[6..^7], null);
            if (!path.StartsWith("Areas/", StringComparison.Ordinal))
                throw new AccessorException($"Unsupported Razor location '{path}'. Use explicit logical TargetPath metadata.");
            var parts = path.Split('/');
            if (parts.Length < 4 || parts[2] != "Pages")
                throw new AccessorException($"Unsupported Razor location '{path}'. Use Pages or Areas/<area>/Pages.");
            return (string.Join("/", parts.Skip(3))[..^7], parts[1]);
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

    private static bool IsController(INamedTypeSymbol type) => type.TypeKind == TypeKind.Class && !type.IsAbstract &&
        type.Arity == 0 && type.ContainingType == null && type.DeclaredAccessibility == Accessibility.Public &&
        !HasAttribute(type, "Microsoft.AspNetCore.Mvc.NonControllerAttribute") &&
        Bases(type).Any(candidate => candidate.ToDisplayString() == "Microsoft.AspNetCore.Mvc.ControllerBase");

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
