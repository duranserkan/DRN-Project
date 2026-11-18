using System.Runtime.CompilerServices;
using DRN.Framework.CompilerTools;
using Microsoft.CodeAnalysis;

namespace DRN.Framework.SharedKernel.Analyzers;

internal static class EntityAnalyzerHelper
{
    internal const string SourceKnownEntityMetadataName = "DRN.Framework.SharedKernel.Domain.SourceKnownEntity";
    internal const string EntityTypeAttributeMetadataName = "DRN.Framework.SharedKernel.Domain.EntityTypeAttribute";
    internal const byte MaxAppId = 127;

    internal sealed class EntityTypeFacts
    {
        private static readonly ConditionalWeakTable<Compilation, EntityTypeFacts> PerCompilation = new();
        private readonly INamedTypeSymbol _entity;
        private readonly SymbolFactCache<INamedTypeSymbol, bool> _derivedTypes = new();
        private readonly Func<INamedTypeSymbol, CancellationToken, bool> _calculateInheritance;

        internal EntityTypeFacts(INamedTypeSymbol entity)
        {
            _entity = entity;
            _calculateInheritance = CalculateInheritance;
        }

        internal static EntityTypeFacts For(Compilation compilation, INamedTypeSymbol sourceKnownEntity) =>
            PerCompilation.GetValue(compilation, _ => new(sourceKnownEntity));

        internal bool IsDerived(INamedTypeSymbol type, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            if (type.BaseType == null) return false;
            if (SymbolEqualityComparer.Default.Equals(type.BaseType, _entity)) return true;
            return _derivedTypes.GetOrAdd(type, _calculateInheritance, cancellation);
        }

        internal bool IsEntity(INamedTypeSymbol type, CancellationToken cancellation) =>
            SymbolEqualityComparer.Default.Equals(type, _entity) || IsDerived(type, cancellation);

        private bool CalculateInheritance(INamedTypeSymbol type, CancellationToken cancellation) =>
            IsOrDerivesFrom(type.BaseType, _entity, cancellation);
    }

    internal enum MetadataStatus { Resolved, UnsupportedAttribute, Unresolvable }

    internal sealed class CompilationMetadata(Compilation compilation)
    {
        private readonly SymbolFactCache<INamedTypeSymbol, bool> _contracts = new(capacity: 31);
        private readonly SymbolFactCache<INamedTypeSymbol, byte?> _appIds = new(capacity: 31);
        private readonly Func<INamedTypeSymbol, CancellationToken, bool> _checkContract =
            (type, cancellation) => EntityAttributeContract.IsSupported(type, compilation, cancellation);
        private readonly Func<INamedTypeSymbol, CancellationToken, byte?> _resolveAppId = new EntityAppIdResolver(compilation).Resolve;

        internal bool IsSupported(INamedTypeSymbol? attribute, CancellationToken cancellation) =>
            attribute != null && _contracts.GetOrAdd(attribute, _checkContract, cancellation);

        internal MetadataStatus Read(AttributeData attribute, CancellationToken cancellation, out byte value, out byte appId)
        {
            cancellation.ThrowIfCancellationRequested();
            value = 0;
            appId = 0;
            if (attribute.AttributeClass is not { } type || !IsSupported(type, cancellation)) return MetadataStatus.UnsupportedAttribute;
            if (attribute.ConstructorArguments is not [var argument] || !TryExtractByte(argument.Value, out value) ||
                _appIds.GetOrAdd(type, _resolveAppId, cancellation) is not { } partition) return MetadataStatus.Unresolvable;
            appId = partition;
            return MetadataStatus.Resolved;
        }
    }

    internal static bool IsEffectivelyPrivate(INamedTypeSymbol typeSymbol)
    {
        for (var current = typeSymbol; current != null; current = current.ContainingType)
            if (current.DeclaredAccessibility == Accessibility.Private)
                return true;

        return false;
    }

    internal static bool IsOrDerivesFrom(INamedTypeSymbol? type, INamedTypeSymbol target, CancellationToken cancellation)
    {
        for (var current = type; current != null; current = current.BaseType)
        {
            cancellation.ThrowIfCancellationRequested();
            if (SymbolEqualityComparer.Default.Equals(current, target)) return true;
        }
        return false;
    }

    internal static AttributeData? FindAttribute(INamedTypeSymbol type, INamedTypeSymbol attribute, CancellationToken cancellation)
    {
        foreach (var candidate in type.GetAttributes())
            if (IsOrDerivesFrom(candidate.AttributeClass, attribute, cancellation)) return candidate;
        return null;
    }

    internal static bool TryExtractByte(object? rawValue, out byte value)
    {
        switch (rawValue)
        {
            case byte b: value = b; return true;
            case sbyte or short or ushort or int or uint or long or ulong:
                var number = Convert.ToDecimal(rawValue);
                if (number is >= byte.MinValue and <= byte.MaxValue)
                {
                    value = (byte)number;
                    return true;
                }
                break;
        }
        value = 0;
        return false;
    }
}
