using DRN.Framework.SharedKernel.Analyzers.Conventions;
using static DRN.Test.Analyzer.Tests.ClassificationTestFixture;

namespace DRN.Test.Analyzer.Tests;

public sealed class ConventionDomainOwnershipAndEnumTests
{
    [Fact]
    public void Folders_Layout_Should_Distinguish_Domains_With_Same_Subdomain_And_Reject_Missing_Subdomain()
    {
        var compilation = Compilation("""
            namespace Sample.Domain.QA.Common.Order
            {
                public class OrderEntity : DRN.Framework.SharedKernel.Domain.AggregateRoot { public override DRN.Framework.SharedKernel.Domain.Dto ToDto() => new(this); }
                public interface IOrderRepository : DRN.Framework.SharedKernel.Domain.Repository.ISourceKnownRepository<OrderEntity>;
            }
            namespace Sample.Domain.Billing.Common.Invoice
            {
                public class InvoiceReader
                {
                    public Sample.Domain.QA.Common.Order.OrderEntity Read() => new();
                }
            }
            namespace Sample.Domain.QA.Order
            {
                public class OrderEntity : DRN.Framework.SharedKernel.Domain.AggregateRoot { public override DRN.Framework.SharedKernel.Domain.Dto ToDto() => new(this); }
            }
            """).WithAssemblyName("Sample.Domain");
        compilation.AssertNoCompilerErrors();
        var options = Options("Sample.Domain", ("DrnDomainLayout", "Folders"));
        var project = ConventionProject.Read(compilation, options.GlobalOptions);
        project.Diagnostics.Should().BeEmpty();
        project.DomainLayout.Should().Be(ConventionProject.FoldersLayout);

        var facts = new ConventionFacts(compilation, project, new([], []), options, CancellationToken.None);
        ConventionOwnershipAnalysis.Analyze(facts);
        ConventionBoundaryAnalysis.Analyze(facts);

        facts.Diagnostics.Where(d => d.Id == "DRNOWNERSHIP002").Should().ContainSingle()
            .Which.GetMessage().Should().Contain("Billing/Common").And.Contain("QA/Common");
        facts.Diagnostics.Where(d => d.Id == "DRNOWNERSHIP001").Should().ContainSingle()
            .Which.Properties["Target"].Should().Be("T:Sample.Domain.QA.Order.OrderEntity");
    }

    [Fact]
    public void Project_Layout_Should_Accept_Subdomain_And_SelfNamed_Subdomain_And_Reject_Missing_Subdomain()
    {
        var compilation = Compilation("""
            namespace QA.Domain.Ecommerce.Order
            {
                public class OrderEntity : DRN.Framework.SharedKernel.Domain.AggregateRoot { public override DRN.Framework.SharedKernel.Domain.Dto ToDto() => new(this); }
            }
            namespace QA.Domain.QA.Order
            {
                public class OrderEntity : DRN.Framework.SharedKernel.Domain.AggregateRoot { public override DRN.Framework.SharedKernel.Domain.Dto ToDto() => new(this); }
            }
            namespace QA.Domain.Order
            {
                public class OrderEntity : DRN.Framework.SharedKernel.Domain.AggregateRoot { public override DRN.Framework.SharedKernel.Domain.Dto ToDto() => new(this); }
            }
            """).WithAssemblyName("QA.Domain");
        compilation.AssertNoCompilerErrors();
        var options = Options("QA.Domain");
        var project = ConventionProject.Read(compilation, options.GlobalOptions);
        project.DomainLayout.Should().Be(ConventionProject.ProjectLayout);
        project.DefaultDomain.Should().Be("QA");

        var facts = new ConventionFacts(compilation, project, new([], []), options, CancellationToken.None);
        ConventionOwnershipAnalysis.Analyze(facts);

        var missing = facts.Diagnostics.Where(d => d.Id == "DRNOWNERSHIP001").Should().ContainSingle().Which;
        missing.Properties["Target"].Should().Be("T:QA.Domain.Order.OrderEntity");
    }

    [Theory]
    [DataInline("Sample.Domain", "Invalid", "")]
    [DataInline("Sample.Domain", "Folders", "QA")]
    [DataInline("Domain", "", "")]
    public void Ambiguous_Or_Invalid_Domain_Layout_Configuration_Should_Report_DRNCONVENTIONINPUT001(
        string projectName, string layout, string domain)
    {
        var compilation = Compilation($"namespace {projectName}.QA.Order; public class OrderEntity;").WithAssemblyName(projectName);
        var entries = new List<(string, string)>();
        if (layout.Length > 0) entries.Add(("DrnDomainLayout", layout));
        if (domain.Length > 0) entries.Add(("DrnDomain", domain));
        var options = Options(projectName, [.. entries]);
        var project = ConventionProject.Read(compilation, options.GlobalOptions);
        project.Diagnostics.Should().Contain(d => d.Id == "DRNCONVENTIONINPUT001");
    }

    [Fact]
    public void Folders_Only_Shared_Scope_And_Root_Identity_Exception_Should_Be_Enforced()
    {
        var compilation = Compilation("""
            namespace Sample.Domain;
            using DRN.Framework.SharedKernel.Attributes;
            using DRN.Framework.SharedKernel.Domain;
            public readonly struct SampleApp : IAppId { public static byte AppId => 1; }
            [DomainOnly] public enum SampleKinds : byte { Order = 1 }
            public sealed class SampleTypeAttribute(SampleKinds kind) : EntityTypeAttribute<SampleApp>((byte)kind);
            """, "SampleEntityTypes.cs")
            .AddSyntaxTrees(
                CSharpSyntaxTree.ParseText("global using System;", path: "Usings.cs"),
                CSharpSyntaxTree.ParseText("namespace Sample.Domain; public class StrayRootHelper;", path: "StrayRoot.cs"),
                CSharpSyntaxTree.ParseText("namespace Sample.Domain.QA; public class DirectSubdomainType;", path: "QA/Direct.cs"),
                CSharpSyntaxTree.ParseText("global using System.Linq;", path: "QA/BadUsings.cs"),
                CSharpSyntaxTree.ParseText("namespace Sample.Domain.QA._Shared; public sealed record Money(decimal Amount);", path: "QA/_Shared/Money.cs"),
                CSharpSyntaxTree.ParseText("namespace Sample.Domain.QA._Shared; public class BadEntity : DRN.Framework.SharedKernel.Domain.AggregateRoot { public override DRN.Framework.SharedKernel.Domain.Dto ToDto() => new(this); }", path: "QA/_Shared/BadEntity.cs"),
                CSharpSyntaxTree.ParseText("namespace Sample.Domain._Shared; public sealed record BadRootShared(int Value);", path: "_Shared/BadRootShared.cs"),
                CSharpSyntaxTree.ParseText("// <auto-generated />\nnamespace Sample.Domain.QA; public class GeneratedMarker;", path: "QA/Generated.g.cs"),
                CSharpSyntaxTree.ParseText("namespace Sample.Domain.QA.Order; public partial class PartialOrder;", path: "QA/Order/PartialOrder.cs"),
                CSharpSyntaxTree.ParseText("namespace Sample.Domain.QA.Order; public partial class PartialOrder { }", path: "QA/PartialOrder.Bad.cs"))
            .WithAssemblyName("Sample.Domain");
        compilation.AssertNoCompilerErrors();
        ConventionInput[] paths =
        [
            PathInput("SampleEntityTypes.cs", "SampleEntityTypes.cs"),
            PathInput("Usings.cs", "Usings.cs"),
            PathInput("StrayRoot.cs", "StrayRoot.cs"),
            PathInput("QA/Direct.cs", "QA/Direct.cs"),
            PathInput("QA/BadUsings.cs", "QA/BadUsings.cs"),
            PathInput("QA/_Shared/Money.cs", "QA/_Shared/Money.cs"),
            PathInput("QA/_Shared/BadEntity.cs", "QA/_Shared/BadEntity.cs"),
            PathInput("_Shared/BadRootShared.cs", "_Shared/BadRootShared.cs"),
            PathInput("QA/Generated.g.cs", "QA/Generated.g.cs"),
            PathInput("QA/Order/PartialOrder.cs", "QA/Order/PartialOrder.cs"),
            PathInput("QA/PartialOrder.Bad.cs", "QA/PartialOrder.Bad.cs")
        ];
        var options = Options("Sample.Domain");
        var project = ConventionProject.Read(compilation, options.GlobalOptions);
        var facts = new ConventionFacts(compilation, project, new([.. paths], []), options, CancellationToken.None);
        ConventionFolderStructureAnalysis.Analyze(facts);

        var structure = facts.Diagnostics.Where(d => d.Id == "DRNSTRUCTURE008").ToArray();
        structure.Select(d => d.Properties["Target"]).Should().BeEquivalentTo(
            "T:Sample.Domain.StrayRootHelper",
            "T:Sample.Domain.QA.DirectSubdomainType",
            "QA/BadUsings.cs",
            "T:Sample.Domain.QA._Shared.BadEntity",
            "T:Sample.Domain._Shared.BadRootShared",
            "T:Sample.Domain.QA.Order.PartialOrder");
        facts.Diagnostics.Should().NotContain(d => d.Id == "DRNOWNERSHIP004");
    }

    [Fact]
    public void Standalone_Domain_Without_Populated_Subdomain_Should_Report_DRNOWNERSHIP004()
    {
        var compilation = Compilation("""
            using DRN.Framework.SharedKernel.Attributes;
            using DRN.Framework.SharedKernel.Domain;
            namespace Sample.Domain;
            public readonly struct SampleApp : IAppId { public static byte AppId => 1; }
            [DomainOnly] public enum SampleKinds : byte { Order = 1 }
            public sealed class SampleTypeAttribute(SampleKinds kind) : EntityTypeAttribute<SampleApp>((byte)kind);
            """, "SampleEntityTypes.cs").WithAssemblyName("Sample.Domain");
        compilation.AssertNoCompilerErrors();
        var options = Options("Sample.Domain");
        var project = ConventionProject.Read(compilation, options.GlobalOptions);
        var facts = new ConventionFacts(compilation, project,
            new([PathInput("SampleEntityTypes.cs", "SampleEntityTypes.cs")], []), options, CancellationToken.None);
        ConventionFolderStructureAnalysis.Analyze(facts);

        var diagnostic = facts.Diagnostics.Should().ContainSingle().Which;
        diagnostic.Id.Should().Be("DRNOWNERSHIP004");
        diagnostic.Properties["Target"].Should().Be("Sample.Domain:Domain");
    }

    [Fact]
    public void Unrelated_Input_Diagnostic_Should_Not_Suppress_DRNOWNERSHIP004_While_Project_Layout_Error_Should()
    {
        var compilation = Compilation("""
            using DRN.Framework.SharedKernel.Attributes;
            using DRN.Framework.SharedKernel.Domain;
            namespace Sample.Domain;
            public readonly struct SampleApp : IAppId { public static byte AppId => 1; }
            [DomainOnly] public enum SampleKinds : byte { Order = 1 }
            public sealed class SampleTypeAttribute(SampleKinds kind) : EntityTypeAttribute<SampleApp>((byte)kind);
            """, "SampleEntityTypes.cs").WithAssemblyName("Sample.Domain");
        compilation.AssertNoCompilerErrors();

        var options = Options("Sample.Domain");
        var project = ConventionProject.Read(compilation, options.GlobalOptions);
        var badOwnership = new ConventionInput("Ownership", "Sample.Domain.MissingType",
            ImmutableDictionary<string, string>.Empty.Add("Subdomain", "QA"), Location.None);
        var facts = new ConventionFacts(compilation, project,
            new([PathInput("SampleEntityTypes.cs", "SampleEntityTypes.cs"), badOwnership], []), options, CancellationToken.None);
        ConventionOwnershipAnalysis.Analyze(facts);
        ConventionFolderStructureAnalysis.Analyze(facts);
        facts.Diagnostics.Should().Contain(d => d.Id == "DRNCONVENTIONINPUT001");
        facts.Diagnostics.Should().Contain(d => d.Id == "DRNOWNERSHIP004");

        var invalidProjectOptions = Options("Domain");
        var invalidProject = ConventionProject.Read(compilation, invalidProjectOptions.GlobalOptions);
        var invalidFacts = new ConventionFacts(compilation, invalidProject,
            new([PathInput("SampleEntityTypes.cs", "SampleEntityTypes.cs")], []), invalidProjectOptions, CancellationToken.None);
        ConventionFolderStructureAnalysis.Analyze(invalidFacts);
        invalidFacts.Diagnostics.Should().Contain(d => d.Id == "DRNCONVENTIONINPUT001");
        invalidFacts.Diagnostics.Should().NotContain(d => d.Id == "DRNOWNERSHIP004");
    }

    [Fact]
    public void Folders_Layout_Reference_Assembly_Should_Distinguish_Domain_Subdomain_Pairs_In_Infra()
    {
        var domain = Compilation("""
            [assembly: System.Reflection.AssemblyMetadata("DRN.Conventions.ProjectName", "Platform.Domain")]
            [assembly: System.Reflection.AssemblyMetadata("DRN.Conventions.RootNamespace", "Platform.Domain")]
            [assembly: System.Reflection.AssemblyMetadata("DRN.Conventions.DomainLayout", "Folders")]
            namespace Platform.Domain.QA.Common.Order { public class OrderEntity : DRN.Framework.SharedKernel.Domain.AggregateRoot { public override DRN.Framework.SharedKernel.Domain.Dto ToDto() => new(this); } }
            """).WithAssemblyName("Platform.Domain");
        domain.AssertNoCompilerErrors();
        var domainPath = "/folders-ref/Platform.Domain.dll";
        var domainRef = AnalyzerTestCompilation.EmitReference(domain, domainPath);

        var infra = Compilation("""
            namespace Platform.Infra.QA.Common { public class CommonContext : Microsoft.EntityFrameworkCore.DbContext; }
            namespace Platform.Infra.Billing.Common { public class BillingCommonContext : Microsoft.EntityFrameworkCore.DbContext; }
            """).WithAssemblyName("Platform.Infra")
            .AddReferences(AnalyzerTestCompilation.EntityFrameworkReference, domainRef);
        infra.AssertNoCompilerErrors();
        var options = AnalyzerTestCompilation.ConventionOptionsWithProperties("Platform.Infra", ("DrnDomainLayout", "Folders"));
        var project = ConventionProject.Read(infra, options.GlobalOptions);
        var records = DomainReferenceRecords("/folders-ref/Platform.Domain.csproj", domainPath);
        var facts = new ConventionFacts(infra, project, new([.. records], []), options, CancellationToken.None);
        ConventionDependencyAnalysis.Analyze(facts);

        var missing = facts.Diagnostics.Should().ContainSingle().Which;
        missing.Id.Should().Be("DRNOWNERSHIP003");
        missing.GetMessage().Should().Contain("BillingCommonContext").And.Contain("Billing/Common");
    }

    [Fact]
    public void DomainOnly_Enum_Rules_Should_Allow_Attribute_Usage_And_Diagnose_Nested_Generic_Public_Exposure()
    {
        var domain = Compilation("""
            using DRN.Framework.SharedKernel.Attributes;
            using DRN.Framework.SharedKernel.Domain;
            namespace Sample.Domain
            {
                public readonly struct SampleApp : IAppId { public static byte AppId => 1; }
                [DomainOnly] public enum SampleEntityTypes : byte { Tag = 1 }
                public sealed class SampleEntityTypeAttribute(SampleEntityTypes kind) : EntityTypeAttribute<SampleApp>((byte)kind);
            }
            namespace Sample.Domain.QA.Tag
            {
                public enum UnmarkedDomainEnum { One = 1 }
                [SampleEntityType(SampleEntityTypes.Tag)]
                public class TagEntity : AggregateRoot { public override Dto ToDto() => new(this); }
            }
            """).WithAssemblyName("Sample.Domain");
        domain.AssertNoCompilerErrors();
        var domainFacts = FactsFor(domain, "Domain");
        ConventionEnumAnalysis.Analyze(domainFacts);
        domainFacts.Diagnostics.Should().ContainSingle().Which.Id.Should().Be("DRNENUM001");

        var domainRef = AnalyzerTestCompilation.EmitReference(domain, "/enum-ref/Sample.Domain.dll");
        var contract = Compilation("""
            using System.Collections.Generic;
            using DRN.Framework.SharedKernel.Attributes;
            namespace Sample.Contract.QA.Tags;
            [DomainOnly] public enum MisplacedMarker { One = 1 }
            public class TagResponse
            {
                public List<Sample.Domain.SampleEntityTypes?> LeakedKinds { get; init; } = [];
            }
            """).WithAssemblyName("Sample.Contract").AddReferences(domainRef);
        contract.AssertNoCompilerErrors();
        var contractFacts = FactsFor(contract, "Contract");
        ConventionEnumAnalysis.Analyze(contractFacts);
        contractFacts.Diagnostics.Select(d => d.Id).Should().BeEquivalentTo("DRNENUM002", "DRNENUM003");
        contractFacts.Diagnostics.Single(d => d.Id == "DRNENUM003").GetMessage()
            .Should().Contain("LeakedKinds").And.Contain("SampleEntityTypes");
    }

    [Theory]
    [DataInline("public enum ImplicitZero { First, Second }", "DRNENUM004")]
    [DataInline("public enum ExplicitZero { None = 0, Active = 1 }", "DRNENUM004")]
    [DataInline("public enum ComputedZero { One = 1, Zero = One - 1 }", "DRNENUM004")]
    [DataInline("public enum AliasedZero { Zero = 0, Alias = Zero, One = 1 }", "DRNENUM004")]
    [DataInline("public enum LaterZero { Negative = -1, Zero }", "DRNENUM004")]
    [DataInline("[System.Flags] public enum FlagsZero { None = 0, Read = 1 }", "DRNENUM004")]
    [DataInline("[DRN.Framework.SharedKernel.Attributes.AllowZero] public enum MissingZero { Active = 1 }", "DRNENUM005")]
    [DataInline("[DRN.Framework.SharedKernel.Attributes.AllowZero] public enum OptedInZero { System = 0, User = 1 }", "")]
    [DataInline("[System.Flags, DRN.Framework.SharedKernel.Attributes.AllowZero] public enum OptedInFlags { None = 0, Read = 1 }", "")]
    [DataInline("public enum NonZeroOnly { Active = 1, Archived = 2 }", "")]
    public void Enum_Zero_Rules_Should_Check_All_Resolved_Member_Values(string declaration, string expectedId)
    {
        var contract = Compilation($"namespace Sample.Contract.QA.Tags; {declaration}").WithAssemblyName("Sample.Contract");
        contract.AssertNoCompilerErrors();
        var facts = FactsFor(contract, "Contract");
        ConventionEnumAnalysis.Analyze(facts);
        if (expectedId.Length == 0) facts.Diagnostics.Should().BeEmpty();
        else facts.Diagnostics.Should().ContainSingle().Which.Id.Should().Be(expectedId);
    }

    [Fact]
    public void DomainOnly_Enum_With_Zero_Should_Also_Require_AllowZero()
    {
        var domain = Compilation("""
            using DRN.Framework.SharedKernel.Attributes;
            namespace Sample.Domain.QA.Tag;
            [DomainOnly] public enum InternalZero : byte { Unknown = 0, Ready = 1 }
            [DomainOnly, AllowZero] public enum InternalAllowedZero : byte { Unknown = 0, Ready = 1 }
            """).WithAssemblyName("Sample.Domain");
        domain.AssertNoCompilerErrors();
        var facts = FactsFor(domain, "Domain");
        ConventionEnumAnalysis.Analyze(facts);
        var diagnostic = facts.Diagnostics.Should().ContainSingle().Which;
        diagnostic.Id.Should().Be("DRNENUM004");
        diagnostic.GetMessage().Should().Contain("InternalZero").And.Contain("Unknown");
    }

    [Fact]
    public void DomainOnly_Enum_In_WriteOnly_Property_And_Wide_Model_Should_Report_DRNENUM003_And_DRNARCHITECTURE001()
    {
        var domain = Compilation("""
            using DRN.Framework.SharedKernel.Attributes;
            namespace Sample.Domain.QA.Tag;
            [DomainOnly] public enum InternalKind : byte { Active = 1 }
            """).WithAssemblyName("Sample.Domain");
        domain.AssertNoCompilerErrors();
        var domainRef = AnalyzerTestCompilation.EmitReference(domain, "/enum-writeonly/Sample.Domain.dll");

        var wideTree = AnalyzerTestCompilation.WideModelTree("Sample.Contract.QA.Tags");
        var contract = Compilation("""
            namespace Sample.Contract.QA.Tags;
            public class NestedFilter
            {
                public Sample.Domain.QA.Tag.InternalKind Kind { set { _ = value; } }
            }
            public class TagRequest
            {
                public NestedFilter Filter { get; init; } = new();
                public Wide Payload { get; init; } = new();
            }
            """).AddSyntaxTrees(wideTree).WithAssemblyName("Sample.Contract").AddReferences(domainRef);
        contract.AssertNoCompilerErrors();
        var facts = FactsFor(contract, "Contract");
        ConventionEnumAnalysis.Analyze(facts);

        facts.Diagnostics.Where(d => d.Id == "DRNENUM003").Select(d => d.Properties["Target"]).Should().BeEquivalentTo(
            "P:Sample.Contract.QA.Tags.NestedFilter.Kind",
            "P:Sample.Contract.QA.Tags.TagRequest.Filter");
        facts.Diagnostics.Where(d => d.Id == "DRNARCHITECTURE001").Should().ContainSingle()
            .Which.Properties["Target"].Should().Be("P:Sample.Contract.QA.Tags.TagRequest.Payload");
    }

    [Fact]
    public void Domain_Folder_Structure_Should_Reject_Stray_Root_Enums_Invalid_Shared_Types_And_Split_Role_Zero_Type_Files()
    {
        var compilation = Compilation("""
            namespace Sample.Domain;
            using DRN.Framework.SharedKernel.Attributes;
            using DRN.Framework.SharedKernel.Domain;
            public readonly struct SampleApp : IAppId { public static byte AppId => 1; }
            [DomainOnly] public enum SampleKinds : byte { Order = 1 }
            public sealed class SampleTypeAttribute(SampleKinds kind) : EntityTypeAttribute<SampleApp>((byte)kind);
            [DomainOnly] public enum StrayRootByteEnum : byte { One = 1 }
            """, "SampleEntityTypes.cs")
            .AddSyntaxTrees(
                CSharpSyntaxTree.ParseText("namespace Sample.Domain.QA.Order; public class OrderEntity : DRN.Framework.SharedKernel.Domain.AggregateRoot { public override DRN.Framework.SharedKernel.Domain.Dto ToDto() => new(this); }", path: "QA/Order/OrderEntity.cs"),
                CSharpSyntaxTree.ParseText("""
                    namespace Sample.Domain.QA._Shared;
                    using DRN.Framework.SharedKernel.Attributes;
                    using DRN.Framework.SharedKernel.Domain;
                    using DRN.Framework.SharedKernel.Domain.Repository;
                    using Sample.Domain.QA.Order;
                    public readonly record struct Money(decimal Amount);
                    public sealed class Address(string City) { public string City { get; } = City; }
                    [DomainOnly] public enum SharedEnum { One = 1 }
                    public interface ISharedContract { void Run(); }
                    public delegate void SharedCallback();
                    public static class SharedHelpers { public static int One => 1; }
                    public sealed class SharedPricingService;
                    public sealed class BoundWorker : ISourceKnownService<OrderEntity>
                    {
                        public ISourceKnownRepository<OrderEntity> Repository => null!;
                    }
                    """, path: "QA/_Shared/Items.cs"))
            .WithAssemblyName("Sample.Domain");
        compilation.AssertNoCompilerErrors();
        ConventionInput[] paths =
        [
            PathInput("SampleEntityTypes.cs", "SampleEntityTypes.cs"),
            PathInput("QA/Order/OrderEntity.cs", "QA/Order/OrderEntity.cs"),
            PathInput("QA/_Shared/Items.cs", "QA/_Shared/Items.cs")
        ];
        var options = Options("Sample.Domain");
        var project = ConventionProject.Read(compilation, options.GlobalOptions);
        var facts = new ConventionFacts(compilation, project, new([.. paths], []), options, CancellationToken.None);
        ConventionFolderStructureAnalysis.Analyze(facts);

        facts.Diagnostics.Where(d => d.Id == "DRNSTRUCTURE008").Select(d => d.Properties["Target"]).Should().BeEquivalentTo(
            "T:Sample.Domain.StrayRootByteEnum",
            "T:Sample.Domain.QA._Shared.SharedEnum",
            "T:Sample.Domain.QA._Shared.ISharedContract",
            "T:Sample.Domain.QA._Shared.SharedCallback",
            "T:Sample.Domain.QA._Shared.SharedHelpers",
            "T:Sample.Domain.QA._Shared.SharedPricingService",
            "T:Sample.Domain.QA._Shared.BoundWorker");

        var split = Compilation("""
            namespace Sample.Application.Domain.QA.Order;
            public class OrderEntity : DRN.Framework.SharedKernel.Domain.AggregateRoot { public override DRN.Framework.SharedKernel.Domain.Dto ToDto() => new(this); }
            """, "Domain/QA/Order/OrderEntity.cs")
            .AddSyntaxTrees(CSharpSyntaxTree.ParseText("global using System.Linq;", path: "Domain/QA/BadUsings.cs"))
            .WithAssemblyName("Sample.Application");
        split.AssertNoCompilerErrors();
        ConventionInput[] splitInputs =
        [
            new("Role", "Domain", ImmutableDictionary<string, string>.Empty
                .Add("NamespacePrefix", "Sample.Application.Domain")
                .Add("LogicalPathPrefix", "Domain"), Location.None),
            PathInput("Domain/QA/Order/OrderEntity.cs", "Domain/QA/Order/OrderEntity.cs"),
            PathInput("Domain/QA/BadUsings.cs", "Domain/QA/BadUsings.cs")
        ];
        var splitOptions = Options("Sample.Application");
        var splitProject = ConventionProject.Read(split, splitOptions.GlobalOptions);
        var splitFacts = new ConventionFacts(split, splitProject, new([.. splitInputs], []), splitOptions, CancellationToken.None);
        ConventionFolderStructureAnalysis.Analyze(splitFacts);

        splitFacts.Diagnostics.Should().ContainSingle().Which.Properties["Target"].Should().Be("Domain/QA/BadUsings.cs");
    }

    [Fact]
    public void Cross_Layout_Ownership_Should_Match_Equal_Domain_Subdomain_And_Reject_Different_Domain()
    {
        var domain = Compilation("""
            [assembly: System.Reflection.AssemblyMetadata("DRN.Conventions.ProjectName", "Platform.Domain")]
            [assembly: System.Reflection.AssemblyMetadata("DRN.Conventions.RootNamespace", "Platform.Domain")]
            [assembly: System.Reflection.AssemblyMetadata("DRN.Conventions.DomainLayout", "Folders")]
            namespace Platform.Domain.QA.Common.Order { public class OrderEntity : DRN.Framework.SharedKernel.Domain.AggregateRoot { public override DRN.Framework.SharedKernel.Domain.Dto ToDto() => new(this); } }
            namespace Platform.Domain.Billing.Common.Invoice { public class InvoiceEntity : DRN.Framework.SharedKernel.Domain.AggregateRoot { public override DRN.Framework.SharedKernel.Domain.Dto ToDto() => new(this); } }
            """).WithAssemblyName("Platform.Domain");
        domain.AssertNoCompilerErrors();
        var domainPath = "/cross-layout/Platform.Domain.dll";
        var domainRef = AnalyzerTestCompilation.EmitReference(domain, domainPath);

        var records = DomainReferenceRecords("/cross-layout/Platform.Domain.csproj", domainPath);

        var qaInfra = Compilation("""
            namespace QA.Infra.Common
            {
                public class CommonContext : Microsoft.EntityFrameworkCore.DbContext
                {
                    public Platform.Domain.QA.Common.Order.OrderEntity Own() => new();
                    public Platform.Domain.Billing.Common.Invoice.InvoiceEntity Foreign() => new();
                }
            }
            """).WithAssemblyName("QA.Infra")
            .AddReferences(AnalyzerTestCompilation.EntityFrameworkReference, domainRef);
        qaInfra.AssertNoCompilerErrors();
        var qaOptions = Options("QA.Infra");
        var qaFacts = new ConventionFacts(qaInfra, ConventionProject.Read(qaInfra, qaOptions.GlobalOptions),
            new([.. records], []), qaOptions, CancellationToken.None);
        ConventionDependencyAnalysis.Analyze(qaFacts);
        ConventionBoundUseAnalysis.Analyze(qaFacts);

        qaFacts.Diagnostics.Should().NotContain(d => d.Id == "DRNOWNERSHIP003");
        qaFacts.Diagnostics.Where(d => d.Id == "DRNOWNERSHIP002").Should().ContainSingle()
            .Which.GetMessage().Should().Contain("InvoiceEntity").And.Contain("Billing/Common");

        var salesInfra = Compilation("""
            namespace Sales.Infra.Common { public class CommonContext : Microsoft.EntityFrameworkCore.DbContext; }
            """).WithAssemblyName("Sales.Infra")
            .AddReferences(AnalyzerTestCompilation.EntityFrameworkReference, domainRef);
        salesInfra.AssertNoCompilerErrors();
        var salesOptions = Options("Sales.Infra");
        var salesFacts = new ConventionFacts(salesInfra, ConventionProject.Read(salesInfra, salesOptions.GlobalOptions),
            new([.. records], []), salesOptions, CancellationToken.None);
        ConventionDependencyAnalysis.Analyze(salesFacts);

        var missing = salesFacts.Diagnostics.Should().ContainSingle().Which;
        missing.Id.Should().Be("DRNOWNERSHIP003");
        missing.GetMessage().Should().Contain("CommonContext");
    }

    [Fact]
    public void DomainOnly_Enum_Exposure_Should_Check_ProducesResponseType_And_App_Wrappers_And_Ignore_FromServices()
    {
        var domain = Compilation("""
            using DRN.Framework.SharedKernel.Attributes;
            [assembly: System.Reflection.AssemblyMetadata("DRN.Conventions.ProjectName", "Sample.Domain")]
            [assembly: System.Reflection.AssemblyMetadata("DRN.Conventions.RootNamespace", "Sample.Domain")]
            namespace Sample.Domain.QA.Tag;
            [DomainOnly] public enum InternalKind : byte { Active = 1 }
            """).WithAssemblyName("Sample.Domain");
        domain.AssertNoCompilerErrors();
        var domainRef = AnalyzerTestCompilation.EmitReference(domain, "/enum-boundary/Sample.Domain.dll");

        var app = Compilation("""
            using Sample.Domain.QA.Tag;
            [assembly: System.Reflection.AssemblyMetadata("DRN.Conventions.ProjectName", "Sample.Application")]
            [assembly: System.Reflection.AssemblyMetadata("DRN.Conventions.RootNamespace", "Sample.Application")]
            namespace Sample.Application.QA
            {
                public class AppKindWrapper { public InternalKind Kind { get; init; } }
                public interface IInternalKindService { InternalKind Current { get; } }
            }
            """).WithAssemblyName("Sample.Application").AddReferences(domainRef);
        app.AssertNoCompilerErrors();
        var appRef = AnalyzerTestCompilation.EmitReference(app, "/enum-boundary/Sample.Application.dll");

        var hosted = Compilation("""
            using Microsoft.AspNetCore.Mvc;
            using Microsoft.Extensions.DependencyInjection;
            using Sample.Application.QA;
            using Sample.Domain.QA.Tag;
            namespace Sample.Hosted.Controllers.QA
            {
                public class KindsController : ControllerBase
                {
                    [HttpGet("meta"), ProducesResponseType(typeof(InternalKind), 200)]
                    public IActionResult Meta() => Ok();

                    [HttpGet("wrapped")]
                    public AppKindWrapper Wrapped() => new();

                    [HttpGet("injected")]
                    public IActionResult Injected(
                        [FromServices] IInternalKindService service,
                        [FromKeyedServices("kinds")] IInternalKindService keyedService) => Ok();
                }

                [ProducesResponseType(typeof(InternalKind), 200)]
                public abstract class KindsResponseControllerBase : ControllerBase;

                public class ClassMetaKindsController : KindsResponseControllerBase, System.IDisposable
                {
                    public void Dispose() { }

                    [HttpGet("class-meta")]
                    public IActionResult ClassMeta() => Ok();

                    [HttpGet("override"), ProducesResponseType<string>(200)]
                    public IActionResult Override() => Ok();

                    [HttpGet("other-status"), ProducesResponseType<string>(404)]
                    public IActionResult OtherStatus() => Ok();
                }
            }
            """).WithAssemblyName("Sample.Hosted").AddReferences(domainRef, appRef);
        hosted.AssertNoCompilerErrors();

        var facts = FactsFor(hosted, "Hosted");
        ConventionEnumAnalysis.Analyze(facts);
        facts.Diagnostics.Where(d => d.Id == "DRNENUM003").Select(d => d.Properties["Target"]).Should().BeEquivalentTo(
            "M:Sample.Hosted.Controllers.QA.KindsController.Meta",
            "M:Sample.Hosted.Controllers.QA.KindsController.Wrapped",
            "M:Sample.Hosted.Controllers.QA.ClassMetaKindsController.ClassMeta",
            "M:Sample.Hosted.Controllers.QA.ClassMetaKindsController.OtherStatus");
    }

    [Fact]
    public void Cross_Project_Producer_Ownership_Should_Reconcile_Omitted_And_Explicit_Default_Domain()
    {
        var producer = Compilation("""
            [assembly: System.Reflection.AssemblyMetadata("DRN.Conventions.ProjectName", "Sample.Infra")]
            [assembly: System.Reflection.AssemblyMetadata("DRN.Conventions.RootNamespace", "Sample.Infra")]
            [assembly: System.Reflection.AssemblyMetadata("DRN.Conventions.Context.Sample.Infra.QA.QAContext.Subdomain", "QA")]
            [assembly: System.Reflection.AssemblyMetadata("DRN.Conventions.Ownership.Sample.Infra.QA.QAContext.Domain", "Sample")]
            [assembly: System.Reflection.AssemblyMetadata("DRN.Conventions.Ownership.Sample.Infra.QA.QAContext.Subdomain", "QA")]
            namespace Sample.Infra.QA;
            public class QAContext : Microsoft.EntityFrameworkCore.DbContext;
            """).WithAssemblyName("Sample.Infra").AddReferences(AnalyzerTestCompilation.EntityFrameworkReference);
        producer.AssertNoCompilerErrors();
        var producerRef = AnalyzerTestCompilation.EmitReference(producer, "/producer-default-domain/Sample.Infra.dll");

        var consumer = Compilation("""
            namespace Sample.Hosted.Controllers.QA;
            public class Consumer { public Sample.Infra.QA.QAContext? Context { get; init; } }
            """).WithAssemblyName("Sample.Hosted").AddReferences(AnalyzerTestCompilation.EntityFrameworkReference, producerRef);
        consumer.AssertNoCompilerErrors();

        var facts = FactsFor(consumer, "Hosted");
        var contextSymbol = consumer.GetTypeByMetadataName("Sample.Infra.QA.QAContext")!;
        var owner = facts.Owner(contextSymbol);
        owner.Should().NotBeNull();
        owner!.Domain.Should().Be("Sample");
        owner.Subdomain.Should().Be("QA");
        owner.HasExplicitDomain.Should().BeTrue();
        facts.Diagnostics.Should().NotContain(d => d.Id == "DRNCONVENTIONINPUT001");
    }

    [Fact]
    public void Domain_Folder_Structure_Should_Support_Zero_Type_File_Exceptions_Pragmas_And_Case_Insensitive_Shared_Checks()
    {
        var badUsingsSource = "#pragma warning disable DRNSTRUCTURE008 // Reviewed legacy global usings.\nglobal using System.Linq;\n#pragma warning restore DRNSTRUCTURE008\n";
        var compilation = Compilation("""
            namespace Sample.Domain.QA.Order;
            public class OrderEntity : DRN.Framework.SharedKernel.Domain.AggregateRoot { public override DRN.Framework.SharedKernel.Domain.Dto ToDto() => new(this); }
            """, "QA/Order/OrderEntity.cs")
            .AddSyntaxTrees(
                CSharpSyntaxTree.ParseText(badUsingsSource, path: "QA/BadUsings.cs"),
                CSharpSyntaxTree.ParseText("namespace Sample.Domain.QA._shared; public sealed record LowerShared(int Value);", path: "QA/_shared/LowerShared.cs"),
                CSharpSyntaxTree.ParseText("namespace Sample.Domain.QA._Shared._Shared; public sealed record NestedShared(int Value);", path: "QA/_Shared/_Shared/NestedShared.cs"))
            .WithAssemblyName("Sample.Domain");
        compilation.AssertNoCompilerErrors();

        ConventionInput[] inputs =
        [
            PathInput("QA/Order/OrderEntity.cs", "QA/Order/OrderEntity.cs"),
            PathInput("QA/BadUsings.cs", "QA/BadUsings.cs"),
            PathInput("QA/_shared/LowerShared.cs", "QA/_shared/LowerShared.cs"),
            PathInput("QA/_Shared/_Shared/NestedShared.cs", "QA/_Shared/_Shared/NestedShared.cs"),
            new("Exception", "DRNSTRUCTURE008", ImmutableDictionary<string, string>.Empty
                .Add("Target", "QA/BadUsings.cs")
                .Add("Reason", "Reviewed legacy grouping-folder global usings.")
                .Add("Owner", "Domain maintainers")
                .Add("Review", "9999-12-31"), Location.None),
            new("Exception", "DRNSTRUCTURE008", ImmutableDictionary<string, string>.Empty
                .Add("Target", "QA/Missing.cs")
                .Add("Reason", "Stale path should fail exception validation.")
                .Add("Owner", "Domain maintainers")
                .Add("Review", "9999-12-31"), Location.None)
        ];

        var options = Options("Sample.Domain");
        var project = ConventionProject.Read(compilation, options.GlobalOptions);
        var facts = new ConventionFacts(compilation, project, new([.. inputs], []), options, CancellationToken.None);
        var exceptions = new ConventionExceptions(facts);
        ConventionFolderStructureAnalysis.Analyze(facts);

        var zeroTypeDiagnostic = facts.Diagnostics.Single(d => d.Id == "DRNSTRUCTURE008" && d.Properties["Target"] == "QA/BadUsings.cs");
        zeroTypeDiagnostic.Location.SourceSpan.Start.Should().BeGreaterThan(0);
        exceptions.Applies(zeroTypeDiagnostic).Should().BeTrue();

        facts.Diagnostics.Where(d => d.Id == "DRNSTRUCTURE008" && !exceptions.Applies(d))
            .Select(d => d.Properties["Target"]).Should().BeEquivalentTo(
                "T:Sample.Domain.QA._shared.LowerShared",
                "T:Sample.Domain.QA._Shared._Shared.NestedShared");
        facts.Diagnostics.Where(d => d.Id == "DRNCONVENTIONEXCEPTION001").Should().ContainSingle()
            .Which.GetMessage().Should().Contain("malformed, unresolved");
    }

    private static ConventionFacts FactsFor(Compilation compilation, string role)
    {
        var name = compilation.AssemblyName!;
        var options = Options(name);
        return new(compilation, new(true, name, role, name, name, []), new([], []), options, CancellationToken.None);
    }

    private static AnalyzerConfigOptionsProvider Options(string projectName, params (string Key, string Value)[] extra) =>
        AnalyzerTestCompilation.ConventionOptionsWithProperties(projectName, extra);

    private static ConventionInput[] DomainReferenceRecords(string csprojPath, string dllPath) =>
    [
        new("Reference", "Inventory", ImmutableDictionary<string, string>.Empty.Add("Stage", "Complete"), Location.None),
        new("Reference", csprojPath, ImmutableDictionary<string, string>.Empty
            .Add("Stage", "Project").Add("Source", csprojPath), Location.None),
        new("Reference", dllPath, ImmutableDictionary<string, string>.Empty
            .Add("Stage", "Resolved").Add("Project", csprojPath), Location.None)
    ];

    private static ConventionInput PathInput(string source, string logical) => new("Path", source,
        ImmutableDictionary<string, string>.Empty.Add("LogicalPath", logical), Location.None);
}
