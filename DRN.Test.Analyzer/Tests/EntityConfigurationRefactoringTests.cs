using DRN.Framework.SharedKernel.CodeFixes;
using DRN.Framework.Testing.Contexts;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.Text;

namespace DRN.Test.Analyzer.Tests;

public sealed class EntityConfigurationRefactoringTests
{
    private const string EntitySource = """
        namespace Sample.Domain.QA;
        [System.ComponentModel.DataAnnotations.Schema.Table("users")]
        public class User : DRN.Framework.SharedKernel.Domain.SourceKnownEntity { public override DRN.Framework.SharedKernel.Domain.Dto ToDto() => new(this); }
        """;

    [Theory]
    [DataInlineUnit]
    public void Destination_Guard_Should_Reject_Linked_Roots_And_Descendants(DrnTestContextUnit context)
    {
        var temp = context.GetTempPath();
        var root = Directory.CreateDirectory(Path.Combine(temp, "Project")).FullName;
        var target = Directory.CreateDirectory(Path.Combine(temp, "Outside")).FullName;
        var link = Path.Combine(root, "Linked");
        try
        {
            Directory.CreateSymbolicLink(link, target);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            Assert.Skip("Directory symbolic links are unavailable.");
        }
        var destination = Path.Combine(link, "Missing", "Config.cs");
        CodeFixDestinationGuard.SafeDestination(link, destination).Should().BeFalse();
        CodeFixDestinationGuard.SafeDestination(root, destination).Should().BeFalse();
        CodeFixDestinationGuard.SafeDestination(root, Path.Combine(root, "Missing", "Config.cs")).Should().BeTrue();
    }

    [Fact]
    public async Task Attribute_Only_Entity_Should_Offer_Additive_Context_Selected_Configuration()
    {
        using var workspace = new AdhocWorkspace();
        var entity = CreateDocuments(workspace);
        var actions = await Actions(entity);
        var action = actions.Should().ContainSingle().Which;
        action.Title.Should().Contain("Sample.Infra.QA.QAContext").And.Contain("QA/Configurations/UserConfig.cs");
        var changed = await action.GetChangedSolutionAsync();
        (await changed.GetDocument(entity.Id)!.GetTextAsync()).ToString().Should().Be(EntitySource);
        var added = changed.GetProject(entity.Project.Id)!.Documents.Single(document => document.Name == "UserConfig.cs");
        var source = (await added.GetTextAsync()).ToString();
        source.Should().Contain("namespace Sample.Infra.QA.Configurations;")
            .And.Contain("IEntityTypeConfiguration<global::Sample.Domain.QA.User>")
            .And.Contain("public void Configure(").And.NotContain("HasKey").And.NotContain("ToTable");
        (await added.Project.GetCompilationAsync())!.AssertNoCompilerErrors();
        workspace.TryApplyChanges(changed).Should().BeTrue();
        (await Actions(workspace.CurrentSolution.GetDocument(entity.Id)!)).Should().BeEmpty();
    }

    [Fact]
    public async Task Existing_Configuration_Even_Outside_Discovery_Should_Block_Duplicate_Action()
    {
        using var workspace = new AdhocWorkspace();
        var entity = CreateDocuments(workspace, """
            namespace Sample.Infra.Other;
            public class Existing : Microsoft.EntityFrameworkCore.IEntityTypeConfiguration<Sample.Domain.QA.User>
            {
                public void Configure(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<Sample.Domain.QA.User> builder) { }
            }
            """);
        (await Actions(entity)).Should().BeEmpty();
    }

    [Fact]
    public async Task Static_DbSet_Should_Not_Establish_Entity_Membership_For_An_Additive_Action()
    {
        using var workspace = new AdhocWorkspace();
        var entity = CreateDocuments(workspace);
        var context = entity.Project.Documents.Single(document => document.Name == "QAContext.cs");
        var source = (await context.GetTextAsync()).ToString().Replace("public Microsoft.EntityFrameworkCore.DbSet",
            "public static Microsoft.EntityFrameworkCore.DbSet", StringComparison.Ordinal);
        workspace.TryApplyChanges(context.WithText(SourceText.From(source)).Project.Solution).Should().BeTrue();
        var current = workspace.CurrentSolution.GetDocument(entity.Id)!;
        (await current.Project.GetCompilationAsync())!.AssertNoCompilerErrors();
        (await Actions(current)).Should().BeEmpty();
    }

    [Theory]
    [DataInlineUnit(false, false, false)]
    [DataInlineUnit(false, true, false)]
    [DataInlineUnit(true, false, false)]
    [DataInlineUnit(true, true, false)]
    [DataInlineUnit(true, false, true)]
    [DataInlineUnit(true, true, true)]
    public async Task DbSet_Membership_Should_Require_Exact_Declared_Type_Or_Explicit_Inclusion(
        bool derivedSet, bool inherited, bool explicitInclusion)
    {
        using var workspace = new AdhocWorkspace();
        var entity = CreateDocuments(workspace);
        var context = entity.Project.Documents.Single(document => document.Name == "QAContext.cs");
        var setType = derivedSet ? "CustomSet" : "Microsoft.EntityFrameworkCore.DbSet";
        var member = $"public {setType}<Sample.Domain.QA.User> Users {{ get; set; }}";
        var inclusion = explicitInclusion
            ? "[DRN.Framework.EntityFramework.Attributes.SourceKnownPersistence(typeof(Sample.Domain.QA.User))]"
            : "";
        var source = $$"""
            namespace Sample.Infra.QA;
            public abstract class CustomSet<TEntity> : Microsoft.EntityFrameworkCore.DbSet<TEntity> where TEntity : class { }
            public abstract class BaseContext : DRN.Framework.EntityFramework.Context.DrnContext<QAContext>
            {
                protected BaseContext() : base(null) { }
                {{(inherited ? member : "")}}
            }
            {{inclusion}}
            public class QAContext : BaseContext
            {
                {{(inherited ? "" : member)}}
            }
            """;
        workspace.TryApplyChanges(context.WithText(SourceText.From(source)).Project.Solution).Should().BeTrue();
        var current = workspace.CurrentSolution.GetDocument(entity.Id)!;
        (await current.Project.GetCompilationAsync())!.AssertNoCompilerErrors();
        (await Actions(current)).Should().HaveCount(!derivedSet || explicitInclusion ? 1 : 0);
    }

    [Theory]
    [DataInline(false)]
    [DataInline(true)]
    public async Task Nonpublic_Entity_Or_Containing_Type_Should_Decline_Public_Configuration(bool nested)
    {
        using var workspace = new AdhocWorkspace();
        var entity = CreateDocuments(workspace);
        var source = nested
            ? "namespace Sample.Domain.QA; internal class Container { public class User : DRN.Framework.SharedKernel.Domain.SourceKnownEntity { public override DRN.Framework.SharedKernel.Domain.Dto ToDto() => new(this); } }"
            : EntitySource.Replace("public class User", "internal class User", StringComparison.Ordinal);
        var context = entity.Project.Documents.Single(document => document.Name == "QAContext.cs");
        var contextSource = (await context.GetTextAsync()).ToString()
            .Replace("public class QAContext", "internal class QAContext", StringComparison.Ordinal);
        if (nested) contextSource = contextSource.Replace("Sample.Domain.QA.User", "Sample.Domain.QA.Container.User", StringComparison.Ordinal);
        var solution = entity.Project.Solution.WithDocumentText(entity.Id, SourceText.From(source))
            .WithDocumentText(context.Id, SourceText.From(contextSource));
        workspace.TryApplyChanges(solution).Should().BeTrue();
        var current = workspace.CurrentSolution.GetDocument(entity.Id)!;
        (await current.Project.GetCompilationAsync())!.AssertNoCompilerErrors();
        (await Actions(current)).Should().BeEmpty();
    }

    [Fact]
    public async Task Existing_Case_Insensitive_Workspace_Path_Should_Not_Be_Overwritten()
    {
        using var workspace = new AdhocWorkspace();
        var entity = CreateDocuments(workspace);
        var collision = entity.Project.AddDocument("userconfig.cs", "// preserved content",
            filePath: "/source/Sample.Infra/QA/Configurations/userconfig.cs");
        workspace.TryApplyChanges(collision.Project.Solution).Should().BeTrue();
        (await Actions(workspace.CurrentSolution.GetDocument(entity.Id)!)).Should().BeEmpty();
    }

    [Theory]
    [DataInline(false)]
    [DataInline(true)]
    public async Task Non_Source_Workspace_Documents_Should_Not_Be_Overwritten(bool analyzerConfig)
    {
        using var workspace = new AdhocWorkspace();
        var entity = CreateDocuments(workspace);
        var id = DocumentId.CreateNewId(entity.Project.Id);
        var path = "/source/Sample.Infra/QA/Configurations/UserConfig.cs";
        var text = SourceText.From("# Existing user content");
        var changed = analyzerConfig
            ? entity.Project.Solution.AddAnalyzerConfigDocument(id, "UserConfig.cs", text, filePath: path)
            : entity.Project.Solution.AddAdditionalDocument(id, "UserConfig.cs", text, filePath: path);
        workspace.TryApplyChanges(changed).Should().BeTrue();
        (await Actions(workspace.CurrentSolution.GetDocument(entity.Id)!)).Should().BeEmpty();
        var preserved = analyzerConfig ? workspace.CurrentSolution.GetAnalyzerConfigDocument(id) : workspace.CurrentSolution.GetAdditionalDocument(id);
        (await preserved!.GetTextAsync()).ContentEquals(text).Should().BeTrue();
    }

    [Fact]
    public async Task Case_Insensitive_Symbol_Collision_Should_Block_Configuration_Even_Without_A_File_Path()
    {
        using var workspace = new AdhocWorkspace();
        var entity = CreateDocuments(workspace);
        var collision = entity.Project.AddDocument("InMemory.cs", "namespace Sample.Infra.QA.Configurations; public class userconfig;");
        workspace.TryApplyChanges(collision.Project.Solution).Should().BeTrue();
        (await Actions(workspace.CurrentSolution.GetDocument(entity.Id)!)).Should().BeEmpty();
    }

    [Theory]
    [DataInline("/source/Sample.Infra/QA/Configurations")]
    [DataInline("/source/Sample.Infra/QA/configurations")]
    [DataInline("/source/Sample.Infra/QA/Configurations/UserConfig.cs/notes.md")]
    public async Task Workspace_File_And_Directory_Conflicts_Should_Decline_Configuration(string path)
    {
        using var workspace = new AdhocWorkspace();
        var entity = CreateDocuments(workspace);
        var collision = entity.Project.AddAdditionalDocument("notes.md", "Existing user content", filePath: path);
        workspace.TryApplyChanges(collision.Project.Solution).Should().BeTrue();
        var before = workspace.CurrentSolution;
        (await Actions(before.GetDocument(entity.Id)!)).Should().BeEmpty();
        workspace.CurrentSolution.Should().BeSameAs(before);
        (await before.GetAdditionalDocument(collision.Id)!.GetTextAsync()).ToString().Should().Be("Existing user content");
    }

    [Fact]
    public async Task Existing_Directory_Casing_Should_Not_Be_Changed_By_A_New_File()
    {
        using var workspace = new AdhocWorkspace();
        var entity = CreateDocuments(workspace);
        var existing = entity.Project.AddDocument("Other.cs", "// preserved content",
            filePath: "/source/Sample.Infra/QA/configurations/Other.cs");
        workspace.TryApplyChanges(existing.Project.Solution).Should().BeTrue();
        (await Actions(workspace.CurrentSolution.GetDocument(entity.Id)!)).Should().BeEmpty();
    }

    [Fact]
    public async Task Invalid_Context_Host_Path_Should_Decline_Without_Throwing()
    {
        using var workspace = new AdhocWorkspace();
        var entity = CreateDocuments(workspace);
        var context = entity.Project.Documents.Single(document => document.Name == "QAContext.cs");
        var changed = context.WithFilePath("/source/Sample.Infra/QA/\0Context.cs");
        workspace.TryApplyChanges(changed.Project.Solution).Should().BeTrue();
        (await Actions(workspace.CurrentSolution.GetDocument(entity.Id)!)).Should().BeEmpty();
    }

    [Fact]
    public async Task Changed_Preimage_Should_Invalidate_Prepared_Configuration_Action()
    {
        using var workspace = new AdhocWorkspace();
        var entity = CreateDocuments(workspace);
        var action = (await Actions(entity)).Single();
        var changed = entity.WithText(SourceText.From(EntitySource + "\n// concurrent edit"));
        workspace.TryApplyChanges(changed.Project.Solution).Should().BeTrue();
        var result = await action.GetChangedSolutionAsync();
        result.GetProject(entity.Project.Id)!.Documents.Should().NotContain(document => document.Name == "UserConfig.cs");
        (await result.GetDocument(entity.Id)!.GetTextAsync()).ToString().Should().EndWith("// concurrent edit");
    }

    [Fact]
    public async Task Cancellation_Should_Leave_Workspace_Unchanged()
    {
        using var workspace = new AdhocWorkspace();
        var entity = CreateDocuments(workspace);
        var action = (await Actions(entity)).Single();
        var before = workspace.CurrentSolution;
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var apply = async () => await action.GetOperationsAsync(cancellation.Token);
        await apply.Should().ThrowAsync<OperationCanceledException>();
        workspace.CurrentSolution.Should().BeSameAs(before);
    }

    [Theory]
    [DataInline("protected override void OnModelCreating(Microsoft.EntityFrameworkCore.ModelBuilder builder) => base.OnModelCreating(builder);", true)]
    [DataInline("protected override void OnModelCreating(Microsoft.EntityFrameworkCore.ModelBuilder builder) { base.OnModelCreating(builder); }", true)]
    [DataInline("protected override void OnModelCreating(Microsoft.EntityFrameworkCore.ModelBuilder builder) { base.OnModelCreating(builder); builder.Ignore<Sample.Domain.QA.User>(); }", false)]
    [DataInline("protected override void OnModelCreating(Microsoft.EntityFrameworkCore.ModelBuilder builder) { Forward(builder); } private void Forward(Microsoft.EntityFrameworkCore.ModelBuilder builder) => base.OnModelCreating(builder);", false)]
    public async Task Discovery_Should_Accept_Only_Exact_Base_Forwarding(string members, bool supported)
    {
        using var workspace = new AdhocWorkspace();
        var entity = CreateDocuments(workspace, contextMembers: members);
        var actions = await Actions(entity);
        actions.Should().HaveCount(supported ? 1 : 0);
        if (!supported) return;
        var changed = await actions[0].GetChangedSolutionAsync();
        (await changed.GetDocument(entity.Id)!.GetTextAsync()).ToString().Should().Be(EntitySource);
        var context = changed.GetProject(entity.Project.Id)!.Documents.Single(document => document.Name == "QAContext.cs");
        (await context.GetTextAsync()).ToString().Should().Contain(members);
    }

    [Fact]
    public async Task Split_Role_Project_Should_Require_Reviewed_Destinations_Instead_Of_Default_Paths()
    {
        using var workspace = new AdhocWorkspace();
        var entity = CreateDocuments(workspace);
        var additional = entity.Project.AddAdditionalDocument("DrnConventionInputs.g.cs", """
            [assembly: DRN.Conventions.Version("1")]
            [assembly: DRN.Conventions.Role("Infra", NamespacePrefix = "Sample.Infra", LogicalPathPrefix = "Persistence")]
            """);
        workspace.TryApplyChanges(additional.Project.Solution).Should().BeTrue();
        (await Actions(workspace.CurrentSolution.GetDocument(entity.Id)!)).Should().BeEmpty();
    }

    [Theory]
    [DataInline("type.Namespace == \"Sample.Infra.QA.Configurations\"", true)]
    [DataInline("\"Sample.Infra.QA.Configurations\" == type.Namespace", true)]
    [DataInline("type.Namespace == \"Sample.Infra.Other.Configurations\"", false)]
    [DataInline("type.Namespace!.StartsWith(\"Sample.Infra\")", false)]
    public async Task Exact_Custom_Assembly_Scan_Should_Offer_Only_Proven_Destination(string predicate, bool supported)
    {
        using var workspace = new AdhocWorkspace();
        var members = "protected override void OnModelCreating(Microsoft.EntityFrameworkCore.ModelBuilder builder) => " +
            "builder.ApplyConfigurationsFromAssembly(typeof(QAContext).Assembly, type => " + predicate + ");";
        var entity = CreateDocuments(workspace, contextMembers: members);
        var actions = await Actions(entity);
        actions.Should().HaveCount(supported ? 1 : 0);
        if (!supported) return;
        var changed = await actions[0].GetChangedSolutionAsync();
        var project = changed.GetProject(entity.Project.Id)!;
        (await project.Documents.Single(document => document.Name == "QAContext.cs").GetTextAsync()).ToString().Should().Contain(members);
        (await project.GetCompilationAsync())!.AssertNoCompilerErrors();
    }

    [Theory]
    [DataInline("typeof(Microsoft.EntityFrameworkCore.DbContext).Assembly", "")]
    [DataInline("typeof(QAContext).Assembly", "builder.Ignore<Sample.Domain.QA.User>();")]
    public async Task External_Or_Model_Mutating_Custom_Scan_Should_Decline(string assembly, string suffix)
    {
        using var workspace = new AdhocWorkspace();
        var entity = CreateDocuments(workspace, contextMembers:
            "protected override void OnModelCreating(Microsoft.EntityFrameworkCore.ModelBuilder builder) { " +
            "builder.ApplyConfigurationsFromAssembly(" + assembly + ", type => type.Namespace == \"Sample.Infra.QA.Configurations\"); " + suffix + " }");
        (await Actions(entity)).Should().BeEmpty();
    }

    [Theory]
    [DataInline(true, true)]
    [DataInline(false, false)]
    public async Task Shared_Discovery_Should_Name_Every_Context_And_Require_Existing_Membership(bool member, bool supported)
    {
        using var workspace = new AdhocWorkspace();
        var entity = CreateDocuments(workspace, $$"""
            namespace Sample.Infra.QA;
            public class QAReadContext : DRN.Framework.EntityFramework.Context.DrnContext<QAReadContext>
            {
                public QAReadContext() : base(null) { }
                {{(member ? "public Microsoft.EntityFrameworkCore.DbSet<Sample.Domain.QA.User> Users { get; set; }" : "")}}
            }
            """);
        var actions = await Actions(entity);
        actions.Should().HaveCount(supported ? 2 : 0);
        foreach (var action in actions) action.Title.Should().Contain("Sample.Infra.QA.QAContext, Sample.Infra.QA.QAReadContext");
    }

    [Fact]
    public async Task Unknown_Other_Context_Discovery_Should_Not_Silently_Change_Its_Model()
    {
        using var workspace = new AdhocWorkspace();
        var entity = CreateDocuments(workspace, """
            namespace Sample.Infra.Other;
            public class OtherContext : DRN.Framework.EntityFramework.Context.DrnContext<OtherContext>
            {
                public OtherContext() : base(null) { }
                protected override void OnModelCreating(Microsoft.EntityFrameworkCore.ModelBuilder builder) => Configure(builder);
                private void Configure(Microsoft.EntityFrameworkCore.ModelBuilder builder) { }
            }
            """);
        (await Actions(entity)).Should().BeEmpty();
    }

    [Fact]
    public async Task Hidden_Model_Method_Should_Not_Override_The_Actual_Default_Scan()
    {
        using var workspace = new AdhocWorkspace();
        var entity = CreateDocuments(workspace, contextMembers:
            "public new void OnModelCreating(Microsoft.EntityFrameworkCore.ModelBuilder builder) => builder.Ignore<Sample.Domain.QA.User>();");
        (await Actions(entity)).Should().ContainSingle();
    }

    [Theory]
    [DataInline(false)]
    [DataInline(true)]
    public async Task Referencing_Context_Scanning_The_Target_Assembly_Should_Block_Unannounced_Sharing(bool externalScan)
    {
        using var workspace = new AdhocWorkspace();
        var entity = CreateDocuments(workspace);
        var dependent = workspace.AddProject("Sample.Read.Infra", LanguageNames.CSharp)
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .AddMetadataReferences(entity.Project.MetadataReferences)
            .AddProjectReference(new ProjectReference(entity.Project.Id));
        var document = dependent.AddDocument("ReadContext.cs", $$"""
            namespace Sample.Read.Infra.QA;
            public class ReadContext() : DRN.Framework.EntityFramework.Context.DrnContext<ReadContext>(null)
            {
                {{(externalScan ? "protected override void OnModelCreating(Microsoft.EntityFrameworkCore.ModelBuilder builder) => builder.ApplyConfigurationsFromAssembly(typeof(Sample.Infra.QA.QAContext).Assembly, type => type.Namespace == \"Sample.Infra.QA.Configurations\");" : "")}}
            }
            """, filePath: "/source/Sample.Read.Infra/QA/ReadContext.cs");
        workspace.TryApplyChanges(document.Project.Solution).Should().BeTrue();
        (await Actions(workspace.CurrentSolution.GetDocument(entity.Id)!)).Should().HaveCount(externalScan ? 0 : 1);
    }

    [Theory]
    [DataInline("namespace Sample.Infra.QA.Configurations.UserConfig { public class Detail; }")]
    [DataInline("namespace Sample.Infra.QA { public class Configurations; }")]
    [DataInline("namespace Sample.Infra.QA { public class configurations; }")]
    public async Task Leaf_Namespaces_And_Namespace_Segment_Types_Should_Block_Configuration(string collision)
    {
        using var workspace = new AdhocWorkspace();
        var entity = CreateDocuments(workspace, extraSource: collision);
        (await Actions(entity)).Should().BeEmpty();
    }

    [Theory]
    [DataInlineUnit("")]
    [DataInlineUnit("[assembly: DRN.Conventions.Version(\"2\")]")]
    [DataInlineUnit("[module: DRN.Conventions.Version(\"1\")]")]
    [DataInlineUnit("[assembly: DRN.Conventions.Version(\"1\")][assembly: DRN.Conventions.Version(\"1\")]")]
    [DataInlineUnit("[assembly: DRN.Conventions.Version(\"1\")] class Executable { }")]
    [DataInlineUnit("[assembly: DRN.Conventions.Version(\"1\")][assembly: DRN.Conventions.Path(\"/source/User.cs\", LogicalPath = \"../outside.cs\")]")]
    [DataInlineUnit("[assembly: DRN.Conventions.Version(\"1\")][assembly: DRN.Conventions.Reference(\"Inventory\", Stage = \"Custom\")]")]
    [DataInlineUnit("[assembly: DRN.Conventions.Version(\"1\")][assembly: DRN.Conventions.Ownership(\"Sample.Domain.QA.User\", Subdomain = \"QA\", Kind = \"Helper\")]")]
    [DataInlineUnit("[assembly: DRN.Conventions.Version(\"1\")][assembly: DRN.Conventions.Context(\"Sample.Infra.QA.QAContext\", Repositories = \"../outside\")]")]
    [DataInlineUnit("[assembly: DRN.Conventions.Version(\"1\")][assembly: DRN.Conventions.Exception(\"DRNARCHITECTURE004\", SymbolTarget = \"T:Sample.Domain.QA.User\")]")]
    public async Task Invalid_Present_Input_Should_Not_Authorize_Default_Configuration_Paths(string input)
    {
        using var workspace = new AdhocWorkspace();
        var entity = CreateDocuments(workspace);
        var changed = entity.Project.AddAdditionalDocument("DrnConventionInputs.g.cs", input).Project.Solution;
        workspace.TryApplyChanges(changed).Should().BeTrue();
        (await Actions(workspace.CurrentSolution.GetDocument(entity.Id)!)).Should().BeEmpty();
    }

    [Theory]
    [DataInline(false)]
    [DataInline(true)]
    public async Task Oversized_Input_Should_Not_Authorize_Default_Configuration_Paths(bool characters)
    {
        using var workspace = new AdhocWorkspace();
        var entity = CreateDocuments(workspace);
        var input = characters ? new string(' ', 1_048_577) : "[assembly: DRN.Conventions.Version(\"1\")]" +
            string.Concat(Enumerable.Repeat("[assembly: DRN.Conventions.Path(\"x\", LogicalPath = \"x\")]", 16_384));
        var changed = entity.Project.AddAdditionalDocument("DrnConventionInputs.g.cs", input).Project.Solution;
        workspace.TryApplyChanges(changed).Should().BeTrue();
        (await Actions(workspace.CurrentSolution.GetDocument(entity.Id)!)).Should().BeEmpty();
    }

    [Fact]
    public async Task Structurally_Valid_Exception_Should_Not_Disable_Independent_Configuration_Generation()
    {
        using var workspace = new AdhocWorkspace();
        var entity = CreateDocuments(workspace);
        var changed = entity.Project.AddAdditionalDocument("DrnConventionInputs.g.cs", """
            [assembly: DRN.Conventions.Version("1")]
            [assembly: DRN.Conventions.Exception("DRNARCHITECTURE004", SymbolTarget = "T:Sample.Domain.QA.User",
                Reason = "Shared legacy assembly placement", Owner = "Storage team", Review = "2099-01-01")]
            """).Project.Solution;
        workspace.TryApplyChanges(changed).Should().BeTrue();
        (await Actions(workspace.CurrentSolution.GetDocument(entity.Id)!)).Should().ContainSingle();
    }

    [Fact]
    public async Task Global_Namespace_Context_Should_Not_Offer_A_Configuration()
    {
        using var workspace = new AdhocWorkspace();
        var entity = CreateDocuments(workspace);
        var context = entity.Project.Documents.Single(document => document.Name == "QAContext.cs");
        var source = (await context.GetTextAsync()).ToString().Replace("namespace Sample.Infra.QA;", "", StringComparison.Ordinal);
        workspace.TryApplyChanges(context.WithText(SourceText.From(source)).Project.Solution).Should().BeTrue();
        entity = workspace.CurrentSolution.GetDocument(entity.Id)!;
        (await entity.Project.GetCompilationAsync())!.AssertNoCompilerErrors();
        (await Actions(entity)).Should().BeEmpty();
    }

    [Fact]
    public async Task EntitySuffixed_Aggregate_Should_Generate_Stemmed_Config_And_Detect_Verbatim_And_Relative_Input_Rules()
    {
        var collisionCompilation = ClassificationTestFixture.Compilation("""
            namespace Sample.@event { public class Order; }
            namespace Sample.QA { public class @event; }
            """);
        ConventionGenerationPreflight.SymbolCollision(collisionCompilation, "Sample.@event", "Order", CancellationToken.None).Should().BeTrue();
        ConventionGenerationPreflight.SymbolCollision(collisionCompilation, "Sample.QA", "@event", CancellationToken.None).Should().BeTrue();

        using var workspace = new AdhocWorkspace();
        var baseEntity = CreateDocuments(workspace);
        var relativeInput = baseEntity.Project.AddAdditionalDocument(
            "obj/Debug/net10.0/DrnConventionInputs.g.cs",
            "[assembly: DRN.Conventions.Version(\"2\")]");
        workspace.TryApplyChanges(relativeInput.Project.Solution).Should().BeTrue();
        (await Actions(workspace.CurrentSolution.GetDocument(baseEntity.Id)!)).Should().BeEmpty();

        using var entityWorkspace = new AdhocWorkspace();
        var domain = entityWorkspace.AddProject(ProjectInfo.Create(ProjectId.CreateNewId(), VersionStamp.Create(),
                "Sample.Domain", "Sample.Domain", LanguageNames.CSharp, filePath: "/source/Sample.Domain/Sample.Domain.csproj"))
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .AddMetadataReferences(ClassificationTestFixture.References);
        var entity = domain.AddDocument("OrderEntity.cs", """
            namespace Sample.Domain.QA.Order;
            public enum SampleKinds : byte { Order = 1 }
            public sealed class SampleTypeAttribute(SampleKinds kind)
                : DRN.Framework.SharedKernel.Domain.EntityTypeAttribute<SampleApp>((byte)kind);
            [SampleType(SampleKinds.Order)]
            public sealed class OrderEntity : DRN.Framework.SharedKernel.Domain.AggregateRoot { public override DRN.Framework.SharedKernel.Domain.Dto ToDto() => new(this); }
            public readonly struct SampleApp : DRN.Framework.SharedKernel.Domain.IAppId { public static byte AppId => 1; }
            """, filePath: "/source/Sample.Domain/QA/Order/OrderEntity.cs");
        entityWorkspace.TryApplyChanges(entity.Project.Solution).Should().BeTrue();

        var infra = entityWorkspace.AddProject(ProjectInfo.Create(ProjectId.CreateNewId(), VersionStamp.Create(),
                "Sample.Infra", "Sample.Infra", LanguageNames.CSharp, filePath: "/source/Sample.Infra/Sample.Infra.csproj"))
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .AddProjectReference(new ProjectReference(domain.Id))
            .AddMetadataReferences(ClassificationTestFixture.References)
            .AddMetadataReference(AnalyzerTestCompilation.EntityFrameworkReference)
            .AddMetadataReference(AnalyzerTestCompilation.UtilsReference);
        var context = infra.AddDocument("QAContext.cs", """
            namespace Sample.Infra.QA;
            public sealed class QAContextNpgsqlDbContextOptionsAttribute : DRN.Framework.EntityFramework.Attributes.NpgsqlDbContextOptionsAttribute;
            [QAContextNpgsqlDbContextOptions]
            public class QAContext : DRN.Framework.EntityFramework.Context.DrnContext<QAContext>
            {
                public QAContext() : base(null) { }
                public QAContext(Microsoft.EntityFrameworkCore.DbContextOptions<QAContext> options) : base(options) { }
                public Microsoft.EntityFrameworkCore.DbSet<Sample.Domain.QA.Order.OrderEntity> Orders { get; set; }
            }
            public static class InfraModule
            {
                public static Microsoft.Extensions.DependencyInjection.IServiceCollection AddInfra(
                    this Microsoft.Extensions.DependencyInjection.IServiceCollection services)
                {
                    DRN.Framework.Utils.DependencyInjection.ServiceCollectionExtensions.AddServicesWithAttributes(services);
                    return services;
                }
            }
            """, filePath: "/source/Sample.Infra/QA/QAContext.cs");
        entityWorkspace.TryApplyChanges(context.Project.Solution).Should().BeTrue();

        var actions = new List<CodeAction>();
        var currentEntity = entityWorkspace.CurrentSolution.GetDocument(entity.Id)!;
        var text = (await currentEntity.GetTextAsync()).ToString();
        await new EntityConfigurationRefactoringProvider().ComputeRefactoringsAsync(new CodeRefactoringContext(currentEntity,
            new TextSpan(text.IndexOf("OrderEntity :", StringComparison.Ordinal), 0), actions.Add, CancellationToken.None));
        var action = actions.Should().ContainSingle().Which;
        action.Title.Should().Contain("QA/Configurations/OrderConfig.cs");
        var changed = await action.GetChangedSolutionAsync();
        var added = changed.Projects.Single(project => project.Name == "Sample.Infra").Documents
            .Single(document => document.Name == "OrderConfig.cs");
        (await added.GetTextAsync()).ToString().Should().Contain("public sealed class OrderConfig : global::Microsoft.EntityFrameworkCore.IEntityTypeConfiguration<global::Sample.Domain.QA.Order.OrderEntity>");
        (await added.Project.GetCompilationAsync())!.AssertNoCompilerErrors();
    }

    [Fact]
    public async Task Alias_Only_Domain_Reference_Cross_Owner_Context_And_Errored_Inclusion_Should_Decline()
    {
        using var aliasWorkspace = new AdhocWorkspace();
        var domain = aliasWorkspace.AddProject(ProjectInfo.Create(ProjectId.CreateNewId(), VersionStamp.Create(),
                "Sample.Domain", "Sample.Domain", LanguageNames.CSharp, filePath: "/source/Sample.Domain/Sample.Domain.csproj"))
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .AddMetadataReferences(ClassificationTestFixture.References);
        var entityDoc = domain.AddDocument("OrderEntity.cs", """
            namespace Sample.Domain.QA.Order;
            public sealed class OrderEntity : DRN.Framework.SharedKernel.Domain.AggregateRoot { public override DRN.Framework.SharedKernel.Domain.Dto ToDto() => new(this); }
            """, filePath: "/source/Sample.Domain/QA/Order/OrderEntity.cs");
        aliasWorkspace.TryApplyChanges(entityDoc.Project.Solution).Should().BeTrue();
        var infra = aliasWorkspace.AddProject(ProjectInfo.Create(ProjectId.CreateNewId(), VersionStamp.Create(),
                "Sample.Infra", "Sample.Infra", LanguageNames.CSharp, filePath: "/source/Sample.Infra/Sample.Infra.csproj"))
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .AddProjectReference(new ProjectReference(domain.Id, ["DomainAlias"]))
            .AddMetadataReferences(ClassificationTestFixture.References)
            .AddMetadataReference(AnalyzerTestCompilation.EntityFrameworkReference)
            .AddMetadataReference(AnalyzerTestCompilation.UtilsReference);
        var aliasedContext = infra.AddDocument("QAContext.cs", """
            extern alias DomainAlias;
            namespace Sample.Infra.QA;
            public class QAContext : DRN.Framework.EntityFramework.Context.DrnContext<QAContext>
            {
                public QAContext() : base(null) { }
                public Microsoft.EntityFrameworkCore.DbSet<DomainAlias::Sample.Domain.QA.Order.OrderEntity> Orders { get; set; }
            }
            """, filePath: "/source/Sample.Infra/QA/QAContext.cs");
        aliasWorkspace.TryApplyChanges(aliasedContext.Project.Solution).Should().BeTrue();
        var currentEntity = aliasWorkspace.CurrentSolution.GetDocument(entityDoc.Id)!;
        var entityText = (await currentEntity.GetTextAsync()).ToString();
        var aliasActions = new List<CodeAction>();
        await new EntityConfigurationRefactoringProvider().ComputeRefactoringsAsync(new CodeRefactoringContext(currentEntity,
            new TextSpan(entityText.IndexOf("OrderEntity :", StringComparison.Ordinal), 0), aliasActions.Add, CancellationToken.None));
        aliasActions.Should().BeEmpty();

        using var crossOwnerWorkspace = new AdhocWorkspace();
        var crossEntity = CreateDocuments(crossOwnerWorkspace);
        var contextDoc = crossEntity.Project.Documents.Single(document => document.Name == "QAContext.cs");
        var crossSource = (await contextDoc.GetTextAsync()).ToString().Replace("namespace Sample.Infra.QA;",
            "namespace Sample.Infra.Legacy;\n[DRN.Framework.SharedKernel.Attributes.SourceKnownSubdomain(\"QA\", Reason = \"Legacy context namespace\")]",
            StringComparison.Ordinal);
        crossOwnerWorkspace.TryApplyChanges(contextDoc.WithText(SourceText.From(crossSource)).Project.Solution).Should().BeTrue();
        (await Actions(crossOwnerWorkspace.CurrentSolution.GetDocument(crossEntity.Id)!)).Should().BeEmpty();

        using var erroredWorkspace = new AdhocWorkspace();
        var erroredEntity = CreateDocuments(erroredWorkspace);
        var erroredContextDoc = erroredEntity.Project.Documents.Single(document => document.Name == "QAContext.cs");
        var erroredContextSource = """
            namespace Sample.Infra.QA;
            [DRN.Framework.EntityFramework.Attributes.SourceKnownPersistence(typeof(Sample.Domain.QA.User), Inclusion = MissingInclusionValue)]
            public class QAContext : DRN.Framework.EntityFramework.Context.DrnContext<QAContext>
            {
                public QAContext() : base(null) { }
            }
            """;
        erroredWorkspace.TryApplyChanges(erroredContextDoc.WithText(SourceText.From(erroredContextSource)).Project.Solution).Should().BeTrue();
        var erroredCurrent = erroredWorkspace.CurrentSolution.GetDocument(erroredEntity.Id)!;
        var erroredCompilation = (await erroredCurrent.Project.GetCompilationAsync())!;
        EntityConfigurationRefactoringProvider.HasMembership(
            erroredCompilation.GetTypeByMetadataName("Sample.Infra.QA.QAContext")!,
            erroredCompilation.GetTypeByMetadataName("Sample.Domain.QA.User")!,
            erroredCompilation).Should().BeFalse();
        (await Actions(erroredCurrent)).Should().BeEmpty();
    }

    [Theory]
    [DataInline(false, "Domain = \"Sample\"", true)]
    [DataInline(false, "Domain = \"Other\", Reason = \"Other domain\"", false)]
    [DataInline(true, "", true)]
    [DataInline(true, "Domain = \"Billing\", Reason = \"Other domain\"", false)]
    public async Task Domain_Subdomain_Attribute_Parity_Should_Support_Project_And_Folders_Layouts(
        bool foldersLayout, string extraArgs, bool offered)
    {
        using var workspace = new AdhocWorkspace();
        var nsSegment = foldersLayout ? "QA.Common" : "QA";
        var pathSegment = foldersLayout ? "QA/Common" : "QA";
        var subdomain = foldersLayout ? "Common" : "QA";
        var entityAttr = foldersLayout ? "\"Common\"" : "\"QA\", Domain = \"Sample\"";
        var contextAttr = extraArgs.Length == 0 ? $"\"{subdomain}\"" : $"\"{subdomain}\", {extraArgs}";

        var domain = workspace.AddProject(ProjectInfo.Create(ProjectId.CreateNewId(), VersionStamp.Create(),
                "Sample.Domain", "Sample.Domain", LanguageNames.CSharp, filePath: "/source/Sample.Domain/Sample.Domain.csproj"))
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .AddMetadataReferences(ClassificationTestFixture.References);
        var entityDoc = domain.AddDocument("User.cs", $$"""
            namespace Sample.Domain.{{nsSegment}};
            [DRN.Framework.SharedKernel.Attributes.SourceKnownSubdomain({{entityAttr}})]
            public class User : DRN.Framework.SharedKernel.Domain.SourceKnownEntity { public override DRN.Framework.SharedKernel.Domain.Dto ToDto() => new(this); }
            """, filePath: "/source/Sample.Domain/" + pathSegment + "/User.cs");
        workspace.TryApplyChanges(entityDoc.Project.Solution).Should().BeTrue();

        var contextName = foldersLayout ? "CommonContext" : "QAContext";
        var infra = workspace.AddProject(ProjectInfo.Create(ProjectId.CreateNewId(), VersionStamp.Create(),
                "Sample.Infra", "Sample.Infra", LanguageNames.CSharp, filePath: "/source/Sample.Infra/Sample.Infra.csproj"))
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .AddProjectReference(new ProjectReference(domain.Id))
            .AddMetadataReferences(ClassificationTestFixture.References)
            .AddMetadataReference(AnalyzerTestCompilation.EntityFrameworkReference)
            .AddMetadataReference(AnalyzerTestCompilation.UtilsReference)
            .AddDocument(contextName + ".cs", $$"""
                namespace Sample.Infra.{{nsSegment}};
                [DRN.Framework.SharedKernel.Attributes.SourceKnownSubdomain({{contextAttr}})]
                public class {{contextName}} : DRN.Framework.EntityFramework.Context.DrnContext<{{contextName}}>
                {
                    public {{contextName}}() : base(null) { }
                    public Microsoft.EntityFrameworkCore.DbSet<Sample.Domain.{{nsSegment}}.User> Users { get; set; }
                }
                """, filePath: "/source/Sample.Infra/" + pathSegment + "/" + contextName + ".cs").Project;
        var solution = foldersLayout ? infra.Solution.WithFoldersLayout() : infra.Solution;
        workspace.TryApplyChanges(solution).Should().BeTrue();
        var current = workspace.CurrentSolution.GetDocument(entityDoc.Id)!;
        var actions = await Actions(current);
        if (offered) actions.Should().ContainSingle();
        else actions.Should().BeEmpty();
    }

    [Theory]
    [DataInline("Domain = \"Sample\", Subdomain = \"QA\"", true)]
    [DataInline("Domain = \"Other\", Subdomain = \"QA\", Reason = \"Other domain\"", false)]
    [DataInline("Domain = \"Sample\"", false)]
    public async Task Context_Input_Domain_Should_Be_Validated_For_Configuration_Offer(string fields, bool offered)
    {
        using var workspace = new AdhocWorkspace();
        var entity = CreateDocuments(workspace);
        var input = entity.Project.AddAdditionalDocument("DrnConventionInputs.g.cs", $$"""
            [assembly: DRN.Conventions.Version("1")]
            [assembly: DRN.Conventions.Context("Sample.Infra.QA.QAContext", {{fields}})]
            """);
        workspace.TryApplyChanges(input.Project.Solution).Should().BeTrue();
        var actions = await Actions(workspace.CurrentSolution.GetDocument(entity.Id)!);
        if (offered) actions.Should().ContainSingle();
        else actions.Should().BeEmpty();
    }

    [Fact]
    public async Task Metadata_Only_Entity_Assembly_Should_Use_RootNamespace_Metadata_Without_Domain_Segment()
    {
        var metadataDomain = ClassificationTestFixture.Compilation("""
            [assembly: System.Reflection.AssemblyMetadata("DRN.Conventions.ProjectName", "Sample.Domain")]
            [assembly: System.Reflection.AssemblyMetadata("DRN.Conventions.RootNamespace", "CustomRoot")]
            namespace CustomRoot.QA;
            public class User : DRN.Framework.SharedKernel.Domain.SourceKnownEntity { public override DRN.Framework.SharedKernel.Domain.Dto ToDto() => new(this); }
            """).WithAssemblyName("Sample.Domain");
        metadataDomain.AssertNoCompilerErrors();
        var metadataRef = AnalyzerTestCompilation.EmitReference(metadataDomain, "/source/Sample.Domain/Sample.Domain.dll");

        using var workspace = new AdhocWorkspace();
        var infra = workspace.AddProject(ProjectInfo.Create(ProjectId.CreateNewId(), VersionStamp.Create(),
                "Sample.Infra", "Sample.Infra", LanguageNames.CSharp, filePath: "/source/Sample.Infra/Sample.Infra.csproj"))
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .AddMetadataReferences(ClassificationTestFixture.References)
            .AddMetadataReference(AnalyzerTestCompilation.EntityFrameworkReference)
            .AddMetadataReference(AnalyzerTestCompilation.UtilsReference)
            .AddMetadataReference(metadataRef);
        const string contextSource = """
            namespace Sample.Infra.QA;
            public class QAContext : DRN.Framework.EntityFramework.Context.DrnContext<QAContext>
            {
                public QAContext() : base(null) { }
                public Microsoft.EntityFrameworkCore.DbSet<CustomRoot.QA.User> Users { get; set; }
            }
            """;
        var contextDoc = infra.AddDocument("QAContext.cs", contextSource, filePath: "/source/Sample.Infra/QA/QAContext.cs");
        workspace.TryApplyChanges(contextDoc.Project.Solution).Should().BeTrue();

        var current = workspace.CurrentSolution.GetDocument(contextDoc.Id)!;
        (await current.Project.GetCompilationAsync())!.AssertNoCompilerErrors();
        var actions = new List<CodeAction>();
        await new EntityConfigurationRefactoringProvider().ComputeRefactoringsAsync(new CodeRefactoringContext(current,
            new TextSpan(contextSource.IndexOf("CustomRoot.QA.User", StringComparison.Ordinal) + "CustomRoot.QA.".Length, 0),
            actions.Add, CancellationToken.None));
        var action = actions.Should().ContainSingle().Which;
        action.Title.Should().Contain("Sample.Infra.QA.QAContext").And.Contain("QA/Configurations/UserConfig.cs");
        var changed = await action.GetChangedSolutionAsync();
        (await changed.GetProject(current.Project.Id)!.GetCompilationAsync())!.AssertNoCompilerErrors();
    }

    private static Document CreateDocuments(AdhocWorkspace workspace, string? extraSource = null, string contextMembers = "")
    {
        var project = workspace.AddProject(ProjectInfo.Create(ProjectId.CreateNewId(), VersionStamp.Create(),
                "Sample.Infra", "Sample.Infra", LanguageNames.CSharp, filePath: "/source/Sample.Infra/Sample.Infra.csproj"))
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .AddMetadataReferences(ClassificationTestFixture.References)
            .AddMetadataReference(AnalyzerTestCompilation.EntityFrameworkReference)
            .AddMetadataReference(AnalyzerTestCompilation.UtilsReference);
        var entity = project.AddDocument("User.cs", EntitySource, filePath: "/source/Sample.Infra/QA/User.cs");
        var context = entity.Project.AddDocument("QAContext.cs", $$"""
            namespace Sample.Infra.QA;
            public class QAContext : DRN.Framework.EntityFramework.Context.DrnContext<QAContext>
            {
                public QAContext() : base(null) { }
                public Microsoft.EntityFrameworkCore.DbSet<Sample.Domain.QA.User> Users { get; set; }
                {{contextMembers}}
            }
            """, filePath: "/source/Sample.Infra/QA/QAContext.cs");
        var solution = context.Project.Solution;
        if (extraSource is not null)
            solution = context.Project.AddDocument("Existing.cs", extraSource, filePath: "/source/Sample.Infra/Other/Existing.cs").Project.Solution;
        workspace.TryApplyChanges(solution).Should().BeTrue();
        return workspace.CurrentSolution.GetDocument(entity.Id)!;
    }

    private static async Task<List<CodeAction>> Actions(Document entity)
    {
        var actions = new List<CodeAction>();
        var text = (await entity.GetTextAsync()).ToString();
        await new EntityConfigurationRefactoringProvider().ComputeRefactoringsAsync(new CodeRefactoringContext(entity,
            new TextSpan(text.IndexOf("User :", StringComparison.Ordinal), 0), actions.Add, CancellationToken.None));
        return actions;
    }
}
