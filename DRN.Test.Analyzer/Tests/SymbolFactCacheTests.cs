using System.Diagnostics.CodeAnalysis;
using DRN.Framework.CompilerTools;
using static DRN.Test.Analyzer.Tests.DiagnosticClassificationAnalyzerTests;

namespace DRN.Test.Analyzer.Tests;

[SuppressMessage("ReSharper", "AccessToDisposedClosure")]
public sealed class SymbolFactCacheTests
{
    [Fact]
    public void Successful_Facts_Should_Be_Reused_Including_Negative_Results()
    {
        var compilation = Compilation("public class Model<T> { }");
        var definition = compilation.GetTypeByMetadataName("Model`1")!;
        var integer = definition.Construct(compilation.GetSpecialType(SpecialType.System_Int32));
        var text = definition.Construct(compilation.GetSpecialType(SpecialType.System_String));
        var cache = new SymbolFactCache<INamedTypeSymbol, bool>();
        var calls = 0;

        bool Calculate(INamedTypeSymbol type, CancellationToken cancellation)
        {
            calls++;
            return type.TypeArguments[0].SpecialType == SpecialType.System_String;
        }

        cache.GetOrAdd(integer, Calculate, CancellationToken.None).Should().BeFalse();
        cache.GetOrAdd(text, Calculate, CancellationToken.None).Should().BeTrue();
        cache.GetOrAdd(definition.Construct(compilation.GetSpecialType(SpecialType.System_Int32)), Calculate, CancellationToken.None)
            .Should().BeFalse();
        cache.GetOrAdd(text, Calculate, CancellationToken.None).Should().BeTrue();
        calls.Should().Be(2);

        new SymbolFactCache<INamedTypeSymbol, bool>().GetOrAdd(integer, Calculate, CancellationToken.None).Should().BeFalse();
        calls.Should().Be(3);
    }

    [Fact]
    public void Cancellation_And_Failure_Should_Not_Publish_A_Fact_And_Canceled_Hits_Should_Throw()
    {
        var type = Compilation("public class Model { }").GetTypeByMetadataName("Model")!;
        var cache = new SymbolFactCache<INamedTypeSymbol, bool>();
        using var cancellation = new CancellationTokenSource();
        Action canceledCalculation = () => cache.GetOrAdd(type, (_, _) =>
        {
            cancellation.Cancel();
            return true;
        }, cancellation.Token);
        canceledCalculation.Should().Throw<OperationCanceledException>();

        Action failedCalculation = () => cache.GetOrAdd(type,
            static (_, _) => throw new InvalidOperationException("Calculation failed."), CancellationToken.None);
        failedCalculation.Should().Throw<InvalidOperationException>();

        cache.GetOrAdd(type, static (_, _) => false, CancellationToken.None).Should().BeFalse();
        Action canceledHit = () => cache.GetOrAdd(type, static (_, _) => true, cancellation.Token);
        canceledHit.Should().Throw<OperationCanceledException>();
        cache.GetOrAdd(type, static (_, _) => throw new InvalidOperationException("Unexpected recalculation."), CancellationToken.None)
            .Should().BeFalse();
    }

    [Fact]
    public void Concurrent_Consumers_Should_Observe_The_Published_Value()
    {
        var type = Compilation("public class Model { }").GetTypeByMetadataName("Model")!;
        var cache = new SymbolFactCache<INamedTypeSymbol, object>();
        var results = new object[32];
        Parallel.For(0, results.Length, index =>
            results[index] = cache.GetOrAdd(type, static (_, _) => new object(), CancellationToken.None));

        foreach (var result in results) result.Should().BeSameAs(results[0]);
        cache.GetOrAdd(type, static (_, _) => throw new InvalidOperationException("Unexpected recalculation."), CancellationToken.None)
            .Should().BeSameAs(results[0]);
    }

    [Fact]
    public void Custom_Small_Capacity_Should_Grow_Cleanly_Without_Losing_Cached_Entries()
    {
        var compilation = Compilation("""
            public class A;
            public class B;
            public class C;
            public class D;
            public class E;
            """);
        var cache = new SymbolFactCache<INamedTypeSymbol, string>(capacity: 2);
        string[] names = ["A", "B", "C", "D", "E"];
        foreach (var name in names)
        {
            var symbol = compilation.GetTypeByMetadataName(name)!;
            cache.GetOrAdd(symbol, static (item, _) => item.Name, CancellationToken.None).Should().Be(name);
        }

        foreach (var name in names)
        {
            var symbol = compilation.GetTypeByMetadataName(name)!;
            cache.GetOrAdd(symbol, static (_, _) => throw new InvalidOperationException("Unexpected recalculation."), CancellationToken.None)
                .Should().Be(name);
        }
    }
}
