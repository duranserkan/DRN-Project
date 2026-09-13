namespace DRN.Test.Analyzer.Tests;

public partial class SourceKnownEntityTypeAnalyzerTests
{
    private const string FreshEntityStubs = """
        using DRN.Framework.SharedKernel.Domain;
        [EntityType<DefaultApp>(1)] public class Order : AggregateRoot
        {
            public string Name { get; }
            public Order(string name = "order") { Name = name; }
        }
        [EntityType<DefaultApp>(2)] public class Customer(long id = 0) : AggregateRoot(id);
        """;

    [Fact]
    public async Task FreshEntities_RejectOperationsOnDirectLocalsAliasesAndNumericIdConstruction()
    {
        const string source = """
            using System;
            using DRN.Framework.SharedKernel.Domain;
            public class Consumer
            {
                public void Run(Guid externalId, SourceKnownEntityId parsedId, long generatedId)
                {
                    new Order().ToSecure(parsedId);
                    var order = new Order();
                    order.GetEntityId<Customer>(externalId);
                    var alias = order;
                    alias.ToPlain(parsedId);
                    SourceKnownEntity other = alias;
                    other.GetEntityId<Customer>(generatedId);
                    new Customer(generatedId).ToSecure(parsedId);
                }
            }
            """;

        var diagnostics = await RunAnalyzerAsync(FreshEntityStubs, source);
        diagnostics.Should().HaveCount(5);
        diagnostics.Should().OnlyContain(d => d.Id == "DRN0014" && d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public async Task ConstructorCalls_RejectUninitializedThis()
    {
        const string source = """
            using System;
            using DRN.Framework.SharedKernel.Domain;
            [EntityType<DefaultApp>(3)] public class DuringConstruction : AggregateRoot
            {
                public DuringConstruction(Guid externalId, SourceKnownEntityId parsedId)
                {
                    GetEntityId<Customer>(externalId);
                    this.ToPlain(parsedId);
                }
            }
            """;

        var diagnostics = await RunAnalyzerAsync(FreshEntityStubs, source);
        diagnostics.Should().HaveCount(2);
        diagnostics.Should().OnlyContain(d => d.Id == "DRN0014");
    }

    [Fact]
    public async Task NullShortCircuitsAndMetadataHelpers_DoNotRequireEntityInitialization()
    {
        const string source = """
            using System;
            using DRN.Framework.SharedKernel.Domain;
            public class Consumer
            {
                public void Run(Guid? maybeGuid, SourceKnownEntityId? maybeId)
                {
                    var order = new Order();
                    order.ToSecure((SourceKnownEntityId?)null);
                    order.ToPlain((SourceKnownEntityId?)null);
                    order.GetEntityId<Customer>((Guid?)null);
                    order.GetEntityId<Customer>((long?)null);
                    order.GetEntityId<Customer>(maybeGuid);
                    order.ToSecure(maybeId);
                    SourceKnownEntity.GetEntityTypeId<Order>();
                }
            }
            """;

        (await RunAnalyzerAsync(FreshEntityStubs, source)).Should().BeEmpty();
    }

    [Fact]
    public async Task UnknownLifecycleCallsAndReassignment_InvalidateFreshnessAcrossAliases()
    {
        const string source = """
            using DRN.Framework.SharedKernel.Domain;
            public class Consumer
            {
                public void Run(Order retrieved, SourceKnownEntityId id)
                {
                    retrieved.ToSecure(id);
                    var order = new Order();
                    var alias = order;
                    Initialize(order);
                    alias.ToSecure(id);
                    order = new Order();
                    order = retrieved;
                    order.ToPlain(id);
                    var unknown = Load();
                    unknown.ToPlain(id);
                }
                private static void Initialize(Order entity) { }
                private static Order Load() => null;
            }
            """;

        (await RunAnalyzerAsync(FreshEntityStubs, source)).Should().BeEmpty();
    }

    [Fact]
    public async Task UnknownConstructorsInitializersAndSetters_AreNotAssumedFresh()
    {
        const string source = """
            using System;
            using DRN.Framework.SharedKernel.Domain;
            public static class Lifecycle { public static void Initialize(SourceKnownEntity entity) { } }
            [EntityType<DefaultApp>(3)] public class Initialized : AggregateRoot
            {
                public Initialized() { Lifecycle.Initialize(this); ToPlain(default(SourceKnownEntityId)); }
            }
            [EntityType<DefaultApp>(4)] public class ViaBase : Initialized;
            [EntityType<DefaultApp>(5)] public class ViaSetter : AggregateRoot
            {
                public bool Ready { set => Lifecycle.Initialize(this); }
            }
            public class Consumer
            {
                public void Run(SourceKnownEntityId id)
                {
                    new Initialized().ToPlain(id);
                    new ViaBase().ToPlain(id);
                    new ViaSetter { Ready = true }.ToPlain(id);
                    var setter = new ViaSetter();
                    setter.Ready = true;
                    setter.ToPlain(id);
                }
            }
            """;

        (await RunAnalyzerAsync(source)).Should().BeEmpty();
    }

    [Fact]
    public async Task ControlFlowCapturesAndAwaits_StopStraightLineProof()
    {
        const string source = """
            using System;
            using System.Threading.Tasks;
            using DRN.Framework.SharedKernel.Domain;
            public class Consumer
            {
                public async Task Run(bool initialize, SourceKnownEntityId id)
                {
                    var order = new Order();
                    if (initialize) Initialize(order);
                    order.ToPlain(id);
                    order = new Order();
                    Action later = () => order.ToPlain(id);
                    later();
                    order = new Order();
                    await Task.CompletedTask;
                    order.ToPlain(id);
                }
                private static void Initialize(Order entity) { }
            }
            """;

        (await RunAnalyzerAsync(FreshEntityStubs, source)).Should().BeEmpty();
    }

    [Fact]
    public async Task ReassignmentToNewEntity_RestoresProofWithoutChangingExistingAlias()
    {
        const string source = """
            using DRN.Framework.SharedKernel.Domain;
            public class Consumer
            {
                public void Run(Order retrieved, SourceKnownEntityId id)
                {
                    var order = retrieved;
                    var old = order;
                    order = new Order();
                    old.ToPlain(id);
                    order.ToPlain(id);
                }
            }
            """;

        var diagnostics = await RunAnalyzerAsync(FreshEntityStubs, source);
        diagnostics.Should().ContainSingle(d => d.Id == "DRN0014");
        diagnostics[0].Location.SourceTree!.GetText().ToString(diagnostics[0].Location.SourceSpan).Should().Be("order.ToPlain(id)");
    }

    [Fact]
    public async Task ReferencedConstructorsWithoutBodies_AreUnknown()
    {
        const string source = """
            using DRN.Framework.SharedKernel.Domain;
            public class Consumer
            {
                public void Run(SourceKnownEntityId id) => new Order().ToSecure(id);
            }
            """;

        (await RunAnalyzerWithReferenceAsync(FreshEntityStubs, source)).Should().BeEmpty();
    }

    [Fact]
    public async Task RefAliases_InvalidateProofInsteadOfAssumingIndependentLocals()
    {
        const string source = """
            using DRN.Framework.SharedKernel.Domain;
            public class Consumer
            {
                public void Run(Order retrieved, SourceKnownEntityId id)
                {
                    var order = new Order();
                    ref var alias = ref order;
                    alias = retrieved;
                    order.ToPlain(id);
                }
            }
            """;

        (await RunAnalyzerAsync(FreshEntityStubs, source)).Should().BeEmpty();
    }

    [Fact]
    public async Task PassiveEntityInitializers_AndKnownNonNullNullableInputs_AreDetected()
    {
        const string source = """
            using System.Collections.Generic;
            using DRN.Framework.SharedKernel.Domain;
            [EntityType<DefaultApp>(3)] public class WithDefaults : AggregateRoot
            {
                public string Name { get; set; } = "name";
                public List<string> Values { get; } = [];
            }
            public class Consumer
            {
                public void Run(SourceKnownEntityId id)
                {
                    var entity = new WithDefaults();
                    entity.ToSecure((SourceKnownEntityId?)id);
                }
            }
            """;

        var diagnostics = await RunAnalyzerAsync(source);
        diagnostics.Should().ContainSingle(d => d.Id == "DRN0014");
    }
}
