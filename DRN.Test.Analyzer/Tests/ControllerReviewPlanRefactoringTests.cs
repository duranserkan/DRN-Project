using System.Diagnostics.CodeAnalysis;
using DRN.Framework.SharedKernel.CodeFixes;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.Text;

namespace DRN.Test.Analyzer.Tests;

[SuppressMessage("ReSharper", "AccessToDisposedClosure")]
public sealed class ControllerReviewPlanRefactoringTests
{
    [Fact]
    public async Task Real_Framework_Controller_Should_Inventory_Crud_Without_Dumping_Mvc_Helpers()
    {
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject(ProjectInfo.Create(ProjectId.CreateNewId(), VersionStamp.Create(),
                "Sample.Hosted", "Sample.Hosted", LanguageNames.CSharp, filePath: "/source/Sample.Hosted/Sample.Hosted.csproj"))
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .AddMetadataReferences(ClassificationTestFixture.References)
            .AddMetadataReference(AnalyzerTestCompilation.HostingReference);
        var source = """
            using DRN.Framework.SharedKernel.Domain;
            using DRN.Framework.SharedKernel.Domain.Repository;
            using DRN.Framework.Hosting.Controllers;
            using Microsoft.AspNetCore.Authorization;
            namespace Sample.Hosted.QA;
            public class Order : AggregateRoot;
            public class OrderDto : Dto;
            public class Request;
            [Authorize("Readers")]
            public abstract class OrdersController : SourceKnownWriteOnlyController<Order, OrderDto, Request>
            {
                protected OrdersController(ISourceKnownRepository<Order> repository) : base(repository) { }
            }
            """;
        var document = project.AddDocument("OrdersController.cs", source, filePath: "/source/Sample.Hosted/QA/OrdersController.cs");
        workspace.TryApplyChanges(document.Project.Solution).Should().BeTrue();
        document = workspace.CurrentSolution.GetDocument(document.Id)!;
        (await document.Project.GetCompilationAsync())!.AssertNoCompilerErrors();
        var action = (await Actions(document)).Should().ContainSingle().Which;
        var changed = await action.GetChangedSolutionAsync();
        var content = (await changed.GetProject(document.Project.Id)!.AdditionalDocuments.Single().GetTextAsync()).ToString();
        content.Length.Should().BeLessThan(12000);
        content.Should().Contain("GetAsync").And.Contain("PaginateWithBodyAsync").And.Contain("PostAsync").And.Contain("DeleteAsync")
            .And.Contain("NonAction").And.Contain("Readers").And.Contain("Excluded exact MVC ControllerBase helpers")
            .And.NotContain("ControllerBase.Ok(");
    }

    private const string Source = """
        namespace Microsoft.AspNetCore.Mvc
        {
            [System.AttributeUsage(System.AttributeTargets.Method)]
            public class NonActionAttribute : System.Attribute;
            [System.AttributeUsage(System.AttributeTargets.Method)]
            public class HttpGetAttribute(string route) : System.Attribute;
        }
        namespace Microsoft.AspNetCore.Authorization
        {
            [System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Method)]
            public class AuthorizeAttribute(string policy) : System.Attribute;
        }
        namespace DRN.Framework.Hosting.Controllers
        {
            public class SourceKnownControllerBase<TEntity,TDto>
            {
                [Microsoft.AspNetCore.Mvc.HttpGet("{id:guid}")]
                public virtual string Read(System.Guid id) => "";
            }
        }
        namespace Sample.Hosted.QA
        {
            [Microsoft.AspNetCore.Authorization.Authorize("Readers")]
            public class OrdersController : DRN.Framework.Hosting.Controllers.SourceKnownControllerBase<int,string>
            {
                [Microsoft.AspNetCore.Mvc.NonAction]
                public sealed override string Read(System.Guid id) => "";
                public string Local() => "";
            }
        }
        """;

    [Fact]
    public async Task Plan_Should_Inventory_Overrides_And_Metadata_Without_Changing_Source()
    {
        using var workspace = new AdhocWorkspace();
        var document = Create(workspace);
        var action = (await Actions(document)).Should().ContainSingle().Which;
        action.Title.Should().StartWith("Create controller review plan only:");
        var changed = await action.GetChangedSolutionAsync();
        var delta = changed.GetChanges(workspace.CurrentSolution).GetProjectChanges().Should().ContainSingle().Which;
        delta.GetAddedDocuments().Should().BeEmpty();
        delta.GetChangedDocuments().Should().BeEmpty();
        delta.GetAddedAdditionalDocuments().Should().ContainSingle();
        var plan = changed.GetProject(document.Project.Id)!.AdditionalDocuments.Single();
        var content = (await plan.GetTextAsync()).ToString();
        content.Should().Contain("NonAction").And.Contain("HttpGet").And.Contain("{id:guid}").And.Contain("Readers")
            .And.Contain("OrdersController.Read").And.Contain("Local()").And.Contain("SHA-256")
            .And.Contain("Override eligibility: not overridable (sealed override)")
            .And.Contain("per-resource authorization").And.Contain("No executable output destinations");
        plan.FilePath.Should().Be("/source/Sample.Hosted/QA/OrdersController.controller-review.md");
        (await changed.GetDocument(document.Id)!.GetTextAsync()).ToString().Should().Be(Source);
        workspace.TryApplyChanges(changed).Should().BeTrue();
        (await Actions(workspace.CurrentSolution.GetDocument(document.Id)!)).Should().BeEmpty();
    }

    [Fact]
    public async Task Additional_Document_Case_Collision_Should_Block_Offer()
    {
        using var workspace = new AdhocWorkspace();
        var document = Create(workspace);
        var changed = document.Project.Solution.AddAdditionalDocument(DocumentId.CreateNewId(document.Project.Id), "existing.md",
            SourceText.From("user plan"), filePath: "/source/Sample.Hosted/QA/orderscontroller.controller-review.md");
        workspace.TryApplyChanges(changed).Should().BeTrue();
        (await Actions(workspace.CurrentSolution.GetDocument(document.Id)!)).Should().BeEmpty();
    }

    [Fact]
    public async Task Stale_Preview_Should_Return_Current_Solution_Without_Plan()
    {
        using var workspace = new AdhocWorkspace();
        var document = Create(workspace);
        var action = (await Actions(document)).Single();
        workspace.TryApplyChanges(document.WithText(SourceText.From(Source + "\n// changed")).Project.Solution).Should().BeTrue();
        var changed = await action.GetChangedSolutionAsync();
        changed.Should().BeSameAs(workspace.CurrentSolution);
        changed.GetProject(document.Project.Id)!.AdditionalDocuments.Should().BeEmpty();
    }

    [Fact]
    public async Task Cancellation_Should_Leave_Workspace_Unchanged()
    {
        using var workspace = new AdhocWorkspace();
        var document = Create(workspace);
        var action = (await Actions(document)).Single();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var apply = async () => await action.GetOperationsAsync(cancellation.Token);
        await apply.Should().ThrowAsync<OperationCanceledException>();
        workspace.CurrentSolution.GetProject(document.Project.Id)!.AdditionalDocuments.Should().BeEmpty();
    }

    [Theory]
    [InlineData("/outside/OrdersController.cs")]
    [InlineData("/source/Sample.Hosted/QA/OrdersController.g.cs")]
    public async Task External_Or_Generated_Source_Should_Not_Offer_Plan(string path)
    {
        using var workspace = new AdhocWorkspace();
        (await Actions(Create(workspace, path))).Should().BeEmpty();
    }

    private static Document Create(AdhocWorkspace workspace, string path = "/source/Sample.Hosted/QA/OrdersController.cs")
    {
        var project = workspace.AddProject(ProjectInfo.Create(ProjectId.CreateNewId(), VersionStamp.Create(),
                "Sample.Hosted", "Sample.Hosted", LanguageNames.CSharp, filePath: "/source/Sample.Hosted/Sample.Hosted.csproj"))
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .AddMetadataReference(AnalyzerTestCompilation.ObjectReference);
        var document = project.AddDocument("OrdersController.cs", Source, filePath: path);
        workspace.TryApplyChanges(document.Project.Solution).Should().BeTrue();
        return workspace.CurrentSolution.GetDocument(document.Id)!;
    }

    private static async Task<List<CodeAction>> Actions(Document document)
    {
        var source = (await document.GetTextAsync()).ToString();
        var start = source.IndexOf("OrdersController :", StringComparison.Ordinal);
        var actions = new List<CodeAction>();
        await new ControllerReviewPlanRefactoringProvider().ComputeRefactoringsAsync(new CodeRefactoringContext(document,
            new TextSpan(start, "OrdersController".Length), actions.Add, CancellationToken.None));
        return actions;
    }
}
