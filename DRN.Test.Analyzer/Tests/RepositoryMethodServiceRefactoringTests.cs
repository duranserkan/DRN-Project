using System.Diagnostics.CodeAnalysis;
using DRN.Framework.SharedKernel.CodeFixes;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.Text;

namespace DRN.Test.Analyzer.Tests;

[SuppressMessage("ReSharper", "AccessToDisposedClosure")]
public sealed class RepositoryMethodServiceRefactoringTests
{
    private const string Method = "System.Threading.Tasks.Task<int> CountVisibleAsync(string filter, System.Threading.CancellationToken cancellationToken = default);";

    [Fact]
    public async Task Selected_Operation_Should_Be_Forwarded_With_Exact_Cancellation_And_Explicit_Registration_Choice()
    {
        using var workspace = new AdhocWorkspace();
        var document = Create(workspace);
        var original = await document.GetTextAsync();
        var actions = await Actions(document);
        actions.Should().HaveCount(2);
        var action = actions.Single(item => item.Title.StartsWith("Add scoped", StringComparison.Ordinal));
        action.Title.Should().Contain("OrderCountVisibleService.cs").And.Contain("CountVisibleAsync");
        var changed = await action.GetChangedSolutionAsync();
        var added = changed.GetChanges(workspace.CurrentSolution).GetProjectChanges().Single();
        added.GetAddedDocuments().Should().ContainSingle();
        added.GetChangedDocuments().Should().BeEmpty();
        added.GetAddedProjectReferences().Should().BeEmpty();
        var service = changed.GetDocument(added.GetAddedDocuments().Single())!;
        var source = (await service.GetTextAsync()).ToString();
        source.Should().Contain("namespace Sample.Application.Services.QA.Orders;")
            .And.Contain("Scoped<OrderCountVisibleService>")
            .And.Contain("SourceKnownService<global::Sample.Domain.QA.Orders.Order>")
            .And.Contain("global::System.Threading.CancellationToken cancellationToken = default")
            .And.Contain("=> this._repository.CountVisibleAsync(filter, cancellationToken);")
            .And.NotContain("NotImplementedException").And.NotContain("ConfigureAwait").And.NotContain("Http");
        (await changed.GetDocument(document.Id)!.GetTextAsync()).ContentEquals(original).Should().BeTrue();
        (await service.Project.GetCompilationAsync())!.AssertNoCompilerErrors();
        workspace.TryApplyChanges(changed).Should().BeTrue();
        (await Actions(workspace.CurrentSolution.GetDocument(document.Id)!)).Should().BeEmpty();
    }

    [Fact]
    public async Task Unregistered_Selection_Should_Not_Invent_A_Lifetime()
    {
        using var workspace = new AdhocWorkspace();
        var document = Create(workspace);
        var action = (await Actions(document)).Single(item => item.Title.StartsWith("Add unregistered", StringComparison.Ordinal));
        var changed = await action.GetChangedSolutionAsync();
        var service = changed.Projects.Single(project => project.Name == "Sample.Application").Documents.Single();
        (await service.GetTextAsync()).ToString().Should().NotContain("Scoped");
    }

    [Fact]
    public async Task Synchronous_Void_Operation_Should_Preserve_Void_Keyword_And_Arguments()
    {
        using var workspace = new AdhocWorkspace();
        var document = Create(workspace, "void Refresh(string key);");
        var changed = await (await Actions(document)).First().GetChangedSolutionAsync();
        var service = changed.Projects.Single(project => project.Name == "Sample.Application").Documents.Single();
        (await service.GetTextAsync()).ToString().Should().Contain("public void Refresh(global::System.String key)")
            .And.Contain("=> this._repository.Refresh(key);");
        (await service.Project.GetCompilationAsync())!.AssertNoCompilerErrors();
    }

    [Fact]
    public async Task Repository_Named_Parameter_Should_Not_Shadow_The_Forwarding_Receiver()
    {
        using var workspace = new AdhocWorkspace();
        var document = Create(workspace, "string? Lookup(string? _repository);");
        var changed = await (await Actions(document)).First().GetChangedSolutionAsync();
        var service = changed.Projects.Single(project => project.Name == "Sample.Application").Documents.Single();
        (await service.GetTextAsync()).ToString().Should().Contain("public global::System.String? Lookup(global::System.String? _repository)")
            .And.Contain("=> this._repository.Lookup(_repository);");
        (await service.Project.GetCompilationAsync())!.AssertNoCompilerErrors();
    }

    [Theory]
    [InlineData("System.Threading.Tasks.Task<int> CountVisibleAsync(string filter);")]
    [InlineData("int CountVisible(string filter = \"default\");")]
    [InlineData("int CountVisible(ref string filter);")]
    [InlineData("int CountVisible<T>(T filter);")]
    [InlineData("[System.Obsolete] int CountVisible(string filter);")]
    [InlineData("int CountVisible(params string[] filters);")]
    [InlineData("int _repository();")]
    public async Task Unsupported_Signatures_Should_Not_Lose_Defaults_Annotations_Or_Cancellation(string method)
    {
        using var workspace = new AdhocWorkspace();
        (await Actions(Create(workspace, method))).Should().BeEmpty();
    }

    [Fact]
    public async Task Invalid_Aggregate_Constraint_Should_Not_Produce_An_Invalid_Service()
    {
        using var workspace = new AdhocWorkspace();
        var document = Create(workspace);
        var source = (await document.GetTextAsync()).ToString().Replace(" : DRN.Framework.SharedKernel.Domain.AggregateRoot", "", StringComparison.Ordinal);
        workspace.TryApplyChanges(document.WithText(SourceText.From(source)).Project.Solution).Should().BeTrue();
        (await Actions(workspace.CurrentSolution.GetDocument(document.Id)!)).Should().BeEmpty();
    }

    [Fact]
    public async Task Unknown_Interface_Annotation_Should_Not_Be_Dropped()
    {
        using var workspace = new AdhocWorkspace();
        var document = Create(workspace);
        var source = (await document.GetTextAsync()).ToString().Replace("public interface", "[System.Obsolete]\npublic interface", StringComparison.Ordinal);
        workspace.TryApplyChanges(document.WithText(SourceText.From(source)).Project.Solution).Should().BeTrue();
        (await Actions(workspace.CurrentSolution.GetDocument(document.Id)!)).Should().BeEmpty();
    }

    [Theory]
    [InlineData("Sample.Application")]
    [InlineData("Other.Application")]
    public async Task Absent_Edge_Should_Not_Create_New_Dependencies(string target)
    {
        using var workspace = new AdhocWorkspace();
        (await Actions(Create(workspace, reference: false, targetName: target))).Should().BeEmpty();
    }

    [Theory]
    [InlineData("Other.Application")]
    [InlineData("Application")]
    public async Task Referencing_Application_Should_Not_Require_A_Shared_Project_Prefix(string target)
    {
        using var workspace = new AdhocWorkspace();
        var document = Create(workspace, targetName: target);
        var action = (await Actions(document)).Single(item => item.Title.StartsWith("Add scoped", StringComparison.Ordinal));
        var changed = await action.GetChangedSolutionAsync();
        var application = changed.Projects.Single(project => project.Name == target);
        (await application.Documents.Single().GetTextAsync()).ToString()
            .Should().Contain("namespace " + target + ".Services.QA.Orders;");
        changed.GetChanges(workspace.CurrentSolution).GetProjectChanges().Should()
            .OnlyContain(change => !change.GetAddedProjectReferences().Any());
        (await application.GetCompilationAsync())!.AssertNoCompilerErrors();
    }

    [Fact]
    public async Task Destination_Collision_Should_Leave_Existing_Content_Unchanged()
    {
        using var workspace = new AdhocWorkspace();
        var document = Create(workspace);
        var application = workspace.CurrentSolution.Projects.Single(project => project.Name == "Sample.Application");
        var collision = application.AddDocument("ordercountvisibleservice.cs", "// keep", filePath:
            "/source/Sample.Application/Services/QA/Orders/ordercountvisibleservice.cs");
        workspace.TryApplyChanges(collision.Project.Solution).Should().BeTrue();
        (await Actions(workspace.CurrentSolution.GetDocument(document.Id)!)).Should().BeEmpty();
    }

    [Fact]
    public async Task Stale_Preview_Should_Not_Reintroduce_A_Removed_Target_Project()
    {
        using var workspace = new AdhocWorkspace();
        var document = Create(workspace);
        var action = (await Actions(document)).First();
        var target = workspace.CurrentSolution.Projects.Single(project => project.Name == "Sample.Application");
        workspace.TryApplyChanges(workspace.CurrentSolution.RemoveProject(target.Id)).Should().BeTrue();
        var changed = await action.GetChangedSolutionAsync();
        changed.GetProject(target.Id).Should().BeNull();
        changed.GetChanges(workspace.CurrentSolution).GetAddedProjects().Should().BeEmpty();
    }

    [Fact]
    public async Task Canceled_Application_Should_Leave_Workspace_Unchanged()
    {
        using var workspace = new AdhocWorkspace();
        var document = Create(workspace);
        var action = (await Actions(document)).First();
        var current = workspace.CurrentSolution;
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var apply = async () => await action.GetOperationsAsync(cancellation.Token);
        await apply.Should().ThrowAsync<OperationCanceledException>();
        workspace.CurrentSolution.Should().BeSameAs(current);
    }

    [Theory]
    [InlineData("namespace Sample.Application.Services.QA.Orders.OrderCountVisibleService { public class Detail; }")]
    [InlineData("namespace Sample.Application { public class Services; }")]
    [InlineData("namespace Sample.Application.Services { public class QA; }")]
    [InlineData("namespace Sample.Application.Services.QA { public class Orders; }")]
    public async Task Leaf_And_Namespace_Segment_Collisions_Should_Block_Forwarding_Service(string collision)
    {
        using var workspace = new AdhocWorkspace();
        var document = Create(workspace);
        var application = workspace.CurrentSolution.Projects.Single(project => project.Name == "Sample.Application");
        var changed = application.AddDocument("Legacy.cs", collision).Project.Solution;
        workspace.TryApplyChanges(changed).Should().BeTrue();
        (await Actions(workspace.CurrentSolution.GetDocument(document.Id)!)).Should().BeEmpty();
    }

    [Theory]
    [InlineData("Sample.Domain", "[module: DRN.Conventions.Version(\"1\")]")]
    [InlineData("Sample.Application", "[assembly: DRN.Conventions.Version(\"2\")]")]
    [InlineData("Sample.Application", "[assembly: DRN.Conventions.Version(\"1\")][assembly: Unknown.Record(\"x\")]")]
    public async Task Invalid_Input_Should_Block_Forwarding_Service_Defaults(string projectName, string input)
    {
        using var workspace = new AdhocWorkspace();
        var document = Create(workspace);
        var project = workspace.CurrentSolution.Projects.Single(item => item.Name == projectName);
        var changed = project.AddAdditionalDocument("DrnConventionInputs.g.cs", input).Project.Solution;
        workspace.TryApplyChanges(changed).Should().BeTrue();
        (await Actions(workspace.CurrentSolution.GetDocument(document.Id)!)).Should().BeEmpty();
    }

    [Fact]
    public async Task EntitySuffixed_Aggregate_Should_Generate_Stemmed_Service_Without_Convention_Diagnostics()
    {
        using var workspace = new AdhocWorkspace();
        var domain = workspace.AddProject(ProjectInfo.Create(ProjectId.CreateNewId(), VersionStamp.Create(),
                "Sample.Domain", "Sample.Domain", LanguageNames.CSharp, filePath: "/source/Sample.Domain/Sample.Domain.csproj"))
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .AddMetadataReferences(ClassificationTestFixture.References);
        var document = domain.AddDocument("IOrderRepository.cs", $$"""
            namespace Sample.Domain.QA.Order;
            public class OrderEntity : DRN.Framework.SharedKernel.Domain.AggregateRoot;
            public interface IOrderRepository : DRN.Framework.SharedKernel.Domain.Repository.ISourceKnownRepository<OrderEntity>
            {
                // select-start
                {{Method}}
                // select-end
            }
            """, filePath: "/source/Sample.Domain/QA/Order/IOrderRepository.cs");
        workspace.TryApplyChanges(document.Project.Solution).Should().BeTrue();
        var application = workspace.AddProject(ProjectInfo.Create(ProjectId.CreateNewId(), VersionStamp.Create(),
                "Sample.Application", "Sample.Application", LanguageNames.CSharp, filePath: "/source/Sample.Application/Sample.Application.csproj"))
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .AddProjectReference(new ProjectReference(domain.Id))
            .AddMetadataReferences(ClassificationTestFixture.References)
            .AddMetadataReference(AnalyzerTestCompilation.UtilsReference);
        application = application.AddDocument("ApplicationModule.cs", """
            namespace Sample.Application;
            public static class ApplicationModule
            {
                public static Microsoft.Extensions.DependencyInjection.IServiceCollection AddApplication(
                    this Microsoft.Extensions.DependencyInjection.IServiceCollection services)
                {
                    DRN.Framework.Utils.DependencyInjection.ServiceCollectionExtensions.AddServicesWithAttributes(services);
                    return services;
                }
            }
            """, filePath: "/source/Sample.Application/ApplicationModule.cs").Project;
        workspace.TryApplyChanges(application.Solution).Should().BeTrue();

        var action = (await Actions(workspace.CurrentSolution.GetDocument(document.Id)!))
            .Single(item => item.Title.StartsWith("Add scoped", StringComparison.Ordinal));
        action.Title.Should().Contain("OrderCountVisibleService.cs");
        var changed = await action.GetChangedSolutionAsync();
        var changedApp = changed.Projects.Single(project => project.Name == "Sample.Application");
        var service = changedApp.Documents.Single(item => item.Name == "OrderCountVisibleService.cs");
        (await service.GetTextAsync()).ToString().Should().Contain("public sealed class OrderCountVisibleService");
        var domainCompilation = (await changed.Projects.Single(project => project.Name == "Sample.Domain").GetCompilationAsync())!;
        domainCompilation.AssertNoCompilerErrors();
        var domainReference = AnalyzerTestCompilation.EmitReference(domainCompilation, "/source/Sample.Domain/Sample.Domain.dll");
        var compilation = (await changedApp.GetCompilationAsync())!;
        var appCompilation = compilation.RemoveReferences(compilation.References.OfType<CompilationReference>()).AddReferences(domainReference);
        var inputs = new ProjectDiagnosticRulesTests.RuleFile(Framework.SharedKernel.Analyzers.Conventions.ConventionInputs.FileName, """
            [assembly: DRN.Conventions.Version("1")]
            [assembly: DRN.Conventions.Reference("Inventory", Stage = "Complete")]
            [assembly: DRN.Conventions.Reference("/source/Sample.Domain/Sample.Domain.csproj", Stage = "Project", Source = "/source/Sample.Domain/Sample.Domain.csproj")]
            [assembly: DRN.Conventions.Reference("/source/Sample.Domain/Sample.Domain.dll", Stage = "Resolved", Project = "/source/Sample.Domain/Sample.Domain.csproj")]
            [assembly: DRN.Conventions.Path("/source/Sample.Application/ApplicationModule.cs", LogicalPath = "ApplicationModule.cs")]
            [assembly: DRN.Conventions.Path("/source/Sample.Application/Services/QA/Order/OrderCountVisibleService.cs", LogicalPath = "Services/QA/Order/OrderCountVisibleService.cs")]
            """);
        var diagnostics = await AnalyzerTestCompilation.Analyze(appCompilation,
            new Framework.SharedKernel.Analyzers.Conventions.SourceKnownConventionAnalyzer(),
            AnalyzerTestCompilation.ConventionOptions("Sample.Application", files: inputs));
        diagnostics.Should().BeEmpty();
    }

    private static Document Create(AdhocWorkspace workspace, string method = Method, bool reference = true, string targetName = "Sample.Application")
    {
        var domain = workspace.AddProject(ProjectInfo.Create(ProjectId.CreateNewId(), VersionStamp.Create(),
                "Sample.Domain", "Sample.Domain", LanguageNames.CSharp, filePath: "/source/Sample.Domain/Sample.Domain.csproj"))
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .AddMetadataReferences(ClassificationTestFixture.References);
        var document = domain.AddDocument("IOrderRepository.cs", $$"""
            namespace Sample.Domain.QA.Orders;
            public class Order : DRN.Framework.SharedKernel.Domain.AggregateRoot;
            public interface IOrderRepository : DRN.Framework.SharedKernel.Domain.Repository.ISourceKnownRepository<Order>
            {
                // select-start
                {{method}}
                // select-end
            }
            """, filePath: "/source/Sample.Domain/QA/Orders/IOrderRepository.cs");
        workspace.TryApplyChanges(document.Project.Solution).Should().BeTrue();
        var application = workspace.AddProject(ProjectInfo.Create(ProjectId.CreateNewId(), VersionStamp.Create(),
                targetName, targetName, LanguageNames.CSharp, filePath: "/source/" + targetName + "/" + targetName + ".csproj"))
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .AddMetadataReferences(ClassificationTestFixture.References)
            .AddMetadataReference(AnalyzerTestCompilation.UtilsReference);
        if (reference) application = application.AddProjectReference(new ProjectReference(domain.Id));
        workspace.TryApplyChanges(application.Solution).Should().BeTrue();
        return workspace.CurrentSolution.GetDocument(document.Id)!;
    }

    private static async Task<List<CodeAction>> Actions(Document document)
    {
        var source = (await document.GetTextAsync()).ToString();
        var start = source.IndexOf("// select-start", StringComparison.Ordinal) + "// select-start".Length;
        var end = source.IndexOf("// select-end", StringComparison.Ordinal);
        var actions = new List<CodeAction>();
        await new RepositoryMethodServiceRefactoringProvider().ComputeRefactoringsAsync(new CodeRefactoringContext(document,
            TextSpan.FromBounds(start, end), actions.Add, CancellationToken.None));
        return actions;
    }
}
