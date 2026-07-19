using System.Text.Json.Nodes;

namespace DRN.Framework.Utils.Data.Json;

public static class JsonMergePatch
{
    public readonly record struct MergeResult(JsonNode? Json, bool Changed);

    /// <summary>
    /// Applies an RFC 7396 JSON Merge Patch without mutating the target or patch.
    /// </summary>
    /// <param name="target">Original JSON node; <see langword="null"/> represents a JSON null or undefined target.</param>
    /// <param name="patch">Merge patch node; <see langword="null"/> represents a root-level JSON null.</param>
    /// <param name="maxDepth">Maximum object or array nesting depth allowed in the patch (default: 64).</param>
    /// <returns>
    /// The merged JSON and whether its value differs from <paramref name="target"/>. An unchanged result reuses
    /// <paramref name="target"/>; a changed non-null result is detached from both inputs.
    /// </returns>
    public static MergeResult SafeApplyMergePatch(JsonNode? target, JsonNode? patch, int maxDepth = 64)
    {
        ValidateMaxDepth(maxDepth);
        ValidatePatchDepth(patch, maxDepth);

        if (!WouldChange(target, patch))
            return new MergeResult(target, false);

        if (patch is not JsonObject patchObject)
            return new MergeResult(patch?.DeepClone(), true);

        var mergedObject = target is JsonObject targetObject
            ? targetObject.DeepClone().AsObject()
            : new JsonObject(patchObject.Options);

        ApplyObjectMergePatchInPlaceCore(mergedObject, patchObject);
        return new MergeResult(mergedObject, true);
    }

    /// <summary>
    /// Applies the object-to-object branch of RFC 7396 directly to an existing target object.
    /// </summary>
    /// <param name="target">Target object to mutate, including existing nested objects.</param>
    /// <param name="patch">Object patch. It is never mutated.</param>
    /// <param name="maxDepth">Maximum object or array nesting depth allowed in the patch (default: 64).</param>
    /// <returns><see langword="true"/> when the target document changed; otherwise <see langword="false"/>.</returns>
    /// <remarks>
    /// Use <see cref="SafeApplyMergePatch"/> for the complete RFC operation, including root replacement by arrays,
    /// scalars, or JSON null. This method is object-only so its in-place guarantee remains exact.
    /// </remarks>
    public static bool ApplyObjectMergePatchInPlace(JsonObject target, JsonObject patch, int maxDepth = 64)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(patch);
        ValidateMaxDepth(maxDepth);
        ValidatePatchDepth(patch, maxDepth);

        var sharesTreeWithTarget = ReferenceEquals(target.Root, patch.Root);
        if (sharesTreeWithTarget && !WouldChange(target, patch))
            return false;

        var detachedPatch = sharesTreeWithTarget ? patch.DeepClone().AsObject() : patch;

        return ApplyObjectMergePatchInPlaceCore(target, detachedPatch);
    }

    private static bool WouldChange(JsonNode? target, JsonNode? patch)
    {
        if (patch is not JsonObject patchObject)
            return !JsonNode.DeepEquals(target, patch);

        if (target is not JsonObject targetObject)
            return true;

        foreach (var (key, patchValue) in patchObject)
        {
            if (patchValue is null)
            {
                if (targetObject.ContainsKey(key))
                    return true;

                continue;
            }

            targetObject.TryGetPropertyValue(key, out var targetValue);
            if (WouldChange(targetValue, patchValue))
                return true;
        }

        return false;
    }

    private static bool ApplyObjectMergePatchInPlaceCore(JsonObject target, JsonObject patch)
    {
        var anyChanged = false;
        foreach (var (key, patchValue) in patch)
        {
            if (patchValue is null)
            {
                anyChanged |= target.Remove(key);
                continue;
            }

            var propertyExists = target.TryGetPropertyValue(key, out var targetValue);
            if (patchValue is JsonObject patchObject)
            {
                if (targetValue is JsonObject targetObject)
                {
                    anyChanged |= ApplyObjectMergePatchInPlaceCore(targetObject, patchObject);
                    continue;
                }

                var mergedChild = new JsonObject(patchObject.Options);
                ApplyObjectMergePatchInPlaceCore(mergedChild, patchObject);
                target[key] = mergedChild;
                anyChanged = true;
                continue;
            }

            if (propertyExists && JsonNode.DeepEquals(targetValue, patchValue))
                continue;

            target[key] = patchValue.DeepClone();
            anyChanged = true;
        }

        return anyChanged;
    }

    private static void ValidateMaxDepth(int maxDepth)
    {
        if (maxDepth <= 0)
            throw new ArgumentException("Max depth must be positive", nameof(maxDepth));
    }

    private static void ValidatePatchDepth(JsonNode? patch, int maxDepth)
    {
        if (patch is not JsonObject && patch is not JsonArray)
            return;

        var containers = new Stack<(JsonNode Node, int Depth)>();
        containers.Push((patch!, 1));

        while (containers.TryPop(out var container))
        {
            ValidateContainerDepth(container.Depth, maxDepth);

            switch (container.Node)
            {
                case JsonObject jsonObject:
                    foreach (var (_, child) in jsonObject)
                        PushContainer(child, container.Depth + 1, containers);
                    break;
                case JsonArray jsonArray:
                    foreach (var child in jsonArray)
                        PushContainer(child, container.Depth + 1, containers);
                    break;
            }
        }
    }

    private static void PushContainer(JsonNode? node, int depth, Stack<(JsonNode Node, int Depth)> containers)
    {
        if (node is JsonObject or JsonArray)
            containers.Push((node, depth));
    }

    private static void ValidateContainerDepth(int currentDepth, int maxDepth)
    {
        if (currentDepth > maxDepth)
            throw new InvalidOperationException(
                $"Maximum patch depth {maxDepth} exceeded. Prevents stack overflow attacks and complex document abuse.");
    }
}
