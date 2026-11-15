using Microsoft.CodeAnalysis;

namespace DRN.Framework.Hosting.Generators;

internal static class DrnProgramConvention
{
    internal const string BaseMetadataName = "DRN.Framework.Hosting.DrnProgram.DrnProgramBase`1";

    internal static INamedTypeSymbol? FindBase(INamedTypeSymbol type, INamedTypeSymbol programBase)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
            if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, programBase))
                return current;
        return null;
    }
}
