using DRN.Framework.SharedKernel.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace DRN.Framework.SharedKernel.Analyzers;

internal sealed class DictionaryCaptureAnalysis(OperationAnalysisContext context, DiagnosticRedactionRules rules, IInvocationOperation call,
    DiagnosticNameExemption exemptions,
    DiagnosticDescriptor? descriptor = null, Action<IOperation>? inspectValue = null)
{
    private const int MaxDictionaryWork = 16384;
    private const int MaxDictionaryDepth = 32;
    private readonly HashSet<ISymbol> _activeLocals = new(SymbolEqualityComparer.Default);
    private readonly HashSet<string> _reported = new(StringComparer.Ordinal);
    private readonly HashSet<string> _classifiedKeys = new(StringComparer.Ordinal);
    private Dictionary<ILocalSymbol, List<IOperation>>? _localWrites;
    // Shared locals need path-sensitive revisits, so bound total work as well as depth.
    private int _remainingWork = MaxDictionaryWork;

    private bool TryVisit()
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        if (_remainingWork == 0) return false;
        _remainingWork--;
        return true;
    }

    private string? Report(IOperation? key, bool allowed, string? parentPath, bool safeValue)
    {
        if (key?.ConstantValue is not { HasValue: true, Value: string name }) return null;
        var fullPath = string.IsNullOrEmpty(parentPath) ? name : parentPath + "." + name;
        var matchesPath = rules.MatchesPath(fullPath);
        var reportedName = matchesPath ? fullPath : name;
        if (!safeValue && (matchesPath || !allowed && rules.MatchesField(name)) && _reported.Add(reportedName))
        {
            if (!matchesPath && exemptions.Allows(call.Syntax, reportedName)) return fullPath;
            context.ReportDiagnostic(Diagnostic.Create(descriptor ?? ClassificationDiagnosticDescriptors.DictionaryClassification,
                call.Syntax.GetLocation(), descriptor == null ? reportedName : DiagnosticNameExemption.NameMessage(reportedName, matchesPath)));
        }
        return fullPath;
    }

    internal void Analyze(IOperation value, bool allowKeys, string? capturePath)
    {
        if (allowKeys) CollectClassifiedKeys();
        Inspect(value, allowKeys, 0, capturePath);
    }

    private void CollectClassifiedKeys()
    {
        var classifications = call.Arguments.FirstOrDefault(argument => argument.Parameter?.Name == "classifications")?.Value;
        while (classifications is IConversionOperation conversion) classifications = conversion.Operand;
        if (classifications is not IArrayCreationOperation { Initializer: { } initializer }) return;
        foreach (var entry in initializer.ElementValues)
        {
            if (!TryVisit()) return;
            // Only inline immutable constructor facts prove protection. Unknown expressions remain checked.
            if (entry is not IObjectCreationOperation { Initializer: null } creation ||
                creation.Type?.ToDisplayString() != "DRN.Framework.Utils.Logging.Diagnostics.DiagnosticKeyClassification") continue;
            var key = creation.Arguments.FirstOrDefault(argument => argument.Parameter?.Ordinal == 0)?.Value;
            var kind = creation.Arguments.FirstOrDefault(argument => argument.Parameter?.Ordinal == 1)?.Value;
            if (key?.ConstantValue is { HasValue: true, Value: string name } &&
                kind?.Type?.ToDisplayString() == "DRN.Framework.Utils.Logging.Diagnostics.DiagnosticDataKind" &&
                kind.ConstantValue is { HasValue: true, Value: 1 or 2 })
                _classifiedKeys.Add(name);
        }
    }

    private void Inspect(IOperation operation, bool allowKeys, int depth, string? capturePath)
    {
        if (!TryVisit() || depth > MaxDictionaryDepth) return;
        if (operation is IConversionOperation conversion) { Inspect(conversion.Operand, allowKeys, depth + 1, capturePath); return; }
        // Scalar projections do not capture their dictionary receiver or arguments.
        if (IsScalar(operation.Type)) return;
        if (operation is ILocalReferenceOperation local && _activeLocals.Add(local.Local))
        {
            InspectLocalWrites(local.Local, allowKeys, depth, capturePath);
            // The same local can be captured under several paths. Only suppress active recursion.
            _activeLocals.Remove(local.Local);
            return;
        }
        if (operation is IObjectCreationOperation creation && IsDictionary(creation.Type))
        {
            InspectDictionaryCreation(creation, allowKeys, depth, capturePath);
            return;
        }
        foreach (var child in operation.ChildOperations)
        {
            if (_remainingWork == 0) return;
            Inspect(child, false, depth + 1, null);
        }
    }

    private static bool IsDictionary(ITypeSymbol? type) => type is INamedTypeSymbol named &&
        new[] { named }.Concat(named.AllInterfaces).Any(item => item.ToDisplayString() == "System.Collections.IDictionary" ||
            item.OriginalDefinition.ToDisplayString() is "System.Collections.Generic.IDictionary<TKey, TValue>" or
                "System.Collections.Generic.IReadOnlyDictionary<TKey, TValue>");

    private static bool IsScalar(ITypeSymbol? type)
    {
        if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable)
            type = nullable.TypeArguments[0];
        return type?.SpecialType is SpecialType.System_Boolean or SpecialType.System_Char or SpecialType.System_String or
            SpecialType.System_Byte or SpecialType.System_SByte or SpecialType.System_Int16 or SpecialType.System_UInt16 or
            SpecialType.System_Int32 or SpecialType.System_UInt32 or SpecialType.System_Int64 or SpecialType.System_UInt64 or
            SpecialType.System_Single or SpecialType.System_Double or SpecialType.System_Decimal or SpecialType.System_DateTime ||
            type?.TypeKind == TypeKind.Enum;
    }

    private void InspectDictionaryCreation(IObjectCreationOperation creation, bool allowKeys, int depth, string? capturePath)
    {
        if (creation.Initializer != null)
            foreach (var entry in creation.Initializer.Initializers)
            {
                if (!TryVisit()) return;
                InspectEntry(entry, allowKeys, depth, capturePath);
            }
        foreach (var argument in creation.Arguments)
        {
            if (_remainingWork == 0) return;
            Inspect(argument.Value, false, depth + 1, capturePath);
        }
    }

    private void InspectLocalWrites(ILocalSymbol local, bool allowKeys, int depth, string? capturePath)
    {
        _localWrites ??= IndexLocalWrites();
        if (!_localWrites.TryGetValue(local, out var writes)) return;
        foreach (var write in writes)
        {
            if (!TryVisit()) return;
            if (write is IVariableDeclaratorOperation { Initializer: { } initializer })
                Inspect(initializer.Value, allowKeys, depth + 1, capturePath);
            else if (write is ISimpleAssignmentOperation { Target: ILocalReferenceOperation } assignment)
                Inspect(assignment.Value, allowKeys, depth + 1, capturePath);
            else
                InspectEntry(write, allowKeys, depth, capturePath);
        }
    }

    private Dictionary<ILocalSymbol, List<IOperation>> IndexLocalWrites()
    {
        var writes = new Dictionary<ILocalSymbol, List<IOperation>>(SymbolEqualityComparer.Default);
        var root = call.Parent;
        while (root?.Parent != null) root = root.Parent;
        if (root == null) return writes;
        // Scan once per capture. Bound indexing separately so unrelated operations do not
        // consume the path-sensitive traversal budget on every shared-local visit.
        var remaining = MaxDictionaryWork;
        foreach (var prior in root.Descendants())
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            if (remaining-- == 0) break;
            ILocalSymbol? local;
            if (prior is IVariableDeclaratorOperation { Initializer: not null } declaration)
                local = declaration.Symbol;
            else
                local = prior.Syntax.SpanStart < call.Syntax.SpanStart ? GetWrittenLocal(prior) : null;
            if (local == null) continue;
            if (!writes.TryGetValue(local, out var entries))
            {
                entries = [];
                writes.Add(local, entries);
            }
            entries.Add(prior);
        }
        return writes;
    }

    private static ILocalSymbol? GetWrittenLocal(IOperation operation)
    {
        if (operation is ISimpleAssignmentOperation { Target: ILocalReferenceOperation local }) return local.Local;
        var receiver = operation switch
        {
            IInvocationOperation { TargetMethod.Name: "Add", Instance: ILocalReferenceOperation instance } => instance,
            ISimpleAssignmentOperation
            {
                Target: IPropertyReferenceOperation { Property.IsIndexer: true, Instance: ILocalReferenceOperation instance }
            } => instance,
            _ => null
        };
        return receiver != null && IsDictionary(receiver.Type) ? receiver.Local : null;
    }

    private void InspectEntry(IOperation entry, bool allowKeys, int depth, string? capturePath)
    {
        if (entry is IInvocationOperation add)
        {
            foreach (var argument in add.Arguments.Skip(1))
                InspectEntryValue(add.Arguments.FirstOrDefault()?.Value, argument.Value, allowKeys, depth, capturePath);
        }
        else if (entry is ISimpleAssignmentOperation { Target: IPropertyReferenceOperation index } assignment)
            InspectEntryValue(index.Arguments.FirstOrDefault()?.Value, assignment.Value, allowKeys, depth, capturePath);
    }

    private void InspectEntryValue(IOperation? key, IOperation value, bool allowKeys, int depth, string? capturePath)
    {
        // A positive classification redacts the whole entry, including its descendants.
        if (allowKeys && key?.ConstantValue is { HasValue: true, Value: string name } && _classifiedKeys.Contains(name)) return;
        var childPath = Report(key, allowKeys, capturePath, LoggingCaptureAnalysis.IsSafeConstant(value));
        inspectValue?.Invoke(value);
        Inspect(value, false, depth + 1, childPath);
    }
}
