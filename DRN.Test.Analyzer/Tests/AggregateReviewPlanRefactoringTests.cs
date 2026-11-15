using DRN.Framework.SharedKernel.CodeFixes;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.Text;

namespace DRN.Test.Analyzer.Tests;

public sealed class AggregateReviewPlanRefactoringTests
{
    private const string Source = "namespace Sample.Domain.Sales; public class Order : DRN.Framework.SharedKernel.Domain.AggregateRoot;";

    [Fact]
    public async Task Missing_Context_Should_Offer_Explicit_Proposed_Context_And_Tracked_Destinations_Only()
    {
        using var workspace = new AdhocWorkspace();
        var document = Create(workspace);
        var action = (await Actions(document, expectAction: true)).Should().ContainSingle().Which;
        action.Title.Should().Contain("plan only").And.Contain("Sample.Infra.Sales.SalesContext");
        var changed = (await action.GetOperationsAsync(CancellationToken.None)).OfType<ApplyChangesOperation>().Single().ChangedSolution;
        var delta = changed.GetChanges(workspace.CurrentSolution).GetProjectChanges().Should().ContainSingle().Which;
        delta.GetAddedDocuments().Should().BeEmpty();
        delta.GetChangedDocuments().Should().BeEmpty();
        delta.GetAddedAdditionalDocuments().Should().ContainSingle();
        var plan = changed.Projects.Single(project => project.Name == "Sample.Infra").AdditionalDocuments.Single();
        var content = (await plan.GetTextAsync()).ToString();
        content.Should().Contain("proposed new context, not created").And.Contain("Sales/SalesContext.cs")
            .And.Contain("Sales/Repositories").And.Contain("Sales/Migrations").And.Contain("No membership is inferred")
            .And.Contain("Missing identity is unresolved").And.Contain("atomic application is unsupported");
        content.Length.Should().BeLessThan(12000);
        (await changed.GetDocument(document.Id)!.GetTextAsync()).ToString().Should().Be(Source);
        workspace.TryApplyChanges(changed).Should().BeTrue();
        (await Actions(workspace.CurrentSolution.GetDocument(document.Id)!)).Should().BeEmpty();
    }

    [Fact]
    public async Task Multiple_Existing_Contexts_Should_Offer_Separate_Review_Choices()
    {
        using var workspace = new AdhocWorkspace();
        var document = Create(workspace, """
            namespace Sample.Infra.Sales;
            [DRN.Framework.SharedKernel.Attributes.SourceKnownSubdomain("Sales", Reason = "CQRS read context")]
            public class ReadContext : DRN.Framework.EntityFramework.Context.DrnContext<ReadContext>
            { public ReadContext() : base(null) { } }
            [DRN.Framework.SharedKernel.Attributes.SourceKnownSubdomain("Sales", Reason = "CQRS write context")]
            public class WriteContext : DRN.Framework.EntityFramework.Context.DrnContext<WriteContext>
            { public WriteContext() : base(null) { } }
            """);
        var actions = await Actions(document);
        actions.Should().HaveCount(2);
        actions.Select(action => action.Title).Should().Contain(title => title.Contains("ReadContext", StringComparison.Ordinal))
            .And.Contain(title => title.Contains("WriteContext", StringComparison.Ordinal));
        foreach (var action in actions)
        {
            var changed = (await action.GetOperationsAsync(CancellationToken.None)).OfType<ApplyChangesOperation>().Single().ChangedSolution;
            var text = (await changed.Projects.Single(project => project.Name == "Sample.Infra").AdditionalDocuments.Single().GetTextAsync()).ToString();
            text.Should().Contain("existing, preserve identity").And.Contain("Context source SHA-256").And.NotContain("proposed new context");
        }
    }

    [Fact]
    public async Task Technical_Context_Alias_Without_Reason_Should_Not_Be_Inferred()
    {
        using var workspace = new AdhocWorkspace();
        var document = Create(workspace, """
            namespace Sample.Infra.Sales;
            public class ReadContext : DRN.Framework.EntityFramework.Context.DrnContext<ReadContext>
            { public ReadContext() : base(null) { } }
            """);
        (await Actions(document)).Should().BeEmpty();
    }

    [Fact]
    public async Task Reasoned_Context_Input_Should_Enable_Exact_Alias_Choice()
    {
        using var workspace = new AdhocWorkspace();
        var document = Create(workspace, """
            namespace Sample.Infra.Sales;
            public class ReadContext : DRN.Framework.EntityFramework.Context.DrnContext<ReadContext>
            { public ReadContext() : base(null) { } }
            """);
        var infra = workspace.CurrentSolution.Projects.Single(project => project.Name == "Sample.Infra");
        var changed = workspace.CurrentSolution.AddAdditionalDocument(DocumentId.CreateNewId(infra.Id), "DrnConventionInputs.g.cs",
            SourceText.From("""
                [assembly: DRN.Conventions.Version("1")]
                [assembly: DRN.Conventions.Context("Sample.Infra.Sales.ReadContext", Subdomain = "Sales", Reason = "CQRS read context")]
                [assembly: DRN.Conventions.Context(("Sample.Infra.Sales." + "ReadContext"), Reason = ("CQRS " + "read context"), Subdomain = "Sales")]
                """));
        workspace.TryApplyChanges(changed).Should().BeTrue();
        (await Actions(workspace.CurrentSolution.GetDocument(document.Id)!)).Should().ContainSingle()
            .Which.Title.Should().Contain("Sample.Infra.Sales.ReadContext");
    }

    [Theory]
    [InlineData("namespace Sample.Infra.Sales.SalesContext { public class Detail; }")]
    [InlineData("namespace Sample.Infra { public class Sales; }")]
    public async Task Proposed_Context_Symbol_Collision_Should_Block_Markdown_Plan(string collision)
    {
        using var workspace = new AdhocWorkspace();
        var document = Create(workspace);
        var infra = workspace.CurrentSolution.Projects.Single(project => project.Name == "Sample.Infra");
        var changed = infra.AddDocument("Legacy.cs", collision).Project.Solution;
        workspace.TryApplyChanges(changed).Should().BeTrue();
        (await Actions(workspace.CurrentSolution.GetDocument(document.Id)!)).Should().BeEmpty();
    }

    [Fact]
    public async Task Proposed_Context_Path_Collision_Should_Block_Plan()
    {
        using var workspace = new AdhocWorkspace();
        var document = Create(workspace);
        var infra = workspace.CurrentSolution.Projects.Single(project => project.Name == "Sample.Infra");
        var solution = infra.AddDocument("collision.cs", "// user content", filePath: "/source/Sample.Infra/Sales/salescontext.cs").Project.Solution;
        workspace.TryApplyChanges(solution).Should().BeTrue();
        (await Actions(workspace.CurrentSolution.GetDocument(document.Id)!)).Should().BeEmpty();
    }

    [Fact]
    public async Task Nested_Context_Should_Retain_Its_Containing_Type_In_Plan_Identity()
    {
        using var workspace = new AdhocWorkspace();
        var document = Create(workspace, """
            namespace Sample.Infra.Sales;
            public class Container
            {
                public class SalesContext : DRN.Framework.EntityFramework.Context.DrnContext<SalesContext>
                { public SalesContext() : base(null) { } }
            }
            """);
        var action = (await Actions(document)).Should().ContainSingle().Which;
        action.Title.Should().Contain("Sample.Infra.Sales.Container.SalesContext");
        var changed = (await action.GetOperationsAsync(CancellationToken.None)).OfType<ApplyChangesOperation>().Single().ChangedSolution;
        var content = (await changed.Projects.Single(project => project.Name == "Sample.Infra").AdditionalDocuments.Single().GetTextAsync()).ToString();
        content.Should().Contain("Context: Sample.Infra.Sales.Container.SalesContext (existing, preserve identity and configuration)")
            .And.NotContain("Context: Sample.Infra.Sales.SalesContext");
    }

    [Fact]
    public async Task Context_Assembly_Ownership_Should_Not_Be_Replaced_By_Namespace_Inference()
    {
        using var workspace = new AdhocWorkspace();
        var document = Create(workspace, """
            [assembly: DRN.Framework.SharedKernel.Attributes.SourceKnownSubdomain(typeof(Sample.Infra.Sales.SalesContext), "Billing", Reason = "Legacy placement under separate owner.")]
            namespace Sample.Infra.Sales;
            public class SalesContext : DRN.Framework.EntityFramework.Context.DrnContext<SalesContext>
            { public SalesContext() : base(null) { } }
            """);
        (await Actions(document)).Should().BeEmpty();
    }

    [Fact]
    public async Task Duplicate_Input_Documents_Should_Not_Create_A_Review_Plan_From_Contradictory_Provenance()
    {
        using var workspace = new AdhocWorkspace();
        var document = Create(workspace);
        var solution = document.Project.Solution;
        foreach (var folder in new[] { "first", "second" })
            solution = solution.AddAdditionalDocument(DocumentId.CreateNewId(document.Project.Id), "DrnConventionInputs.g.cs",
                SourceText.From("[assembly: DRN.Conventions.Version(\"1\")]"), filePath: "/source/Sample.Domain/" + folder + "/DrnConventionInputs.g.cs");
        workspace.TryApplyChanges(solution).Should().BeTrue();
        (await Actions(workspace.CurrentSolution.GetDocument(document.Id)!)).Should().BeEmpty();
    }

    [Fact]
    public async Task Explicit_Ownership_Input_Should_Not_Be_Overridden_By_Proposed_Namespace_Owner()
    {
        using var workspace = new AdhocWorkspace();
        var document = Create(workspace);
        var changed = document.Project.Solution.AddAdditionalDocument(DocumentId.CreateNewId(document.Project.Id), "DrnConventionInputs.g.cs",
            SourceText.From("[assembly: DRN.Conventions.Version(\"1\")]\n[assembly: DRN.Conventions.Ownership(\"Sample.Domain.Sales.Order\", Subdomain = \"Billing\")]"));
        workspace.TryApplyChanges(changed).Should().BeTrue();
        (await Actions(workspace.CurrentSolution.GetDocument(document.Id)!)).Should().BeEmpty();
    }

    [Fact]
    public async Task Selected_Ownership_Input_Should_Be_Escaped_In_Review_Plan()
    {
        using var workspace = new AdhocWorkspace();
        var document = Create(workspace, """
            namespace Sample.Infra.Sales;
            public class SalesContext : DRN.Framework.EntityFramework.Context.DrnContext<SalesContext>
            { public SalesContext() : base(null) { } }
            """);
        var changed = document.Project.Solution.AddAdditionalDocument(DocumentId.CreateNewId(document.Project.Id), "DrnConventionInputs.g.cs",
            SourceText.From("""
                [assembly: DRN.Conventions.Version("1")]
                [assembly: DRN.Conventions.Context("Sample.Infra.Sales.SalesContext", Subdomain = "Sales", Reason = "<script>alert(1)</script> [link](https://example.invalid) `code`")]
                """));
        workspace.TryApplyChanges(changed).Should().BeTrue();
        var action = (await Actions(workspace.CurrentSolution.GetDocument(document.Id)!)).Single();
        var result = (await action.GetOperationsAsync(CancellationToken.None)).OfType<ApplyChangesOperation>().Single().ChangedSolution;
        var text = (await result.Projects.Single(project => project.Name == "Sample.Infra").AdditionalDocuments.Single().GetTextAsync()).ToString();
        text.Should().Contain("&lt;script&gt;").And.Contain("&#91;link&#93;").And.Contain("&#96;code&#96;").And.NotContain("<script>");
    }

    [Theory]
    [InlineData("Sample.Domain")]
    [InlineData("Sample.Infra")]
    public async Task Large_Valid_Inventory_Should_Produce_A_Compact_Fingerprinted_Plan(string projectName)
    {
        using var workspace = new AdhocWorkspace();
        var document = Create(workspace);
        var project = workspace.CurrentSolution.Projects.Single(item => item.Name == projectName);
        var inventory = "[assembly: DRN.Conventions.Version(\"1\")]\n[assembly: DRN.Conventions.Reference(\"Inventory\", Stage = \"Complete\")]\n" +
            string.Join("\n", Enumerable.Range(0, 128).Select(index =>
            "[assembly: DRN.Conventions.Reference(\"/references/InventoryOnly" + index + ".dll\", Stage = \"Resolved\")]"));
        inventory.Length.Should().BeGreaterThan(8000).And.BeLessThan(1_048_576);
        var changed = project.Solution.AddAdditionalDocument(DocumentId.CreateNewId(project.Id), "DrnConventionInputs.g.cs", SourceText.From(inventory));
        workspace.TryApplyChanges(changed).Should().BeTrue();
        var action = (await Actions(workspace.CurrentSolution.GetDocument(document.Id)!)).Should().ContainSingle().Which;
        var result = (await action.GetOperationsAsync(CancellationToken.None)).OfType<ApplyChangesOperation>().Single().ChangedSolution;
        var plan = result.Projects.Single(item => item.Name == "Sample.Infra").AdditionalDocuments.Single(item => item.Name.EndsWith(".aggregate-review.md", StringComparison.Ordinal));
        var text = (await plan.GetTextAsync()).ToString();
        var fingerprint = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(inventory)));
        text.Should().Contain("Supplied inputs for " + projectName + "; SHA-256 " + fingerprint)
            .And.Contain("records: 130").And.NotContain("InventoryOnly");
        text.Length.Should().BeLessThan(12000);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Inventory_Exceeding_Analyzer_Size_Or_Record_Bound_Should_Decline_Plan(bool exceedCharacters)
    {
        using var workspace = new AdhocWorkspace();
        var document = Create(workspace);
        var inventory = "[assembly: DRN.Conventions.Version(\"1\")]\n" + (exceedCharacters
            ? "// " + new string('x', 1_048_576)
            : string.Concat(Enumerable.Repeat("[assembly: DRN.Conventions.Path(\"x\", LogicalPath = \"x\")]\n", 16_384)));
        var changed = document.Project.Solution.AddAdditionalDocument(DocumentId.CreateNewId(document.Project.Id), "DrnConventionInputs.g.cs", SourceText.From(inventory));
        workspace.TryApplyChanges(changed).Should().BeTrue();
        (await Actions(workspace.CurrentSolution.GetDocument(document.Id)!)).Should().BeEmpty();
    }

    [Fact]
    public async Task Changed_Project_Input_Should_Invalidate_Preview()
    {
        using var workspace = new AdhocWorkspace();
        var document = Create(workspace);
        var action = (await Actions(document)).Single();
        var solution = document.Project.Solution.AddAdditionalDocument(DocumentId.CreateNewId(document.Project.Id), "notes.md", SourceText.From("changed"));
        workspace.TryApplyChanges(solution).Should().BeTrue();
        var changed = (await action.GetOperationsAsync(CancellationToken.None)).OfType<ApplyChangesOperation>().Single().ChangedSolution;
        changed.Should().BeSameAs(workspace.CurrentSolution);
        changed.Projects.Single(project => project.Name == "Sample.Infra").AdditionalDocuments.Should().BeEmpty();
    }

    [Fact]
    public async Task Cancellation_Should_Not_Add_Plan()
    {
        using var workspace = new AdhocWorkspace();
        var document = Create(workspace);
        var action = (await Actions(document, expectAction: true)).Should().ContainSingle().Which;
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var apply = async () => await action.GetOperationsAsync(cancellation.Token);
        await apply.Should().ThrowAsync<OperationCanceledException>();
        workspace.CurrentSolution.Projects.SelectMany(project => project.AdditionalDocuments).Should().BeEmpty();
    }

    [Theory]
    [InlineData("Storage.Infra")]
    [InlineData("Infra")]
    public async Task Referencing_Infrastructure_Should_Use_Its_Own_Namespace_For_Proposed_Context(string infraName)
    {
        using var workspace = new AdhocWorkspace();
        var document = Create(workspace, infraName: infraName);
        var action = (await Actions(document)).Should().ContainSingle().Which;
        action.Title.Should().Contain(infraName + ".Sales.SalesContext");
        var changed = (await action.GetOperationsAsync(CancellationToken.None)).OfType<ApplyChangesOperation>().Single().ChangedSolution;
        var plan = changed.Projects.Single(project => project.Name == infraName).AdditionalDocuments.Single();
        (await plan.GetTextAsync()).ToString().Should().Contain("Context: " + infraName + ".Sales.SalesContext")
            .And.NotContain("Context: Sample.Infra.");
        changed.GetChanges(workspace.CurrentSolution).GetProjectChanges().Should()
            .OnlyContain(change => !change.GetAddedProjectReferences().Any());
    }

    [Fact]
    public async Task Unreferenced_Infrastructure_Should_Not_Be_Proposed_From_A_Matching_Name()
    {
        using var workspace = new AdhocWorkspace();
        (await Actions(Create(workspace, reference: false))).Should().BeEmpty();
    }

    private static Document Create(AdhocWorkspace workspace, string contexts = "", string infraName = "Sample.Infra", bool reference = true)
    {
        var domain = workspace.AddProject(ProjectInfo.Create(ProjectId.CreateNewId(), VersionStamp.Create(),
                "Sample.Domain", "Sample.Domain", LanguageNames.CSharp, filePath: "/source/Sample.Domain/Sample.Domain.csproj"))
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .AddMetadataReferences(DiagnosticClassificationAnalyzerTests.References);
        var document = domain.AddDocument("Order.cs", Source, filePath: "/source/Sample.Domain/Sales/Order.cs");
        workspace.TryApplyChanges(document.Project.Solution).Should().BeTrue();
        var infra = workspace.AddProject(ProjectInfo.Create(ProjectId.CreateNewId(), VersionStamp.Create(),
                infraName, infraName, LanguageNames.CSharp, filePath: "/source/" + infraName + "/" + infraName + ".csproj"))
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .AddMetadataReferences(DiagnosticClassificationAnalyzerTests.References)
            .AddMetadataReference(MetadataReference.CreateFromFile(typeof(Framework.EntityFramework.Context.IDrnContext).Assembly.Location));
        if (reference) infra = infra.AddProjectReference(new ProjectReference(domain.Id));
        if (contexts.Length > 0) infra = infra.AddDocument("Contexts.cs", contexts, filePath: "/source/" + infraName + "/Sales/Contexts.cs").Project;
        workspace.TryApplyChanges(infra.Solution).Should().BeTrue();
        return workspace.CurrentSolution.GetDocument(document.Id)!;
    }

    private static async Task<List<CodeAction>> Actions(Document document, bool expectAction = false)
    {
        var source = (await document.GetTextAsync()).ToString();
        var start = source.IndexOf("Order :", StringComparison.Ordinal);
        var snapshot = document.Project.Solution;
        var evidence = new List<string>();
        if (expectAction)
        {
            start.Should().BeGreaterThanOrEqualTo(0, "the fixture must select the Order identifier");
            snapshot.Workspace.CanApplyChange(ApplyChangesKind.AddAdditionalDocument).Should().BeTrue(
                "the review plan requires additional-document support");
            snapshot.Should().BeSameAs(snapshot.Workspace.CurrentSolution, "the preview must use the current solution");
            document.GetLinkedDocumentIds().Should().BeEmpty("the selected source must not be linked");
            foreach (var project in snapshot.Projects)
            {
                project.FilePath.Should().NotBeNullOrEmpty("project {0} needs a path to locate plan destinations", project.Name);
                var compilation = await project.GetCompilationAsync();
                compilation.Should().NotBeNull("project {0} must support compilation", project.Name);
                compilation.GetDiagnostics().Where(item => item.Severity == DiagnosticSeverity.Error)
                    .Should().BeEmpty("project {0} must compile before a plan can be offered", project.Name);
                var requiredType = project.Id == document.Project.Id
                    ? "DRN.Framework.SharedKernel.Domain.AggregateRoot"
                    : "DRN.Framework.EntityFramework.Context.IDrnContext";
                compilation.GetTypeByMetadataName(requiredType).Should().NotBeNull(
                    "project {0} must resolve {1} unambiguously", project.Name, requiredType);
                project.AnalyzerOptions.AnalyzerConfigOptionsProvider.GlobalOptions.TryGetValue("build_property.RootNamespace", out var rootNamespace);
                evidence.Add($"Project={project.Name}; Path={project.FilePath}; RootNamespace={rootNamespace ?? "<unset>"}; " +
                    $"ReferencesDomain={project.ProjectReferences.Any(reference => reference.ProjectId == document.Project.Id)}; " +
                    $"AdditionalFiles={string.Join(", ", project.AdditionalDocuments.Select(item => item.Name))}");
            }
        }
        var actions = new List<CodeAction>();
        await new AggregateReviewPlanRefactoringProvider().ComputeRefactoringsAsync(new CodeRefactoringContext(document,
            new TextSpan(start, "Order".Length), actions.Add, CancellationToken.None));
        if (expectAction)
        {
            snapshot.Should().BeSameAs(snapshot.Workspace.CurrentSolution, "the workspace must remain unchanged during discovery");
            actions.Should().NotBeEmpty("the valid fixture should offer a plan. Source={0}; Selection={1}; {2}",
                document.FilePath, start, string.Join(Environment.NewLine, evidence));
        }
        return actions;
    }
}
