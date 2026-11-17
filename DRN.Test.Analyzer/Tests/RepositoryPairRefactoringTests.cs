using DRN.Framework.SharedKernel.CodeFixes;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.Text;

namespace DRN.Test.Analyzer.Tests;

public sealed class RepositoryPairRefactoringTests
{
    private static readonly MetadataReference FrameworkReference = MetadataReference.CreateFromFile(
        typeof(DRN.Framework.EntityFramework.Context.DrnContext<>).Assembly.Location);
    private static readonly MetadataReference UtilsReference = MetadataReference.CreateFromFile(
        typeof(DRN.Framework.Utils.Settings.IAppSettings).Assembly.Location);

    private const string EntitySource = """
        namespace Sample.Domain.QA.Orders;
        [System.ComponentModel.DataAnnotations.Schema.Table("orders")]
        public class Order : DRN.Framework.SharedKernel.Domain.AggregateRoot;
        """;

    [Fact]
    public async Task Selected_Persistence_Bundle_Should_Add_Three_Files_In_One_Solution_And_Be_Idempotent()
    {
        using var workspace = new AdhocWorkspace();
        var entity = CreateDocuments(workspace);
        (await Actions(entity)).Should().ContainSingle();
        var action = (await Actions(entity, bundles: true)).Should().ContainSingle().Which;
        action.Title.Should().StartWith("Add scoped repository pair and entity configuration:")
            .And.Contain("Sample.Infra.QA.QAContext").And.Contain("QA/Configurations/OrderConfig.cs");
        var changed = await ChangedSolution(action);
        var changes = changed.GetChanges(workspace.CurrentSolution).GetProjectChanges().ToArray();
        changes.Should().HaveCount(2);
        changes.Sum(change => change.GetAddedDocuments().Count()).Should().Be(3);
        changes.Should().OnlyContain(change => !change.GetChangedDocuments().Any() && !change.GetRemovedDocuments().Any() &&
            !change.GetAddedProjectReferences().Any() && !change.GetChangedAdditionalDocuments().Any());
        var domain = changed.Projects.Single(project => project.Name == "Sample.Domain");
        var infra = changed.Projects.Single(project => project.Name == "Sample.Infra");
        domain.Documents.Should().ContainSingle(document => document.Name == "IOrderRepository.cs");
        infra.Documents.Should().ContainSingle(document => document.Name == "OrderRepository.cs");
        var configuration = infra.Documents.Single(document => document.Name == "OrderConfig.cs");
        configuration.FilePath.Should().Be("/source/Sample.Infra/QA/Configurations/OrderConfig.cs");
        (await configuration.GetTextAsync()).ToString().Should().Contain("IEntityTypeConfiguration<global::Sample.Domain.QA.Orders.Order>")
            .And.NotContain("HasKey").And.NotContain("ToTable");
        (await changed.GetDocument(entity.Id)!.GetTextAsync()).ToString().Should().Be(EntitySource);
        (await domain.GetCompilationAsync())!.GetDiagnostics().Should().NotContain(item => item.Severity == DiagnosticSeverity.Error);
        (await infra.GetCompilationAsync())!.GetDiagnostics().Should().NotContain(item => item.Severity == DiagnosticSeverity.Error);
        workspace.TryApplyChanges(changed).Should().BeTrue();
        (await Actions(workspace.CurrentSolution.GetDocument(entity.Id)!, bundles: true)).Should().BeEmpty();
        (await Actions(workspace.CurrentSolution.GetDocument(entity.Id)!)).Should().BeEmpty();
    }

    [Fact]
    public async Task Configuration_Collision_Should_Decline_The_Bundle_While_Keeping_The_Standalone_Pair()
    {
        using var workspace = new AdhocWorkspace();
        var entity = CreateDocuments(workspace);
        var infra = workspace.CurrentSolution.Projects.Single(project => project.Name == "Sample.Infra");
        var collision = infra.AddAdditionalDocument("orderconfig.cs", "// retained", filePath:
            "/source/Sample.Infra/QA/Configurations/orderconfig.cs");
        workspace.TryApplyChanges(collision.Project.Solution).Should().BeTrue();
        var currentEntity = workspace.CurrentSolution.GetDocument(entity.Id)!;
        (await Actions(currentEntity, bundles: true)).Should().BeEmpty();
        (await Actions(currentEntity)).Should().ContainSingle();
        (await workspace.CurrentSolution.GetAdditionalDocument(collision.Id)!.GetTextAsync()).ToString().Should().Be("// retained");
    }

    [Fact]
    public async Task Existing_Mapping_Should_Not_Be_Hidden_By_An_Empty_Bundled_Configuration()
    {
        using var workspace = new AdhocWorkspace();
        var entity = CreateDocuments(workspace);
        var infra = workspace.CurrentSolution.Projects.Single(project => project.Name == "Sample.Infra");
        var mapping = infra.AddDocument("Existing.cs", """
            namespace Sample.Infra.Other;
            public class Existing : Microsoft.EntityFrameworkCore.IEntityTypeConfiguration<Sample.Domain.QA.Orders.Order>
            {
                public void Configure(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<Sample.Domain.QA.Orders.Order> builder) { }
            }
            """, filePath: "/source/Sample.Infra/Other/Existing.cs");
        workspace.TryApplyChanges(mapping.Project.Solution).Should().BeTrue();
        var current = workspace.CurrentSolution.GetDocument(entity.Id)!;
        (await Actions(current, bundles: true)).Should().BeEmpty();
        (await Actions(current)).Should().ContainSingle();
    }

    [Fact]
    public async Task Stale_Bundle_Should_Not_Create_Any_Of_Its_Three_Files()
    {
        using var workspace = new AdhocWorkspace();
        var entity = CreateDocuments(workspace);
        var action = (await Actions(entity, bundles: true)).Single();
        var concurrent = entity.WithText(SourceText.From(EntitySource + "\n// concurrent edit"));
        workspace.TryApplyChanges(concurrent.Project.Solution).Should().BeTrue();
        var changed = await ChangedSolution(action);
        changed.Projects.SelectMany(project => project.Documents).Should().NotContain(document =>
            document.Name == "IOrderRepository.cs" || document.Name == "OrderRepository.cs" || document.Name == "OrderConfig.cs");
        (await changed.GetDocument(entity.Id)!.GetTextAsync()).ToString().Should().EndWith("// concurrent edit");
    }

    [Fact]
    public async Task Canceled_Bundle_Should_Not_Apply_A_Partial_Transaction()
    {
        using var workspace = new AdhocWorkspace();
        var entity = CreateDocuments(workspace);
        var action = (await Actions(entity, bundles: true)).Single();
        var snapshot = workspace.CurrentSolution;
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var apply = async () => await action.GetOperationsAsync(cancellation.Token);
        await apply.Should().ThrowAsync<OperationCanceledException>();
        workspace.CurrentSolution.Should().BeSameAs(snapshot);
    }

    [Fact]
    public async Task Cqrs_Bundles_Should_Keep_The_Selected_Context_And_Configuration_Destination_Together()
    {
        using var workspace = new AdhocWorkspace();
        var entity = CreateDocuments(workspace, cqrs: true);
        var actions = await Actions(entity, bundles: true);
        actions.Should().HaveCount(2);
        var changed = await ChangedSolution(actions.Single(action => action.Title.Contains("ReadContext", StringComparison.Ordinal)));
        var infra = changed.Projects.Single(project => project.Name == "Sample.Infra");
        infra.Documents.Single(document => document.Name == "OrderRepository.cs").FilePath.Should()
            .Be("/source/Sample.Infra/QA/Read/Repositories/OrderRepository.cs");
        infra.Documents.Single(document => document.Name == "OrderConfig.cs").FilePath.Should()
            .Be("/source/Sample.Infra/QA/Read/Configurations/OrderConfig.cs");
        (await infra.GetCompilationAsync())!.GetDiagnostics().Should().NotContain(item => item.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public async Task Shared_Model_Configuration_Should_Not_Be_Bundled_As_A_Single_Context_Choice()
    {
        using var workspace = new AdhocWorkspace();
        var entity = CreateDocuments(workspace);
        var infra = workspace.CurrentSolution.Projects.Single(project => project.Name == "Sample.Infra");
        var document = infra.AddDocument("QAReadContext.cs", """
            namespace Sample.Infra.QA;
            public class QAReadContext() : DRN.Framework.EntityFramework.Context.DrnContext<QAReadContext>(null)
            {
                public Microsoft.EntityFrameworkCore.DbSet<Sample.Domain.QA.Orders.Order> Orders { get; set; }
            }
            """, filePath: "/source/Sample.Infra/QA/QAReadContext.cs");
        workspace.TryApplyChanges(document.Project.Solution).Should().BeTrue();
        var current = workspace.CurrentSolution.GetDocument(entity.Id)!;
        (await Actions(current, bundles: true)).Should().BeEmpty();
        (await Actions(current)).Should().ContainSingle();
    }

    [Fact]
    public async Task Selected_Repository_Pair_Should_Be_One_Compiling_Solution_Change_And_Idempotent()
    {
        using var workspace = new AdhocWorkspace();
        var entity = CreateDocuments(workspace);
        var action = (await Actions(entity)).Should().ContainSingle().Which;
        action.Title.Should().Contain("IOrderRepository + OrderRepository").And.Contain("Sample.Infra.QA.QAContext");
        var changed = await ChangedSolution(action);
        var changes = changed.GetChanges(workspace.CurrentSolution).GetProjectChanges().ToArray();
        changes.Should().HaveCount(2);
        changes.Should().OnlyContain(change => change.GetAddedDocuments().Count() == 1 && !change.GetChangedDocuments().Any() &&
            !change.GetAddedProjectReferences().Any() && !change.GetRemovedDocuments().Any());
        var contract = changed.Projects.SelectMany(project => project.Documents).Single(document => document.Name == "IOrderRepository.cs");
        var implementation = changed.Projects.SelectMany(project => project.Documents).Single(document => document.Name == "OrderRepository.cs");
        contract.FilePath.Should().Be("/source/Sample.Domain/QA/Orders/IOrderRepository.cs");
        implementation.FilePath.Should().Be("/source/Sample.Infra/QA/Repositories/OrderRepository.cs");
        (await changed.GetDocument(entity.Id)!.GetTextAsync()).ToString().Should().Be(EntitySource);
        var source = (await implementation.GetTextAsync()).ToString();
        source.Should().Contain("Scoped<global::Sample.Domain.QA.Orders.IOrderRepository>")
            .And.Contain("SourceKnownRepository<global::Sample.Infra.QA.QAContext, global::Sample.Domain.QA.Orders.Order>")
            .And.Contain(": base(context, utils)").And.NotContain("NotImplementedException").And.NotContain("Settings");
        (await contract.Project.GetCompilationAsync())!.GetDiagnostics().Should().NotContain(item => item.Severity == DiagnosticSeverity.Error);
        (await implementation.Project.GetCompilationAsync())!.GetDiagnostics().Should().NotContain(item => item.Severity == DiagnosticSeverity.Error);
        workspace.TryApplyChanges(changed).Should().BeTrue();
        (await Actions(workspace.CurrentSolution.GetDocument(entity.Id)!)).Should().BeEmpty();
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public async Task Missing_Membership_Should_Not_Offer_Partial_Scaffolding(bool membership, bool destination)
    {
        using var workspace = new AdhocWorkspace();
        var entity = CreateDocuments(workspace, membership: membership, destination: destination);
        (await Actions(entity)).Should().BeEmpty();
        workspace.CurrentSolution.Projects.SelectMany(project => project.Documents).Should()
            .NotContain(document => document.Name == "IOrderRepository.cs" || document.Name == "OrderRepository.cs");
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, true)]
    public async Task DbSet_Membership_Should_Require_Exact_Declared_Type_Or_Explicit_Inclusion(
        bool derivedSet, bool inherited, bool explicitInclusion)
    {
        using var workspace = new AdhocWorkspace();
        var entity = CreateDocuments(workspace);
        var infra = workspace.CurrentSolution.Projects.Single(project => project.Name == "Sample.Infra");
        var context = infra.Documents.Single(document => document.Name == "QAContext.cs");
        var setType = derivedSet ? "CustomSet" : "Microsoft.EntityFrameworkCore.DbSet";
        var member = $"public {setType}<Sample.Domain.QA.Orders.Order> Orders {{ get; set; }}";
        var inclusion = explicitInclusion
            ? "[DRN.Framework.EntityFramework.Attributes.SourceKnownPersistence(typeof(Sample.Domain.QA.Orders.Order))]"
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
        var currentInfra = workspace.CurrentSolution.GetProject(infra.Id)!;
        (await currentInfra.GetCompilationAsync())!.GetDiagnostics().Should()
            .NotContain(item => item.Severity == DiagnosticSeverity.Error);
        var expectedCount = !derivedSet || explicitInclusion ? 1 : 0;
        (await Actions(current)).Should().HaveCount(expectedCount);
        (await Actions(current, bundles: true)).Should().HaveCount(expectedCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Conventional_Repository_Destination_Should_Not_Require_Explicit_Metadata(bool contextRecord)
    {
        using var workspace = new AdhocWorkspace();
        var entity = CreateDocuments(workspace, destination: false);
        if (contextRecord)
        {
            var infra = workspace.CurrentSolution.Projects.Single(project => project.Name == "Sample.Infra");
            var input = infra.AddAdditionalDocument("DrnConventionInputs.g.cs", """
                [assembly: DRN.Conventions.Version("1")]
                [assembly: DRN.Conventions.Context("Sample.Infra.QA.QAContext", Migrations = "QA/Migrations")]
                """);
            workspace.TryApplyChanges(input.Project.Solution).Should().BeTrue();
            entity = workspace.CurrentSolution.GetDocument(entity.Id)!;
        }
        var action = (await Actions(entity)).Should().ContainSingle().Which;
        var changed = await ChangedSolution(action);
        var implementation = changed.Projects.Single(project => project.Name == "Sample.Infra")
            .Documents.Single(document => document.Name == "OrderRepository.cs");
        implementation.FilePath.Should().Be("/source/Sample.Infra/QA/Repositories/OrderRepository.cs");
        foreach (var project in changed.Projects)
            (await project.GetCompilationAsync())!.GetDiagnostics().Should().NotContain(item => item.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public async Task Nonconventional_Context_Home_Should_Not_Guess_A_Repository_Destination()
    {
        using var workspace = new AdhocWorkspace();
        var entity = CreateDocuments(workspace, destination: false);
        var infra = workspace.CurrentSolution.Projects.Single(project => project.Name == "Sample.Infra");
        var context = infra.Documents.Single(document => document.Name == "QAContext.cs");
        workspace.TryApplyChanges(context.WithFilePath("/source/Sample.Infra/Legacy/QAContext.cs").Project.Solution).Should().BeTrue();
        (await Actions(workspace.CurrentSolution.GetDocument(entity.Id)!)).Should().BeEmpty();
    }

    [Fact]
    public async Task Collision_In_One_Project_Should_Block_The_Whole_Pair()
    {
        using var workspace = new AdhocWorkspace();
        var entity = CreateDocuments(workspace);
        var infra = workspace.CurrentSolution.Projects.Single(project => project.Name == "Sample.Infra");
        var collision = infra.AddDocument("orderrepository.cs", "// existing content", filePath: "/source/Sample.Infra/QA/Repositories/orderrepository.cs");
        workspace.TryApplyChanges(collision.Project.Solution).Should().BeTrue();
        (await Actions(workspace.CurrentSolution.GetDocument(entity.Id)!)).Should().BeEmpty();
        workspace.CurrentSolution.Projects.SelectMany(project => project.Documents).Should().NotContain(document => document.Name == "IOrderRepository.cs");
    }

    [Fact]
    public async Task Stale_Preview_Should_Preserve_Concurrent_Content_Without_Creating_Either_File()
    {
        using var workspace = new AdhocWorkspace();
        var entity = CreateDocuments(workspace);
        var action = (await Actions(entity)).Single();
        var changedEntity = entity.WithText(SourceText.From(EntitySource + "\n// concurrent edit"));
        workspace.TryApplyChanges(changedEntity.Project.Solution).Should().BeTrue();
        var changed = await ChangedSolution(action);
        changed.Projects.SelectMany(project => project.Documents).Should().NotContain(document =>
            document.Name == "IOrderRepository.cs" || document.Name == "OrderRepository.cs");
        (await changed.GetDocument(entity.Id)!.GetTextAsync()).ToString().Should().EndWith("// concurrent edit");
    }

    [Fact]
    public async Task Cancellation_Should_Not_Apply_Either_Project_Edit()
    {
        using var workspace = new AdhocWorkspace();
        var entity = CreateDocuments(workspace);
        var action = (await Actions(entity)).Single();
        var snapshot = workspace.CurrentSolution;
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var apply = async () => await action.GetOperationsAsync(cancellation.Token);
        await apply.Should().ThrowAsync<OperationCanceledException>();
        workspace.CurrentSolution.Should().BeSameAs(snapshot);
    }

    [Fact]
    public async Task Cqrs_Contexts_Should_Be_Explicit_Choices_With_Separate_Destinations()
    {
        using var workspace = new AdhocWorkspace();
        var entity = CreateDocuments(workspace, cqrs: true);
        var actions = await Actions(entity);
        actions.Should().HaveCount(2);
        actions.Should().Contain(action => action.Title.Contains("Sample.Infra.QA.Read.ReadContext", StringComparison.Ordinal));
        actions.Should().Contain(action => action.Title.Contains("Sample.Infra.QA.Write.WriteContext", StringComparison.Ordinal));
        var changed = await ChangedSolution(actions.Single(action => action.Title.Contains("ReadContext", StringComparison.Ordinal)));
        changed.Projects.SelectMany(project => project.Documents).Single(document => document.Name == "OrderRepository.cs")
            .FilePath.Should().Be("/source/Sample.Infra/QA/Read/Repositories/OrderRepository.cs");
    }

    [Fact]
    public async Task Unmapped_Cqrs_Context_Should_Not_Become_An_Implicit_Ownership_Choice()
    {
        using var workspace = new AdhocWorkspace();
        var entity = CreateDocuments(workspace, cqrs: true);
        var infra = workspace.CurrentSolution.Projects.Single(project => project.Name == "Sample.Infra");
        var inputs = infra.AdditionalDocuments.Single();
        var content = (await inputs.GetTextAsync()).ToString().Replace(", Subdomain = \"QA\", Reason = \"CQRS context in QA.\"", "", StringComparison.Ordinal);
        workspace.TryApplyChanges(infra.Solution.WithAdditionalDocumentText(inputs.Id, SourceText.From(content))).Should().BeTrue();
        (await Actions(workspace.CurrentSolution.GetDocument(entity.Id)!)).Should().BeEmpty();
    }

    [Fact]
    public async Task Alias_Only_Domain_Reference_Should_Not_Generate_Global_Type_Uses()
    {
        using var workspace = new AdhocWorkspace();
        var entity = CreateDocuments(workspace);
        var infra = workspace.CurrentSolution.Projects.Single(project => project.Name == "Sample.Infra");
        var context = infra.Documents.Single();
        var source = "extern alias DomainAlias;\n" + (await context.GetTextAsync()).ToString()
            .Replace("Sample.Domain.QA.Orders.Order", "DomainAlias::Sample.Domain.QA.Orders.Order", StringComparison.Ordinal);
        var changed = context.WithText(SourceText.From(source)).Project.WithProjectReferences(
            [new ProjectReference(entity.Project.Id, ["DomainAlias"])]).Solution;
        workspace.TryApplyChanges(changed).Should().BeTrue();
        (await Actions(workspace.CurrentSolution.GetDocument(entity.Id)!)).Should().BeEmpty();
    }

    [Theory]
    [InlineData("protected override void OnModelCreating(Microsoft.EntityFrameworkCore.ModelBuilder builder) => base.OnModelCreating(builder);", true)]
    [InlineData("protected override void OnModelCreating(Microsoft.EntityFrameworkCore.ModelBuilder builder) { base.OnModelCreating(builder); builder.Ignore<Sample.Domain.QA.Orders.Order>(); }", false)]
    public async Task Context_Override_Should_Require_Exact_Base_Forwarding(string members, bool supported)
    {
        using var workspace = new AdhocWorkspace();
        var entity = CreateDocuments(workspace, contextMembers: members);
        var actions = await Actions(entity);
        actions.Should().HaveCount(supported ? 1 : 0);
        if (!supported) return;
        var changed = await ChangedSolution(actions[0]);
        (await changed.GetDocument(entity.Id)!.GetTextAsync()).ToString().Should().Be(EntitySource);
        var context = changed.Projects.SelectMany(project => project.Documents).Single(document => document.Name == "QAContext.cs");
        (await context.GetTextAsync()).ToString().Should().Contain(members);
    }

    [Theory]
    [InlineData("[assembly: DRN.Conventions.Role(\"Infra\", NamespacePrefix = \"Sample.Infra\", LogicalPathPrefix = \"Storage\")]")]
    [InlineData("[assembly: DRN.Conventions.Ownership(\"Sample.Infra.QA.QAContext\", Subdomain = \"Billing\", Reason = \"Reviewed ownership alias\")]")]
    public async Task Split_Roles_Or_Conflicting_Ownership_Should_Not_Use_Default_Pair_Placement(string record)
    {
        using var workspace = new AdhocWorkspace();
        var entity = CreateDocuments(workspace);
        var infra = workspace.CurrentSolution.Projects.Single(project => project.Name == "Sample.Infra");
        var inputs = infra.AdditionalDocuments.Single();
        var text = (await inputs.GetTextAsync()).ToString();
        var changed = infra.Solution.WithAdditionalDocumentText(inputs.Id, SourceText.From(text + "\n" + record));
        workspace.TryApplyChanges(changed).Should().BeTrue();
        (await Actions(workspace.CurrentSolution.GetDocument(entity.Id)!)).Should().BeEmpty();
    }

    [Theory]
    [InlineData("Sample.Domain", "namespace Sample.Domain.QA.Orders.IOrderRepository { public class Detail; }")]
    [InlineData("Sample.Infra", "namespace Sample.Infra.QA.Repositories.OrderRepository { public class Detail; }")]
    [InlineData("Sample.Infra", "namespace Sample.Infra.QA { public class Repositories; }")]
    [InlineData("Sample.Infra", "namespace Sample.Infra.QA { public class repositories; }")]
    public async Task Leaf_And_Namespace_Segment_Collisions_Should_Block_Repository_Pair(string projectName, string collision)
    {
        using var workspace = new AdhocWorkspace();
        var entity = CreateDocuments(workspace);
        var project = workspace.CurrentSolution.Projects.Single(item => item.Name == projectName);
        var changed = project.AddDocument("Legacy.cs", collision).Project.Solution;
        workspace.TryApplyChanges(changed).Should().BeTrue();
        var current = workspace.CurrentSolution.GetDocument(entity.Id)!;
        (await Actions(current)).Should().BeEmpty();
        (await Actions(current, bundles: true)).Should().BeEmpty();
    }

    [Fact]
    public async Task Identical_Concatenated_Context_Records_Should_Preserve_Repository_Destination()
    {
        using var workspace = new AdhocWorkspace();
        var entity = CreateDocuments(workspace);
        var infra = workspace.CurrentSolution.Projects.Single(project => project.Name == "Sample.Infra");
        var input = infra.AdditionalDocuments.Single();
        var text = await input.GetTextAsync();
        var changed = infra.Solution.WithAdditionalDocumentText(input.Id, SourceText.From(text + """

            [assembly: DRN.Conventions.Context(("Sample.Infra.QA." + "QAContext"), Repositories = ("QA/" + "Repositories"))]
            """));
        workspace.TryApplyChanges(changed).Should().BeTrue();
        (await Actions(workspace.CurrentSolution.GetDocument(entity.Id)!)).Should().ContainSingle();
    }

    [Fact]
    public async Task Contradictory_Context_Destinations_Should_Block_Repository_Generation()
    {
        using var workspace = new AdhocWorkspace();
        var entity = CreateDocuments(workspace);
        var infra = workspace.CurrentSolution.Projects.Single(project => project.Name == "Sample.Infra");
        var input = infra.AdditionalDocuments.Single();
        var text = await input.GetTextAsync();
        var changed = infra.Solution.WithAdditionalDocumentText(input.Id, SourceText.From(text + """

            [assembly: DRN.Conventions.Context("Sample.Infra.QA.QAContext", Repositories = "Other/Repositories")]
            """));
        workspace.TryApplyChanges(changed).Should().BeTrue();
        (await Actions(workspace.CurrentSolution.GetDocument(entity.Id)!)).Should().BeEmpty();
    }

    [Theory]
    [InlineData("Storage.Infra")]
    [InlineData("Infra")]
    public async Task Referencing_Infrastructure_Should_Not_Require_A_Shared_Project_Prefix(string infraName)
    {
        using var workspace = new AdhocWorkspace();
        var entity = CreateDocuments(workspace, infraName: infraName);
        var action = (await Actions(entity)).Should().ContainSingle().Which;
        var changed = await ChangedSolution(action);
        var infra = changed.Projects.Single(project => project.Name == infraName);
        var repository = infra.Documents.Single(document => document.Name == "OrderRepository.cs");
        (await repository.GetTextAsync()).ToString().Should().Contain("namespace " + infraName + ".QA.Repositories;")
            .And.Contain("global::Sample.Domain.QA.Orders.Order");
        changed.GetChanges(workspace.CurrentSolution).GetProjectChanges().Should()
            .OnlyContain(change => !change.GetAddedProjectReferences().Any());
        (await infra.GetCompilationAsync())!.GetDiagnostics().Should()
            .NotContain(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Reasoned_Owner_Should_Not_Generate_A_Pair_In_A_Different_Namespace_Owner(bool moveEntity)
    {
        using var workspace = new AdhocWorkspace();
        var entity = CreateDocuments(workspace);
        var infra = workspace.CurrentSolution.Projects.Single(project => project.Name == "Sample.Infra");
        var context = infra.Documents.Single(document => document.Name == "QAContext.cs");
        var contextSource = (await context.GetTextAsync()).ToString();
        var ownership = "[DRN.Framework.SharedKernel.Attributes.SourceKnownSubdomain(\"QA\", Reason = \"Reviewed legacy placement\")]\n";
        var solution = workspace.CurrentSolution;
        if (moveEntity)
        {
            var source = EntitySource.Replace("Sample.Domain.QA.Orders", "Sample.Domain.Legacy.Orders", StringComparison.Ordinal)
                .Replace("public class Order", ownership + "public class Order", StringComparison.Ordinal);
            solution = solution.WithDocumentText(entity.Id, SourceText.From(source));
            contextSource = contextSource.Replace("Sample.Domain.QA.Orders", "Sample.Domain.Legacy.Orders", StringComparison.Ordinal);
        }
        else
        {
            contextSource = contextSource.Replace("namespace Sample.Infra.QA;", "namespace Sample.Infra.Legacy;", StringComparison.Ordinal)
                .Replace("public class QAContext", ownership + "public class QAContext", StringComparison.Ordinal);
            var input = infra.AdditionalDocuments.Single();
            var inputSource = (await input.GetTextAsync()).ToString().Replace("Sample.Infra.QA.QAContext", "Sample.Infra.Legacy.QAContext", StringComparison.Ordinal);
            solution = solution.WithAdditionalDocumentText(input.Id, SourceText.From(inputSource));
        }
        solution = solution.WithDocumentText(context.Id, SourceText.From(contextSource));
        workspace.TryApplyChanges(solution).Should().BeTrue();
        foreach (var project in workspace.CurrentSolution.Projects)
            (await project.GetCompilationAsync())!.GetDiagnostics().Should().NotContain(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        (await Actions(workspace.CurrentSolution.GetDocument(entity.Id)!)).Should().BeEmpty();
    }

    [Fact]
    public async Task EntitySuffixed_Aggregate_Should_Generate_Stemmed_Bundle_Without_Convention_Diagnostics()
    {
        using var workspace = new AdhocWorkspace();
        var domain = workspace.AddProject(ProjectInfo.Create(ProjectId.CreateNewId(), VersionStamp.Create(),
                "Sample.Domain", "Sample.Domain", LanguageNames.CSharp, filePath: "/source/Sample.Domain/Sample.Domain.csproj"))
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .AddMetadataReferences(DiagnosticClassificationAnalyzerTests.References);
        var entity = domain.AddDocument("OrderEntity.cs", """
            namespace Sample.Domain.QA.Order;
            public class OrderEntity : DRN.Framework.SharedKernel.Domain.AggregateRoot;
            """, filePath: "/source/Sample.Domain/QA/Order/OrderEntity.cs");
        workspace.TryApplyChanges(entity.Project.Solution).Should().BeTrue();

        var infra = workspace.AddProject(ProjectInfo.Create(ProjectId.CreateNewId(), VersionStamp.Create(),
                "Sample.Infra", "Sample.Infra", LanguageNames.CSharp, filePath: "/source/Sample.Infra/Sample.Infra.csproj"))
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .AddProjectReference(new ProjectReference(domain.Id))
            .AddMetadataReferences(DiagnosticClassificationAnalyzerTests.References)
            .AddMetadataReference(FrameworkReference)
            .AddMetadataReference(UtilsReference);
        infra = infra.AddDocument("QAContext.cs", """
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
            """, filePath: "/source/Sample.Infra/QA/QAContext.cs").Project;
        workspace.TryApplyChanges(infra.Solution).Should().BeTrue();

        var currentEntity = workspace.CurrentSolution.GetDocument(entity.Id)!;
        var text = (await currentEntity.GetTextAsync()).ToString();
        var actions = new List<CodeAction>();
        await new RepositoryPairRefactoringProvider().ComputeRefactoringsAsync(new CodeRefactoringContext(currentEntity,
            new TextSpan(text.IndexOf("OrderEntity :", StringComparison.Ordinal), 0), actions.Add, CancellationToken.None));
        var bundle = actions.Single(action => action.EquivalenceKey!.StartsWith("DrnPersistenceBundle:", StringComparison.Ordinal));
        var changed = await ChangedSolution(bundle);
        var changedDomain = changed.Projects.Single(project => project.Name == "Sample.Domain");
        var changedInfra = changed.Projects.Single(project => project.Name == "Sample.Infra");
        changedDomain.Documents.Should().ContainSingle(document => document.Name == "IOrderRepository.cs");
        changedInfra.Documents.Should().ContainSingle(document => document.Name == "OrderRepository.cs");
        changedInfra.Documents.Should().ContainSingle(document => document.Name == "OrderConfig.cs");

        var domainCompilation = (await changedDomain.GetCompilationAsync())!;
        var (layeredDomain, domainLayerRecords) = ConventionTestDependencies.WithRequiredLayers(domainCompilation, "Domain");
        layeredDomain.GetDiagnostics().Should().NotContain(item => item.Severity == DiagnosticSeverity.Error);

        using var domainImage = new MemoryStream();
        domainCompilation.Emit(domainImage).Success.Should().BeTrue();
        var domainReference = MetadataReference.CreateFromImage(domainImage.ToArray(), filePath: "/source/Sample.Domain/Sample.Domain.dll");
        var infraCompilation = (await changedInfra.GetCompilationAsync())!;
        var layeredInfra = infraCompilation.RemoveReferences(infraCompilation.References.OfType<CompilationReference>()).AddReferences(domainReference);
        layeredInfra.GetDiagnostics().Should().NotContain(item => item.Severity == DiagnosticSeverity.Error);

        var domainInputs = new ProjectDiagnosticRulesTests.RuleFile(DRN.Framework.SharedKernel.Analyzers.Conventions.ConventionInputs.FileName, $$"""
            [assembly: DRN.Conventions.Version("1")]
            [assembly: DRN.Conventions.Reference("Inventory", Stage = "Complete")]
            {{ConventionTestDependencies.Serialize(domainLayerRecords)}}
            [assembly: DRN.Conventions.Path("/source/Sample.Domain/QA/Order/OrderEntity.cs", LogicalPath = "QA/Order/OrderEntity.cs")]
            [assembly: DRN.Conventions.Path("/source/Sample.Domain/QA/Order/IOrderRepository.cs", LogicalPath = "QA/Order/IOrderRepository.cs")]
            """);
        var infraInputs = new ProjectDiagnosticRulesTests.RuleFile(DRN.Framework.SharedKernel.Analyzers.Conventions.ConventionInputs.FileName, """
            [assembly: DRN.Conventions.Version("1")]
            [assembly: DRN.Conventions.Reference("Inventory", Stage = "Complete")]
            [assembly: DRN.Conventions.Reference("/source/Sample.Domain/Sample.Domain.csproj", Stage = "Project", Source = "/source/Sample.Domain/Sample.Domain.csproj")]
            [assembly: DRN.Conventions.Reference("/source/Sample.Domain/Sample.Domain.dll", Stage = "Resolved", Project = "/source/Sample.Domain/Sample.Domain.csproj")]
            [assembly: DRN.Conventions.Path("/source/Sample.Infra/QA/QAContext.cs", LogicalPath = "QA/QAContext.cs")]
            [assembly: DRN.Conventions.Path("/source/Sample.Infra/QA/Repositories/OrderRepository.cs", LogicalPath = "QA/Repositories/OrderRepository.cs")]
            [assembly: DRN.Conventions.Path("/source/Sample.Infra/QA/Configurations/OrderConfig.cs", LogicalPath = "QA/Configurations/OrderConfig.cs")]
            """);
        var analyzer = new DRN.Framework.SharedKernel.Analyzers.Conventions.SourceKnownConventionAnalyzer();
        var domainDiagnostics = await layeredDomain.WithAnalyzers([analyzer],
            new AnalyzerOptions([domainInputs], new Options("Domain"))).GetAnalyzerDiagnosticsAsync();
        var infraDiagnostics = await layeredInfra.WithAnalyzers([analyzer],
            new AnalyzerOptions([infraInputs], new Options("Infra"))).GetAnalyzerDiagnosticsAsync();
        domainDiagnostics.Should().BeEmpty();
        infraDiagnostics.Should().BeEmpty();
    }

    private sealed class Options(string role) : AnalyzerConfigOptionsProvider
    {
        public override AnalyzerConfigOptions GlobalOptions { get; } = new ConventionInputTests.Config(new()
        {
            ["build_property.MSBuildProjectName"] = "Sample." + role,
            ["build_property.RootNamespace"] = "Sample." + role
        });
        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => GlobalOptions;
        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => GlobalOptions;
    }

    private static Document CreateDocuments(AdhocWorkspace workspace, bool membership = true, bool destination = true, bool cqrs = false,
        string contextMembers = "", string infraName = "Sample.Infra")
    {
        var domain = workspace.AddProject(ProjectInfo.Create(ProjectId.CreateNewId(), VersionStamp.Create(),
                "Sample.Domain", "Sample.Domain", LanguageNames.CSharp, filePath: "/source/Sample.Domain/Sample.Domain.csproj"))
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .AddMetadataReferences(DiagnosticClassificationAnalyzerTests.References);
        var entity = domain.AddDocument("Order.cs", EntitySource, filePath: "/source/Sample.Domain/QA/Orders/Order.cs");
        workspace.TryApplyChanges(entity.Project.Solution).Should().BeTrue();
        var infra = workspace.AddProject(ProjectInfo.Create(ProjectId.CreateNewId(), VersionStamp.Create(),
                infraName, infraName, LanguageNames.CSharp, filePath: "/source/" + infraName + "/" + infraName + ".csproj"))
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .AddProjectReference(new ProjectReference(domain.Id))
            .AddMetadataReferences(DiagnosticClassificationAnalyzerTests.References)
            .AddMetadataReference(FrameworkReference)
            .AddMetadataReference(UtilsReference);
        var contexts = cqrs ? new[] { ("ReadContext", "QA.Read", "QA/Read"), ("WriteContext", "QA.Write", "QA/Write") }
            : new[] { ("QAContext", "QA", "QA") };
        var records = new List<string> { "[assembly: DRN.Conventions.Version(\"1\")]" };
        foreach (var (name, ns, path) in contexts)
        {
            var member = membership ? "public Microsoft.EntityFrameworkCore.DbSet<Sample.Domain.QA.Orders.Order> Orders { get; set; }" : "";
            infra = infra.AddDocument(name + ".cs", $$"""
                namespace {{infraName}}.{{ns}};
                public class {{name}} : DRN.Framework.EntityFramework.Context.DrnContext<{{name}}>
                {
                    public {{name}}() : base(null) { }
                    {{member}}
                    {{contextMembers}}
                }
                """, filePath: "/source/" + infraName + "/" + path + "/" + name + ".cs").Project;
            records.Add("[assembly: DRN.Conventions.Context(\"" + infraName + "." + ns + "." + name + "\", Repositories = \"" + path +
                        "/Repositories\"" + (cqrs ? ", Subdomain = \"QA\", Reason = \"CQRS context in QA.\"" : "") + ")]");
        }
        if (destination)
            infra = infra.AddAdditionalDocument("DrnConventionInputs.g.cs", string.Join("\n", records),
                filePath: "/source/" + infraName + "/obj/DrnConventionInputs.g.cs").Project;
        workspace.TryApplyChanges(infra.Solution).Should().BeTrue();
        return workspace.CurrentSolution.GetDocument(entity.Id)!;
    }

    private static async Task<List<CodeAction>> Actions(Document entity, bool bundles = false)
    {
        var actions = new List<CodeAction>();
        var text = (await entity.GetTextAsync()).ToString();
        await new RepositoryPairRefactoringProvider().ComputeRefactoringsAsync(new CodeRefactoringContext(entity,
            new TextSpan(text.IndexOf("Order :", StringComparison.Ordinal), 0), actions.Add, CancellationToken.None));
        return actions.Where(action => action.EquivalenceKey!.StartsWith("DrnPersistenceBundle:", StringComparison.Ordinal) == bundles).ToList();
    }

    private static async Task<Solution> ChangedSolution(CodeAction action) =>
        (await action.GetOperationsAsync(CancellationToken.None)).OfType<ApplyChangesOperation>().Single().ChangedSolution;
}
