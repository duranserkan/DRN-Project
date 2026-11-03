using System.Collections;
using DRN.Framework.SharedKernel.Attributes;
using static DRN.Framework.Utils.Logging.Diagnostics.DiagnosticSanitizer;

namespace DRN.Framework.Utils.Logging.Diagnostics;

// Traversal runs inside the owner's Guard and shares its scope budget, status, and generated keys.
internal sealed class DiagnosticValueProjector(DiagnosticSanitizer owner)
{
    private DiagnosticLogOptions Options => owner.Options;

    private static string? ChildNamePath(string? parent, string name)
    {
        if (parent == null) return null;
        return parent.Length == 0 ? name : parent + "." + name;
    }

    internal object? Project(DiagnosticCaptureNode node, object? value, int depth, HashSet<object>? ancestors,
        DiagnosticCaptureContext context)
    {
        if (!owner.VisitNode(depth)) return Limited;
        if (value is DiagnosticCapturedValue)
            return context.DeferRedaction ? owner.CopySafe(value, depth) : Redacted;
        if (value.IgnoredLog()) return ScopedLogConventions.IgnoredLogValue;
        if (node.Kind == DiagnosticCaptureKind.NamedEntry && value != null && IsModelType(value.GetType())) node = node with { NamePath = "" };
        var exempt = node.Exempt;
        exempt |= context.Exempts(node.Kind, node.Segments);
        var sensitive = owner.IsSensitive(node.Path, exempt, node.NamePath) || owner.RedactionEnabled && (node.Classified || HasClassifiedType(value));
        if (sensitive && !context.DeferRedaction) return Redacted;
        if (!sensitive) context.Decisions.Add(new(node.Path, exempt, node.NamePath));
        var result = ProjectValue(node, value, depth, ancestors, context);
        return sensitive ? new DiagnosticCapturedValue(result, DiagnosticRedactionKind.Classified) : result;
    }

    private object? ProjectValue(DiagnosticCaptureNode node, object? value, int depth, HashSet<object>? ancestors,
        DiagnosticCaptureContext context)
    {
        return value switch
        {
            null => null,
            string text => owner.StringValue(node.Path, text, context),
            Uri uri => owner.Accept(uri.OriginalString) ? owner.CaptureUrl(uri.OriginalString, context) : Limited,
            ScopeEvent scopeEvent => scopeEvent,
            bool or byte or sbyte or short or ushort or int or uint or long or ulong or decimal or Guid or
                DateTime or DateTimeOffset or DateOnly or TimeOnly or TimeSpan => value,
            _ when value.GetType().IsEnum => value,
            double number => double.IsFinite(number) ? number : Invalid,
            float single => float.IsFinite(single) ? single : Invalid,
            char character => character.ToString(),
            _ => ProjectComplexValue(node, value, depth, ancestors, context)
        };
    }

    private object ProjectComplexValue(DiagnosticCaptureNode node, object value, int depth, HashSet<object>? ancestors,
        DiagnosticCaptureContext context)
    {
        ancestors ??= new HashSet<object>(ReferenceEqualityComparer.Instance);
        if (!ancestors.Add(value)) return "[diagnostic cycle]";
        try
        {
            if (value is IDictionary dictionary)
                return ProjectDictionary(node, dictionary, depth, ancestors, context);
            if (value is IReadOnlyDictionary<string, object?> readOnly)
                return ProjectReadOnlyDictionary(node, readOnly, depth, ancestors, context);
            if (value is IList list)
                return ProjectList(node, list, depth, ancestors, context);
            if (value is IEnumerable) return "[unsupported lazy diagnostic collection]";
            return ProjectModel(node, value, depth, ancestors, context);
        }
        finally { ancestors.Remove(value); }
    }

    private Dictionary<string, object?> ProjectDictionary(DiagnosticCaptureNode node, IDictionary dictionary, int depth,
        HashSet<object> ancestors, DiagnosticCaptureContext context)
    {
        var output = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (DictionaryEntry item in dictionary)
        {
            if (output.Count >= Options.MaxCollectionEntries || !owner.WorkAvailable()) { output[CollectionLimitKey] = Limited; break; }
            if (item.Key is not string key) { output[owner.NextFieldKey()] = "[unsupported diagnostic key]"; continue; }
            AddDictionaryEntry(output, key, item.Value, node, depth, ancestors, context);
        }
        return output;
    }

    private Dictionary<string, object?> ProjectReadOnlyDictionary(DiagnosticCaptureNode node, IReadOnlyDictionary<string, object?> readOnly,
        int depth, HashSet<object> ancestors, DiagnosticCaptureContext context)
    {
        var output = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var item in readOnly)
        {
            if (output.Count >= Options.MaxCollectionEntries || !owner.WorkAvailable()) { output[CollectionLimitKey] = Limited; break; }
            AddDictionaryEntry(output, item.Key, item.Value, node, depth, ancestors, context);
        }
        return output;
    }

    private List<object?> ProjectList(DiagnosticCaptureNode node, IList list, int depth, HashSet<object> ancestors,
        DiagnosticCaptureContext context)
    {
        var output = new List<object?>();
        for (var index = 0; index < list.Count && index < Options.MaxCollectionEntries && owner.WorkAvailable(); index++)
            output.Add(Project(new DiagnosticCaptureNode(node.Path,
                    [.. node.Segments, index.ToString(System.Globalization.CultureInfo.InvariantCulture)], DiagnosticCaptureKind.ListItem,
                    NamePath: node.NamePath), list[index], depth + 1, ancestors, context));
        if (output.Count < list.Count) output.Add(Limited);
        return output;
    }

    private void AddDictionaryEntry(Dictionary<string, object?> output, string key, object? item, DiagnosticCaptureNode node, int depth,
        HashSet<object> ancestors, DiagnosticCaptureContext context)
    {
        var safe = owner.UniqueKey(output, key);
        output[safe] = key.Length > Options.MaxKeyBytes ? Limited : Project(
            new DiagnosticCaptureNode(node.Path + "." + key, [.. node.Segments, key], DiagnosticCaptureKind.DictionaryKey,
                NamePath: ChildNamePath(node.NamePath, key)), item, depth + 1, ancestors, context);
    }

    private Dictionary<string, object?> ProjectModel(DiagnosticCaptureNode node, object value, int depth, HashSet<object> ancestors,
        DiagnosticCaptureContext context)
    {
        var typedModel = IsModelType(value.GetType());
        var projected = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var member in DiagnosticTypeMetadata.For(value.GetType()).Members)
        {
            if (projected.Count >= Options.MaxCollectionEntries || !owner.WorkAvailable()) { projected[CollectionLimitKey] = Limited; break; }
            var key = owner.UniqueKey(projected, member.Name);
            var memberNamePath = typedModel ? member.Name : ChildNamePath(node.NamePath, member.Name);
            projected[key] = ProjectMember(node, value, member, depth, ancestors, context, memberNamePath);
        }
        return projected;
    }

    private object? ProjectMember(DiagnosticCaptureNode parent, object value, DiagnosticTypeMetadata.Member member, int depth,
        HashSet<object> ancestors, DiagnosticCaptureContext context, string? memberNamePath)
    {
        if (member.Ignored) return ScopedLogConventions.IgnoredLogValue;
        if (!context.DeferRedaction && owner.RedactionEnabled && member.Sensitive) return Redacted;
        var node = new DiagnosticCaptureNode(parent.Path + "." + member.Name, [.. parent.Segments, member.Name],
            DiagnosticCaptureKind.Property, member.Exempt, memberNamePath, member.Sensitive);
        bool MatchesMemberName() => owner.IsSensitive(node.Path,
            node.Exempt || context.Exempts(node.Kind, node.Segments), node.NamePath);
        if (!context.DeferRedaction && MatchesMemberName()) return Redacted;
        try
        {
            return Project(node, member.Read(value), depth + 1, ancestors, context);
        }
        catch (Exception)
        {
            var result = context.DeferRedaction && (member.Sensitive || MatchesMemberName())
                ? (object)new DiagnosticCapturedValue(Invalid, DiagnosticRedactionKind.Classified) : Invalid;
            owner.RecordGetterFailure();
            return result;
        }
    }
}
