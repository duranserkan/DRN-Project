using System.Collections;
using DRN.Framework.SharedKernel.Attributes;
using static DRN.Framework.Utils.Logging.Diagnostics.DiagnosticSanitizer;

namespace DRN.Framework.Utils.Logging.Diagnostics;

// Traversal runs inside the owner's Guard and shares its scope budget, status, and generated keys.
internal sealed class DiagnosticValueProjector(DiagnosticSanitizer owner)
{
    private DiagnosticLogOptions Options => owner.Options;

    private static string? ChildNamePath(string? parent, string name) => parent == null ? null :
        parent.Length == 0 ? name : parent + "." + name;

    internal object? Project(string path, object? value, int depth, HashSet<object>? ancestors,
        DiagnosticCaptureContext context, string[] segments, DiagnosticCaptureKind kind, bool exempt = false, string? namePath = null,
        bool classified = false)
    {
        if (!owner.VisitNode(depth)) return Limited;
        if (value is DiagnosticCapturedValue)
            return context.DeferRedaction ? owner.CopySafe(value, depth) : Redacted;
        if (value.IgnoredLog()) return ScopedLogConventions.IgnoredLogValue;
        if (kind == DiagnosticCaptureKind.NamedEntry && value != null && IsModelType(value.GetType())) namePath = "";
        exempt |= context.Exempts(kind, segments);
        var sensitive = owner.IsSensitive(path, exempt, namePath) || owner.RedactionEnabled && (classified || HasClassifiedType(value));
        if (sensitive && !context.DeferRedaction) return Redacted;
        if (!sensitive) context.Decisions.Add(new(path, exempt, namePath));
        var result = ProjectValue(path, value, depth, ancestors, context, segments, namePath);
        return sensitive ? new DiagnosticCapturedValue(result, DiagnosticRedactionKind.Classified) : result;
    }

    private object? ProjectValue(string path, object? value, int depth, HashSet<object>? ancestors,
        DiagnosticCaptureContext context, string[] segments, string? namePath)
    {
        return value switch
        {
            null => null,
            string text => owner.StringValue(path, text, context),
            Uri uri => owner.Accept(uri.OriginalString) ? owner.CaptureUrl(uri.OriginalString, context) : Limited,
            ScopeEvent scopeEvent => scopeEvent,
            bool or byte or sbyte or short or ushort or int or uint or long or ulong or decimal or Guid or
                DateTime or DateTimeOffset or DateOnly or TimeOnly or TimeSpan => value,
            _ when value.GetType().IsEnum => value,
            double number => double.IsFinite(number) ? number : Invalid,
            float single => float.IsFinite(single) ? single : Invalid,
            char character => character.ToString(),
            _ => ProjectComplexValue(path, value, depth, ancestors, context, segments, namePath)
        };
    }

    private object ProjectComplexValue(string path, object value, int depth, HashSet<object>? ancestors,
        DiagnosticCaptureContext context, string[] segments, string? namePath)
    {
        ancestors ??= new HashSet<object>(ReferenceEqualityComparer.Instance);
        if (!ancestors.Add(value)) return "[diagnostic cycle]";
        try
        {
            if (value is IDictionary dictionary)
                return ProjectDictionary(path, dictionary, depth, ancestors, context, segments, namePath);
            if (value is IReadOnlyDictionary<string, object?> readOnly)
                return ProjectReadOnlyDictionary(path, readOnly, depth, ancestors, context, segments, namePath);
            if (value is IList list)
                return ProjectList(path, list, depth, ancestors, context, segments, namePath);
            if (value is IEnumerable) return "[unsupported lazy diagnostic collection]";
            return ProjectModel(path, value, depth, ancestors, context, segments, namePath);
        }
        finally { ancestors.Remove(value); }
    }

    private Dictionary<string, object?> ProjectDictionary(string path, IDictionary dictionary, int depth,
        HashSet<object> ancestors, DiagnosticCaptureContext context, string[] segments, string? namePath)
    {
        var output = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (DictionaryEntry item in dictionary)
        {
            if (output.Count >= Options.MaxCollectionEntries || !owner.WorkAvailable()) { output["_diagnostic_limit"] = Limited; break; }
            if (item.Key is not string key) { output[owner.NextFieldKey()] = "[unsupported diagnostic key]"; continue; }
            AddDictionaryEntry(output, key, item.Value, path, depth, ancestors, context, segments, namePath);
        }
        return output;
    }

    private Dictionary<string, object?> ProjectReadOnlyDictionary(string path, IReadOnlyDictionary<string, object?> readOnly,
        int depth, HashSet<object> ancestors, DiagnosticCaptureContext context, string[] segments, string? namePath)
    {
        var output = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var item in readOnly)
        {
            if (output.Count >= Options.MaxCollectionEntries || !owner.WorkAvailable()) { output["_diagnostic_limit"] = Limited; break; }
            AddDictionaryEntry(output, item.Key, item.Value, path, depth, ancestors, context, segments, namePath);
        }
        return output;
    }

    private List<object?> ProjectList(string path, IList list, int depth, HashSet<object> ancestors,
        DiagnosticCaptureContext context, string[] segments, string? namePath)
    {
        var output = new List<object?>();
        for (var index = 0; index < list.Count && index < Options.MaxCollectionEntries && owner.WorkAvailable(); index++)
            output.Add(Project(path, list[index], depth + 1, ancestors, context,
                [.. segments, index.ToString(System.Globalization.CultureInfo.InvariantCulture)], DiagnosticCaptureKind.ListItem,
                namePath: namePath));
        if (output.Count < list.Count) output.Add(Limited);
        return output;
    }

    private void AddDictionaryEntry(Dictionary<string, object?> output, string key, object? item, string path, int depth,
        HashSet<object> ancestors, DiagnosticCaptureContext context, string[] segments, string? namePath)
    {
        var safe = owner.UniqueKey(output, key);
        output[safe] = key.Length > Options.MaxKeyBytes ? Limited : Project(path + "." + key, item, depth + 1, ancestors,
            context, [.. segments, key], DiagnosticCaptureKind.DictionaryKey, namePath: ChildNamePath(namePath, key));
    }

    private Dictionary<string, object?> ProjectModel(string path, object value, int depth, HashSet<object> ancestors,
        DiagnosticCaptureContext context, string[] segments, string? namePath)
    {
        var typedModel = IsModelType(value.GetType());
        var projected = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var member in DiagnosticTypeMetadata.For(value.GetType()).Members)
        {
            if (projected.Count >= Options.MaxCollectionEntries || !owner.WorkAvailable()) { projected["_diagnostic_limit"] = Limited; break; }
            var key = owner.UniqueKey(projected, member.Name);
            var memberNamePath = typedModel ? member.Name : ChildNamePath(namePath, member.Name);
            projected[key] = ProjectMember(path, value, member, depth, ancestors, context, segments, memberNamePath);
        }
        return projected;
    }

    private object? ProjectMember(string path, object value, DiagnosticTypeMetadata.Member member, int depth,
        HashSet<object> ancestors, DiagnosticCaptureContext context, string[] segments, string? memberNamePath)
    {
        if (member.Ignored) return ScopedLogConventions.IgnoredLogValue;
        if (!context.DeferRedaction && owner.RedactionEnabled && member.Sensitive) return Redacted;
        var memberPath = path + "." + member.Name;
        string[] memberSegments = [.. segments, member.Name];
        bool MatchesMemberName() => owner.IsSensitive(memberPath,
            member.Exempt || context.Exempts(DiagnosticCaptureKind.Property, memberSegments), memberNamePath);
        if (!context.DeferRedaction && MatchesMemberName()) return Redacted;
        try
        {
            return Project(memberPath, member.Read(value), depth + 1, ancestors,
                context, memberSegments, DiagnosticCaptureKind.Property, member.Exempt, memberNamePath, member.Sensitive);
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
