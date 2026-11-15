using DRN.Framework.SharedKernel.CodeFixes;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.Text;

namespace DRN.Test.Analyzer.Tests;

public sealed class EntityDtoRefactoringTests
{
    private const string Source = """
        #nullable enable
        namespace Sample.Domain.QA;
        public class User : DRN.Framework.SharedKernel.Domain.SourceKnownEntity
        {
            // select-start
            [DRN.Framework.SharedKernel.Attributes.Personal]
            public string Name { get; private set; } = "";
            public int Count { get; private set; }
            // select-end
            private string Secret { get; set; } = "";
            public string? Note { get; set; }
        }
        """;

    [Fact]
    public async Task Explicit_Scalar_Selection_Should_Add_One_Dto_And_One_Mapping_Atomically()
    {
        using var workspace = new AdhocWorkspace();
        var document = Create(workspace);
        var action = (await Actions(document)).Should().ContainSingle().Which;
        action.Title.Should().Contain("Sample.Contract").And.Contain("QA/UserDto.cs").And.Contain("Name required").And.Contain("Count");
        var operations = await action.GetOperationsAsync(CancellationToken.None);
        operations.OfType<ApplyChangesOperation>().Should().ContainSingle();
        var changed = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution;
        var added = changed.GetChanges(workspace.CurrentSolution).GetProjectChanges().SelectMany(change => change.GetAddedDocuments()).ToArray();
        added.Should().HaveCount(2);
        var dto = changed.Projects.Single(project => project.Name == "Sample.Contract").Documents.Single();
        var dtoText = (await dto.GetTextAsync()).ToString();
        dtoText.Should().Contain("SourceKnownEntity? entity = null").And.Contain(": base(entity)")
            .And.Contain("PersonalAttribute").And.Contain("required global::System.String Name")
            .And.Contain("global::System.Int32 Count").And.NotContain("Secret").And.NotContain("Note")
            .And.NotContain("Sample.Domain").And.NotContain("default!");
        var mapper = changed.GetProject(document.Project.Id)!.Documents.Single(item => item.Name == "UserDtoMapping.cs");
        var mapperText = (await mapper.GetTextAsync()).ToString();
        mapperText.Should().Contain("ToUserDto(this global::Sample.Domain.QA.User entity)")
            .And.Contain("Name = entity.Name").And.Contain("Count = entity.Count").And.NotContain("Secret");
        (await changed.GetDocument(document.Id)!.GetTextAsync()).ToString().Should().Be(Source);
        (await dto.Project.GetCompilationAsync())!.GetDiagnostics().Should().NotContain(item => item.Severity == DiagnosticSeverity.Error);
        (await mapper.Project.GetCompilationAsync())!.GetDiagnostics().Should().NotContain(item => item.Severity == DiagnosticSeverity.Error);
        workspace.TryApplyChanges(changed).Should().BeTrue();
        (await Actions(workspace.CurrentSolution.GetDocument(document.Id)!)).Should().BeEmpty();
    }

    [Theory]
    [InlineData("private string Name { get; set; } = \"\";")]
    [InlineData("public static string Name { get; set; } = \"\";")]
    [InlineData("public User Name { get; set; } = null!;")]
    [InlineData("public string[] Name { get; set; } = [];")]
    [InlineData("[System.Obsolete] public string Name { get; set; } = \"\";")]
    public async Task Nonpublic_Persistence_Collection_Or_Unknown_Annotated_Selection_Should_Be_Declined(string property)
    {
        using var workspace = new AdhocWorkspace();
        var source = """
            namespace Sample.Domain.QA;
            public class User : DRN.Framework.SharedKernel.Domain.SourceKnownEntity
            {
                // select-start
            """ + property + "\n// select-end\n}";
        (await Actions(Create(workspace, source))).Should().BeEmpty();
    }

    [Fact]
    public async Task Selected_Property_Matching_Dto_Class_Name_Should_Decline_Projection()
    {
        using var workspace = new AdhocWorkspace();
        var source = Source.Replace("public int Count", "public int UserDto", StringComparison.Ordinal);
        var document = Create(workspace, source);
        (await document.Project.GetCompilationAsync())!.GetDiagnostics()
            .Should().NotContain(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        (await Actions(document)).Should().BeEmpty();
        workspace.CurrentSolution.Should().BeSameAs(document.Project.Solution);
    }

    [Fact]
    public async Task Interface_Classification_Should_Not_Be_Silently_Dropped()
    {
        using var workspace = new AdhocWorkspace();
        var source = """
            namespace Sample.Domain.QA;
            public interface IUser
            {
                [DRN.Framework.SharedKernel.Attributes.Personal]
                string Name { get; }
            }
            public class User : DRN.Framework.SharedKernel.Domain.SourceKnownEntity, IUser
            {
                // select-start
                public string Name { get; private set; } = "";
                // select-end
            }
            """;
        (await Actions(Create(workspace, source))).Should().BeEmpty();
    }

    [Fact]
    public async Task Type_Classification_Should_Be_Preserved_On_The_Dto()
    {
        using var workspace = new AdhocWorkspace();
        var source = Source.Replace("public class User", "[DRN.Framework.SharedKernel.Attributes.Sensitive]\npublic class User", StringComparison.Ordinal);
        var document = Create(workspace, source);
        var action = (await Actions(document)).Single();
        var operations = await action.GetOperationsAsync(CancellationToken.None);
        var dto = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution.Projects
            .Single(project => project.Name == "Sample.Contract").Documents.Single();
        (await dto.GetTextAsync()).ToString().Should().Contain("[global::DRN.Framework.SharedKernel.Attributes.SensitiveAttribute]\npublic sealed class UserDto");
    }

    [Theory]
    [InlineData("Sample.Contract")]
    [InlineData("Other.Contract")]
    public async Task Missing_Direct_Contract_Reference_Should_Not_Create_An_Edge(string contractName)
    {
        using var workspace = new AdhocWorkspace();
        (await Actions(Create(workspace, reference: false, contractName: contractName))).Should().BeEmpty();
    }

    [Theory]
    [InlineData("Other.Contract")]
    [InlineData("Contract")]
    public async Task Referenced_Contract_Should_Not_Require_A_Shared_Project_Prefix(string contractName)
    {
        using var workspace = new AdhocWorkspace();
        var document = Create(workspace, contractName: contractName);
        var action = (await Actions(document)).Should().ContainSingle().Which;
        var changed = (await action.GetOperationsAsync(CancellationToken.None)).OfType<ApplyChangesOperation>().Single().ChangedSolution;
        var contract = changed.Projects.Single(project => project.Name == contractName);
        (await contract.Documents.Single().GetTextAsync()).ToString().Should().Contain("namespace " + contractName + ".QA;");
        changed.GetChanges(workspace.CurrentSolution).GetProjectChanges().Should()
            .OnlyContain(change => !change.GetAddedProjectReferences().Any());
        (await changed.GetProject(document.Project.Id)!.GetCompilationAsync())!.GetDiagnostics()
            .Should().NotContain(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public async Task Multiple_Referenced_Contracts_Should_Offer_Explicit_Target_Choices()
    {
        using var workspace = new AdhocWorkspace();
        var document = Create(workspace, contractName: "Shared.Contract");
        var other = workspace.AddProject(ProjectInfo.Create(ProjectId.CreateNewId(), VersionStamp.Create(),
                "Other.Contract", "Other.Contract", LanguageNames.CSharp, filePath: "/source/Other.Contract/Other.Contract.csproj"))
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .AddMetadataReferences(DiagnosticClassificationAnalyzerTests.References);
        var solution = other.Solution.AddProjectReference(document.Project.Id, new ProjectReference(other.Id));
        workspace.TryApplyChanges(solution).Should().BeTrue();
        document = workspace.CurrentSolution.GetDocument(document.Id)!;

        var actions = await Actions(document);
        actions.Should().HaveCount(2);
        foreach (var targetName in new[] { "Shared.Contract", "Other.Contract" })
        {
            var action = actions.Single(candidate => candidate.Title.Contains(targetName, StringComparison.Ordinal));
            var changed = (await action.GetOperationsAsync(CancellationToken.None)).OfType<ApplyChangesOperation>().Single().ChangedSolution;
            changed.Projects.Single(project => project.Name == targetName).Documents.Should().ContainSingle();
            changed.Projects.Where(project => project.Name.EndsWith(".Contract", StringComparison.Ordinal) && project.Name != targetName)
                .Should().OnlyContain(project => !project.Documents.Any());
            changed.GetChanges(workspace.CurrentSolution).GetProjectChanges().Should()
                .OnlyContain(change => !change.GetAddedProjectReferences().Any());
        }
    }

    [Theory]
    [InlineData("Sample.Contract", "/source/Sample.Contract/QA/userdto.cs")]
    [InlineData("Sample.Domain", "/source/Sample.Domain/QA/userdtomapping.cs")]
    public async Task Either_Destination_Collision_Should_Prevent_Both_Files(string projectName, string path)
    {
        using var workspace = new AdhocWorkspace();
        var document = Create(workspace);
        var project = workspace.CurrentSolution.Projects.Single(item => item.Name == projectName);
        var collision = project.AddDocument(Path.GetFileName(path), "// existing content", filePath: path);
        workspace.TryApplyChanges(collision.Project.Solution).Should().BeTrue();
        (await Actions(workspace.CurrentSolution.GetDocument(document.Id)!)).Should().BeEmpty();
    }

    [Fact]
    public async Task Changed_Selection_Preimage_Should_Not_Apply_Either_File()
    {
        using var workspace = new AdhocWorkspace();
        var document = Create(workspace);
        var action = (await Actions(document)).Single();
        workspace.TryApplyChanges(document.WithText(SourceText.From(Source + "\n// user edit")).Project.Solution).Should().BeTrue();
        var current = workspace.CurrentSolution;
        var operations = await action.GetOperationsAsync(CancellationToken.None);
        var changed = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution;
        changed.GetChanges(current).GetProjectChanges().Should().BeEmpty();
        changed.Projects.SelectMany(project => project.Documents).Should().NotContain(item => item.Name == "UserDto.cs" || item.Name == "UserDtoMapping.cs");
    }

    [Fact]
    public async Task Canceled_Action_Should_Leave_Both_Projects_Unchanged()
    {
        using var workspace = new AdhocWorkspace();
        var document = Create(workspace);
        var action = (await Actions(document)).Single();
        var current = workspace.CurrentSolution;
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var apply = async () => await action.GetOperationsAsync(cancellation.Token);
        await apply.Should().ThrowAsync<OperationCanceledException>();
        workspace.CurrentSolution.Should().BeSameAs(current);
    }

    [Fact]
    public async Task Cursor_Without_Explicit_Field_Selection_Should_Offer_Nothing()
    {
        using var workspace = new AdhocWorkspace();
        var document = Create(workspace);
        var actions = new List<CodeAction>();
        await new EntityDtoRefactoringProvider().ComputeRefactoringsAsync(new CodeRefactoringContext(document,
            new TextSpan(Source.IndexOf("Name", StringComparison.Ordinal), 0), actions.Add, CancellationToken.None));
        actions.Should().BeEmpty();
    }

    [Fact]
    public async Task Case_Insensitive_Symbol_Collision_Should_Be_Declined_Without_A_Physical_Path()
    {
        using var workspace = new AdhocWorkspace();
        var document = Create(workspace);
        var contract = workspace.CurrentSolution.Projects.Single(project => project.Name == "Sample.Contract");
        var collision = contract.AddDocument("Legacy.cs", "namespace Sample.Contract.QA; public class userdto;");
        workspace.TryApplyChanges(collision.Project.Solution).Should().BeTrue();
        (await Actions(workspace.CurrentSolution.GetDocument(document.Id)!)).Should().BeEmpty();
    }

    [Theory]
    [InlineData("Sample.Contract", "Sample.Contract.QA.UserDto")]
    [InlineData("Sample.Contract", "Sample.Contract.QA.userdto")]
    [InlineData("Sample.Domain", "Sample.Domain.QA.UserDtoMapping")]
    [InlineData("Sample.Domain", "Sample.Domain.QA.userdtomapping")]
    public async Task Namespace_Collision_At_Either_Generated_Type_Should_Prevent_Both_Files(string projectName, string namespaceName)
    {
        using var workspace = new AdhocWorkspace();
        var document = Create(workspace);
        (await Actions(document)).Should().ContainSingle();
        var project = workspace.CurrentSolution.Projects.Single(item => item.Name == projectName);
        var collision = project.AddDocument("Legacy.cs", "namespace " + namespaceName + "; public class Detail;");
        workspace.TryApplyChanges(collision.Project.Solution).Should().BeTrue();
        var current = workspace.CurrentSolution;
        foreach (var candidate in current.Projects)
            (await candidate.GetCompilationAsync())!.GetDiagnostics()
                .Should().NotContain(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        (await Actions(current.GetDocument(document.Id)!)).Should().BeEmpty();
        workspace.CurrentSolution.Should().BeSameAs(current);
    }

    [Theory]
    [InlineData("QA")]
    [InlineData("qa")]
    public async Task Namespace_Segment_Type_Should_Prevent_Both_Dto_Files(string segment)
    {
        using var workspace = new AdhocWorkspace();
        var document = Create(workspace);
        var contract = workspace.CurrentSolution.Projects.Single(project => project.Name == "Sample.Contract");
        var collision = contract.AddDocument("Legacy.cs", "namespace Sample.Contract; public class " + segment + ";");
        workspace.TryApplyChanges(collision.Project.Solution).Should().BeTrue();
        (await Actions(workspace.CurrentSolution.GetDocument(document.Id)!)).Should().BeEmpty();
    }

    [Theory]
    [InlineData("Sample.Domain", "[module: DRN.Conventions.Version(\"1\")]")]
    [InlineData("Sample.Contract", "[assembly: DRN.Conventions.Version(\"2\")]")]
    [InlineData("Sample.Contract", "[assembly: DRN.Conventions.Context(\"Sample.Context\")]")]
    public async Task Invalid_Input_In_Either_Project_Should_Prevent_Dto_Generation(string projectName, string input)
    {
        using var workspace = new AdhocWorkspace();
        var document = Create(workspace);
        var project = workspace.CurrentSolution.Projects.Single(item => item.Name == projectName);
        var changed = project.AddAdditionalDocument("DrnConventionInputs.g.cs", input).Project.Solution;
        workspace.TryApplyChanges(changed).Should().BeTrue();
        (await Actions(workspace.CurrentSolution.GetDocument(document.Id)!)).Should().BeEmpty();
    }

    [Fact]
    public async Task Explicit_Different_Ownership_Should_Not_Be_Replaced_By_Namespace_Inference()
    {
        using var workspace = new AdhocWorkspace();
        var source = Source.Replace("public class User",
            "[DRN.Framework.SharedKernel.Attributes.SourceKnownSubdomain(\"Identity\", Reason = \"Reviewed authentication owner\")]\npublic class User",
            StringComparison.Ordinal);
        (await Actions(Create(workspace, source))).Should().BeEmpty();
    }

    [Theory]
    [InlineData("[assembly: DRN.Conventions.Role(\"Domain\", NamespacePrefix = \"Sample.Domain\", LogicalPathPrefix = \"Business\")]")]
    [InlineData("[assembly: DRN.Conventions.Ownership(\"Sample.Domain.QA.User\", Subdomain = \"Identity\", Reason = \"Reviewed owner\")]")]
    public async Task Explicit_Role_Or_Ownership_Inputs_Should_Not_Be_Ignored_By_Dto_Placement(string record)
    {
        using var workspace = new AdhocWorkspace();
        var document = Create(workspace);
        var input = document.Project.AddAdditionalDocument("DrnConventionInputs.g.cs",
            "[assembly: DRN.Conventions.Version(\"1\")]\n" + record);
        workspace.TryApplyChanges(input.Project.Solution).Should().BeTrue();
        (await Actions(workspace.CurrentSolution.GetDocument(document.Id)!)).Should().BeEmpty();
    }

    [Fact]
    public async Task Nonstandard_Project_Without_Role_Token_Should_Not_Guess_From_Namespace()
    {
        using var workspace = new AdhocWorkspace();
        var document = Create(workspace, contractName: "Sample.Custom");
        var contract = workspace.CurrentSolution.Projects.Single(project => project.Name == "Sample.Custom");
        var configuration = contract.AddAnalyzerConfigDocument("contract.globalconfig", SourceText.From("""
            is_global = true
            build_property.RootNamespace = Sample.Contract
            """), filePath: "/source/Sample.Custom/contract.globalconfig");
        workspace.TryApplyChanges(configuration.Project.Solution).Should().BeTrue();
        (await Actions(workspace.CurrentSolution.GetDocument(document.Id)!)).Should().BeEmpty();
    }

    private static Document Create(AdhocWorkspace workspace, string source = Source, bool reference = true, string contractName = "Sample.Contract")
    {
        var contract = workspace.AddProject(ProjectInfo.Create(ProjectId.CreateNewId(), VersionStamp.Create(),
                contractName, contractName, LanguageNames.CSharp, filePath: "/source/" + contractName + "/" + contractName + ".csproj"))
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .AddMetadataReferences(DiagnosticClassificationAnalyzerTests.References);
        workspace.TryApplyChanges(contract.Solution).Should().BeTrue();
        var domain = workspace.AddProject(ProjectInfo.Create(ProjectId.CreateNewId(), VersionStamp.Create(),
                "Sample.Domain", "Sample.Domain", LanguageNames.CSharp, filePath: "/source/Sample.Domain/Sample.Domain.csproj"))
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .AddMetadataReferences(DiagnosticClassificationAnalyzerTests.References);
        if (reference) domain = domain.AddProjectReference(new ProjectReference(contract.Id));
        var document = domain.AddDocument("User.cs", source, filePath: "/source/Sample.Domain/QA/User.cs");
        workspace.TryApplyChanges(document.Project.Solution).Should().BeTrue();
        return workspace.CurrentSolution.GetDocument(document.Id)!;
    }

    private static async Task<List<CodeAction>> Actions(Document document)
    {
        var source = (await document.GetTextAsync()).ToString();
        var start = source.IndexOf("// select-start", StringComparison.Ordinal) + "// select-start".Length;
        var end = source.IndexOf("// select-end", StringComparison.Ordinal);
        var actions = new List<CodeAction>();
        await new EntityDtoRefactoringProvider().ComputeRefactoringsAsync(new CodeRefactoringContext(document,
            TextSpan.FromBounds(start, end), actions.Add, CancellationToken.None));
        return actions;
    }
}
