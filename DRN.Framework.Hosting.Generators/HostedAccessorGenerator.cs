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

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var pages = context.AdditionalTextsProvider
            .Where(file => file.Path.EndsWith(".cshtml", StringComparison.OrdinalIgnoreCase))
            .Combine(context.AnalyzerConfigOptionsProvider)
            .Select((input, cancellation) =>
            {
                var options = input.Right.GetOptions(input.Left);
                options.TryGetValue("build_metadata.AdditionalFiles.TargetPath", out var target);
                input.Right.GlobalOptions.TryGetValue("build_property.MSBuildProjectDirectory", out var directory);
                if (!string.IsNullOrEmpty(target))
                {
                    try
                    {
                        var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(target));
                        if (decoded.EndsWith(".cshtml", StringComparison.OrdinalIgnoreCase)) target = decoded;
                    }
                    catch (FormatException) { /* Plain TargetPath values are supported too. */ }
                }
                target ??= directory == null ? null : Path.GetRelativePath(directory, input.Left.Path);
                return new PageInput(target?.Replace('\\', '/'), input.Left.GetText(cancellation)?.ToString() ?? "");
            }).Collect();
        var hostedProject = context.AnalyzerConfigOptionsProvider.Select((options, _) =>
            options.GlobalOptions.TryGetValue("build_property.UsingMicrosoftNETSdkWeb", out var value) &&
            string.Equals(value, "true", StringComparison.OrdinalIgnoreCase));
        context.RegisterSourceOutput(context.CompilationProvider.Combine(pages).Combine(hostedProject), (output, input) =>
        {
            var compilation = input.Left.Left;
            var hosts = Attributes(compilation.Assembly, "HostedAccessorsAttribute")
                .OrderBy(attribute => attribute.ConstructorArguments.FirstOrDefault().Value?.ToString(), StringComparer.Ordinal).ToArray();
            var programs = new HashSet<string>(StringComparer.Ordinal);
            var generatedTypes = new HashSet<string>(StringComparer.Ordinal);
            foreach (var host in hosts)
            {
                if (!input.Right)
                {
                    Report(output, host, "HostedAccessors is supported only in SDK Web hosted projects. Remove the opt-in from this library.");
                    continue;
                }
                var program = host.ConstructorArguments.FirstOrDefault().Value as INamedTypeSymbol;
                if (program == null || !programs.Add(program.ToDisplayString()))
                {
                    Report(output, host, "Each program must have exactly one HostedAccessors declaration.");
                    continue;
                }
                try
                {
                    new Emitter(compilation, output, program, host, input.Left.Right, generatedTypes).Emit();
                }
                catch (AccessorException exception)
                {
                    Report(output, host, exception.Message);
                }
            }
        });
    }

    private sealed record PageInput(string? Path, string Content);
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
        type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat.WithMiscellaneousOptions(
            SymbolDisplayFormat.FullyQualifiedFormat.MiscellaneousOptions & ~SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier));
    private static void Report(SourceProductionContext output, AttributeData attribute, string message) =>
        output.ReportDiagnostic(Diagnostic.Create(Invalid, attribute.ApplicationSyntaxReference?.GetSyntax().GetLocation() ?? Location.None, message));

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

    private sealed class Emitter(Compilation compilation, SourceProductionContext output, INamedTypeSymbol program,
        AttributeData host, ImmutableArray<PageInput> pages, HashSet<string> generatedTypes)
    {
        private readonly Dictionary<string, AttributeData> _members = new(StringComparer.Ordinal);
        private readonly HashSet<string> _usedMembers = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _groups = new(StringComparer.Ordinal);
        private readonly HashSet<string> _usedGroups = new(StringComparer.Ordinal);

        public void Emit()
        {
            if (!SymbolEqualityComparer.Default.Equals(program.ContainingAssembly, compilation.Assembly) ||
                program.IsAbstract || program.Arity != 0 || program.ContainingType != null ||
                program.DeclaredAccessibility != Accessibility.Public ||
                !Bases(program).Any(type => type.OriginalDefinition.ToDisplayString() == "DRN.Framework.Hosting.DrnProgram.DrnProgramBase<TProgram>"))
                throw new AccessorException($"'{program}' must be a public, concrete, non-generic DRN program declared in this hosted project.");
            var ns = Text(host, "Namespace") ?? (program.ContainingNamespace.IsGlobalNamespace ? "Helpers" : program.ContainingNamespace.ToDisplayString() + ".Helpers");
            var stem = Text(host, "Name") ?? (program.Name == "Program" ? "App" : program.Name.EndsWith("Program", StringComparison.Ordinal) ? program.Name[..^7] : program.Name);
            Identifier(stem);
            ns = string.Join(".", ns.Split('.').Select(Identifier));
            foreach (var attribute in ForProgram("AccessorMemberAttribute"))
                if (!_members.TryAdd(ArgumentText(attribute, 1), attribute))
                    throw new AccessorException("Duplicate AccessorMember path. Remove or combine the declarations.");
            foreach (var attribute in ForProgram("AccessorGroupAttribute"))
                if (!_groups.TryAdd(ArgumentText(attribute, 1), ArgumentText(attribute, 2)))
                    throw new AccessorException("Duplicate AccessorGroup path. Remove or combine the declarations.");

            var endpoints = new Node("", stem + "EndpointFor");
            if (Types(compilation.Assembly.GlobalNamespace).Any(type => type.ToDisplayString() != ns.Replace("@", "") + ".EndpointFor." + endpoints.Name &&
                    Bases(type).Any(candidate => candidate.OriginalDefinition.ToDisplayString() == "DRN.Framework.Hosting.Endpoints.EndpointCollectionBase<TProgram>" &&
                        SymbolEqualityComparer.Default.Equals(candidate.TypeArguments[0], program))))
                throw new AccessorException($"'{program}' already has another endpoint collection. Adopt that root explicitly instead of generating a second collection.");
            AddControllers(endpoints);
            var pageRoot = new Node("", stem + "PageFor");
            if (Flag(host, "Pages"))
            {
                if (pages.Length == 0) throw new AccessorException("Pages=true requires Razor AdditionalFiles. Enable Razor compilation or disable page generation for a host without pages.");
                AddPages(pageRoot);
            }
            foreach (var path in _members.Keys.Except(_usedMembers).Order(StringComparer.Ordinal))
                throw new AccessorException($"AccessorMember '{path}' does not identify an eligible action/page. Remove stale configuration.");
            foreach (var path in _groups.Keys.Except(_usedGroups).Order(StringComparer.Ordinal))
                throw new AccessorException($"AccessorGroup '{path}' does not identify a generated group.");

            // Validate the entire host before publishing any source for it.
            var sources = new SortedDictionary<string, string>(StringComparer.Ordinal);
            Render(endpoints, ns + ".EndpointFor", false, true, sources);
            if (pageRoot.Children.Count > 0) Render(pageRoot, ns + ".PageFor", true, true, sources);
            foreach (var source in sources)
                if (!generatedTypes.Add(source.Key)) throw new AccessorException($"Generated type '{source.Key}' collides with another program's accessors.");
            foreach (var source in sources)
                output.AddSource(source.Key + ".g.cs", SourceText.From(source.Value, Encoding.UTF8));
        }

        private IEnumerable<AttributeData> ForProgram(string name) => Attributes(compilation.Assembly, name)
            .Where(attribute => SymbolEqualityComparer.Default.Equals(attribute.ConstructorArguments.FirstOrDefault().Value as ITypeSymbol, program));

        private void AddControllers(Node root)
        {
            var mappings = new Dictionary<string, AttributeData>(StringComparer.Ordinal);
            foreach (var mapping in ForProgram("AccessorControllerAttribute"))
            {
                var controller = mapping.ConstructorArguments.Length > 1 ? mapping.ConstructorArguments[1].Value as INamedTypeSymbol : null;
                if (controller == null) throw new AccessorException("AccessorController requires a controller type.");
                if (!mappings.TryAdd(controller.ToDisplayString(), mapping))
                    throw new AccessorException($"Controller '{controller}' has duplicate mappings for '{program}'.");
            }
            var controllers = Types(compilation.Assembly.GlobalNamespace).Where(IsControllerCandidate)
                .Concat(mappings.Values.Select(mapping => (INamedTypeSymbol)mapping.ConstructorArguments[1].Value!))
                .GroupBy(type => type.ToDisplayString(), StringComparer.Ordinal).Select(group => group.First())
                .OrderBy(type => type.ToDisplayString(), StringComparer.Ordinal);
            foreach (var controller in controllers)
            {
                output.CancellationToken.ThrowIfCancellationRequested();
                mappings.TryGetValue(controller.ToDisplayString(), out var mapping);
                if (Flag(mapping, "Exclude")) continue;
                if (!IsController(controller)) throw new AccessorException($"'{controller}' is not an eligible MVC controller. Exclude it or correct the mapping.");
                var controllerName = controller.Name.EndsWith("Controller", StringComparison.Ordinal) ? controller.Name[..^10] : controller.Name;
                var prefix = program.ContainingNamespace + ".Controllers";
                var controllerNamespace = controller.ContainingNamespace.ToDisplayString();
                var group = controllerNamespace.StartsWith(prefix + ".", StringComparison.Ordinal) ? controllerNamespace[(prefix.Length + 1)..] : "";
                var path = mapping == null ? (group.Length == 0 ? controllerName : group + "." + controllerName) : ArgumentText(mapping, 2);
                var parts = path.Split('.');
                // EndpointCollectionBase starts at the first group and traverses at most five group levels.
                if (parts.Length > 6)
                    throw new AccessorException($"Accessor '{path}' exceeds the runtime discovery limit of five groups. Supply a shorter controller alias.");
                var parent = Group(root, parts[..^1], false);
                var leaf = new Node(path, Text(mapping, "TypeName") ?? controllerName + "For")
                {
                    Controller = controller, Route = Text(mapping, "RouteTemplate") ?? ControllerRouteTemplate(controller)
                };
                if (!parent.Children.TryAdd(parts[^1], leaf)) throw new AccessorException($"Accessor '{path}' collides. Supply a controller alias.");
                foreach (var method in Actions(controller))
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
                var content = Regex.Replace(page.Content, @"@\*[\s\S]*?\*@", "");
                if (!Regex.IsMatch(content, @"(?m)^[\t ]*@page(?:[\t ]|\r?$)")) continue;
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
            var name = Identifier(node.Name);
            var fullName = ns.Replace("@", "") + "." + node.Name;
            var existing = compilation.GetTypeByMetadataName(fullName);
            if (existing != null && (existing.DeclaringSyntaxReferences.Length == 0 || existing.DeclaringSyntaxReferences.Any(reference =>
                    reference.GetSyntax() is not ClassDeclarationSyntax declaration || !declaration.Modifiers.Any(SyntaxKind.PartialKeyword))))
                throw new AccessorException($"'{fullName}' already exists and must be a partial class to customize generated accessors.");
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
                ? page ? $"PageCollectionBase<{name}>" : $"EndpointCollectionBase<{TypeName(program)}>"
                : page ? "PageForBase" : node.Controller != null ? $"ControllerForBase<{TypeName(node.Controller)}>" : null;
            var source = new StringBuilder("// <auto-generated/>\n#nullable enable\nnamespace " + ns + ";\n\npublic partial class " + name);
            if (baseType != null) source.Append(" : global::DRN.Framework.Hosting.Endpoints.").Append(baseType);
            source.Append("\n{\n");
            if (node.Controller != null) source.Append("    public ").Append(name).Append("() : base(").Append(Literal(node.Route)).Append(") { }\n");
            if (page && !root) source.Append("    protected override string[] PathSegments { get; } = [];\n");
            if (node.Area != null) source.Append("    public const string Area = ").Append(Literal(node.Area)).Append(";\n");
            foreach (var member in members.Values)
                source.Append("    [global::DRN.Framework.SharedKernel.Attributes.Sensitive(false, Justification = \"Static accessor identity or metadata, not request data.\")]\n    ")
                    .Append(member).Append('\n');
            source.Append("}\n");
            if (!sources.TryAdd(fullName, source.ToString())) throw new AccessorException($"Generated type '{fullName}' collides. Supply a TypeName or group alias.");
            foreach (var child in node.Children.Values) Render(child, ns, page, false, sources);
        }

        private static bool IsController(INamedTypeSymbol type) => !type.IsAbstract && type.Arity == 0 && type.ContainingType == null &&
            type.DeclaredAccessibility == Accessibility.Public && !HasAttribute(type, "Microsoft.AspNetCore.Mvc.NonControllerAttribute") &&
            Bases(type).Any(candidate => candidate.ToDisplayString() == "Microsoft.AspNetCore.Mvc.ControllerBase");

        private static bool IsControllerCandidate(INamedTypeSymbol type) => type.TypeKind == TypeKind.Class && !type.IsAbstract &&
            type.Arity == 0 && type.DeclaredAccessibility == Accessibility.Public &&
            !HasAttribute(type, "Microsoft.AspNetCore.Mvc.NonControllerAttribute") &&
            (type.Name.EndsWith("Controller", StringComparison.OrdinalIgnoreCase) || HasAttribute(type, "Microsoft.AspNetCore.Mvc.ControllerAttribute"));

        private static IEnumerable<IMethodSymbol> Actions(INamedTypeSymbol type)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var current in Bases(type))
            foreach (var method in current.GetMembers().OfType<IMethodSymbol>().OrderBy(method => method.ToDisplayString(), StringComparer.Ordinal))
            {
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

        private static bool HasAttribute(ISymbol symbol, string name)
        {
            for (ISymbol? current = symbol; current != null; current = current switch
                 { INamedTypeSymbol type => type.BaseType, IMethodSymbol method => method.OverriddenMethod, _ => null })
                if (current.GetAttributes().Any(attribute => attribute.AttributeClass != null && Bases(attribute.AttributeClass).Any(type => type.ToDisplayString() == name))) return true;
            return false;
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

        private static IEnumerable<INamedTypeSymbol> Bases(INamedTypeSymbol type)
        {
            for (var current = type; current != null; current = current.BaseType) yield return current;
        }
        private static IEnumerable<INamedTypeSymbol> Types(INamespaceSymbol ns) => ns.GetTypeMembers()
            .Concat(ns.GetNamespaceMembers().SelectMany(Types));
    }
}
