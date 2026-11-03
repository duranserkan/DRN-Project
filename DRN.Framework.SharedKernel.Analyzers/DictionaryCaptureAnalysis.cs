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
    // Shared locals need path-sensitive revisits, so bound total work as well as depth.
    private int _remainingWork = MaxDictionaryWork;

    private bool TryVisit()
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        if (_remainingWork == 0) return false;
        _remainingWork--;
        return true;
    }

    private string? Report(IOperation? key, bool allowed, string? parentPath)
    {
        if (key?.ConstantValue is not { HasValue: true, Value: string name }) return null;
        var fullPath = string.IsNullOrEmpty(parentPath) ? name : parentPath + "." + name;
        var matchesPath = rules.MatchesPath(fullPath);
        var reportedName = matchesPath ? fullPath : name;
        if (!allowed && (rules.MatchesField(name) || matchesPath) && _reported.Add(reportedName))
        {
            if (!matchesPath && exemptions.Allows(call.Syntax, reportedName)) return fullPath;
            context.ReportDiagnostic(Diagnostic.Create(descriptor ?? ClassificationDiagnosticDescriptors.DictionaryClassification,
                call.Syntax.GetLocation(), descriptor == null ? reportedName : DiagnosticNameExemption.NameMessage(reportedName, matchesPath)));
        }
        return fullPath;
    }

    internal void Analyze(IOperation value, bool allowKeys, string? capturePath) =>
        Inspect(value, allowKeys, 0, capturePath);

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
        // Reuse the analyzed operation tree for declarations and direct writes before capture.
        var root = call.Parent;
        while (root?.Parent != null) root = root.Parent;
        if (root == null) return;
        foreach (var prior in root.Descendants())
        {
            if (!TryVisit()) break;
            InspectLocalWrite(prior, local, allowKeys, depth, capturePath);
        }
    }

    private void InspectLocalWrite(IOperation prior, ILocalSymbol local, bool allowKeys, int depth, string? capturePath)
    {
        if (prior is IVariableDeclaratorOperation { Initializer: { } initializer } declaration &&
            SymbolEqualityComparer.Default.Equals(declaration.Symbol, local))
            Inspect(initializer.Value, allowKeys, depth + 1, capturePath);
        if (prior.Syntax.SpanStart >= call.Syntax.SpanStart) return;
        if (prior is ISimpleAssignmentOperation { Target: ILocalReferenceOperation assignedLocal } localAssignment &&
            SymbolEqualityComparer.Default.Equals(assignedLocal.Local, local))
            Inspect(localAssignment.Value, allowKeys, depth + 1, capturePath);
        if (IsDictionaryWrite(prior, local))
            InspectEntry(prior, allowKeys, depth, capturePath);
    }

    private static bool IsDictionaryWrite(IOperation operation, ILocalSymbol local)
    {
        var receiver = operation switch
        {
            IInvocationOperation { TargetMethod.Name: "Add", Instance: ILocalReferenceOperation instance } => instance,
            ISimpleAssignmentOperation
            {
                Target: IPropertyReferenceOperation { Property.IsIndexer: true, Instance: ILocalReferenceOperation instance }
            } => instance,
            _ => null
        };
        return receiver != null && SymbolEqualityComparer.Default.Equals(receiver.Local, local) && IsDictionary(receiver.Type);
    }

    private void InspectEntry(IOperation entry, bool allowKeys, int depth, string? capturePath)
    {
        if (entry is IInvocationOperation add)
        {
            var childPath = Report(add.Arguments.FirstOrDefault()?.Value, allowKeys, capturePath);
            foreach (var argument in add.Arguments.Skip(1))
            {
                inspectValue?.Invoke(argument.Value);
                Inspect(argument.Value, false, depth + 1, childPath);
            }
        }
        else if (entry is ISimpleAssignmentOperation { Target: IPropertyReferenceOperation index } assignment)
        {
            var childPath = Report(index.Arguments.FirstOrDefault()?.Value, allowKeys, capturePath);
            inspectValue?.Invoke(assignment.Value);
            Inspect(assignment.Value, false, depth + 1, childPath);
        }
    }
}
