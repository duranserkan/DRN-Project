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
        var scalar = type.SpecialType != SpecialType.None || type.TypeKind is TypeKind.Enum or TypeKind.TypeParameter ||
            name is "System.TimeSpan" or "System.DateTimeOffset" or "System.DateOnly" or "System.TimeOnly" or
                "System.Uri" or "System.Guid" or "DRN.Framework.Utils.Logging.ScopeEvent";
        if (scalar) return new(true, false, timeSpan, true, null);
        if (type is not INamedTypeSymbol named) return new(scalar, false, timeSpan, false, null);

        var uri = false;
        for (var current = named; current != null; current = current.BaseType)
        {
            cancellation.ThrowIfCancellationRequested();
            if (current.ToDisplayString() != "System.Uri") continue;
            uri = true;
            break;
        }
        var dictionary = false;
        var enumerable = false;
        ITypeSymbol? listElement = null;
        InspectContract(named);
        foreach (var contract in named.AllInterfaces)
        {
            cancellation.ThrowIfCancellationRequested();
            InspectContract(contract);
        }
        var json = name == "System.Text.Json.JsonElement";
        var model = !named.IsAnonymousType && type.SpecialType == SpecialType.None && type.TypeKind != TypeKind.Enum &&
            name is not ("System.TimeSpan" or "System.DateTimeOffset" or "System.DateOnly" or "System.TimeOnly" or
                "System.Guid" or "System.Text.Json.JsonElement" or "DRN.Framework.Utils.Logging.ScopeEvent") &&
            !uri && !enumerable;
        if (named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
            model = Read(named.TypeArguments[0], cancellation).Model;
        return new(scalar, model, timeSpan, uri || dictionary || json || listElement is null && enumerable, listElement);

        void InspectContract(INamedTypeSymbol contract)
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
}
