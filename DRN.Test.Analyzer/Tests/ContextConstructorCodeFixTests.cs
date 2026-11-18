using System.Diagnostics.CodeAnalysis;
using DRN.Framework.SharedKernel.Analyzers.Conventions;
using DRN.Framework.SharedKernel.CodeFixes;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace DRN.Test.Analyzer.Tests;

[SuppressMessage("ReSharper", "AccessToDisposedClosure")]
public sealed class ContextConstructorCodeFixTests
{
    [Theory]
    [DataInlineUnit("")]
    [DataInlineUnit(";")]
    public async Task Constructor_Action_Should_Forward_Exact_Options_Preserve_Content_And_Be_Idempotent(string trailingSemicolon)
    {
        using var workspace = new AdhocWorkspace();
        var document = CreateDocument(workspace, $$"""
            // Retain the operational context identity.
            public class QAContext : DRN.Framework.EntityFramework.Context.DrnContext<QAContext>
            { // Retain opening brace trivia.
                public QAContext() : base(null) { /* retain */ }
            /* Retain closing brace trivia. */ }{{trailingSemicolon}}
            """);
        var action = (await Actions(document)).Single();
        action.Title.Should().Be("Add context options constructor");
        var operations = await action.GetOperationsAsync(CancellationToken.None);
        var changed = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution.GetDocument(document.Id)!;
        var compilation = await changed.Project.GetCompilationAsync();
        compilation!.GetDiagnostics().Should().NotContain(item => item.Severity == DiagnosticSeverity.Error);
        var text = (await changed.GetTextAsync()).ToString();
        text.Should().Contain("/* retain */").And.Contain("Retain the operational context identity.")
            .And.Contain("// Retain opening brace trivia.").And.Contain("/* Retain closing brace trivia. */")
            .And.Contain("global::Microsoft.EntityFrameworkCore.DbContextOptions<global::QAContext> options")
            .And.Contain(": base(options)");
        var declaration = (await changed.GetSyntaxRootAsync())!.DescendantNodes().OfType<ClassDeclarationSyntax>().Single();
        declaration.SemicolonToken.Text.Should().Be(trailingSemicolon);
        (await Actions(changed)).Should().BeEmpty();
        new ContextConstructorCodeFixProvider().GetFixAllProvider().Should().BeNull();
    }

    [Theory]
    [InlineData("public class QAContext() : DRN.Framework.EntityFramework.Context.DrnContext<QAContext>(null);", "Context.cs")]
    [InlineData("public class QAContext : DRN.Framework.EntityFramework.Context.DrnContext<QAContext> { public QAContext() : base(null) { System.Console.WriteLine(1); } }", "Context.cs")]
    [InlineData("public class QAContext : DRN.Framework.EntityFramework.Context.DrnContext<QAContext> { public QAContext() : base(null) { } public QAContext(string value) : base(null) { } }", "Context.cs")]
    [InlineData("public class QAContext : DRN.Framework.EntityFramework.Context.DrnContext<QAContext> { public QAContext() : base(null) { } }", "Context.g.cs")]
    [InlineData("public class QAContext { public QAContext() { } }", "Context.cs")]
    public async Task Constructor_Action_Should_Reject_Ambiguous_Or_Unsafe_Construction(string source, string name)
    {
        using var workspace = new AdhocWorkspace();
        (await Actions(CreateDocument(workspace, source, name))).Should().BeEmpty();
    }

    [Fact]
    public async Task Changed_Preimage_Should_Leave_Current_Content_Unchanged()
    {
        using var workspace = new AdhocWorkspace();
        var document = CreateDocument(workspace,
            "public class QAContext : DRN.Framework.EntityFramework.Context.DrnContext<QAContext> { public QAContext() : base(null) { } }");
        var action = (await Actions(document)).Single();
        var newText = SourceText.From((await document.GetTextAsync()) + "\n// Concurrent user edit\n");
        workspace.TryApplyChanges(document.WithText(newText).Project.Solution).Should().BeTrue();
        var operations = await action.GetOperationsAsync(CancellationToken.None);
        var changed = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution.GetDocument(document.Id)!;
        (await changed.GetTextAsync()).ContentEquals(newText).Should().BeTrue();
    }

    [Fact]
    public async Task Cancellation_Should_Not_Change_Workspace()
    {
        using var workspace = new AdhocWorkspace();
        var document = CreateDocument(workspace,
            "public class QAContext : DRN.Framework.EntityFramework.Context.DrnContext<QAContext> { public QAContext() : base(null) { } }");
        var action = (await Actions(document)).Single();
        var original = workspace.CurrentSolution;
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var apply = async () => await action.GetOperationsAsync(cancellation.Token);
        await apply.Should().ThrowAsync<OperationCanceledException>();
        workspace.CurrentSolution.Should().BeSameAs(original);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Removed_Document_Or_Project_Should_Not_Be_Reintroduced(bool removeProject)
    {
        using var workspace = new AdhocWorkspace();
        var document = CreateDocument(workspace,
            "public class QAContext : DRN.Framework.EntityFramework.Context.DrnContext<QAContext> { public QAContext() : base(null) { } }");
        var action = (await Actions(document)).Single();
        var removed = removeProject ? workspace.CurrentSolution.RemoveProject(document.Project.Id)
            : workspace.CurrentSolution.RemoveDocument(document.Id);
        workspace.TryApplyChanges(removed).Should().BeTrue();
        var current = workspace.CurrentSolution;
        var operations = await action.GetOperationsAsync(CancellationToken.None);
        var changed = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution;
        changed.GetDocument(document.Id).Should().BeNull();
        changed.GetChanges(current).GetProjectChanges().Should().BeEmpty();
        changed.GetChanges(current).GetAddedProjects().Should().BeEmpty();
    }

    [Fact]
    public async Task Replaced_Workspace_Solution_Should_Not_Receive_An_Old_Edit()
    {
        using var workspace = new AdhocWorkspace();
        var document = CreateDocument(workspace,
            "public class QAContext : DRN.Framework.EntityFramework.Context.DrnContext<QAContext> { public QAContext() : base(null) { } }");
        var action = (await Actions(document)).Single();
        workspace.ClearSolution();
        workspace.AddProject("Replacement", LanguageNames.CSharp);
        var current = workspace.CurrentSolution;
        var operations = await action.GetOperationsAsync(CancellationToken.None);
        var changed = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution;
        changed.Id.Should().Be(current.Id);
        changed.ProjectIds.Should().Equal(current.ProjectIds);
        changed.GetDocument(document.Id).Should().BeNull();
    }

    [Theory]
    [InlineData("public int Number => 1;", 1)]
    [InlineData("public MissingType Invalid => null;", 0)]
    public async Task Constructor_Action_Should_Check_All_Partial_Declarations(string member, int expectedActions)
    {
        using var workspace = new AdhocWorkspace();
        var document = CreateDocument(workspace,
            "public partial class QAContext : DRN.Framework.EntityFramework.Context.DrnContext<QAContext> { public QAContext() : base(null) { } }");
        var other = document.Project.AddDocument("Other.cs", "public partial class QAContext { " + member + " }");
        workspace.TryApplyChanges(other.Project.Solution).Should().BeTrue();
        var current = workspace.CurrentSolution.GetDocument(document.Id)!;
        (await Actions(current)).Should().HaveCount(expectedActions);
    }

    [Theory]
    [DataInlineUnit("null")]
    [DataInlineUnit("null!")]
    [DataInlineUnit("default")]
    [DataInlineUnit("default(Microsoft.EntityFrameworkCore.DbContextOptions<QAContext>)")]
    public async Task Constructor_Action_Should_Accept_Equivalent_Empty_Base_Arguments(string baseArgument)
    {
        using var workspace = new AdhocWorkspace();
        var document = CreateDocument(workspace, $$"""
            public class QAContext : DRN.Framework.EntityFramework.Context.DrnContext<QAContext>
            {
                public QAContext() : base({{baseArgument}}) { }
            }
            """);
        var action = (await Actions(document)).Single();
        var operations = await action.GetOperationsAsync(CancellationToken.None);
        var changed = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution.GetDocument(document.Id)!;
        var compilation = await changed.Project.GetCompilationAsync();
        compilation!.GetDiagnostics().Should().NotContain(item => item.Severity == DiagnosticSeverity.Error);
        (await Actions(changed)).Should().BeEmpty();
    }

    [Theory]
    [DataInlineUnit("default(int)", 0)]
    [DataInlineUnit("default(Microsoft.EntityFrameworkCore.DbContextOptions<QAContext>)", 1)]
    public async Task Constructor_Action_Should_Verify_Bound_Options_Overload_For_Typed_Defaults(string baseArgument, int expectedActions)
    {
        using var workspace = new AdhocWorkspace();
        var document = CreateDocument(workspace, $$"""
            public class QAContext : CustomContextBase
            {
                public QAContext() : base({{baseArgument}}) { }
            }
            public abstract class CustomContextBase : DRN.Framework.EntityFramework.Context.DrnContext<QAContext>
            {
                protected CustomContextBase(Microsoft.EntityFrameworkCore.DbContextOptions<QAContext> options) : base(options) { }
                protected CustomContextBase(int mode) : base(null) { }
            }
            """);
        var compilation = await document.Project.GetCompilationAsync();
        compilation!.GetDiagnostics().Should().NotContain(item => item.Severity == DiagnosticSeverity.Error);

        var actions = await Actions(document);
        actions.Should().HaveCount(expectedActions);
        if (expectedActions == 0) return;

        var operations = await actions.Single().GetOperationsAsync(CancellationToken.None);
        var changed = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution.GetDocument(document.Id)!;
        var changedCompilation = await changed.Project.GetCompilationAsync();
        changedCompilation!.GetDiagnostics().Should().NotContain(item => item.Severity == DiagnosticSeverity.Error);
        (await Actions(changed)).Should().BeEmpty();
    }

    [Fact]
    public async Task Constructor_Action_Should_Convert_Semicolon_Bodied_Partial_Context_To_Valid_Braced_Class()
    {
        using var workspace = new AdhocWorkspace();
        var document = CreateDocument(workspace,
            "public partial class QAContext : DRN.Framework.EntityFramework.Context.DrnContext<QAContext>;\n",
            "A_QAContext.cs");
        var other = document.Project.AddDocument("B_QAContext.cs",
            "public partial class QAContext { public QAContext() : base(null) { } }");
        workspace.TryApplyChanges(other.Project.Solution).Should().BeTrue();
        var current = workspace.CurrentSolution.GetDocument(document.Id)!;

        var action = (await Actions(current)).Should().ContainSingle().Which;
        var operations = await action.GetOperationsAsync(CancellationToken.None);
        var changed = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution.GetDocument(document.Id)!;
        var compilation = await changed.Project.GetCompilationAsync();
        compilation!.GetDiagnostics().Should().NotContain(item => item.Severity == DiagnosticSeverity.Error);
        var text = (await changed.GetTextAsync()).ToString();
        text.Should().Contain("DbContextOptions<global::QAContext> options").And.Contain(": base(options)");
        (await Actions(changed)).Should().BeEmpty();
    }

    private static Document CreateDocument(AdhocWorkspace workspace, string source, string name = "Context.cs")
    {
        var project = workspace.AddProject("Consumer", LanguageNames.CSharp)
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .AddMetadataReferences(DiagnosticClassificationAnalyzerTests.References)
            .AddMetadataReference(AnalyzerTestCompilation.EntityFrameworkReference)
            .AddMetadataReference(AnalyzerTestCompilation.UtilsReference);
        var document = project.AddDocument(name, source);
        workspace.TryApplyChanges(document.Project.Solution).Should().BeTrue();
        return workspace.CurrentSolution.GetDocument(document.Id)!;
    }

    private static async Task<List<CodeAction>> Actions(Document document)
    {
        var root = await document.GetSyntaxRootAsync();
        var declaration = root!.DescendantNodes().OfType<ClassDeclarationSyntax>().First();
        var diagnostic = Diagnostic.Create(ConventionDiagnosticDescriptors.ContextConstructor,
            declaration.Identifier.GetLocation(), declaration.Identifier.ValueText);
        var actions = new List<CodeAction>();
        var provider = new ContextConstructorCodeFixProvider();
        provider.FixableDiagnosticIds.Should().Contain(diagnostic.Id);
        await provider.RegisterCodeFixesAsync(new CodeFixContext(document, diagnostic,
            (action, _) => actions.Add(action), CancellationToken.None));
        return actions;
    }
}
