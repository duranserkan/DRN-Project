using Microsoft.CodeAnalysis.Operations;

namespace DRN.Framework.SharedKernel.Analyzers.Conventions;

internal readonly record struct FlowState(
    Dictionary<ILocalSymbol, List<LocalBinding>> Env,
    HashSet<IAnonymousFunctionOperation> ActiveLambdas);

internal static class SelectorFlowEnvironment
{
    internal static FlowState CreateInitialState(SelectorScope? parent)
    {
        var state = new FlowState(new(SymbolEqualityComparer.Default), []);
        for (var scope = parent; scope is not null; scope = scope.Parent)
        {
            foreach (var (local, values) in scope.CapturedLocalValues)
                if (!state.Env.ContainsKey(local)) state.Env[local] = [.. values];
        }
        return state;
    }

    internal static FlowState Clone(FlowState state)
    {
        var env = new Dictionary<ILocalSymbol, List<LocalBinding>>(state.Env.Count, SymbolEqualityComparer.Default);
        foreach (var (local, values) in state.Env) env[local] = [.. values];
        return new(env, [.. state.ActiveLambdas]);
    }

    internal static void Replace(FlowState target, FlowState source)
    {
        target.Env.Clear();
        foreach (var (local, values) in source.Env)
            target.Env[local] = values;
        target.ActiveLambdas.Clear();
        target.ActiveLambdas.UnionWith(source.ActiveLambdas);
    }

    internal static Dictionary<ILocalSymbol, ImmutableArray<LocalBinding>> ToImmutableMap(
        Dictionary<ILocalSymbol, List<LocalBinding>> source)
    {
        var result = new Dictionary<ILocalSymbol, ImmutableArray<LocalBinding>>(source.Count, SymbolEqualityComparer.Default);
        foreach (var (local, values) in source) result[local] = [.. values];
        return result;
    }

    internal static List<BranchWriteDiff>? CollectBranchDiffs(
        Dictionary<ILocalSymbol, List<LocalBinding>> firstEnv,
        Dictionary<ILocalSymbol, List<LocalBinding>> secondEnv)
    {
        List<BranchWriteDiff>? writes = null;
        foreach (var (local, firstValues) in firstEnv)
        {
            secondEnv.TryGetValue(local, out var secondValues);
            CollectModifiedBindings(local, firstValues, secondValues, ref writes);
        }
        foreach (var (local, secondValues) in secondEnv)
        {
            if (firstEnv.ContainsKey(local)) continue;
            CollectModifiedBindings(local, null, secondValues, ref writes);
        }
        return writes;
    }

    internal static void MergeBranchEnv(
        Dictionary<ILocalSymbol, List<LocalBinding>> target,
        Dictionary<ILocalSymbol, List<LocalBinding>> alternate)
    {
        foreach (var (local, targetValues) in target)
        {
            alternate.TryGetValue(local, out var alternateValues);
            DemoteUnsharedReplacements(targetValues, alternateValues);
        }
        foreach (var (local, alternateValues) in alternate)
        {
            if (!target.TryGetValue(local, out var targetValues))
            {
                var copied = new List<LocalBinding>(alternateValues.Count);
                for (var i = 0; i < alternateValues.Count; i++)
                {
                    var binding = alternateValues[i];
                    AddUnique(copied, binding.ReplacesMember ? binding with { ReplacesMember = false } : binding);
                }
                target[local] = copied;
                continue;
            }
            for (var i = 0; i < alternateValues.Count; i++)
            {
                var binding = alternateValues[i];
                var keepsReplacement = binding.ReplacesMember &&
                    BranchDefinitelyReplacesSameMember(targetValues, binding.WriteTarget);
                var effective = binding.ReplacesMember && !keepsReplacement
                    ? binding with { ReplacesMember = false }
                    : binding;
                AddUnique(targetValues, effective);
            }
        }
    }

    internal static void MergeInto(
        Dictionary<ILocalSymbol, List<LocalBinding>> target,
        Dictionary<ILocalSymbol, List<LocalBinding>> source)
    {
        foreach (var (local, values) in source)
            MergeLocalBindings(target, local, values);
    }

    internal static void MergeLocalBindings(
        Dictionary<ILocalSymbol, List<LocalBinding>> target, ILocalSymbol local, List<LocalBinding> values)
    {
        if (!target.TryGetValue(local, out var existing))
        {
            target[local] = [.. values];
            return;
        }
        for (var i = 0; i < values.Count; i++) AddUnique(existing, values[i]);
    }

    internal static bool ContainsBinding(List<LocalBinding> values, LocalBinding candidate)
    {
        for (var i = 0; i < values.Count; i++)
        {
            if (LocalBindingComparer.Instance.Equals(values[i], candidate))
                return true;
        }
        return false;
    }

    internal static void AddUnique(List<LocalBinding> target, LocalBinding value)
    {
        if (!ContainsBinding(target, value)) target.Add(value);
    }

    private static void CollectModifiedBindings(
        ILocalSymbol local, List<LocalBinding>? firstValues, List<LocalBinding>? secondValues,
        ref List<BranchWriteDiff>? writes)
    {
        if (SameBindingList(firstValues, secondValues)) return;
        if (firstValues is not null)
        {
            for (var i = 0; i < firstValues.Count; i++)
            {
                var binding = firstValues[i];
                if (secondValues is null || !ContainsBinding(secondValues, binding))
                    (writes ??= []).Add(new(local, binding.WriteTarget));
            }
        }
        if (secondValues is not null)
        {
            for (var i = 0; i < secondValues.Count; i++)
            {
                var binding = secondValues[i];
                if (firstValues is null || !ContainsBinding(firstValues, binding))
                    (writes ??= []).Add(new(local, binding.WriteTarget));
            }
        }
    }

    private static bool SameBindingList(List<LocalBinding>? left, List<LocalBinding>? right)
    {
        if (ReferenceEquals(left, right)) return true;
        if (left is null || right is null || left.Count != right.Count) return false;
        for (var i = 0; i < left.Count; i++)
        {
            if (!LocalBindingComparer.Instance.Equals(left[i], right[i]))
                return false;
        }
        return true;
    }

    private static void DemoteUnsharedReplacements(List<LocalBinding> values, List<LocalBinding>? otherBranch)
    {
        var demoted = false;
        for (var i = 0; i < values.Count; i++)
        {
            var binding = values[i];
            if (!binding.ReplacesMember || BranchDefinitelyReplacesSameMember(otherBranch, binding.WriteTarget))
                continue;
            values[i] = binding with { ReplacesMember = false };
            demoted = true;
        }
        if (!demoted) return;
        for (var i = values.Count - 1; i >= 0; i--)
        {
            for (var j = 0; j < i; j++)
            {
                if (!LocalBindingComparer.Instance.Equals(values[j], values[i])) continue;
                values.RemoveAt(i);
                break;
            }
        }
    }

    private static bool BranchDefinitelyReplacesSameMember(List<LocalBinding>? bindings, IOperation? writeTarget)
    {
        if (bindings is null || PropertyAccessFacts.DirectWrittenProperty(writeTarget) is not { } property)
            return false;
        for (var i = 0; i < bindings.Count; i++)
        {
            var binding = bindings[i];
            if (binding.ReplacesMember &&
                PropertyAccessFacts.DirectWrittenProperty(binding.WriteTarget) is { } candidate &&
                PropertyAccessFacts.SameOrMatchingProperty(candidate, property))
                return true;
        }
        return false;
    }
}
