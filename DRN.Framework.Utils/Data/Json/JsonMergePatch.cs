using System.Text.Json.Nodes;

namespace DRN.Framework.Utils.Data.Json;

public static class JsonMergePatch
{
    public record MergeResult(JsonNode Json, bool Changed);

    /// <summary>
    /// Applies a JSON Merge Patch with optional original node modification
    /// </summary>
    /// <param name="target">Original JSON node</param>
    /// <param name="patch">Merge patch node</param>
    /// <param name="maxDepth">Maximum recursion depth (default: 64)</param>
    /// <param name="changeOriginal">When true, modifies and returns original node instead of cloning</param>
    /// <returns>Merged JsonNode (original or new instance)</returns>
    public static MergeResult SafeApplyMergePatch(JsonNode target, JsonNode patch, bool changeOriginal, int maxDepth = 64) =>
        maxDepth <= 0
            ? throw new ArgumentException("Max depth must be positive", nameof(maxDepth))
            : ApplyMergePatchImpl(target, patch, maxDepth, changeOriginal, currentDepth: 0);

    private static MergeResult ApplyMergePatchImpl(JsonNode target, JsonNode patch, int maxDepth, bool changeOriginal, int currentDepth)
    {
        ValidateDepth(currentDepth, maxDepth);

        // Handle non-object patches (RFC 7386 §2: replace entire target)
        if (patch is not JsonObject patchObject)
            return new MergeResult(patch.DeepClone(), true);

        // Handle non-object targets (replace with patch object)
        if (target is not JsonObject targetObject)
            return new MergeResult(patch.DeepClone(), true);

        currentDepth++;
        ValidateDepth(currentDepth, maxDepth);

        var mergedObject = changeOriginal // Use original or create clone based on flag
            ? targetObject
            : targetObject.DeepClone().AsObject();

        var changed = MergeObjectsImpl(mergedObject, patchObject, maxDepth, currentDepth);

        return new MergeResult(mergedObject, changed);
    }

    private static bool MergeObjectsImpl(JsonObject target, JsonObject patch, int maxDepth, int currentDepth)
    {
        var anyChanged = false;
        foreach (var (key, patchValue) in patch)
        {
            if (patchValue is null)
            {
                target.Remove(key);
                anyChanged = true;
                continue;
            }

            var (newValue, changed) = HandlePropertyMerge(target, key, patchValue, maxDepth, currentDepth);
            if (!changed) continue;

            target[key] = newValue;
            anyChanged = true;
        }

        return anyChanged;
    }

    private static (JsonNode? Value, bool Changed) HandlePropertyMerge(
        JsonObject target, string key, JsonNode patchValue, int maxDepth, int currentDepth)
    {
        if (!target.TryGetPropertyValue(key, out var targetValue))
            return (patchValue.DeepClone(), true); // New property - always changed

        if (targetValue is not JsonObject targetObject || patchValue is not JsonObject patchObject)
            return !JsonNode.DeepEquals(targetValue, patchValue) // Value replacement check
                ? (patchValue.DeepClone(), true)
                : (null, false);

        ValidateDepth(currentDepth + 1, maxDepth);
        var mergedChild = targetObject.DeepClone().AsObject();
        var childChanged = MergeObjectsImpl(mergedChild, patchObject, maxDepth, currentDepth + 1);

        return childChanged ? (mergedChild, true) : (null, false);
    }

    private static void ValidateDepth(int currentDepth, int maxDepth)
    {
        if (currentDepth > maxDepth)
            throw new InvalidOperationException(
                $"Maximum recursion depth {maxDepth} exceeded. " +
                "Prevents stack overflow attacks and complex document abuse.");
    }
}