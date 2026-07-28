namespace DRN.Framework.Testing.DataAttributes;

internal static class TheoryDataRowMetadata
{
    /// <summary>
    /// Merges user-provided row metadata with its owning data attribute.
    /// Row values take precedence, skip settings move as a group, and traits are combined.
    /// </summary>
    public static ITheoryDataRow MergeWithAttribute(
        ITheoryDataRow? sourceRow,
        object?[] data,
        IDataAttribute dataAttribute)
    {
        var sourceRowOwnsSkip = sourceRow?.Skip is not null;

        return new TheoryDataRow(data)
        {
            DisableParallelization = sourceRow?.DisableParallelization ?? dataAttribute.DisableParallelization,
            Explicit = sourceRow?.Explicit ?? dataAttribute.Explicit,
            Label = sourceRow?.Label ?? dataAttribute.Label,
            Skip = sourceRow?.Skip ?? dataAttribute.Skip,
            SkipType = sourceRowOwnsSkip ? sourceRow!.SkipType : dataAttribute.SkipType,
            SkipUnless = sourceRowOwnsSkip ? sourceRow!.SkipUnless : dataAttribute.SkipUnless,
            SkipWhen = sourceRowOwnsSkip ? sourceRow!.SkipWhen : dataAttribute.SkipWhen,
            TestDisplayName = sourceRow?.TestDisplayName ?? dataAttribute.TestDisplayName,
            Timeout = sourceRow?.Timeout ?? dataAttribute.Timeout,
            Traits = MergeTraits(sourceRow?.Traits, dataAttribute.Traits)
        };
    }

    /// <summary>
    /// Applies already-merged user metadata to a row reconstructed by the AutoFixture provider.
    /// User metadata takes precedence over provider metadata, and traits are combined.
    /// </summary>
    public static ITheoryDataRow ApplyToGeneratedRow(
        ITheoryDataRow generatedRow,
        ITheoryDataRow metadataRow)
    {
        var metadataRowOwnsSkip = metadataRow.Skip is not null;

        return new TheoryDataRow(generatedRow.GetData())
        {
            DisableParallelization = metadataRow.DisableParallelization ?? generatedRow.DisableParallelization,
            Explicit = metadataRow.Explicit ?? generatedRow.Explicit,
            Label = metadataRow.Label ?? generatedRow.Label,
            Skip = metadataRow.Skip ?? generatedRow.Skip,
            SkipType = metadataRowOwnsSkip ? metadataRow.SkipType : generatedRow.SkipType,
            SkipUnless = metadataRowOwnsSkip ? metadataRow.SkipUnless : generatedRow.SkipUnless,
            SkipWhen = metadataRowOwnsSkip ? metadataRow.SkipWhen : generatedRow.SkipWhen,
            TestDisplayName = metadataRow.TestDisplayName ?? generatedRow.TestDisplayName,
            Timeout = metadataRow.Timeout ?? generatedRow.Timeout,
            Traits = MergeTraits(metadataRow.Traits, generatedRow.Traits)
        };
    }

    /// <summary>
    /// Applies outer attribute metadata to a row reconstructed by the AutoFixture provider.
    /// Attribute metadata takes precedence over provider metadata, and traits are combined.
    /// </summary>
    public static ITheoryDataRow ApplyAttributeToGeneratedRow(
        ITheoryDataRow generatedRow,
        IDataAttribute dataAttribute)
    {
        var attributeMetadata = MergeWithAttribute(null, [], dataAttribute);
        return ApplyToGeneratedRow(generatedRow, attributeMetadata);
    }

    private static Dictionary<string, HashSet<string>> MergeTraits(
        Dictionary<string, HashSet<string>>? rowTraits,
        string[]? attributeTraits)
    {
        var result = CreateTraits(rowTraits);

        if (attributeTraits is null)
            return result;

        for (var index = 0; index < attributeTraits.Length - 1; index += 2)
            AddTrait(result, attributeTraits[index], attributeTraits[index + 1]);

        return result;
    }

    private static Dictionary<string, HashSet<string>> MergeTraits(
        Dictionary<string, HashSet<string>>? primaryTraits,
        Dictionary<string, HashSet<string>>? secondaryTraits)
    {
        var result = CreateTraits(primaryTraits);
        AddTraits(result, secondaryTraits);
        return result;
    }

    private static Dictionary<string, HashSet<string>> CreateTraits(
        Dictionary<string, HashSet<string>>? traits)
    {
        var result = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        AddTraits(result, traits);
        return result;
    }

    private static void AddTraits(
        Dictionary<string, HashSet<string>> target,
        Dictionary<string, HashSet<string>>? source)
    {
        if (source is null)
            return;

        foreach (var (name, values) in source)
            foreach (var value in values)
                AddTrait(target, name, value);
    }

    private static void AddTrait(
        Dictionary<string, HashSet<string>> traits,
        string name,
        string value)
    {
        if (!traits.TryGetValue(name, out var values))
        {
            values = [];
            traits.Add(name, values);
        }

        values.Add(value);
    }
}
