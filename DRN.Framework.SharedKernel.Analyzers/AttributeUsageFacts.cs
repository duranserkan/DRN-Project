using System.Runtime.CompilerServices;
using DRN.Framework.CompilerTools;
using Microsoft.CodeAnalysis;

namespace DRN.Framework.SharedKernel.Analyzers;

internal readonly record struct AttributeUsageFacts(bool Inherited, bool AllowMultiple)
{
    internal sealed class Cache
    {
        private static readonly ConditionalWeakTable<Compilation, Cache> PerCompilation = new();
        private readonly SymbolFactCache<INamedTypeSymbol, AttributeUsageFacts> _values = new(capacity: 31);
        private readonly Func<INamedTypeSymbol, CancellationToken, AttributeUsageFacts> _read;

        private Cache(Compilation compilation)
        {
            UsageAttribute = compilation.GetTypeByMetadataName("System.AttributeUsageAttribute");
            _read = (type, cancellation) => Read(type, UsageAttribute, cancellation);
        }

        internal INamedTypeSymbol? UsageAttribute { get; }
        internal static Cache For(Compilation compilation) => PerCompilation.GetValue(compilation, static key => new(key));
        internal AttributeUsageFacts Get(INamedTypeSymbol type, CancellationToken cancellation) => _values.GetOrAdd(type, _read, cancellation);
    }

    private static AttributeUsageFacts Read(INamedTypeSymbol type, INamedTypeSymbol? usageAttribute, CancellationToken cancellation)
    {
        for (var current = type; current != null; current = current.BaseType)
        {
            cancellation.ThrowIfCancellationRequested();
            foreach (var attribute in current.GetAttributes())
            {
                if (usageAttribute == null || !SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, usageAttribute)) continue;
                var inherited = true;
                var multiple = false;
                foreach (var argument in attribute.NamedArguments)
                {
                    if (argument.Key == "Inherited" && argument.Value.Value is bool inherits) inherited = inherits;
                    if (argument.Key == "AllowMultiple" && argument.Value.Value is bool allowsMultiple) multiple = allowsMultiple;
                }
                return new(inherited, multiple);
            }
        }
        return new(true, false);
    }
}
