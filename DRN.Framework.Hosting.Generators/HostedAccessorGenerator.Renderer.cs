using System.Diagnostics.CodeAnalysis;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DRN.Framework.Hosting.Generators;

public sealed partial class HostedAccessorGenerator
{
    private sealed record AccessorModel(string HintName, string Namespace, string Name, string? BaseType,
        bool UsesFrameworkImport, string? ConstructorRoute, bool PageLeaf, string? Area, string Members);

    [SuppressMessage("SonarQube", "S3267", Justification = "Direct validation loops avoid Where predicates capturing the member dictionary and authored type. Keep member insertion and first-collision reporting in their existing order.")]
    private sealed class AccessorRenderer(Compilation compilation, CompilationFacts facts, INamedTypeSymbol program,
        CancellationToken cancellation)
    {
        public void Prepare(Node node, string ns, bool page, bool root, SortedDictionary<string, AccessorModel> sources)
        {
            cancellation.ThrowIfCancellationRequested();
            var name = Identifier(node.Name);
            var fullName = ns.Replace("@", "") + "." + node.Name;
            var existing = ValidateExistingType(fullName, page, root);
            var members = BuildMembers(node, fullName, existing, page, root);
            var types = new FrameworkTypeNames(facts, node, ns);
            var baseType = BaseType(node, name, page, root, types);
            var declarations = string.Join("\n    ", members.Values.Select(member => RenderMember(member, node, types)));
            var hintName = "Helpers/" + (page ? "PageFor/" : "EndpointFor/") + fullName + ".g.cs";
            var model = new AccessorModel(hintName, ns, name, baseType, types.UsesFrameworkImport,
                node.Controller == null ? null : node.Route, page && !root, node.Area, declarations);
            if (!sources.TryAdd(fullName, model))
                throw new AccessorException($"Generated type '{fullName}' collides. Supply a TypeName or group alias.");
            foreach (var child in node.Children.Values) Prepare(child, ns, page, false, sources);
        }

        private INamedTypeSymbol? ValidateExistingType(string fullName, bool page, bool root)
        {
            var existing = compilation.GetTypeByMetadataName(fullName);
            if (existing != null && (existing.DeclaringSyntaxReferences.Length == 0 || existing.DeclaringSyntaxReferences.Any(reference =>
                    reference.GetSyntax(cancellation) is not ClassDeclarationSyntax declaration || !declaration.Modifiers.Any(SyntaxKind.PartialKeyword))))
                throw new AccessorException($"'{fullName}' already exists and must be a partial class to customize generated accessors.");
            if (root && !page && existing?.GetMembers("EndpointCollection").Length > 0)
                throw new AccessorException($"'{fullName}.EndpointCollection' is reserved for the inherited typed singleton. Remove or rename the authored member.");
            return existing;
        }

        private static SortedDictionary<string, string> BuildMembers(Node node, string fullName, INamedTypeSymbol? existing,
            bool page, bool root)
        {
            var members = new SortedDictionary<string, string>(node.Members, StringComparer.Ordinal);
            if (node.Area != null && (members.ContainsKey("Area") || existing?.GetMembers("Area").Length > 0))
                throw new AccessorException($"'{fullName}.Area' is reserved for the destination area. Rename the page member.");
            foreach (var child in node.Children)
                if (!members.TryAdd(child.Key, $"public {Identifier(child.Value.Name)} {Identifier(child.Key)} {{ get; }} = new();"))
                    throw new AccessorException($"'{fullName}.{child.Key}' is both a group and a member. Supply an alias.");
            foreach (var member in members.Keys)
                if (member == node.Name || existing?.GetMembers(member).Length > 0 || IsReservedMember(member, node, page, root))
                    throw new AccessorException($"'{fullName}.{member}' collides with an authored member. Explicitly exclude the generated member or rename it.");
            return members;
        }

        private static bool IsReservedMember(string member, Node node, bool page, bool root) =>
            (root && member is "All" or "PageCollection" or "EndpointCollection" or "EndpointSource" or "Endpoints" or "ApiEndpoints" or "PageEndpoints") ||
            (page && member is "PathSegments" or "GetPages") ||
            (node.Controller != null && member is "Endpoints" or "ControllerRoute" or "Controller" or "ControllerName" or "ControllerClassName");

        private string? BaseType(Node node, string name, bool page, bool root, FrameworkTypeNames types)
        {
            if (root)
                return page ? $"{types.Resolve("PageCollectionBase")}<{name}>"
                    : $"{types.Resolve("EndpointCollectionBase")}<{TypeName(program)}, {name}>";
            if (page) return types.Resolve("PageForBase");
            return node.Controller != null ? $"{types.Resolve("ControllerForBase")}<{TypeName(node.Controller)}>" : null;
        }

        private static string RenderMember(string member, Node node, FrameworkTypeNames types)
        {
            if (node.Controller == null) return member;
            return member
                .Replace("[global::" + AttributeNamespace + "EndpointAction(", "[" + types.Resolve(EndpointActionAttributeName) + "(", StringComparison.Ordinal)
                .Replace("public global::" + AttributeNamespace + "ApiEndpoint ", "public " + types.Resolve("ApiEndpoint") + " ", StringComparison.Ordinal);
        }

        public static string Render(AccessorModel model, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            var source = new StringBuilder("// <auto-generated/>\n#nullable enable\n");
            if (model.UsesFrameworkImport) source.Append("using global::DRN.Framework.Hosting.Endpoints;\n\n");
            source.Append("namespace ").Append(model.Namespace).Append(";\n\npublic partial class ").Append(model.Name);
            if (model.BaseType != null) source.Append(" : ").Append(model.BaseType);
            source.Append("\n{\n");
            if (model.ConstructorRoute != null) source.Append("    public ").Append(model.Name).Append("() : base(").Append(Literal(model.ConstructorRoute)).Append(") { }\n");
            if (model.PageLeaf) source.Append("    protected override string[] PathSegments { get; } = [];\n");
            if (model.Area != null) source.Append("    public const string Area = ").Append(Literal(model.Area)).Append(";\n");
            if (model.Members.Length != 0) source.Append("    ").Append(model.Members).Append('\n');
            cancellation.ThrowIfCancellationRequested();
            source.Append("}\n");
            return source.ToString();
        }
    }

    private sealed class FrameworkTypeNames(CompilationFacts facts, Node node, string ns)
    {
        private readonly Dictionary<string, string> _resolved = new(StringComparer.Ordinal);
        public bool UsesFrameworkImport { get; private set; }

        public string Resolve(string typeName)
        {
            if (_resolved.TryGetValue(typeName, out var resolved)) return resolved;
            // Authored and generated names must not shadow imported framework contracts.
            var shortName = typeName == EndpointActionAttributeName ? "EndpointAction" : typeName;
            var collision = Conflicts(typeName) || shortName != typeName && Conflicts(shortName);
            UsesFrameworkImport |= !collision;
            resolved = collision ? "global::" + AttributeNamespace + typeName : shortName;
            _resolved.Add(typeName, resolved);
            return resolved;
        }

        private bool Conflicts(string candidate) =>
            facts.Conflicts(ns, candidate) || node.Name == candidate || node.Members.ContainsKey(candidate) || node.Children.ContainsKey(candidate);
    }
}
