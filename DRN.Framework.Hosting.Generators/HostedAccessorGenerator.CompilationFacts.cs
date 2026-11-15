using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using DRN.Framework.CompilerTools;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DRN.Framework.Hosting.Generators;

public sealed partial class HostedAccessorGenerator
{
    private sealed class CompilationFacts
    {
        private static readonly string[] FrameworkNames =
            ["ApiEndpoint", "EndpointAction", EndpointActionAttributeName, "ControllerForBase", "EndpointCollectionBase", "PageCollectionBase", "PageForBase"];
        private readonly Compilation _compilation;
        private readonly CancellationToken _cancellation;
        private readonly HashSet<string> _nameConflicts = new(StringComparer.Ordinal);
        private readonly Dictionary<string, HashSet<string>> _namespaceConflicts = new(StringComparer.Ordinal);
        private readonly Dictionary<ISymbol, Dictionary<string, List<AttributeData>>> _programAttributes = new(SymbolEqualityComparer.Default);
        private readonly Dictionary<ITypeSymbol, List<string>> _endpointCollections = new(SymbolEqualityComparer.Default);
        private readonly SymbolFactCache<INamedTypeSymbol, ImmutableArray<IMethodSymbol>>? _actions;

        public CompilationFacts(Compilation compilation, CancellationToken cancellation, bool reuseActions)
        {
            _compilation = compilation;
            _cancellation = cancellation;
            if (reuseActions) _actions = new();
            cancellation.ThrowIfCancellationRequested();
            Controllers = DiscoverControllers();
            CollectProgramAttributes();
            CollectNameConflicts();
        }

        public KeyValuePair<string, INamedTypeSymbol>[] Controllers { get; }

        public IEnumerable<IMethodSymbol> Actions(INamedTypeSymbol controller) =>
            _actions == null ? DiscoverActions(controller, _cancellation) :
                _actions.GetOrAdd(controller, static (type, cancellation) => DiscoverActions(type, cancellation).ToImmutableArray(), _cancellation);

        private static IEnumerable<IMethodSymbol> DiscoverActions(INamedTypeSymbol type, CancellationToken cancellation)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var current in Bases(type))
            {
                foreach (var method in current.GetMembers().OfType<IMethodSymbol>().OrderBy(method => method.ToDisplayString(), StringComparer.Ordinal))
                {
                    cancellation.ThrowIfCancellationRequested();
                    if (method.MethodKind != MethodKind.Ordinary || method.DeclaredAccessibility != Accessibility.Public) continue;
                    var signature = method.Name + "`" + method.Arity + "(" + string.Join(",", method.Parameters.Select(parameter => parameter.RefKind + ":" + TypeName(parameter.Type))) + ")";
                    if (seen.Add(signature) && IsAction(method)) yield return method;
                }
            }
        }

        private static bool IsAction(IMethodSymbol method)
        {
            if (method.IsStatic || method.IsGenericMethod || method.IsAbstract ||
                HasAttribute(method, "Microsoft.AspNetCore.Mvc.NonActionAttribute")) return false;
            var original = method;
            while (original.OverriddenMethod != null) original = original.OverriddenMethod;
            if (original.ContainingType.SpecialType == SpecialType.System_Object) return false;
            if (original.Parameters.Length != 0) return true;
            var interfaceName = original.Name switch
            {
                "Dispose" => "System.IDisposable",
                "DisposeAsync" => "System.IAsyncDisposable",
                _ => null
            };
            if (interfaceName == null) return true;
            // Preserve disposal exclusions across overrides and derived interface reimplementations.
            var declaringType = original.ContainingType;
            var disposable = declaringType.AllInterfaces.FirstOrDefault(item => item.ToDisplayString() == interfaceName);
            var dispose = disposable?.GetMembers(original.Name).OfType<IMethodSymbol>().SingleOrDefault();
            var implementation = dispose == null ? null : declaringType.FindImplementationForInterfaceMember(dispose);
            return !SymbolEqualityComparer.Default.Equals(original, implementation);
        }

        private KeyValuePair<string, INamedTypeSymbol>[] DiscoverControllers()
        {
            var controllers = new SortedDictionary<string, INamedTypeSymbol>(StringComparer.Ordinal);
            var collectionBase = _compilation.GetTypeByMetadataName("DRN.Framework.Hosting.Endpoints.EndpointCollection`1");
            foreach (var type in Types(_compilation.Assembly.GlobalNamespace, _cancellation))
            {
                if (IsControllerCandidate(type)) controllers.TryAdd(type.ToDisplayString(), type);
                if (collectionBase == null) continue;
                foreach (var nested in SelfAndNestedTypes(type, _cancellation))
                    CollectEndpointCollection(nested, collectionBase);
            }
            return controllers.ToArray();
        }

        private void CollectEndpointCollection(INamedTypeSymbol type, INamedTypeSymbol collectionBase)
        {
            foreach (var current in Bases(type))
            {
                _cancellation.ThrowIfCancellationRequested();
                if (!SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, collectionBase)) continue;
                var owner = current.TypeArguments[0];
                if (!_endpointCollections.TryGetValue(owner, out var collections))
                {
                    collections = [];
                    _endpointCollections.Add(owner, collections);
                }
                collections.Add(type.ToDisplayString());
                break;
            }
        }

        private void CollectProgramAttributes()
        {
            foreach (var attribute in _compilation.Assembly.GetAttributes())
            {
                _cancellation.ThrowIfCancellationRequested();
                var name = attribute.AttributeClass?.Name;
                if (name is not ("AccessorControllerAttribute" or "AccessorGroupAttribute" or "AccessorMemberAttribute") ||
                    attribute.AttributeClass!.ToDisplayString() != AttributeNamespace + name ||
                    attribute.ConstructorArguments.FirstOrDefault().Value is not INamedTypeSymbol program) continue;
                AddProgramAttribute(program, name, attribute);
                CollectAliasConflict(name, attribute);
            }
        }

        private void AddProgramAttribute(INamedTypeSymbol program, string name, AttributeData attribute)
        {
            if (!_programAttributes.TryGetValue(program, out var attributes))
            {
                attributes = new(StringComparer.Ordinal);
                _programAttributes.Add(program, attributes);
            }
            if (!attributes.TryGetValue(name, out var declarations))
            {
                declarations = [];
                attributes.Add(name, declarations);
            }
            declarations.Add(attribute);
        }

        private void CollectAliasConflict(string name, AttributeData attribute)
        {
            // Programs can share a generated namespace. Their aliases must not shadow each other's contracts.
            if (name == "AccessorGroupAttribute" && attribute.ConstructorArguments.ElementAtOrDefault(2).Value is string groupName)
                AddFrameworkNameConflict(groupName);
            if (name == "AccessorControllerAttribute" && Text(attribute, "TypeName") is { } typeName)
                AddFrameworkNameConflict(typeName);
        }

        private void AddFrameworkNameConflict(string name)
        {
            if (Array.IndexOf(FrameworkNames, name) >= 0) _nameConflicts.Add(name);
        }

        private void CollectNameConflicts()
        {
            foreach (var name in FrameworkNames)
            {
                _cancellation.ThrowIfCancellationRequested();
                if (_compilation.GetSymbolsWithName(name, SymbolFilter.Type | SymbolFilter.Namespace, _cancellation).Any())
                    _nameConflicts.Add(name);
            }
            foreach (var tree in _compilation.SyntaxTrees)
                CollectGlobalImportConflicts(tree);
        }

        [SuppressMessage("SonarQube", "S3267", Justification = "The direct loop preserves per-import cancellation and lazy semantic-model creation.")]
        private void CollectGlobalImportConflicts(SyntaxTree tree)
        {
            _cancellation.ThrowIfCancellationRequested();
            var root = (CompilationUnitSyntax)tree.GetRoot(_cancellation);
            SemanticModel? model = null;
            // Valid global imports occur only at compilation-unit scope. Method bodies are irrelevant.
            foreach (var directive in root.Usings)
            {
                if (!directive.GlobalKeyword.IsKind(SyntaxKind.GlobalKeyword)) continue;
                _cancellation.ThrowIfCancellationRequested();
                if (directive.Alias != null)
                {
                    AddFrameworkNameConflict(directive.Alias.Name.Identifier.ValueText);
                    continue;
                }
                if (directive.Name == null) continue;
                model ??= _compilation.GetSemanticModel(tree);
                CollectImportedNames(model.GetSymbolInfo(directive.Name, _cancellation).Symbol);
            }
        }

        [SuppressMessage("SonarQube", "S3267", Justification = "A Where predicate would capture the conflict set that the loop updates.")]
        private void CollectImportedNames(ISymbol? imported)
        {
            foreach (var name in FrameworkNames)
            {
                if (_nameConflicts.Contains(name)) continue;
                var frameworkName = name == "EndpointAction" ? EndpointActionAttributeName : name;
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

        public bool HasOtherEndpointCollection(INamedTypeSymbol program, string rootName)
        {
            if (!_endpointCollections.TryGetValue(program, out var collections)) return false;
            foreach (var collection in collections)
                if (collection != rootName) return true;
            return false;
        }

        public List<AttributeData> ForProgram(INamedTypeSymbol program, string name) =>
            _programAttributes.TryGetValue(program, out var attributes) && attributes.TryGetValue(name, out var declarations) ? declarations : [];

        public bool Conflicts(string ns, string name)
        {
            if (_nameConflicts.Contains(name)) return true;
            if (!_namespaceConflicts.TryGetValue(ns, out var conflicts))
            {
                conflicts = FindNamespaceConflicts(ns);
                _namespaceConflicts.Add(ns, conflicts);
            }
            return conflicts.Contains(name);
        }

        private HashSet<string> FindNamespaceConflicts(string ns)
        {
            var conflicts = new HashSet<string>(StringComparer.Ordinal);
            var scope = _compilation.GlobalNamespace;
            AddNamespaceConflicts(scope, conflicts);
            foreach (var segment in ns.Replace("@", "").Split('.'))
            {
                _cancellation.ThrowIfCancellationRequested();
                var nested = scope.GetNamespaceMembers().FirstOrDefault(candidate => candidate.Name == segment);
                if (nested == null) break;
                scope = nested;
                AddNamespaceConflicts(scope, conflicts);
            }
            return conflicts;
        }

        [SuppressMessage("SonarQube", "S3267", Justification = "A Where predicate would capture the current namespace during conflict collection.")]
        private static void AddNamespaceConflicts(INamespaceSymbol current, HashSet<string> conflicts)
        {
            foreach (var candidate in FrameworkNames)
                if (current.GetMembers(candidate).Any()) conflicts.Add(candidate);
        }

        private static bool IsControllerCandidate(INamedTypeSymbol type) => IsController(type) &&
            (type.Name.EndsWith("Controller", StringComparison.OrdinalIgnoreCase) || HasAttribute(type, "Microsoft.AspNetCore.Mvc.ControllerAttribute"));

        private static IEnumerable<INamedTypeSymbol> Types(INamespaceSymbol ns, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            foreach (var type in ns.GetTypeMembers())
            {
                cancellation.ThrowIfCancellationRequested();
                yield return type;
            }
            foreach (var child in ns.GetNamespaceMembers())
            {
                foreach (var type in Types(child, cancellation)) yield return type;
            }
        }

        private static IEnumerable<INamedTypeSymbol> SelfAndNestedTypes(INamedTypeSymbol type, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            yield return type;
            foreach (var child in type.GetTypeMembers())
            {
                foreach (var nested in SelfAndNestedTypes(child, cancellation)) yield return nested;
            }
        }
    }
}
