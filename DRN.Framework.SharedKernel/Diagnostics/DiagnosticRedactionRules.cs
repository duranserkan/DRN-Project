using System.Collections.Immutable;
using System.Text.Json;

namespace DRN.Framework.SharedKernel.Diagnostics;

// Linked into the analyzer so project catalogs have identical runtime and compiler semantics.
internal sealed class DiagnosticRedactionRules
{
    internal const int MaxCharacters = 65536;
    internal const int MaxRules = 256;
    internal const int MaxRuleLength = 512;
    private const int MaxInheritedRules = 4096;
    private const int MaxAssemblies = 1024;
    private const int MaxAssemblyReferences = 1024;
    private const int MaxCatalogs = 64;
    internal const string MetadataName = "DRN.Framework.SharedKernel.Attributes.DataClassificationRulesAttribute";
    internal static readonly DiagnosticRedactionRules Empty = new([], [], [], []);

    internal ImmutableArray<string> SecretFields { get; }
    internal ImmutableArray<string> PersonalFields { get; }
    internal ImmutableArray<string> FieldPaths { get; }
    internal ImmutableArray<string> UrlSegments { get; }

    private DiagnosticRedactionRules(ImmutableArray<string> secretFields, ImmutableArray<string> personalFields,
        ImmutableArray<string> fieldPaths, ImmutableArray<string> urlSegments)
    {
        SecretFields = secretFields;
        PersonalFields = personalFields;
        FieldPaths = fieldPaths;
        UrlSegments = urlSegments;
    }

    internal bool MatchesField(string path)
    {
        var normalized = DiagnosticRuleCatalog.Normalize(path);
        return DiagnosticRuleCatalog.Matches(normalized, DiagnosticRuleCatalog.Fields) ||
               DiagnosticRuleCatalog.Matches(normalized, SecretFields) ||
               DiagnosticRuleCatalog.Matches(normalized, PersonalFields) ||
               DiagnosticRuleCatalog.Matches(normalized, FieldPaths);
    }

    internal bool MatchesPath(string path) => DiagnosticRuleCatalog.Matches(DiagnosticRuleCatalog.Normalize(path), FieldPaths);

    internal DiagnosticRedactionRules Merge(DiagnosticRedactionRules other)
    {
        if (ReferenceEquals(other, Empty)) return this;
        if (ReferenceEquals(this, Empty)) return other;
        var merged = new DiagnosticRedactionRules(
            MergePatterns(SecretFields, other.SecretFields),
            MergePatterns(PersonalFields, other.PersonalFields),
            MergePatterns(FieldPaths, other.FieldPaths),
            MergePatterns(UrlSegments, other.UrlSegments));
        if (merged.SecretFields.Length + merged.PersonalFields.Length + merged.FieldPaths.Length + merged.UrlSegments.Length > MaxInheritedRules)
            throw new FormatException("Inherited classification catalogs must contain at most 4096 distinct rules.");
        return merged;
    }

    private static ImmutableArray<string> MergePatterns(ImmutableArray<string> first, ImmutableArray<string> second) =>
        first.Concat(second).Distinct(StringComparer.Ordinal).ToImmutableArray();

    internal static DiagnosticRedactionRules FromGraph<T>(T root, Func<T, string> name,
        Func<T, IEnumerable<T>> references, Func<T, IEnumerable<string?>> catalogs, IEqualityComparer<T> comparer,
        CancellationToken cancellationToken = default) where T : notnull
    {
        var graph = BuildGraph(root, references, comparer, cancellationToken);
        var consumers = FindConsumers(graph, name, comparer, cancellationToken);
        var result = Empty;
        var count = 0;
        foreach (var assembly in graph.Keys.Where(consumers.Contains))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var own = catalogs(assembly).Take(2).ToArray();
            if (own.Length > 1) throw new FormatException("An assembly must publish at most one data classification catalog.");
            if (own.Length == 0) continue;
            if (++count > MaxCatalogs) throw new FormatException("Classification inheritance supports at most 64 catalogs.");
            if (own[0] == null) throw new FormatException("An assembly classification catalog cannot be null.");
            try { result = result.Merge(Parse(own[0]!)); }
            catch (FormatException exception)
            {
                throw new FormatException($"Invalid classification catalog in assembly '{name(assembly)}': {exception.Message}", exception);
            }
        }
        return result;
    }

    private static Dictionary<T, T[]> BuildGraph<T>(T root, Func<T, IEnumerable<T>> references,
        IEqualityComparer<T> comparer, CancellationToken cancellationToken) where T : notnull
    {
        var graph = new Dictionary<T, T[]>(comparer);
        var pending = new Stack<T>();
        pending.Push(root);
        while (pending.TryPop(out var assembly))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (graph.ContainsKey(assembly)) continue;
            if (graph.Count >= MaxAssemblies) throw new FormatException("Classification inheritance supports at most 1024 assemblies.");
            var dependencies = references(assembly).Take(MaxAssemblyReferences + 1).ToArray();
            if (dependencies.Length > MaxAssemblyReferences) throw new FormatException("Classification inheritance supports at most 1024 assembly references.");
            graph.Add(assembly, dependencies);
            foreach (var dependency in dependencies) pending.Push(dependency);
        }
        return graph;
    }

    private static HashSet<T> FindConsumers<T>(Dictionary<T, T[]> graph, Func<T, string> name,
        IEqualityComparer<T> comparer, CancellationToken cancellationToken) where T : notnull
    {
        // Propagate backwards from SharedKernel, including transitive consumers and cycles.
        var consumers = new HashSet<T>(graph.Keys.Where(assembly => name(assembly) == "DRN.Framework.SharedKernel"), comparer);
        bool changed;
        do
        {
            cancellationToken.ThrowIfCancellationRequested();
            changed = false;
            foreach (var (assembly, dependencies) in graph)
                if (!consumers.Contains(assembly) && dependencies.Any(consumers.Contains))
                    changed |= consumers.Add(assembly);
        } while (changed);

        return consumers;
    }

    internal static DiagnosticRedactionRules Parse(string json)
    {
        if (json.Length > MaxCharacters)
            throw new FormatException("A diagnostic redaction rule file must contain at most 65536 characters.");
        JsonDocument document;
        try { document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 4 }); }
        catch (JsonException) { throw new FormatException("Diagnostic redaction rules must be valid JSON."); }
        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                throw new FormatException("Diagnostic redaction rules must be a JSON object.");
            var groups = new Dictionary<string, ImmutableArray<string>>(StringComparer.Ordinal);
            var count = 0;
            foreach (var group in document.RootElement.EnumerateObject())
            {
                if (group.Name is not ("secretFields" or "personalFields" or "sensitiveFieldPaths" or "secretPathSegments") ||
                    groups.ContainsKey(group.Name))
                    throw new FormatException("Diagnostic redaction rules contain an unknown or duplicate group.");
                groups.Add(group.Name, ParseGroup(group.Value, ref count));
            }
            return new(groups.GetValueOrDefault("secretFields", []), groups.GetValueOrDefault("personalFields", []),
                groups.GetValueOrDefault("sensitiveFieldPaths", []), groups.GetValueOrDefault("secretPathSegments", []));
        }
    }

    private static ImmutableArray<string> ParseGroup(JsonElement group, ref int count)
    {
        if (group.ValueKind != JsonValueKind.Array)
            throw new FormatException("Diagnostic redaction rule groups must be arrays of strings.");
        var values = ImmutableArray.CreateBuilder<string>();
        foreach (var entry in group.EnumerateArray())
        {
            if (++count > MaxRules)
                throw new FormatException("A diagnostic redaction rule file must contain at most 256 rules.");
            if (entry.ValueKind != JsonValueKind.String || entry.GetString() is not { } value ||
                value.Length > MaxRuleLength || DiagnosticRuleCatalog.Normalize(value) is not { Length: > 0 } normalized)
                throw new FormatException("Diagnostic redaction rules must be nonempty names of at most 512 characters with ASCII letters or digits.");
            values.Add(normalized);
        }
        return values.Distinct(StringComparer.Ordinal).ToImmutableArray();
    }
}
