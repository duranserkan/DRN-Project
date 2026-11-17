using Microsoft.CodeAnalysis;

namespace DRN.Framework.SharedKernel.Analyzers;

internal readonly record struct ModelTypeFacts(bool Scalar, bool Model, bool TimeSpan, bool SkipProperties, ITypeSymbol? ListElement)
{
    internal static ModelTypeFacts Read(ITypeSymbol type, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        if (type.IsReferenceType && type.NullableAnnotation != NullableAnnotation.NotAnnotated)
            type = type.WithNullableAnnotation(NullableAnnotation.NotAnnotated);
        var name = type.ToDisplayString();
        var timeSpan = name is "System.TimeSpan" or "System.TimeSpan?";
        if (IsScalar(type, name)) return new(true, false, timeSpan, true, null);
        if (type is not INamedTypeSymbol named) return new(false, false, timeSpan, false, null);

        var uri = DerivesFromUri(named, cancellation);
        var (dictionary, enumerable, listElement) = ReadCollectionFacts(named, cancellation);
        var json = name == "System.Text.Json.JsonElement";
        var model = named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T
            ? Read(named.TypeArguments[0], cancellation).Model
            : !named.IsAnonymousType && !json && !uri && !enumerable;
        var skipProperties = uri || dictionary || json || (enumerable && listElement is null);
        return new(false, model, timeSpan, skipProperties, listElement);
    }

    private static bool IsScalar(ITypeSymbol type, string name) =>
        type.SpecialType != SpecialType.None || type.TypeKind is TypeKind.Enum or TypeKind.TypeParameter ||
        name is "System.TimeSpan" or "System.DateTimeOffset" or "System.DateOnly" or "System.TimeOnly" or
            "System.Uri" or "System.Guid" or "DRN.Framework.Utils.Logging.ScopeEvent";

    private static bool DerivesFromUri(INamedTypeSymbol named, CancellationToken cancellation)
    {
        for (var current = named; current != null; current = current.BaseType)
        {
            cancellation.ThrowIfCancellationRequested();
            if (current.ToDisplayString() == "System.Uri") return true;
        }
        return false;
    }

    private static (bool Dictionary, bool Enumerable, ITypeSymbol? ListElement) ReadCollectionFacts(
        INamedTypeSymbol named, CancellationToken cancellation)
    {
        var dictionary = false;
        var enumerable = false;
        ITypeSymbol? listElement = null;
        InspectContract(named, ref dictionary, ref enumerable, ref listElement);
        foreach (var contract in named.AllInterfaces)
        {
            cancellation.ThrowIfCancellationRequested();
            InspectContract(contract, ref dictionary, ref enumerable, ref listElement);
        }
        return (dictionary, enumerable, listElement);
    }

    private static void InspectContract(INamedTypeSymbol contract, ref bool dictionary, ref bool enumerable, ref ITypeSymbol? listElement)
    {
        var contractName = contract.ToDisplayString();
        var definitionName = contract.OriginalDefinition.ToDisplayString();
        dictionary |= contractName == "System.Collections.IDictionary" ||
                      definitionName == "System.Collections.Generic.IReadOnlyDictionary<TKey, TValue>";
        enumerable |= contractName == "System.Collections.IEnumerable";
        if (listElement is null && definitionName == "System.Collections.Generic.IList<T>")
        {
            var element = contract.TypeArguments[0];
            if (element.IsReferenceType && element.NullableAnnotation != NullableAnnotation.NotAnnotated)
                element = element.WithNullableAnnotation(NullableAnnotation.NotAnnotated);
            listElement = element;
        }
    }
}
