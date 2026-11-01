using DRN.Framework.SharedKernel.Attributes;
using DRN.Framework.Utils.Logging.Diagnostics;

namespace DRN.Test.Unit.Tests.Framework.Utils.Logging;

public sealed class DiagnosticTypeMetadataTests
{
    [Fact]
    public void Registration_Should_Defer_Attributes_And_Cache_Known_And_Unknown_Type_Decisions()
    {
        var assembly = typeof(DiagnosticTypeMetadataTests).Assembly;
        var before = CountingClassificationAttribute.Constructions;
        DiagnosticTypeMetadata.Register(assembly, assembly.GetTypes());
        CountingClassificationAttribute.Constructions.Should().Be(before);

        var known = DiagnosticTypeMetadata.For(typeof(RegisteredModel));
        var runtimePlan = DiagnosticTypeMetadata.For(typeof(GenericModel<int>));
        var ordinary = DiagnosticTypeMetadata.For(typeof(UnclassifiedModel));
        known.Classified.Should().BeTrue();
        runtimePlan.Classified.Should().BeTrue();
        ordinary.Classified.Should().BeFalse();
        var after = CountingClassificationAttribute.Constructions;
        for (var index = 0; index < 10; index++)
        {
            DiagnosticTypeMetadata.For(typeof(RegisteredModel)).Should().BeSameAs(known);
            DiagnosticTypeMetadata.For(typeof(GenericModel<int>)).Should().BeSameAs(runtimePlan);
            DiagnosticTypeMetadata.For(typeof(UnclassifiedModel)).Should().BeSameAs(ordinary);
        }
        CountingClassificationAttribute.Constructions.Should().Be(after);
    }

    [Fact]
    public void Warm_Type_Lookups_Should_Not_Allocate()
    {
        var type = typeof(WarmModel<long>);
        for (var index = 0; index < 1000; index++) DiagnosticTypeMetadata.For(type);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < 1000; index++) DiagnosticTypeMetadata.For(type);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        allocated.Should().Be(0);
    }

    [CountingClassification] private sealed class RegisteredModel;
    [CountingClassification] private sealed class GenericModel<T>;
    [Personal] private sealed class WarmModel<T>;
    private sealed class UnclassifiedModel;

    private sealed class CountingClassificationAttribute : PersonalAttribute
    {
        private static int _constructions;
        internal static int Constructions => Volatile.Read(ref _constructions);
        public CountingClassificationAttribute() => Interlocked.Increment(ref _constructions);
    }
}
