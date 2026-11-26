using System.Diagnostics.CodeAnalysis;
using DRN.Framework.SharedKernel.CodeFixes;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.Text;
using static DRN.Test.Analyzer.Tests.AnalyzerTestCompilation;

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
    [DataInline("System.Threading.Tasks.Task<int> CountVisibleAsync(string filter);")]
    [DataInline("int CountVisible(string filter = \"default\");")]
    [DataInline("int CountVisible(ref string filter);")]
    [DataInline("int CountVisible<T>(T filter);")]
    [DataInline("[System.Obsolete] int CountVisible(string filter);")]
    [DataInline("int CountVisible(params string[] filters);")]
    [DataInline("int _repository();")]
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
        var source = (await document.GetTextAsync()).ToString().Replace(
            " : DRN.Framework.SharedKernel.Domain.AggregateRoot { public override DRN.Framework.SharedKernel.Domain.Dto ToDto() => new(this); }",
            ";", StringComparison.Ordinal);
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
    [DataInline("Sample.Application")]
    [DataInline("Other.Application")]
    public async Task Absent_Edge_Should_Not_Create_New_Dependencies(string target)
    {
        using var workspace = new AdhocWorkspace();
        (await Actions(Create(workspace, reference: false, targetName: target))).Should().BeEmpty();
    }

    [Theory]
    [DataInline("Other.Application")]
    [DataInline("Application")]
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
            SourcePath("Sample.Application", "Services", "QA", "Orders", "ordercountvisibleservice.cs"));
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
    [DataInline("namespace Sample.Application.Services.QA.Orders.OrderCountVisibleService { public class Detail; }")]
    [DataInline("namespace Sample.Application { public class Services; }")]
    [DataInline("namespace Sample.Application.Services { public class QA; }")]
    [DataInline("namespace Sample.Application.Services.QA { public class Orders; }")]
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
    [DataInline("Sample.Domain", "[module: DRN.Conventions.Version(\"1\")]")]
    [DataInline("Sample.Application", "[assembly: DRN.Conventions.Version(\"2\")]")]
    [DataInline("Sample.Application", "[assembly: DRN.Conventions.Version(\"1\")][assembly: Unknown.Record(\"x\")]")]
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
                "Sample.Domain", "Sample.Domain", LanguageNames.CSharp, filePath: SourcePath("Sample.Domain/Sample.Domain.csproj")))
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .AddMetadataReferences(ClassificationTestFixture.References);
        var document = domain.AddDocument("IOrderRepository.cs", $$"""
            namespace Sample.Domain.QA.Order;
            public class OrderEntity : DRN.Framework.SharedKernel.Domain.AggregateRoot { public override DRN.Framework.SharedKernel.Domain.Dto ToDto() => new(this); }
            public interface IOrderRepository : DRN.Framework.SharedKernel.Domain.Repository.ISourceKnownRepository<OrderEntity>
            {
                // select-start
                {{Method}}
                // select-end
            }
            """, filePath: SourcePath("Sample.Domain/QA/Order/IOrderRepository.cs"));
        workspace.TryApplyChanges(document.Project.Solution).Should().BeTrue();
        var application = workspace.AddProject(ProjectInfo.Create(ProjectId.CreateNewId(), VersionStamp.Create(),
                "Sample.Application", "Sample.Application", LanguageNames.CSharp, filePath: SourcePath("Sample.Application/Sample.Application.csproj")))
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
            """, filePath: SourcePath("Sample.Application/ApplicationModule.cs")).Project;
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
        var domainReference = AnalyzerTestCompilation.EmitReference(domainCompilation, SourcePath("Sample.Domain/Sample.Domain.dll"), AnalyzerTestCompilation.ReferenceAssemblyOptions);
        var compilation = (await changedApp.GetCompilationAsync())!;
        var appCompilation = compilation.RemoveReferences(compilation.References.OfType<CompilationReference>()).AddReferences(domainReference);
        var inputs = new ProjectDiagnosticRulesTests.RuleFile(Framework.SharedKernel.Analyzers.Conventions.ConventionInputs.FileName, $"""
            [assembly: DRN.Conventions.Version("1")]
            [assembly: DRN.Conventions.Reference("Inventory", Stage = "Complete")]
            [assembly: DRN.Conventions.Reference({SourcePathLiteral("Sample.Domain/Sample.Domain.csproj")}, Stage = "Project", Source = {SourcePathLiteral("Sample.Domain/Sample.Domain.csproj")})]
            [assembly: DRN.Conventions.Reference({SourcePathLiteral("Sample.Domain/Sample.Domain.dll")}, Stage = "Resolved", Project = {SourcePathLiteral("Sample.Domain/Sample.Domain.csproj")})]
            [assembly: DRN.Conventions.Path({SourcePathLiteral("Sample.Application/ApplicationModule.cs")}, LogicalPath = "ApplicationModule.cs")]
            [assembly: DRN.Conventions.Path({SourcePathLiteral("Sample.Application/Services/QA/Order/OrderCountVisibleService.cs")}, LogicalPath = "Services/QA/Order/OrderCountVisibleService.cs")]
            """);
        var diagnostics = await AnalyzerTestCompilation.Analyze(appCompilation,
            new Framework.SharedKernel.Analyzers.Conventions.SourceKnownConventionAnalyzer(),
            AnalyzerTestCompilation.ConventionOptions("Sample.Application", files: inputs));
        diagnostics.Should().BeEmpty();
    }

    [Theory]
    [DataInlineUnit("Sample.Application", true)]
    [DataInlineUnit("Other.Application", false)]
    public async Task Explicit_Domain_Attribute_Should_Enforce_Cross_Layer_Domain_Match(string targetName, bool offered)
    {
        using var workspace = new AdhocWorkspace();
        var document = Create(workspace, targetName: targetName);
        var source = (await document.GetTextAsync()).ToString().Replace(
            "public interface IOrderRepository",
            "[DRN.Framework.SharedKernel.Attributes.SourceKnownSubdomain(\"QA\", Domain = \"Sample\")]\npublic interface IOrderRepository",
            StringComparison.Ordinal);
        workspace.TryApplyChanges(document.WithText(SourceText.From(source)).Project.Solution).Should().BeTrue();
        var actions = await Actions(workspace.CurrentSolution.GetDocument(document.Id)!);
        if (offered) actions.Should().HaveCount(2);
        else actions.Should().BeEmpty();
    }

    [Theory]
    [DataInlineUnit(true, "Sample.Domain", "Billing.Application", "Sample.Domain.Billing.QA.Order", "Billing/QA/Order/IOrderRepository.cs", true, "Billing.Application.Services.QA.Order", "Billing.Application/Services/QA/Order/OrderCountVisibleService.cs")]
    [DataInlineUnit(true, "Sample.Domain", "Sales.Application", "Sample.Domain.Billing.QA.Order", "Billing/QA/Order/IOrderRepository.cs", false, "", "")]
    [DataInlineUnit(false, "Billing.Domain", "Sample.Application", "Billing.Domain.QA.Order", "QA/Order/IOrderRepository.cs", true, "Sample.Application.Services.Billing.QA.Order", "Sample.Application/Services/Billing/QA/Order/OrderCountVisibleService.cs")]
    public async Task Cross_Layout_Solutions_Should_Emit_Target_Layout_Namespace_And_Reject_Domain_Mismatch(
        bool domainFolders, string domainName, string appName, string repoNamespace, string repoPath,
        bool offered, string expectedNamespace, string expectedRelativePath)
    {
        using var workspace = new AdhocWorkspace();
        var layoutConfig = SourceText.From("is_global = true\nbuild_property.DrnDomainLayout = Folders\n");
        var domain = workspace.AddProject(ProjectInfo.Create(ProjectId.CreateNewId(), VersionStamp.Create(),
                domainName, domainName, LanguageNames.CSharp, filePath: SourcePath(domainName, domainName + ".csproj")))
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .AddMetadataReferences(ClassificationTestFixture.References);
        if (domainFolders)
            domain = domain.AddAnalyzerConfigDocument("layout.globalconfig", layoutConfig,
                filePath: SourcePath(domainName, "layout.globalconfig")).Project;
        var document = domain.AddDocument("IOrderRepository.cs", $$"""
            namespace {{repoNamespace}};
            public class OrderEntity : DRN.Framework.SharedKernel.Domain.AggregateRoot { public override DRN.Framework.SharedKernel.Domain.Dto ToDto() => new(this); }
            public interface IOrderRepository : DRN.Framework.SharedKernel.Domain.Repository.ISourceKnownRepository<OrderEntity>
            {
                // select-start
                {{Method}}
                // select-end
            }
            """, filePath: SourcePath([domainName, .. repoPath.Split('/')]));
        workspace.TryApplyChanges(document.Project.Solution).Should().BeTrue();

        var application = workspace.AddProject(ProjectInfo.Create(ProjectId.CreateNewId(), VersionStamp.Create(),
                appName, appName, LanguageNames.CSharp, filePath: SourcePath(appName, appName + ".csproj")))
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .AddMetadataReferences(ClassificationTestFixture.References)
            .AddMetadataReference(AnalyzerTestCompilation.UtilsReference)
            .AddProjectReference(new ProjectReference(domain.Id));
        if (!domainFolders)
            application = application.AddAnalyzerConfigDocument("layout.globalconfig", layoutConfig,
                filePath: SourcePath(appName, "layout.globalconfig")).Project;
        workspace.TryApplyChanges(application.Solution).Should().BeTrue();

        var actions = await Actions(workspace.CurrentSolution.GetDocument(document.Id)!);
        if (!offered)
        {
            actions.Should().BeEmpty();
            return;
        }
        var action = actions.Single(item => item.Title.StartsWith("Add scoped", StringComparison.Ordinal));
        var changed = await action.GetChangedSolutionAsync();
        var changedApp = changed.Projects.Single(project => project.Name == appName);
        var service = changedApp.Documents.Single();
        service.FilePath.Should().Be(SourcePath(expectedRelativePath.Split('/')));
        (await service.GetTextAsync()).ToString().Should().Contain("namespace " + expectedNamespace + ";");
        (await changedApp.GetCompilationAsync())!.AssertNoCompilerErrors();
    }

    [Theory]
    [DataInlineUnit(false, false, "Sample.Domain", "Sample.Application", "Sample.Domain.QA.Order", "QA/Order/IOrderRepository.cs", "", true)]
    [DataInlineUnit(false, false, "Sample.Domain", "Sample.Application", "Sample.Domain.QA.Order", "QA/Order/IOrderRepository.cs", """
        [assembly: System.Reflection.AssemblyMetadata("DRN.Conventions.Role.Domain", "Sample.Domain")]
        """, false)]
    [DataInlineUnit(false, false, "Sample.Domain", "Sample.Application", "Sample.Domain.QA.Order", "QA/Order/IOrderRepository.cs", """
        [assembly: System.Reflection.AssemblyMetadata("DRN.Conventions.Domain", "Other")]
        [assembly: System.Reflection.AssemblyMetadata("DRN.Conventions.Domain", "Sample")]
        """, false)]
    [DataInlineUnit(false, false, "Sample.Domain", "Sample.Application", "Sample.Domain.QA.Order", "QA/Order/IOrderRepository.cs", """
        [assembly: System.Reflection.AssemblyMetadata("DRN.Conventions.Ownership.Sample.Domain.QA.Order.OrderEntity.Subdomain", "Billing")]
        """, false)]
    [DataInlineUnit(false, false, "Sample.Domain", "Sample.Application", "Sample.Domain.QA.Order", "QA/Order/IOrderRepository.cs", """
        [assembly: System.Reflection.AssemblyMetadata("DRN.Conventions.Ownership.Sample.Domain.QA.Order.IOrderRepository.Subdomain", "Billing")]
        """, false)]
    [DataInlineUnit(false, false, "Sample.Domain", "Other.Application", "Sample.Domain.QA.Order", "QA/Order/IOrderRepository.cs", """
        [assembly: System.Reflection.AssemblyMetadata("DRN.Conventions.Ownership.Sample.Domain.QA.Order.OrderEntity.Domain", "Sample")]
        [assembly: System.Reflection.AssemblyMetadata("DRN.Conventions.Ownership.Sample.Domain.QA.Order.OrderEntity.Subdomain", "QA")]
        """, false)]
    [DataInlineUnit(true, false, "Sample.Domain", "Billing.Application", "Sample.Domain.Billing.QA.Order", "Billing/QA/Order/IOrderRepository.cs", """
        [assembly: System.Reflection.AssemblyMetadata("DRN.Conventions.Ownership.Sample.Domain.Billing.QA.Order.OrderEntity.Domain", "Billing")]
        [assembly: System.Reflection.AssemblyMetadata("DRN.Conventions.Ownership.Sample.Domain.Billing.QA.Order.OrderEntity.Subdomain", "QA")]
        [assembly: System.Reflection.AssemblyMetadata("DRN.Conventions.Ownership.Sample.Domain.Billing.QA.Order.IOrderRepository.Domain", "Billing")]
        [assembly: System.Reflection.AssemblyMetadata("DRN.Conventions.Ownership.Sample.Domain.Billing.QA.Order.IOrderRepository.Subdomain", "QA")]
        """, true)]
    [DataInlineUnit(false, true, "Billing.Domain", "Sample.Application", "Billing.Domain.QA.Order", "QA/Order/IOrderRepository.cs", """
        [assembly: System.Reflection.AssemblyMetadata("DRN.Conventions.Ownership.Billing.Domain.QA.Order.OrderEntity.Domain", "Billing")]
        [assembly: System.Reflection.AssemblyMetadata("DRN.Conventions.Ownership.Billing.Domain.QA.Order.OrderEntity.Subdomain", "QA")]
        [assembly: System.Reflection.AssemblyMetadata("DRN.Conventions.Ownership.Billing.Domain.QA.Order.IOrderRepository.Domain", "Billing")]
        [assembly: System.Reflection.AssemblyMetadata("DRN.Conventions.Ownership.Billing.Domain.QA.Order.IOrderRepository.Subdomain", "QA")]
        """, true)]
    [DataInlineUnit(true, false, "Sample.Domain", "Billing.Application", "Sample.Domain.Billing.QA.Order", "Billing/QA/Order/IOrderRepository.cs", """
        [assembly: System.Reflection.AssemblyMetadata("DRN.Conventions.Ownership.Sample.Domain.Billing.QA.Order.OrderEntity.Domain", "Billing")]
        [assembly: System.Reflection.AssemblyMetadata("DRN.Conventions.Ownership.Sample.Domain.Billing.QA.Order.OrderEntity.Subdomain", "QA")]
        [assembly: System.Reflection.AssemblyMetadata("DRN.Conventions.Ownership.Sample.Domain.Billing.QA.Order.IOrderRepository.Domain", "Sales")]
        [assembly: System.Reflection.AssemblyMetadata("DRN.Conventions.Ownership.Sample.Domain.Billing.QA.Order.IOrderRepository.Subdomain", "QA")]
        """, false)]
    [DataInlineUnit(true, false, "Sample.Domain", "Billing.Application", "Sample.Domain.Billing.QA.Order", "Billing/QA/Order/IOrderRepository.cs", """
        [assembly: System.Reflection.AssemblyMetadata("DRN.Conventions.DomainLayout", "Folders")]
        """, true)]
    [DataInlineUnit(true, false, "Sample.Domain", "Billing.Application", "Sample.Domain.Billing.QA.Order", "Billing/QA/Order/IOrderRepository.cs", """
        [assembly: System.Reflection.AssemblyMetadata("DRN.Conventions.DomainLayout", "Project")]
        """, false)]
    [DataInlineUnit(false, false, "Sample.Domain", "Sample.Application", "Sample.Domain.QA.Order", "QA/Order/IOrderRepository.cs", """
        [assembly: System.Reflection.AssemblyMetadata("DRN.Conventions.DomainLayout", "Folders")]
        """, false)]
    [DataInlineUnit(true, false, "Sample.Domain", "Billing.Application", "Sample.Domain.Billing.QA.Order", "Billing/QA/Order/IOrderRepository.cs", """
        [assembly: System.Reflection.AssemblyMetadata("DRN.Conventions.Domain", "Billing")]
        """, false)]
    [DataInlineUnit(false, false, "Sample.Domain", "Sample.Application", "Sample.Domain.QA.Order", "QA/Order/IOrderRepository.cs", """
        [assembly: System.Reflection.AssemblyMetadata("DRN.Conventions.ProjectName", "Domain.Infra")]
        """, false)]
    [DataInlineUnit(false, false, "Sample.Domain", "Sample.Application", "Sample.Domain.QA.Order", "QA/Order/IOrderRepository.cs", """
        [assembly: System.Reflection.AssemblyMetadata("DRN.Conventions.ProjectName", "Other.Domain")]
        """, false)]
    [DataInlineUnit(false, false, "Sample.Domain", "Sample.Application", "Sample.Domain.QA.Order", "QA/Order/IOrderRepository.cs", """
        [assembly: System.Reflection.AssemblyMetadata("DRN.Conventions.RootNamespace", "Other.Domain")]
        """, false)]
    [DataInlineUnit(false, false, "Sample.Domain", "Sample.Application", "Sample.Domain.QA.Order", "QA/Order/IOrderRepository.cs", """
        [assembly: System.Reflection.AssemblyMetadata("DRN.Conventions.ProjectName", "Sample.Domain")]
        [assembly: System.Reflection.AssemblyMetadata("DRN.Conventions.RootNamespace", "Sample.Domain")]
        """, true)]
    [DataInlineUnit(false, false, "Sample.Domain", "Sample.Application", "Sample.Domain.QA.Order", "QA/Order/IOrderRepository.cs", """
        [assembly: System.Reflection.AssemblyMetadata("DRN.Conventions.Domain", "Sample")]
        """, true)]
    [DataInlineUnit(false, false, "Sample.Domain", "Other.Application", "Sample.Domain.QA.Order", "QA/Order/IOrderRepository.cs", """
        [assembly: System.Reflection.AssemblyMetadata("DRN.Conventions.Domain", "Sample")]
        """, false)]
    [DataInlineUnit(false, false, "Sample.Domain", "Other.Application", "Sample.Domain.QA.Order", "QA/Order/IOrderRepository.cs", """
        [assembly: System.Reflection.AssemblyMetadata("DRN.Conventions.Domain", "Sample")]
        [assembly: System.Reflection.AssemblyMetadata("DRN.Conventions.Ownership.Sample.Domain.QA.Order.OrderEntity.Subdomain", "QA")]
        [assembly: System.Reflection.AssemblyMetadata("DRN.Conventions.Ownership.Sample.Domain.QA.Order.IOrderRepository.Subdomain", "QA")]
        """, false)]
    public async Task Producer_Ownership_Metadata_Should_Validate_Entity_And_Repository_Records_Across_Layouts(
        bool domainFolders, bool appFolders, string domainName, string appName, string repoNamespace, string repoPath,
        string metadataAttributes, bool offered)
    {
        using var workspace = new AdhocWorkspace();
        var layoutConfig = SourceText.From("is_global = true\nbuild_property.DrnDomainLayout = Folders\n");
        var domain = workspace.AddProject(ProjectInfo.Create(ProjectId.CreateNewId(), VersionStamp.Create(),
                domainName, domainName, LanguageNames.CSharp, filePath: SourcePath(domainName, domainName + ".csproj")))
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .AddMetadataReferences(ClassificationTestFixture.References);
        if (domainFolders)
            domain = domain.AddAnalyzerConfigDocument("layout.globalconfig", layoutConfig,
                filePath: SourcePath(domainName, "layout.globalconfig")).Project;
        var document = domain.AddDocument("IOrderRepository.cs", $$"""
            {{metadataAttributes}}
            namespace {{repoNamespace}};
            public class OrderEntity : DRN.Framework.SharedKernel.Domain.AggregateRoot { public override DRN.Framework.SharedKernel.Domain.Dto ToDto() => new(this); }
            public interface IOrderRepository : DRN.Framework.SharedKernel.Domain.Repository.ISourceKnownRepository<OrderEntity>
            {
                // select-start
                {{Method}}
                // select-end
            }
            """, filePath: SourcePath([domainName, .. repoPath.Split('/')]));
        workspace.TryApplyChanges(document.Project.Solution).Should().BeTrue();

        var application = workspace.AddProject(ProjectInfo.Create(ProjectId.CreateNewId(), VersionStamp.Create(),
                appName, appName, LanguageNames.CSharp, filePath: SourcePath(appName, appName + ".csproj")))
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .AddMetadataReferences(ClassificationTestFixture.References)
            .AddMetadataReference(AnalyzerTestCompilation.UtilsReference)
            .AddProjectReference(new ProjectReference(domain.Id));
        if (appFolders)
            application = application.AddAnalyzerConfigDocument("layout.globalconfig", layoutConfig,
                filePath: SourcePath(appName, "layout.globalconfig")).Project;
        workspace.TryApplyChanges(application.Solution).Should().BeTrue();

        (await workspace.CurrentSolution.GetProject(domain.Id)!.GetCompilationAsync())!.AssertNoCompilerErrors();
        var actions = await Actions(workspace.CurrentSolution.GetDocument(document.Id)!);
        if (offered) actions.Should().HaveCount(2);
        else actions.Should().BeEmpty();
    }

    [Theory]
    [DataInlineUnit("build_property.DrnDomainLayout = Folders", "Billing.Application", "Domain.Billing.QA.Order", "Billing/QA/Order/IOrderRepository.cs", "", true)]
    [DataInlineUnit("build_property.DrnDomain = Sales", "Sales.Application", "Domain.QA.Order", "QA/Order/IOrderRepository.cs", "", true)]
    [DataInlineUnit("build_property.DrnDomain = Sales", "Other.Application", "Domain.QA.Order", "QA/Order/IOrderRepository.cs", "", false)]
    [DataInlineUnit("build_property.DrnDomainLayout = Folders", "Billing.Application", "Domain.Billing.QA.Order", "Billing/QA/Order/IOrderRepository.cs", """
        [assembly: System.Reflection.AssemblyMetadata("DRN.Conventions.ProjectName", "Domain")]
        """, false)]
    [DataInlineUnit("build_property.DrnDomainLayout = Folders", "Billing.Application", "Domain.Billing.QA.Order", "Billing/QA/Order/IOrderRepository.cs", """
        [assembly: System.Reflection.AssemblyMetadata("DRN.Conventions.ProjectName", "Domain.Infra")]
        """, false)]
    public async Task Bare_Domain_Project_Should_Allow_Absent_Metadata_Fallback_And_Reject_Invalid_Explicit_Identity(
        string configLine, string appName, string repoNamespace, string repoPath, string metadataAttributes, bool offered)
    {
        using var workspace = new AdhocWorkspace();
        var domain = workspace.AddProject(ProjectInfo.Create(ProjectId.CreateNewId(), VersionStamp.Create(),
                "Domain", "Domain", LanguageNames.CSharp, filePath: SourcePath("Domain", "Domain.csproj")))
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .AddMetadataReferences(ClassificationTestFixture.References)
            .AddAnalyzerConfigDocument("layout.globalconfig", SourceText.From("is_global = true\n" + configLine + "\n"),
                filePath: SourcePath("Domain", "layout.globalconfig")).Project;
        var document = domain.AddDocument("IOrderRepository.cs", $$"""
            {{metadataAttributes}}
            namespace {{repoNamespace}};
            public class OrderEntity : DRN.Framework.SharedKernel.Domain.AggregateRoot { public override DRN.Framework.SharedKernel.Domain.Dto ToDto() => new(this); }
            public interface IOrderRepository : DRN.Framework.SharedKernel.Domain.Repository.ISourceKnownRepository<OrderEntity>
            {
                // select-start
                {{Method}}
                // select-end
            }
            """, filePath: SourcePath(["Domain", .. repoPath.Split('/')]));
        workspace.TryApplyChanges(document.Project.Solution).Should().BeTrue();

        var application = workspace.AddProject(ProjectInfo.Create(ProjectId.CreateNewId(), VersionStamp.Create(),
                appName, appName, LanguageNames.CSharp, filePath: SourcePath(appName, appName + ".csproj")))
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .AddMetadataReferences(ClassificationTestFixture.References)
            .AddMetadataReference(AnalyzerTestCompilation.UtilsReference)
            .AddProjectReference(new ProjectReference(domain.Id));
        workspace.TryApplyChanges(application.Solution).Should().BeTrue();

        (await workspace.CurrentSolution.GetProject(domain.Id)!.GetCompilationAsync())!.AssertNoCompilerErrors();
        var actions = await Actions(workspace.CurrentSolution.GetDocument(document.Id)!);
        if (offered) actions.Should().HaveCount(2);
        else actions.Should().BeEmpty();
    }

    [Theory]
    [DataInlineUnit("", true)]
    [DataInlineUnit("""
        [assembly: System.Reflection.AssemblyMetadata("DRN.Conventions.ProjectName", "Shop.Domain.Infra")]
        """, false)]
    [DataInlineUnit("""
        [assembly: System.Reflection.AssemblyMetadata("DRN.Conventions.RootNamespace", "Shop.Domain")]
        """, false)]
    [DataInlineUnit("""
        [assembly: System.Reflection.AssemblyMetadata("DRN.Conventions.Domain", "Shop")]
        """, false)]
    public async Task Loaded_Domain_Project_With_Ambiguous_AssemblyName_Should_Allow_Absent_Metadata_And_Reject_Explicit_Or_Partial_Metadata(
        string metadataAttributes, bool offered)
    {
        using var workspace = new AdhocWorkspace();
        var domain = workspace.AddProject(ProjectInfo.Create(ProjectId.CreateNewId(), VersionStamp.Create(),
                "Shop.Domain", "Shop.Domain.Infra", LanguageNames.CSharp, filePath: SourcePath("Shop.Domain", "Shop.Domain.csproj")))
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .AddMetadataReferences(ClassificationTestFixture.References);
        var document = domain.AddDocument("IOrderRepository.cs", $$"""
            {{metadataAttributes}}
            namespace Shop.Domain.QA.Order;
            public class OrderEntity : DRN.Framework.SharedKernel.Domain.AggregateRoot { public override DRN.Framework.SharedKernel.Domain.Dto ToDto() => new(this); }
            public interface IOrderRepository : DRN.Framework.SharedKernel.Domain.Repository.ISourceKnownRepository<OrderEntity>
            {
                // select-start
                {{Method}}
                // select-end
            }
            """, filePath: SourcePath("Shop.Domain", "QA", "Order", "IOrderRepository.cs"));
        workspace.TryApplyChanges(document.Project.Solution).Should().BeTrue();

        var application = workspace.AddProject(ProjectInfo.Create(ProjectId.CreateNewId(), VersionStamp.Create(),
                "Shop.Application", "Shop.Application", LanguageNames.CSharp, filePath: SourcePath("Shop.Application", "Shop.Application.csproj")))
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .AddMetadataReferences(ClassificationTestFixture.References)
            .AddMetadataReference(AnalyzerTestCompilation.UtilsReference)
            .AddProjectReference(new ProjectReference(domain.Id));
        workspace.TryApplyChanges(application.Solution).Should().BeTrue();

        (await workspace.CurrentSolution.GetProject(domain.Id)!.GetCompilationAsync())!.AssertNoCompilerErrors();
        var actions = await Actions(workspace.CurrentSolution.GetDocument(document.Id)!);
        if (offered) actions.Should().HaveCount(2);
        else actions.Should().BeEmpty();
    }

    [Fact]
    public async Task Repository_And_Aggregate_In_Separate_Assemblies_Should_Not_Offer_A_Service()
    {
        using var workspace = new AdhocWorkspace();
        var producer = ClassificationTestFixture.Compilation("namespace Sample.Domain.QA.Orders; " + OrderDeclaration)
            .WithAssemblyName("Other.Domain");
        var entityReference = AnalyzerTestCompilation.EmitReference(producer, SourcePath("Other.Domain/Other.Domain.dll"),
            AnalyzerTestCompilation.ReferenceAssemblyOptions);
        var document = Create(workspace, entityReference: entityReference);
        foreach (var project in document.Project.Solution.Projects)
            (await project.GetCompilationAsync())!.AssertNoCompilerErrors();
        (await Actions(document)).Should().BeEmpty();
    }

    private const string OrderDeclaration =
        "public class Order : DRN.Framework.SharedKernel.Domain.AggregateRoot { public override DRN.Framework.SharedKernel.Domain.Dto ToDto() => new(this); }";

    private static Document Create(AdhocWorkspace workspace, string method = Method, bool reference = true,
        string targetName = "Sample.Application", MetadataReference? entityReference = null)
    {
        var domain = workspace.AddProject(ProjectInfo.Create(ProjectId.CreateNewId(), VersionStamp.Create(),
                "Sample.Domain", "Sample.Domain", LanguageNames.CSharp, filePath: SourcePath("Sample.Domain", "Sample.Domain.csproj")))
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .AddMetadataReferences(ClassificationTestFixture.References);
        if (entityReference is not null) domain = domain.AddMetadataReference(entityReference);
        var document = domain.AddDocument("IOrderRepository.cs", $$"""
            namespace Sample.Domain.QA.Orders;
            {{(entityReference is null ? OrderDeclaration : "")}}
            public interface IOrderRepository : DRN.Framework.SharedKernel.Domain.Repository.ISourceKnownRepository<Order>
            {
                // select-start
                {{method}}
                // select-end
            }
            """, filePath: SourcePath("Sample.Domain", "QA", "Orders", "IOrderRepository.cs"));
        workspace.TryApplyChanges(document.Project.Solution).Should().BeTrue();
        var application = workspace.AddProject(ProjectInfo.Create(ProjectId.CreateNewId(), VersionStamp.Create(),
                targetName, targetName, LanguageNames.CSharp, filePath: SourcePath(targetName, targetName + ".csproj")))
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .AddMetadataReferences(ClassificationTestFixture.References)
            .AddMetadataReference(AnalyzerTestCompilation.UtilsReference);
        if (reference) application = application.AddProjectReference(new ProjectReference(domain.Id));
        if (entityReference is not null) application = application.AddMetadataReference(entityReference);
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
