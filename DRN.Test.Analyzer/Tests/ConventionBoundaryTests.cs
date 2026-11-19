using DRN.Framework.SharedKernel.Analyzers.Conventions;
using static DRN.Test.Analyzer.Tests.ClassificationTestFixture;

namespace DRN.Test.Analyzer.Tests;

public sealed class ConventionBoundaryTests
{
    [Theory]
    [DataInline("Hosted", "public class Consumer : Microsoft.AspNetCore.Mvc.RazorPages.PageModel", false)]
    [DataInline("Hosted", "public class Consumer : Microsoft.AspNetCore.Mvc.ControllerBase", false)]
    [DataInline("Hosted", "public class Consumer : Microsoft.AspNetCore.Razor.TagHelpers.TagHelper", false)]
    [DataInline("Hosted", "public class Consumer", false)]
    [DataInline("Application", "public class Consumer", false)]
    [DataInline("Infra", "public class Consumer", false)]
    [DataInline("Infra", "public class Consumer : Microsoft.EntityFrameworkCore.DbContext", true)]
    [DataInline("Infra", "public interface IConsumer : DRN.Framework.SharedKernel.Domain.Repository.ISourceKnownRepository<Sample.Infra.QA.User>", true)]
    [DataInline("Application", "public interface IConsumer : DRN.Framework.SharedKernel.Domain.ISourceKnownService<Sample.Application.QA.User>", true)]
    [DataInline("Domain", "public class Consumer", true)]
    [DataInline("Application", "public interface IConsumerService", true)]
    public void Entity_Boundaries_Should_Require_An_Architectural_Role(string role, string declaration, bool scoped)
    {
        var member = declaration.Contains("interface", StringComparison.Ordinal)
            ? "Sample." + role + ".QA.User Read();"
            : "public Sample." + role + ".QA.User Read() => new();";
        var facts = Facts($$"""
            namespace Sample.{{role}}.QA { public class User : DRN.Framework.SharedKernel.Domain.AggregateRoot; }
            namespace Sample.{{role}}.Billing { {{declaration}} { {{member}} } }
            """, role);
        ConventionBoundaryAnalysis.Analyze(facts);
        facts.Diagnostics.Any(item => item.Id == "DRNOWNERSHIP002").Should().Be(scoped);
        if (declaration.Contains("PageModel", StringComparison.Ordinal))
        {
            facts.Diagnostics.Should().NotContain(item => item.Id == "DRNARCHITECTURE002");
            ConventionOwnershipAnalysis.Analyze(facts);
            facts.Diagnostics.Should().NotContain(item => item.Id == "DRNOWNERSHIP001" || item.Id == "DRNSTRUCTURE001");
        }
    }

    [Fact]
    public void SourceKnown_Controllers_Should_Retain_Entity_Boundaries()
    {
        var compilation = Compilation("""
            namespace Sample.Hosted.QA
            {
                public class User : DRN.Framework.SharedKernel.Domain.AggregateRoot;
                public class UserDto : DRN.Framework.SharedKernel.Domain.Dto;
            }
            namespace Sample.Hosted.Controllers.Billing
            {
                public class UsersController() : DRN.Framework.Hosting.Controllers.SourceKnownControllerBase<Sample.Hosted.QA.User, Sample.Hosted.QA.UserDto>(null!)
                {
                    protected override Sample.Hosted.QA.UserDto ToDto(Sample.Hosted.QA.User entity) => new();
                }
            }
            """).AddReferences(AnalyzerTestCompilation.HostingReference);
        compilation.AssertNoCompilerErrors();
        var facts = CreateFacts(compilation, "Hosted");
        ConventionBoundaryAnalysis.Analyze(facts);
        facts.Diagnostics.Should().Contain(item => item.Id == "DRNOWNERSHIP002");
    }

    [Theory]
    [DataInline("QA", 0)]
    [DataInline("Billing", 1)]
    public void Entity_Uses_Should_Compare_Actual_Ownership_And_Deduplicate_A_Member(string owner, int count)
    {
        var facts = Facts($$"""
            namespace Sample.Domain.QA { public class User : DRN.Framework.SharedKernel.Domain.SourceKnownEntity; }
            namespace Sample.Domain.{{owner}}
            {
                public class Reader
                {
                    public long Read()
                    {
                        var user = new Sample.Domain.QA.User();
                        return user.Id + user.Id;
                    }
                }
            }
            """);
        ConventionBoundaryAnalysis.Analyze(facts);
        var diagnostics = facts.Diagnostics.Where(item => item.Id == "DRNOWNERSHIP002").ToArray();
        diagnostics.Should().HaveCount(count);
        if (count == 0) return;
        diagnostics[0].GetMessage().Should().Contain("Billing").And.Contain("QA")
            .And.Contain("Use the entity's identifier or a contract from 'QA'.");
        diagnostics[0].Location.SourceTree!.GetText().ToString(diagnostics[0].Location.SourceSpan).Should().Be("var");
        diagnostics[0].Properties["Consumer"].Should().Be("M:Sample.Domain.Billing.Reader.Read");
        diagnostics[0].AdditionalLocations.Should().NotBeEmpty();
    }

    [Fact]
    public void Using_Comments_Strings_And_Same_Named_Nonentities_Should_Not_Create_Entity_Uses()
    {
        var facts = Facts("""
            using Alias = Sample.Domain.QA.User;
            namespace Sample.Domain.QA { public class User; }
            namespace Sample.Domain.Billing
            {
                public class Reader
                {
                    // Sample.Domain.QA.User
                    public string Text => "Sample.Domain.QA.User";
                    public Alias Value => new();
                }
            }
            """);
        ConventionBoundaryAnalysis.Analyze(facts);
        facts.Diagnostics.Should().NotContain(item => item.Id == "DRNOWNERSHIP002");
    }

    [Fact]
    public void Signatures_Should_Unwrap_Generic_And_Array_Entity_Uses()
    {
        var facts = Facts("""
            namespace Sample.Domain.QA { public class User : DRN.Framework.SharedKernel.Domain.SourceKnownEntity; }
            namespace Sample.Domain.Billing
            {
                public class Reader
                {
                    public System.Collections.Generic.List<Sample.Domain.QA.User[]> Users { get; } = new();
                }
            }
            """);
        ConventionBoundaryAnalysis.Analyze(facts);
        facts.Diagnostics.Where(item => item.Id == "DRNOWNERSHIP002").Should().ContainSingle()
            .Which.Properties["Consumer"].Should().Be("P:Sample.Domain.Billing.Reader.Users");
    }

    [Theory]
    [DataInline("QA", 0)]
    [DataInline("Billing", 1)]
    public void Generated_Partial_Members_Should_Report_Once_At_Their_User_Type(string owner, int count)
    {
        var compilation = GeneratedCompilation($$"""
            namespace Sample.Domain.QA { public class User : DRN.Framework.SharedKernel.Domain.SourceKnownEntity; }
            namespace Sample.Domain.{{owner}} { public partial class Reader; }
            """, $$"""
            // <auto-generated />
            namespace Sample.Domain.{{owner}}
            {
                public partial class Reader
                {
                    public Sample.Domain.QA.User First() => new();
                    public Sample.Domain.QA.User Second() => new();
                }
            }
            """);
        var facts = CreateFacts(compilation, "Domain");
        ConventionBoundaryAnalysis.Analyze(facts);
        var diagnostics = facts.Diagnostics.Where(diagnostic => diagnostic.Id == "DRNOWNERSHIP002").ToArray();
        diagnostics.Should().HaveCount(count);
        if (count == 0) return;
        diagnostics[0].Properties["Consumer"].Should().Be("T:Sample.Domain.Billing.Reader");
        diagnostics[0].Location.SourceTree!.FilePath.Should().Be("User.cs");
        diagnostics[0].Location.SourceTree!.GetText().ToString(diagnostics[0].Location.SourceSpan).Should().Be("Reader");
        diagnostics[0].AdditionalLocations.Should().NotBeEmpty().And.OnlyContain(location =>
            location.SourceTree != null && facts.IsGenerated(location.SourceTree));
    }

    [Fact]
    public void A_Generated_Partial_Method_Body_Should_Use_The_Handwritten_Method_As_Its_Smallest_Target()
    {
        var compilation = GeneratedCompilation("""
            namespace Sample.Domain.QA { public class User : DRN.Framework.SharedKernel.Domain.SourceKnownEntity; }
            namespace Sample.Domain.Billing
            {
                public partial class Reader { public partial long Read(); }
            }
            """, """
            // <auto-generated />
            namespace Sample.Domain.Billing
            {
                public partial class Reader
                {
                    public partial long Read()
                    {
                        var user = new Sample.Domain.QA.User();
                        return user.Id;
                    }
                }
            }
            """);
        var facts = CreateFacts(compilation, "Domain");
        ConventionBoundaryAnalysis.Analyze(facts);
        var diagnostic = facts.Diagnostics.Where(item => item.Id == "DRNOWNERSHIP002").Should().ContainSingle().Which;
        diagnostic.Properties["Consumer"].Should().Be("M:Sample.Domain.Billing.Reader.Read");
        diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan).Should().Be("Read");
        diagnostic.AdditionalLocations.Should().NotBeEmpty().And.OnlyContain(location => location.SourceTree != null && facts.IsGenerated(location.SourceTree));
    }

    [Fact]
    public void Generated_Evidence_Should_Not_Replace_A_Handwritten_Bound_Use_As_Primary_Location()
    {
        var compilation = GeneratedCompilation("""
            namespace Sample.Domain.QA { public class User : DRN.Framework.SharedKernel.Domain.SourceKnownEntity; }
            namespace Sample.Domain.Billing
            {
                public partial class Reader { public partial Sample.Domain.QA.User Read(); }
            }
            """, """
            // <auto-generated />
            namespace Sample.Domain.Billing
            {
                public partial class Reader { public partial Sample.Domain.QA.User Read() => new(); }
            }
            """);
        var facts = CreateFacts(compilation, "Domain");
        ConventionBoundaryAnalysis.Analyze(facts);
        var diagnostic = facts.Diagnostics.Where(item => item.Id == "DRNOWNERSHIP002").Should().ContainSingle().Which;
        diagnostic.Location.SourceTree!.FilePath.Should().Be("User.cs");
        diagnostic.Properties["Consumer"].Should().Be("M:Sample.Domain.Billing.Reader.Read");
        diagnostic.AdditionalLocations.Should().Contain(location => location.SourceTree != null && facts.IsGenerated(location.SourceTree));
    }

    [Fact]
    public async Task A_User_Partial_Method_Suppression_Should_Not_Cover_Other_Generated_Member_Uses()
    {
        var compilation = GeneratedCompilation("""
            namespace Sample.Domain.QA { public class User : DRN.Framework.SharedKernel.Domain.SourceKnownEntity; }
            namespace Sample.Domain.Billing
            {
                public partial class Reader
                {
                    [System.Diagnostics.CodeAnalysis.SuppressMessage("DRN.Conventions.Ownership", "DRNOWNERSHIP002",
                        Justification = "Reviewed billing reconciliation uses the QA identifier.")]
                    public partial long Allowed();
                    public partial long Other();
                }
            }
            """, """
            // <auto-generated />
            namespace Sample.Domain.Billing
            {
                public partial class Reader
                {
                    public partial long Allowed() => new Sample.Domain.QA.User().Id;
                    public partial long Other() => new Sample.Domain.QA.User().Id;
                }
            }
            """);
        var diagnostics = await AnalyzeWithConventions(compilation);
        diagnostics.Where(item => item.Id == "DRNOWNERSHIP002").Should().ContainSingle()
            .Which.Properties["Consumer"].Should().Be("M:Sample.Domain.Billing.Reader.Other");
    }

    [Theory]
    [DataInline(false)]
    [DataInline(true)]
    public void Fully_Generated_Types_Should_Require_A_Related_User_Input_To_Report(bool declaredOwner)
    {
        var compilation = GeneratedCompilation("""
            namespace Sample.Domain.QA { public class User : DRN.Framework.SharedKernel.Domain.SourceKnownEntity; }
            """, """
            // <auto-generated />
            namespace Sample.Domain.Billing
            {
                public class Reader { public Sample.Domain.QA.User Read() => new(); }
            }
            """);
        var input = new ConventionInput("Ownership", "Sample.Domain.Billing.Reader",
            ImmutableDictionary<string, string>.Empty.Add("Subdomain", "Billing"),
            Location.Create("DrnConventionInputs.g.cs", new(0, 1), new(new(0, 0), new(0, 1))));
        var facts = new ConventionFacts(compilation, new(true, "Sample.Domain", "Domain", "Sample.Domain", compilation.AssemblyName!, []),
            new(declaredOwner ? [input] : [], []), AnalyzerTestCompilation.EmptyOptionsProvider, CancellationToken.None);
        ConventionBoundaryAnalysis.Analyze(facts);
        var diagnostics = facts.Diagnostics.Where(item => item.Id == "DRNOWNERSHIP002").ToArray();
        diagnostics.Should().HaveCount(declaredOwner ? 1 : 0);
        if (declaredOwner)
        {
            diagnostics[0].Location.Kind.Should().Be(LocationKind.ExternalFile);
            diagnostics[0].Properties["Consumer"].Should().Be("T:Sample.Domain.Billing.Reader");
            diagnostics[0].AdditionalLocations.Should().NotBeEmpty();
        }
    }

    [Theory]
    [DataInline("class", "get; set;")]
    [DataInline("interface", "get;")]
    public void Closed_Referenced_Base_Signatures_Should_Retain_Their_Substituted_Entity_Owner(string kind, string accessors)
    {
        var producer = Compilation($$"""
            [assembly: System.Reflection.AssemblyMetadata("DRN.Conventions.ProjectName", "Sample.Domain")]
            [assembly: System.Reflection.AssemblyMetadata("DRN.Conventions.RootNamespace", "Sample.Domain")]
            namespace Sample.Domain.QA;
            public class User : DRN.Framework.SharedKernel.Domain.SourceKnownEntity;
            public {{kind}} Generic<T> { public System.Collections.Generic.List<T[]> Items { {{accessors}} } }
            public {{kind}} Closed : Generic<User>;
            """).WithAssemblyName("Referenced.Domain");
        producer.AssertNoCompilerErrors();
        var consumer = Compilation($$"""
            namespace Sample.Domain.Billing;
            public {{kind}} Reader : Sample.Domain.QA.Closed;
            """).AddReferences(AnalyzerTestCompilation.EmitReference(producer));
        consumer.AssertNoCompilerErrors();
        var facts = CreateFacts(consumer, "Domain");
        ConventionBoundaryAnalysis.Analyze(facts);
        var diagnostic = facts.Diagnostics.Where(item => item.Id == "DRNOWNERSHIP002").Should().ContainSingle().Which;
        diagnostic.Properties["Consumer"].Should().Be("T:Sample.Domain.Billing.Reader");
        diagnostic.Properties["Entity"].Should().Be("Sample.Domain.QA.User");
        diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan).Should().Be("Reader");
    }

    [Fact]
    public void Generic_Base_Syntax_And_Inherited_Signatures_Should_Deduplicate_The_Same_Type_Entity_Pair()
    {
        var facts = Facts("""
            namespace Sample.Domain.QA
            {
                public class User : DRN.Framework.SharedKernel.Domain.SourceKnownEntity;
                public class Generic<T> { public T[] Items { get; set; } }
            }
            namespace Sample.Domain.Billing
            {
                public class Reader : Sample.Domain.QA.Generic<Sample.Domain.QA.User>;
            }
            """);
        ConventionBoundaryAnalysis.Analyze(facts);
        facts.Diagnostics.Where(item => item.Id == "DRNOWNERSHIP002").Should().ContainSingle()
            .Which.Properties["Consumer"].Should().Be("T:Sample.Domain.Billing.Reader");
    }

    [Fact]
    public void Generated_Lookalikes_And_Unknown_Consumers_Should_Not_Acquire_Ownership()
    {
        var compilation = GeneratedCompilation("""
            namespace Sample.Domain.QA { public class User; public class Entity : DRN.Framework.SharedKernel.Domain.SourceKnownEntity; }
            namespace Sample.Domain.Billing { public partial class Reader; }
            namespace Unmapped { public partial class Reader; }
            """, """
            // <auto-generated />
            namespace Sample.Domain.Billing { public partial class Reader { public Sample.Domain.QA.User Read() => new(); } }
            namespace Unmapped { public partial class Reader { public Sample.Domain.QA.Entity Read() => new(); } }
            """);
        var facts = CreateFacts(compilation, "Domain");
        ConventionBoundaryAnalysis.Analyze(facts);
        facts.Diagnostics.Should().NotContain(item => item.Id == "DRNOWNERSHIP002");
    }

    [Fact]
    public void Boundary_Analysis_Should_Propagate_Cancellation()
    {
        using var cancellation = new CancellationTokenSource();
        var compilation = Compilation("namespace Sample.Domain.Billing; public class Reader;");
        var facts = new ConventionFacts(compilation, new(true, "Sample.Domain", "Domain", "Sample.Domain", compilation.AssemblyName!, []),
            new([], []), AnalyzerTestCompilation.EmptyOptionsProvider, cancellation.Token);
        cancellation.Cancel();
        var analyze = () => ConventionBoundaryAnalysis.Analyze(facts);
        analyze.Should().Throw<OperationCanceledException>();
    }

    [Fact]
    public async Task Standard_Justified_Suppression_Should_Cover_Only_Its_Target_Member()
    {
        var compilation = Compilation("""
            namespace Sample.Domain.QA { public class User : DRN.Framework.SharedKernel.Domain.SourceKnownEntity; }
            namespace Sample.Domain.Billing
            {
                public class Reader
                {
                    [System.Diagnostics.CodeAnalysis.SuppressMessage("DRN.Conventions.Ownership", "DRNOWNERSHIP002",
                        Justification = "Reviewed billing reconciliation consumes the QA account identifier")]
                    public Sample.Domain.QA.User Allowed() => new();
                    public Sample.Domain.QA.User Other() => new();
                }
            }
            """);
        compilation.AssertNoCompilerErrors();
        var diagnostics = await AnalyzeWithConventions(compilation);
        diagnostics.Where(item => item.Id == "DRNOWNERSHIP002").Should().ContainSingle()
            .Which.Properties["Consumer"].Should().Be("M:Sample.Domain.Billing.Reader.Other");
    }

    [Theory]
    [DataInline("System.Collections.Generic.List<Envelope<User[]>>", true)]
    [DataInline("System.Collections.Generic.List<Envelope<System.Guid>>", false)]
    [DataInline("Microsoft.EntityFrameworkCore.DbSet<Envelope<System.Guid>>", true)]
    public void Public_Contract_Shapes_Should_Find_Nested_Persistence_Without_Name_Heuristics(string shape, bool violation)
    {
        var facts = ContractFacts($$"""
            namespace Sample.Contract.QA;
            public class Envelope<T> { public T Value { get; set; } }
            public class Response { public {{shape}} Payload { get; set; } }
            """);
        ConventionBoundaryAnalysis.Analyze(facts);
        var diagnostics = facts.Diagnostics.Where(item => item.Id == "DRNARCHITECTURE002").ToArray();
        diagnostics.Should().HaveCount(violation ? 1 : 0);
        if (violation)
        {
            diagnostics[0].GetMessage().Should().Contain("Response.Payload").And.Contain(
                shape.StartsWith("Microsoft.EntityFrameworkCore.DbSet", StringComparison.Ordinal) ? "DbSet" : "Sample.Domain.QA.User");
            diagnostics[0].Location.SourceTree!.GetText().ToString(diagnostics[0].Location.SourceSpan).Should().Be("Payload");
        }
    }

    [Theory]
    [DataInline("public static User Read() => new();", false)]
    [DataInline("public User Read<T>() => new();", false)]
    [DataInline("[Microsoft.AspNetCore.Mvc.NonAction] public User Read() => new();", false)]
    [DataInline("public User Read() => new();", true)]
    public void Transport_Contract_Check_Should_Exclude_Static_And_Generic_Helpers(string member, bool violation)
    {
        var facts = Facts($$"""
            namespace Sample.Hosted.QA;
            public class User : DRN.Framework.SharedKernel.Domain.SourceKnownEntity;
            public class UsersController : Microsoft.AspNetCore.Mvc.ControllerBase
            {
                {{member}}
            }
            """, "Hosted");
        ConventionBoundaryAnalysis.Analyze(facts);
        facts.Diagnostics.Count(item => item.Id == "DRNARCHITECTURE002").Should().Be(violation ? 1 : 0);
    }

    [Fact]
    public void Dto_Mapping_Constructor_And_Private_State_Should_Not_Expose_An_Entity()
    {
        var facts = ContractFacts("""
            namespace Sample.Contract.QA;
            public class Response : DRN.Framework.SharedKernel.Domain.Dto
            {
                private User source;
                public Response(User user) : base(user) { source = user; }
                public System.Guid Identifier => Id;
            }
            """);
        ConventionBoundaryAnalysis.Analyze(facts);
        facts.Diagnostics.Should().NotContain(item => item.Id == "DRNARCHITECTURE002");
    }

    [Fact]
    public void Mapping_Constructor_Exception_Should_Not_Exempt_Other_Persistence_Parameters()
    {
        var facts = ContractFacts("""
            namespace Sample.Contract.QA;
            public class Response : DRN.Framework.SharedKernel.Domain.Dto
            {
                public Response(User user, User other) : base(user) { }
            }
            """);
        ConventionBoundaryAnalysis.Analyze(facts);
        facts.Diagnostics.Where(item => item.Id == "DRNARCHITECTURE002").Should().ContainSingle();
    }

    [Theory]
    [DataInline("User user", 0)]
    [DataInline("User user, User other", 1)]
    public void Primary_Dto_Constructor_Should_Exempt_Only_The_Forwarded_Entity(string parameters, int count)
    {
        var facts = ContractFacts($$"""
            namespace Sample.Contract.QA;
            public class Response({{parameters}}) : DRN.Framework.SharedKernel.Domain.Dto(user);
            """);
        ConventionBoundaryAnalysis.Analyze(facts);
        facts.Diagnostics.Where(item => item.Id == "DRNARCHITECTURE002").Should().HaveCount(count);
    }

    [Theory]
    [DataInline("class", "get; set;")]
    [DataInline("interface", "get;")]
    public async Task Inherited_Referenced_Contract_Members_Should_Be_Checked_At_The_Consumer_Declaration(string kind, string accessors)
    {
        var dependency = Compilation($$"""
            namespace Upstream.Models;
            public class User : DRN.Framework.SharedKernel.Domain.SourceKnownEntity;
            public {{kind}} BaseResponse { public User Payload { {{accessors}} } }
            """).WithAssemblyName("Upstream.Models");
        dependency.AssertNoCompilerErrors();
        var compilation = Compilation($$"""
            namespace Sample.Contract.QA;
            public {{kind}} Response : Upstream.Models.BaseResponse;
            """).AddReferences(dependency.ToMetadataReference());
        compilation.AssertNoCompilerErrors();
        var diagnostics = await AnalyzeWithConventions(compilation, "Contract");
        diagnostics.Should().NotContain(item => item.Id == "AD0001");
        var diagnostic = diagnostics.Where(item => item.Id == "DRNARCHITECTURE002").Should().ContainSingle().Which;
        diagnostic.GetMessage().Should().Contain("BaseResponse.Payload").And.Contain("Upstream.Models.User");
        (await diagnostic.Location.SourceTree!.GetTextAsync()).ToString(diagnostic.Location.SourceSpan).Should().Be("Response");
        diagnostic.AdditionalLocations.Should().OnlyContain(location => location.SourceTree == null || compilation.ContainsSyntaxTree(location.SourceTree));
    }

    [Fact]
    public async Task Nested_Referenced_Interface_Models_Should_Include_Inherited_Properties()
    {
        var dependency = Compilation("""
            [assembly: System.Reflection.AssemblyMetadata("DRN.Conventions.ProjectName", "Upstream.Contract")]
            [assembly: System.Reflection.AssemblyMetadata("DRN.Conventions.RootNamespace", "Upstream.Contract")]
            namespace Upstream.Contract.QA;
            public class User : DRN.Framework.SharedKernel.Domain.SourceKnownEntity;
            public interface IBaseView { User Payload { get; } }
            public interface IView : IBaseView;
            """).WithAssemblyName("Upstream.Contract");
        dependency.AssertNoCompilerErrors();
        var compilation = Compilation("""
            namespace Sample.Contract.QA;
            public class Response { public Upstream.Contract.QA.IView View { get; set; } }
            """).AddReferences(dependency.ToMetadataReference());
        compilation.AssertNoCompilerErrors();
        var diagnostics = await AnalyzeWithConventions(compilation, "Contract");
        diagnostics.Should().NotContain(item => item.Id == "AD0001");
        var diagnostic = diagnostics.Where(item => item.Id == "DRNARCHITECTURE002").Should().ContainSingle().Which;
        diagnostic.GetMessage().Should().Contain("Response.View").And.Contain("Upstream.Contract.QA.User");
        (await diagnostic.Location.SourceTree!.GetTextAsync()).ToString(diagnostic.Location.SourceSpan).Should().Be("View");
    }

    [Fact]
    public void Nested_Models_Should_Not_Expose_Private_Explicit_Interface_State()
    {
        var facts = ContractFacts("""
            namespace Sample.Contract.QA;
            internal interface IPrivateState { User Payload { get; } }
            public class Model : IPrivateState { User IPrivateState.Payload => null!; }
            public class Response { public Model View { get; set; } }
            """);
        ConventionBoundaryAnalysis.Analyze(facts);
        facts.Diagnostics.Should().NotContain(item => item.Id == "DRNARCHITECTURE002");
    }

    [Theory]
    [DataInline("Domain", "Hosted", true, true)]
    [DataInline("Application", "Infra", true, false)]
    [DataInline("Domain", "Hosted", false, false)]
    public void Reference_Rules_Should_Use_Direct_Provenance_And_Exported_Roles(string from, string to, bool direct, bool violation)
    {
        var dependency = Compilation($$"""
            [assembly: System.Reflection.AssemblyMetadata("DRN.Conventions.ProjectName", "Sample.{{to}}")]
            [assembly: System.Reflection.AssemblyMetadata("DRN.Conventions.RootNamespace", "Sample.{{to}}")]
            public class Marker;
            """).WithAssemblyName("PublishedDependency");
        var reference = AnalyzerTestCompilation.EmitReference(dependency, "/references/dependency.dll");
        var compilation = Compilation("public class Consumer;").AddReferences(reference);
        var records = new List<ConventionInput>
        {
            new("Reference", "/references/dependency.dll", ImmutableDictionary<string, string>.Empty
                .Add("Stage", "Resolved").Add("Package", "App.Dependency"), Location.None)
        };
        if (direct) records.Add(new("Reference", "App.Dependency",
            ImmutableDictionary<string, string>.Empty.Add("Stage", "Package"), Location.None));
        var facts = new ConventionFacts(compilation, new(true, "Sample." + from, from, "Sample." + from, compilation.AssemblyName!, []),
            new([.. records], []), AnalyzerTestCompilation.EmptyOptionsProvider, CancellationToken.None);
        ConventionBoundaryAnalysis.Analyze(facts);
        facts.Diagnostics.Where(item => item.Id == "DRNARCHITECTURE003").Should().HaveCount(violation ? 1 : 0);
        if (violation) facts.Diagnostics.Single(item => item.Id == "DRNARCHITECTURE003").Properties["Target"].Should().Be("App.Dependency");
    }

    [Theory]
    [DataInline("Domain", "Domain", false)]
    [DataInline("Infra", "Domain", true)]
    [DataInline("Utils", "Domain", true)]
    public void Entity_Role_Should_Be_Semantic(string role, string expected, bool violation)
    {
        var facts = Facts($$"""
            namespace Sample.{{role}}.QA;
            public class User : DRN.Framework.SharedKernel.Domain.SourceKnownEntity;
            public class Repository;
            """, role);
        ConventionBoundaryAnalysis.Analyze(facts);
        var diagnostics = facts.Diagnostics.Where(item => item.Id == "DRNARCHITECTURE004").ToArray();
        diagnostics.Should().HaveCount(violation ? 1 : 0);
        if (violation) diagnostics[0].GetMessage().Should().Contain("role '" + expected + "'");
    }

    [Fact]
    public void Cyclic_Public_Models_Should_Terminate_Without_Claiming_Entity_Leakage()
    {
        var facts = Facts("""
            namespace Sample.Contract.QA;
            public class Response { public Node Root { get; set; } }
            public class Node { public Node Next { get; set; } public System.Guid Id { get; set; } }
            """, "Contract");
        ConventionBoundaryAnalysis.Analyze(facts);
        facts.Diagnostics.Should().NotContain(item => item.Id == "DRNARCHITECTURE002" || item.Id == "DRNARCHITECTURE001");
    }

    [Fact]
    public void Closed_Local_Generic_Contract_Base_Should_Not_Be_Skipped_As_Already_Checked()
    {
        var facts = ContractFacts("""
            namespace Sample.Contract.QA;
            public class Envelope<T> { public T Payload { get; set; } }
            public class Response : Envelope<User>;
            """);
        ConventionBoundaryAnalysis.Analyze(facts);
        var diagnostic = facts.Diagnostics.Where(item => item.Id == "DRNARCHITECTURE002").Should().ContainSingle().Which;
        diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan).Should().Be("Response");
    }

    [Fact]
    public void Public_Entity_Constrained_Type_Parameter_Should_Retain_Its_Known_Persistence_Shape()
    {
        var facts = ContractFacts("""
            namespace Sample.Contract.QA;
            public class Envelope<T> where T : User { public T Payload { get; set; } }
            """);
        ConventionBoundaryAnalysis.Analyze(facts);
        facts.Diagnostics.Where(item => item.Id == "DRNARCHITECTURE002").Should().ContainSingle()
            .Which.GetMessage().Should().Contain("Envelope<T>.Payload").And.Contain("Sample.Domain.QA.User");
    }

    [Fact]
    public void Public_Method_Constraint_Should_Expose_Entity_Without_A_Value_Parameter()
    {
        var facts = ContractFacts("""
            namespace Sample.Contract.QA;
            public class Response { public void Read<T>() where T : User { } }
            """);
        ConventionBoundaryAnalysis.Analyze(facts);
        facts.Diagnostics.Where(item => item.Id == "DRNARCHITECTURE002").Should().ContainSingle()
            .Which.GetMessage().Should().Contain("Response.Read<T>()").And.Contain("Sample.Domain.QA.User");
    }

    [Theory]
    [DataInlineUnit("Domain", true, true, true)]
    [DataInlineUnit("Contract", true, false, true)]
    [DataInlineUnit("Domain", false, true, false)]
    [DataInlineUnit("Contract", false, false, false)]
    public async Task Dto_Placement_And_Persistence_Dependencies_Should_Report_Independently(
        string role, bool usesContext, bool misplaced, bool forbidden)
    {
        var members = usesContext ? "private DbContext First; private DbContext Second;" : "";
        var compilation = Compilation($$"""
            using Microsoft.EntityFrameworkCore;
            namespace Sample.{{role}}.QA;
            public class Response : DRN.Framework.SharedKernel.Domain.Dto { {{members}} }
            """);
        var diagnostics = await AnalyzeWithConventions(compilation, role);
        diagnostics.Where(item => item.Id == "DRNARCHITECTURE004").Should().HaveCount(misplaced ? 1 : 0);
        var dependencies = diagnostics.Where(item => item.Id == "DRNARCHITECTURE005").ToArray();
        dependencies.Should().HaveCount(forbidden ? 1 : 0);
        if (forbidden)
        {
            var evidence = dependencies.Single().AdditionalLocations;
            evidence.Should().HaveCount(2).And.OnlyHaveUniqueItems();
            evidence.Select(location => location.SourceTree!.GetText().ToString(location.SourceSpan))
                .Should().OnlyContain(text => text == "DbContext");
        }
    }

    [Theory]
    [DataInline("Domain", "private Microsoft.EntityFrameworkCore.DbContext context;")]
    [DataInline("Domain", "private Microsoft.EntityFrameworkCore.ModelBuilder model;")]
    [DataInline("Contract", "public Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder Configure() => null;")]
    [DataInline("Utils", "public bool Read() { var context = new Microsoft.EntityFrameworkCore.DbContext(new Microsoft.EntityFrameworkCore.DbContextOptions<Microsoft.EntityFrameworkCore.DbContext>()); return context.ChangeTracker.HasChanges(); }")]
    public void Persistence_Implementation_Use_Should_Be_Diagnosed_Without_Inheritance(string role, string body)
    {
        var facts = Facts($$"""
            namespace Sample.{{role}}.QA;
            public class Consumer { {{body}} }
            """, role);
        ConventionBoundaryAnalysis.Analyze(facts);
        var diagnostic = facts.Diagnostics.Where(item => item.Id == "DRNARCHITECTURE005").Should().ContainSingle().Which;
        diagnostic.GetMessage().Should().Be($"Type 'Consumer' in layer '{role}' uses persistence APIs. Remove this dependency.");
        diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan).Should().Be("Consumer");
        diagnostic.AdditionalLocations.Should().NotBeEmpty();
    }

    [Fact]
    public void Lightweight_Mapping_Attributes_And_Implementation_Lookalikes_Should_Remain_Valid()
    {
        var facts = Facts("""
            namespace Sample.Domain.QA;
            [Microsoft.EntityFrameworkCore.Owned]
            public class Address { public string City { get; set; } }
            public class DbContext;
            public class EntityTypeBuilder;
            public class ControllerBase;
            public class Consumer
            {
                public DbContext Read(EntityTypeBuilder builder, ControllerBase controller) => new();
            }
            """);
        ConventionBoundaryAnalysis.Analyze(facts);
        facts.Diagnostics.Should().NotContain(item => item.Id == "DRNARCHITECTURE005" || item.Id == "DRNARCHITECTURE004");
    }

    [Fact]
    public void Domain_Repository_Contracts_Should_Remain_Valid_Dependencies()
    {
        var facts = Facts("""
            namespace Sample.Domain.QA;
            public class Order : DRN.Framework.SharedKernel.Domain.AggregateRoot;
            public class Consumer
            {
                private DRN.Framework.SharedKernel.Domain.Repository.ISourceKnownRepository<Order> repository;
            }
            """);
        ConventionBoundaryAnalysis.Analyze(facts);
        facts.Diagnostics.Should().NotContain(item => item.Id == "DRNARCHITECTURE005" || item.Id == "DRNARCHITECTURE004");
    }

    [Fact]
    public void Repository_Implementation_Typeof_Use_Should_Not_Be_Mistaken_For_A_Domain_Contract()
    {
        var compilation = Compilation("""
            namespace Sample.Domain.QA;
            public class Consumer
            {
                public System.Type Repository => typeof(DRN.Framework.EntityFramework.Domain.SourceKnownRepository<,>);
            }
            """).AddReferences(
            AnalyzerTestCompilation.EntityFrameworkReference,
            AnalyzerTestCompilation.UtilsReference);
        compilation.AssertNoCompilerErrors();
        var facts = CreateFacts(compilation, "Domain");
        ConventionBoundaryAnalysis.Analyze(facts);
        facts.Diagnostics.Where(item => item.Id == "DRNARCHITECTURE005").Should().ContainSingle()
            .Which.GetMessage().Should().Contain("uses persistence APIs");
    }

    [Fact]
    public void Mvc_Operation_Use_Should_Belong_To_Hosted()
    {
        var facts = Facts("""
            namespace Sample.Domain.QA;
            public class Consumer
            {
                public bool Render() { var result = new Microsoft.AspNetCore.Mvc.OkResult(); return result.StatusCode == 200; }
            }
            """);
        ConventionBoundaryAnalysis.Analyze(facts);
        facts.Diagnostics.Where(item => item.Id == "DRNARCHITECTURE005").Should().ContainSingle()
            .Which.GetMessage().Should().Contain("uses transport APIs");
    }

    [Fact]
    public async Task Dependency_Suppression_Should_Not_Suppress_Public_Contract_Leakage()
    {
        var compilation = Compilation("""
            namespace Sample.Contract.QA;
            [System.Diagnostics.CodeAnalysis.SuppressMessage("DRN.Conventions.Architecture", "DRNARCHITECTURE005",
                Justification = "Reviewed compatibility adapter scheduled for removal")]
            public class Consumer { public Microsoft.EntityFrameworkCore.DbContext Context { get; set; } }
            """);
        compilation.AssertNoCompilerErrors();
        var diagnostics = await AnalyzeWithConventions(compilation, "Contract");
        diagnostics.Should().NotContain(item => item.Id == "DRNARCHITECTURE005");
        diagnostics.Where(item => item.Id == "DRNARCHITECTURE002").Should().ContainSingle();
    }

    [Theory]
    [DataInline("Contract", "DRN.Framework.SharedKernel", false)]
    [DataInline("Contract", "DRN.Framework.Utils", true)]
    [DataInline("Domain", "DRN.Framework.Utils", false)]
    [DataInline("Domain", "DRN.Framework.EntityFramework", true)]
    [DataInline("Utils", "DRN.Framework.Hosting", true)]
    [DataInline("Domain", "Microsoft.EntityFrameworkCore", false)]
    [DataInline("Domain", "Microsoft.Extensions.Identity.Stores", false)]
    [DataInline("Contract", "Neutral.Values", false)]
    [DataInline("Contract", "DRN.Framework.Hosting.Extensions", false)]
    public void Framework_Package_Rules_Should_Use_Exact_Identities_And_Preserve_Mapping_Dependencies(string role, string package, bool violation)
    {
        var compilation = Compilation("public class Consumer;");
        var input = new ConventionInput("Reference", package,
            ImmutableDictionary<string, string>.Empty.Add("Stage", "Package"), Location.None);
        var facts = new ConventionFacts(compilation, new(true, "Sample." + role, role, "Sample." + role, compilation.AssemblyName!, []),
            new([input], []), AnalyzerTestCompilation.EmptyOptionsProvider, CancellationToken.None);
        ConventionBoundaryAnalysis.Analyze(facts);
        var diagnostics = facts.Diagnostics.Where(item => item.Id == "DRNARCHITECTURE003").ToArray();
        diagnostics.Should().HaveCount(violation ? 1 : 0);
        if (violation) diagnostics[0].Properties["Target"].Should().Be(package);
    }

    [Fact]
    public void Source_Framework_Reference_Should_Apply_The_Same_Rule_As_Its_Package()
    {
        var path = typeof(Framework.EntityFramework.Context.DrnContext<>).Assembly.Location;
        var compilation = Compilation("public class Consumer;").AddReferences(AnalyzerTestCompilation.EntityFrameworkReference);
        var direct = new ConventionInput("Reference", "/projects/framework.csproj",
            ImmutableDictionary<string, string>.Empty.Add("Stage", "Project").Add("Source", "../framework.csproj"), Location.None);
        var resolved = new ConventionInput("Reference", path, ImmutableDictionary<string, string>.Empty
            .Add("Stage", "Resolved").Add("Project", direct.Identity), Location.None);
        var facts = new ConventionFacts(compilation, new(true, "Sample.Contract", "Contract", "Sample.Contract", compilation.AssemblyName!, []),
            new([direct, resolved], []), AnalyzerTestCompilation.EmptyOptionsProvider, CancellationToken.None);
        ConventionBoundaryAnalysis.Analyze(facts);
        var diagnostic = facts.Diagnostics.Where(item => item.Id == "DRNARCHITECTURE003").Should().ContainSingle().Which;
        diagnostic.GetMessage().Should().Contain("Direct project").And.Contain("DRN.Framework.EntityFramework");
        diagnostic.Properties["Target"].Should().Be(direct.Identity);
    }

    [Fact]
    public void Framework_Reference_Exception_Should_Bind_One_Exact_Declared_Edge()
    {
        var compilation = Compilation("public class Consumer;");
        var direct = new ConventionInput("Reference", "DRN.Framework.Hosting",
            ImmutableDictionary<string, string>.Empty.Add("Stage", "Package"), Location.None);
        var exception = new ConventionInput("Exception", "DRNARCHITECTURE003", ImmutableDictionary<string, string>.Empty
            .Add("Target", direct.Identity).Add("Reason", "Reviewed compatibility integration")
            .Add("Owner", "Contract team").Add("Review", "2099-01-01"), Location.None);
        var facts = new ConventionFacts(compilation, new(true, "Sample.Contract", "Contract", "Sample.Contract", compilation.AssemblyName!, []),
            new([direct, exception], []), AnalyzerTestCompilation.EmptyOptionsProvider, CancellationToken.None);
        ConventionBoundaryAnalysis.Analyze(facts);
        var exceptions = new ConventionExceptions(facts, new DateOnly(2026, 11, 15));
        facts.Diagnostics.Should().NotContain(item => item.Id == "DRNCONVENTIONEXCEPTION001");
        exceptions.Applies(facts.Diagnostics.Single(item => item.Id == "DRNARCHITECTURE003")).Should().BeTrue();
    }

    [Theory]
    [DataInline("User?", "Sample.Domain.QA.User?")]
    [DataInline("(System.Guid Key, User? Entity)", "Sample.Domain.QA.User?")]
    [DataInline("(User Entity, int Count)?", "Sample.Domain.QA.User")]
    [DataInline("System.Collections.Generic.IReadOnlyList<(string Name, User?[] Entities)>", "Sample.Domain.QA.User?")]
    public void Nullable_And_Tuple_Contracts_Should_Report_The_Exact_First_Public_Member(string shape, string exposed)
    {
        var facts = ContractFacts($$"""
            #nullable enable
            namespace Sample.Contract.QA;
            public class Response { public {{shape}} Payload { get; init; } }
            """);
        ConventionBoundaryAnalysis.Analyze(facts);
        var diagnostic = facts.Diagnostics.Where(item => item.Id == "DRNARCHITECTURE002").Should().ContainSingle().Which;
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Warning);
        diagnostic.GetMessage().Should().Be("Public contract member 'Sample.Contract.QA.Response.Payload' exposes persistence/domain type '" +
            exposed + "'; expose an approved DTO or value model.");
        diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan).Should().Be("Payload");
        diagnostic.Properties["Target"].Should().Be("P:Sample.Contract.QA.Response.Payload");
        diagnostic.AdditionalLocations.Should().ContainSingle().Which.SourceTree!.GetText()
            .ToString(diagnostic.AdditionalLocations.Single().SourceSpan).Should().Be("User");
    }

    [Fact]
    public void Dto_Identity_Timestamps_And_Nullable_Value_Tuples_Should_Not_Expose_Private_Entity_State()
    {
        var facts = Facts("""
            #nullable enable
            namespace Sample.Contract.QA;
            public class Response : DRN.Framework.SharedKernel.Domain.Dto
            {
                public Response(DRN.Framework.SharedKernel.Domain.SourceKnownEntity? entity = null) : base(entity) { }
                public (System.Guid? Key, System.DateTimeOffset? ChangedAt)? Cursor { get; init; }
            }
            """, "Contract");
        var dto = facts.Compilation.GetTypeByMetadataName("DRN.Framework.SharedKernel.Domain.Dto")!;
        ((IPropertySymbol)dto.GetMembers("Id").Single()).Type.SpecialType.Should().Be(SpecialType.None);
        ((IPropertySymbol)dto.GetMembers("Id").Single()).Type.ToDisplayString().Should().Be("System.Guid");
        ((IPropertySymbol)dto.GetMembers("CreatedAt").Single()).Type.ToDisplayString().Should().Be("System.DateTimeOffset");
        ((IPropertySymbol)dto.GetMembers("ModifiedAt").Single()).Type.ToDisplayString().Should().Be("System.DateTimeOffset");
        ConventionBoundaryAnalysis.Analyze(facts);
        facts.Diagnostics.Should().BeEmpty();
    }

    [Theory]
    [DataInline("Microsoft.EntityFrameworkCore.DbContext", "Microsoft.EntityFrameworkCore.DbContext")]
    [DataInline("Microsoft.EntityFrameworkCore.ModelBuilder", "Microsoft.EntityFrameworkCore.ModelBuilder")]
    [DataInline("Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder", "Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder")]
    [DataInline("DRN.Framework.SharedKernel.Domain.Repository.ISourceKnownRepository<Order>", "DRN.Framework.SharedKernel.Domain.Repository.ISourceKnownRepository<Sample.Domain.QA.Order>")]
    public void Contract_Context_And_Repository_Exposure_Should_Report_Exact_Public_Surface(string shape, string exposed)
    {
        var facts = ContractFacts($$"""
            namespace Sample.Contract.QA;
            public class Response { public {{shape}} Persistence { get; init; } }
            """);
        ConventionBoundaryAnalysis.Analyze(facts);
        var diagnostic = facts.Diagnostics.Where(item => item.Id == "DRNARCHITECTURE002").Should().ContainSingle().Which;
        diagnostic.GetMessage().Should().Be("Public contract member 'Sample.Contract.QA.Response.Persistence' exposes persistence/domain type '" +
            exposed + "'; expose an approved DTO or value model.");
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Warning);
        diagnostic.Properties["Target"].Should().Be("P:Sample.Contract.QA.Response.Persistence");
        diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan).Should().Be("Persistence");
    }

    [Fact]
    public void Contract_Near_Miss_Persistence_Type_Names_Should_Not_Match_Framework_Metadata()
    {
        var facts = Facts("""
            namespace Sample.Contract.QA;
            public class DbContext;
            public class SourceKnownEntity;
            public interface ISourceKnownRepository<T>;
            public class Response
            {
                public DbContext Context { get; init; }
                public SourceKnownEntity Entity { get; init; }
                public ISourceKnownRepository<System.Guid> Repository { get; init; }
            }
            """, "Contract");
        ConventionBoundaryAnalysis.Analyze(facts);
        facts.Diagnostics.Should().BeEmpty();
    }

    [Theory]
    [DataInline(false)]
    [DataInline(true)]
    public void Explicit_Neutral_Referenced_Value_Should_Be_Traversed_Without_Exempting_Nested_Persistence(bool exposesEntity)
    {
        var dependency = Compilation($$"""
            [assembly: System.Reflection.AssemblyMetadata("DRN.Conventions.ProjectName", "Shared.Utils")]
            [assembly: System.Reflection.AssemblyMetadata("DRN.Conventions.RootNamespace", "Shared.Utils")]
            [assembly: System.Reflection.AssemblyMetadata("DRN.Conventions.Ownership.Shared.Utils.Value.Subdomain", "Shared")]
            [assembly: System.Reflection.AssemblyMetadata("DRN.Conventions.Ownership.Shared.Utils.Value.Kind", "Value")]
            [assembly: System.Reflection.AssemblyMetadata("DRN.Conventions.Ownership.Shared.Utils.Value.Reason", "Reviewed neutral transport value.")]
            namespace Shared.Utils;
            public class Entity : DRN.Framework.SharedKernel.Domain.SourceKnownEntity;
            public class Value { public {{(exposesEntity ? "Entity" : "System.Guid?")}} Data { get; init; } }
            """).WithAssemblyName("Shared.Values");
        dependency.AssertNoCompilerErrors();
        var compilation = Compilation("""
            namespace Sample.Contract.QA;
            public class Response { public Shared.Utils.Value Value { get; init; } }
            """).AddReferences(dependency.ToMetadataReference());
        compilation.AssertNoCompilerErrors();
        var facts = CreateFacts(compilation, "Contract");
        ConventionBoundaryAnalysis.Analyze(facts);
        facts.Diagnostics.Should().NotContain(item => item.Id == "DRNCONVENTIONINPUT001");
        var diagnostics = facts.Diagnostics.Where(item => item.Id == "DRNARCHITECTURE002").ToArray();
        diagnostics.Should().HaveCount(exposesEntity ? 1 : 0);
        if (!exposesEntity) return;
        diagnostics[0].GetMessage().Should().Be("Public contract member 'Sample.Contract.QA.Response.Value' exposes persistence/domain type 'Shared.Utils.Entity'; expose an approved DTO or value model.");
        diagnostics[0].Location.SourceTree!.GetText().ToString(diagnostics[0].Location.SourceSpan).Should().Be("Value");
    }

    [Theory]
    [DataInline("object")]
    [DataInline("dynamic")]
    public void Opaque_Contract_Values_Should_Not_Fabricate_A_Provable_Entity_Leak(string shape)
    {
        var facts = Facts($$"""
            namespace Sample.Contract.QA;
            public class Response { public {{shape}} Payload { get; init; } }
            """, "Contract");
        ConventionBoundaryAnalysis.Analyze(facts);
        // Absence of a statically bound entity is not serializer or runtime safety certification.
        facts.Diagnostics.Should().NotContain(item => item.Id == "DRNARCHITECTURE002");
    }

    private static Compilation GeneratedCompilation(string source, string generated)
    {
        var compilation = Compilation(source, "User.cs");
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new GeneratedUses(generated).AsSourceGenerator());
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var generatorDiagnostics);
        generatorDiagnostics.Should().NotContain(item => item.Severity == DiagnosticSeverity.Error);
        output.AssertNoCompilerErrors();
        return output;
    }

    private sealed class GeneratedUses(string source) : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context) =>
            context.RegisterPostInitializationOutput(output => output.AddSource("Reader.g.cs", source));
    }

    private static ConventionFacts ContractFacts(string source)
    {
        var compilation = Compilation(source).AddSyntaxTrees(CSharpSyntaxTree.ParseText("""
            global using Sample.Domain.QA;
            namespace Sample.Domain.QA;
            public class User : DRN.Framework.SharedKernel.Domain.SourceKnownEntity;
            public class Order : DRN.Framework.SharedKernel.Domain.AggregateRoot;
            """, path: "Entities.cs"));
        compilation.AssertNoCompilerErrors();
        var domain = new ConventionInput("Role", "Domain", ImmutableDictionary<string, string>.Empty
            .Add("NamespacePrefix", "Sample.Domain").Add("LogicalPathPrefix", "Domain"), Location.None);
        return new(compilation, new(true, "Sample.Contract", "Contract", "Sample.Contract", compilation.AssemblyName!, []),
            new([domain], []), AnalyzerTestCompilation.EmptyOptionsProvider, CancellationToken.None);
    }

    private static ConventionFacts Facts(string source, string role = "Domain")
    {
        var compilation = Compilation(source);
        compilation.AssertNoCompilerErrors();
        return CreateFacts(compilation, role);
    }

    private static ConventionFacts CreateFacts(Compilation compilation, string role) => new(compilation,
        new(true, "Sample." + role, role, "Sample." + role, compilation.AssemblyName!, []), new([], []), AnalyzerTestCompilation.EmptyOptionsProvider, CancellationToken.None);

    private static async Task<ImmutableArray<Diagnostic>> AnalyzeWithConventions(Compilation compilation, string role = "Domain")
    {
        var paths = compilation.SyntaxTrees.Select(tree => "[assembly: DRN.Conventions.Path(" +
            SymbolDisplay.FormatLiteral(tree.FilePath, true) + ", LogicalPath = " +
            SymbolDisplay.FormatLiteral("QA/" + Path.GetFileName(tree.FilePath), true) + ")]");
        var fixture = ConventionTestDependencies.WithRequiredLayers(compilation, role);
        var input = new ProjectDiagnosticRulesTests.RuleFile(ConventionInputs.FileName,
            "[assembly: DRN.Conventions.Version(\"1\")]\n[assembly: DRN.Conventions.Reference(\"Inventory\", Stage = \"Complete\")]\n" +
            string.Join("\n", paths) + "\n" + ConventionTestDependencies.Serialize(fixture.Records));
        var diagnostics = await AnalyzerTestCompilation.Analyze(fixture.Compilation, new SourceKnownConventionAnalyzer(),
            AnalyzerTestCompilation.ConventionOptions("Sample." + role, files: input));
        diagnostics.Should().NotContain(item => item.Id == "AD0001" || item.Id == "DRNCONVENTIONINPUT001");
        return diagnostics;
    }
}
