using System.Diagnostics.CodeAnalysis;
using DRN.Framework.SharedKernel.CodeFixes;
using DRN.Framework.Testing.Contexts;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace DRN.Test.Analyzer.Tests;

[SuppressMessage("ReSharper", "AccessToDisposedClosure")]
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

    [Theory, DataInlineUnit]
    public async Task Explicit_Scalar_Selection_Should_Add_One_Dto_And_One_Mapping_Atomically(DrnTestContextUnit context)
    {
        using var workspace = new AdhocWorkspace();
        var document = Create(workspace, context);
        var action = (await Actions(document)).Should().ContainSingle().Which;
        action.Title.Should().Contain("Sample.Contract").And.Contain("QA/Users/UserDto.cs").And.Contain("Name required").And.Contain("Count");
        var changed = await action.GetChangedSolutionAsync();
        var added = changed.GetChanges(workspace.CurrentSolution).GetProjectChanges().SelectMany(change => change.GetAddedDocuments()).ToArray();
        added.Should().ContainSingle();
        var dto = changed.Projects.Single(project => project.Name == "Sample.Contract").Documents.Single();
        var dtoText = (await dto.GetTextAsync()).ToString();
        dtoText.Should().Contain("SourceKnownEntity? entity = null").And.Contain(": base(entity)")
            .And.Contain("PersonalAttribute").And.Contain("required global::System.String Name")
            .And.Contain("global::System.Int32 Count").And.NotContain("Secret").And.NotContain("Note")
            .And.NotContain("Sample.Domain").And.NotContain("default!");
        var mapper = changed.GetDocument(document.Id)!;
        var mapperText = (await mapper.GetTextAsync()).ToString();
        mapperText.Should().Contain("ToDto() => new(this)").And.Contain("Name = this.Name").And.Contain("Count = this.Count")
            .And.NotContain("Secret = this.Secret").And.NotContain("Note = this.Note");
        var contractCompilation = (await dto.Project.GetCompilationAsync())!;
        var domainCompilation = (await mapper.Project.GetCompilationAsync())!;
        contractCompilation.AssertNoCompilerErrors();
        domainCompilation.AssertNoCompilerErrors();
        ConventionDtoTests.Analyze(contractCompilation, "Contract").Should().BeEmpty();
        ConventionDtoTests.Analyze(domainCompilation, "Domain").Should().BeEmpty();
        workspace.TryApplyChanges(changed).Should().BeTrue();
        (await Actions(workspace.CurrentSolution.GetDocument(document.Id)!)).Should().BeEmpty();
    }

    [Theory]
    [DataInlineUnit("private string Name { get; set; } = \"\";")]
    [DataInlineUnit("public static string Name { get; set; } = \"\";")]
    [DataInlineUnit("public User Name { get; set; } = null!;")]
    [DataInlineUnit("public string[] Name { get; set; } = [];")]
    [DataInlineUnit("[System.Obsolete] public string Name { get; set; } = \"\";")]
    public async Task Nonpublic_Persistence_Collection_Or_Unknown_Annotated_Selection_Should_Be_Declined(DrnTestContextUnit context, string property)
    {
        using var workspace = new AdhocWorkspace();
        var source = """
            namespace Sample.Domain.QA;
            public class User : DRN.Framework.SharedKernel.Domain.SourceKnownEntity
            {
                // select-start
            """ + property + "\n// select-end\n}";
        (await Actions(Create(workspace, context, source))).Should().BeEmpty();
    }

    [Theory, DataInlineUnit]
    public async Task Selected_Property_Matching_Dto_Class_Name_Should_Decline_Projection(DrnTestContextUnit context)
    {
        using var workspace = new AdhocWorkspace();
        var source = Source.Replace("public int Count", "public int UserDto", StringComparison.Ordinal);
        var document = Create(workspace, context, source);
        (await document.Project.GetCompilationAsync())!.AssertNoCompilerErrors();
        (await Actions(document)).Should().BeEmpty();
        workspace.CurrentSolution.Should().BeSameAs(document.Project.Solution);
    }

    [Theory, DataInlineUnit]
    public async Task Interface_Classification_Should_Not_Be_Silently_Dropped(DrnTestContextUnit context)
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
        (await Actions(Create(workspace, context, source))).Should().BeEmpty();
    }

    [Theory, DataInlineUnit]
    public async Task Type_Classification_Should_Be_Preserved_On_The_Dto(DrnTestContextUnit context)
    {
        using var workspace = new AdhocWorkspace();
        var source = Source.Replace("public class User", "[DRN.Framework.SharedKernel.Attributes.Sensitive]\npublic class User", StringComparison.Ordinal);
        var document = Create(workspace, context, source);
        var action = (await Actions(document)).Single();
        var dto = (await action.GetChangedSolutionAsync()).Projects
            .Single(project => project.Name == "Sample.Contract").Documents.Single();
        (await dto.GetTextAsync()).ToString().Should().Contain("[global::DRN.Framework.SharedKernel.Attributes.SensitiveAttribute]\npublic sealed class UserDto");
    }

    [Theory]
    [DataInlineUnit("Sample.Contract")]
    [DataInlineUnit("Other.Contract")]
    public async Task Missing_Direct_Contract_Reference_Should_Not_Create_An_Edge(DrnTestContextUnit context, string contractName)
    {
        using var workspace = new AdhocWorkspace();
        (await Actions(Create(workspace, context, reference: false, contractName: contractName))).Should().BeEmpty();
    }

    [Theory]
    [DataInlineUnit("Other.Contract")]
    [DataInlineUnit("Contract")]
    public async Task Referenced_Contract_Should_Not_Require_A_Shared_Project_Prefix(DrnTestContextUnit context, string contractName)
    {
        using var workspace = new AdhocWorkspace();
        var document = Create(workspace, context, contractName: contractName);
        var action = (await Actions(document)).Should().ContainSingle().Which;
        var changed = await action.GetChangedSolutionAsync();
        var contract = changed.Projects.Single(project => project.Name == contractName);
        (await contract.Documents.Single().GetTextAsync()).ToString().Should().Contain("namespace " + contractName + ".QA.Users;");
        changed.GetChanges(workspace.CurrentSolution).GetProjectChanges().Should()
            .OnlyContain(change => !change.GetAddedProjectReferences().Any());
        (await changed.GetProject(document.Project.Id)!.GetCompilationAsync())!.AssertNoCompilerErrors();
    }

    [Theory, DataInlineUnit]
    public async Task Multiple_Referenced_Contracts_Should_Offer_Explicit_Target_Choices(DrnTestContextUnit context)
    {
        using var workspace = new AdhocWorkspace();
        var document = Create(workspace, context, contractName: "Shared.Contract");
        var other = workspace.AddProject(ProjectInfo.Create(ProjectId.CreateNewId(), VersionStamp.Create(),
                "Other.Contract", "Other.Contract", LanguageNames.CSharp, filePath: Path.Combine(context.GetTempPath(), "Other.Contract", "Other.Contract.csproj")))
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .AddMetadataReferences(ClassificationTestFixture.References);
        var solution = other.Solution.AddProjectReference(document.Project.Id, new ProjectReference(other.Id));
        workspace.TryApplyChanges(solution).Should().BeTrue();
        document = workspace.CurrentSolution.GetDocument(document.Id)!;

        var actions = await Actions(document);
        actions.Should().HaveCount(2);
        foreach (var targetName in new[] { "Shared.Contract", "Other.Contract" })
        {
            var action = actions.Single(candidate => candidate.Title.Contains(targetName, StringComparison.Ordinal));
            var changed = await action.GetChangedSolutionAsync();
            changed.Projects.Single(project => project.Name == targetName).Documents.Should().ContainSingle();
            changed.Projects.Where(project => project.Name.EndsWith(".Contract", StringComparison.Ordinal) && project.Name != targetName)
                .Should().OnlyContain(project => !project.Documents.Any());
            changed.GetChanges(workspace.CurrentSolution).GetProjectChanges().Should()
                .OnlyContain(change => !change.GetAddedProjectReferences().Any());
        }
    }

    [Theory]
    [DataInlineUnit("Sample.Contract", "userdto.cs")]
    public async Task Dto_Path_Collision_Should_Prevent_The_Refactoring(DrnTestContextUnit context, string projectName, string fileName)
    {
        using var workspace = new AdhocWorkspace();
        var document = Create(workspace, context);
        var project = workspace.CurrentSolution.Projects.Single(item => item.Name == projectName);
        var collision = project.AddDocument(fileName, "// existing content", filePath: Path.Combine(context.GetTempPath(), projectName, "QA", "Users", fileName));
        workspace.TryApplyChanges(collision.Project.Solution).Should().BeTrue();
        (await Actions(workspace.CurrentSolution.GetDocument(document.Id)!)).Should().BeEmpty();
    }

    [Theory, DataInlineUnit]
    public async Task Existing_Entity_Mapper_Should_Prevent_A_Conflicting_Member(DrnTestContextUnit context)
    {
        using var workspace = new AdhocWorkspace();
        var source = Source.Replace("private string Secret", "public object ToDto() => this;\n    private string Secret", StringComparison.Ordinal);
        (await Actions(Create(workspace, context, source))).Should().BeEmpty();
    }

    [Theory]
    [DataInlineUnit("int value = 0", "", false)]
    [DataInlineUnit("params int[] values", "", false)]
    [DataInlineUnit("int value", "1", true)]
    public async Task Existing_Instance_Call_Should_Keep_Its_Overload(DrnTestContextUnit context, string parameter, string argument, bool offered)
    {
        using var workspace = new AdhocWorkspace();
        var source = Source.Replace("private string Secret", $"public object ToDto({parameter}) => this;\n    private string Secret", StringComparison.Ordinal)
            + $"\npublic class Caller {{ public object Map(User user) => user.ToDto({argument}); }}";
        var document = Create(workspace, context, source);
        (await document.Project.GetCompilationAsync())!.AssertNoCompilerErrors();
        var actions = await Actions(document);
        if (offered) actions.Should().ContainSingle();
        else actions.Should().BeEmpty();
    }

    [Theory]
    [DataInlineUnit("ToDto(this User user)", "Map(User user) => user.ToDto()", false, false)]
    [DataInlineUnit("ToDto(this SourceKnownEntity user)", "Map(User user) => user.ToDto()", true, false)]
    [DataInlineUnit("ToDto(this IProfile user, int mode = 0)", "Map(User user) => user?.ToDto()", true, false)]
    [DataInlineUnit("ToDto<T>(this T user) where T : IProfile", "Map(User user) => user.ToDto()", true, false)]
    [DataInlineUnit("ToDto<T>(this T user) where T : IProfile", "Map<T>(T user) where T : User => user.ToDto()", true, false)]
    [DataInlineUnit("ToDto(this IProfile user)", "Map(User user) => ((IProfile)user).ToDto()", true, true)]
    [DataInlineUnit("ToDto(this User user)", "Map(User user) => Extensions.ToDto(user)", true, true)]
    [DataInlineUnit("ToDto<T>(this T user) where T : struct", "Map(int value) => value.ToDto()", true, true)]
    [DataInlineUnit("ToDto(this User user, int mode)", "Map(User user) => user.ToDto(1)", true, true)]
    public async Task Extension_Collision_Should_Depend_On_The_Binding_At_The_Caller(
        DrnTestContextUnit context, string signature, string callerMethod, bool dependent, bool offered)
    {
        using var workspace = new AdhocWorkspace();
        var caller = "public class Caller { public object? " + callerMethod + "; }";
        var source = Source.Replace("Domain.SourceKnownEntity", "Domain.SourceKnownEntity, IProfile", StringComparison.Ordinal)
            + "\npublic interface IProfile { }";
        if (!dependent) source = "using Mapping;\n" + source + "\n" + caller;
        var document = Create(workspace, context, source);
        var project = document.Project;
        if (dependent)
        {
            project = workspace.AddProject("Consumer", LanguageNames.CSharp)
                .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
                .AddMetadataReferences(ClassificationTestFixture.References)
                .AddProjectReference(new ProjectReference(document.Project.Id))
                .AddProjectReference(new ProjectReference(document.Project.Solution.Projects.Single(item => item.Name == "Sample.Contract").Id))
                .AddDocument("Caller.cs", "using Sample.Domain.QA; using Mapping;\n" + caller).Project;
        }
        var extensions = project.AddDocument("Extensions.cs", "using Sample.Domain.QA; using DRN.Framework.SharedKernel.Domain;\n" +
            "namespace Mapping; public static class Extensions { public static object " + signature + " => user; }");
        workspace.TryApplyChanges(extensions.Project.Solution).Should().BeTrue();
        (await workspace.CurrentSolution.GetProject(project.Id)!.GetCompilationAsync())!.AssertNoCompilerErrors();
        var actions = await Actions(workspace.CurrentSolution.GetDocument(document.Id)!);
        if (offered) actions.Should().ContainSingle();
        else actions.Should().BeEmpty();
    }

    [Theory]
    [DataInlineUnit("paramEntity")]
    [DataInlineUnit("iEntity")]
    [DataInlineUnit("aEntity")]
    public async Task Keyword_Folder_Should_Prevent_Invalid_Namespace_Generation(DrnTestContextUnit context, string entityName)
    {
        using var workspace = new AdhocWorkspace();
        var document = Create(workspace, context, Source.Replace("class User :", "class " + entityName + " :", StringComparison.Ordinal));
        (await document.Project.GetCompilationAsync())!.AssertNoCompilerErrors();
        (await Actions(document)).Should().BeEmpty();
    }

    [Theory, DataInlineUnit]
    public async Task Changed_Selection_Preimage_Should_Preserve_The_Solution(DrnTestContextUnit context)
    {
        using var workspace = new AdhocWorkspace();
        var document = Create(workspace, context);
        var action = (await Actions(document)).Single();
        workspace.TryApplyChanges(document.WithText(SourceText.From(Source + "\n// user edit")).Project.Solution).Should().BeTrue();
        var current = workspace.CurrentSolution;
        var changed = await action.GetChangedSolutionAsync();
        changed.GetChanges(current).GetProjectChanges().Should().BeEmpty();
    }

    [Theory]
    [DataInlineUnit("missing")]
    [DataInlineUnit("directory")]
    [DataInlineUnit("linked file")]
    [DataInlineUnit("linked folder")]
    [DataInlineUnit("linked project root")]
    public async Task Unsafe_Entity_Path_Should_Decline_The_Offer_And_An_Existing_Preview(DrnTestContextUnit context, string replacement)
    {
        using var workspace = new AdhocWorkspace();
        var document = Create(workspace, context);
        var snapshot = workspace.CurrentSolution;
        var action = (await Actions(document)).Should().ContainSingle().Which;
        (await action.GetChangedSolutionAsync()).GetChanges(snapshot).GetProjectChanges().Should().HaveCount(2);
        var path = document.FilePath!;
        var outside = Path.Combine(context.GetTempPath(), "Moved");
        if (replacement is "linked folder" or "linked project root")
        {
            var directory = Path.GetDirectoryName(replacement == "linked folder" ? path : document.Project.FilePath!)!;
            Directory.Move(directory, outside);
            try { Directory.CreateSymbolicLink(directory, outside); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
            {
                Assert.Skip("Directory symbolic links are unavailable.");
            }
        }
        else
        {
            File.Move(path, outside);
            if (replacement == "directory") Directory.CreateDirectory(path);
            if (replacement == "linked file")
            {
                try { File.CreateSymbolicLink(path, outside); }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
                {
                    Assert.Skip("File symbolic links are unavailable.");
                }
            }
        }
        (await Actions(document)).Should().BeEmpty();
        (await action.GetChangedSolutionAsync()).GetChanges(snapshot).GetProjectChanges().Should().BeEmpty();
        workspace.CurrentSolution.Should().BeSameAs(snapshot);
    }

    [Theory, DataInlineUnit]
    public async Task Ordinary_File_Outside_The_Domain_Project_Should_Be_Declined(DrnTestContextUnit context)
    {
        using var workspace = new AdhocWorkspace();
        var document = Create(workspace, context, entityPath: "../Outside/User.cs");
        (await Actions(document)).Should().BeEmpty();
    }

    [Theory, DataInlineUnit]
    public async Task Canceled_Action_Should_Leave_Both_Projects_Unchanged(DrnTestContextUnit context)
    {
        using var workspace = new AdhocWorkspace();
        var document = Create(workspace, context);
        var action = (await Actions(document)).Single();
        var current = workspace.CurrentSolution;
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var apply = async () => await action.GetOperationsAsync(cancellation.Token);
        await apply.Should().ThrowAsync<OperationCanceledException>();
        workspace.CurrentSolution.Should().BeSameAs(current);
    }

    [Theory, DataInlineUnit]
    public async Task Cursor_Without_Explicit_Field_Selection_Should_Offer_Nothing(DrnTestContextUnit context)
    {
        using var workspace = new AdhocWorkspace();
        var document = Create(workspace, context);
        var actions = new List<CodeAction>();
        await new EntityDtoRefactoringProvider().ComputeRefactoringsAsync(new CodeRefactoringContext(document,
            new TextSpan(Source.IndexOf("Name", StringComparison.Ordinal), 0), actions.Add, CancellationToken.None));
        actions.Should().BeEmpty();
    }

    [Theory, DataInlineUnit]
    public async Task Case_Insensitive_Symbol_Collision_Should_Be_Declined_Without_A_Physical_Path(DrnTestContextUnit context)
    {
        using var workspace = new AdhocWorkspace();
        var document = Create(workspace, context);
        var contract = workspace.CurrentSolution.Projects.Single(project => project.Name == "Sample.Contract");
        var collision = contract.AddDocument("Legacy.cs", "namespace Sample.Contract.QA.Users; public class userdto;");
        workspace.TryApplyChanges(collision.Project.Solution).Should().BeTrue();
        (await Actions(workspace.CurrentSolution.GetDocument(document.Id)!)).Should().BeEmpty();
    }

    [Theory]
    [DataInlineUnit("Sample.Contract", "Sample.Contract.QA.Users.UserDto")]
    [DataInlineUnit("Sample.Contract", "Sample.Contract.QA.Users.userdto")]
    public async Task Dto_Namespace_Collision_Should_Prevent_The_Refactoring(DrnTestContextUnit context, string projectName, string namespaceName)
    {
        using var workspace = new AdhocWorkspace();
        var document = Create(workspace, context);
        (await Actions(document)).Should().ContainSingle();
        var project = workspace.CurrentSolution.Projects.Single(item => item.Name == projectName);
        var collision = project.AddDocument("Legacy.cs", "namespace " + namespaceName + "; public class Detail;");
        workspace.TryApplyChanges(collision.Project.Solution).Should().BeTrue();
        var current = workspace.CurrentSolution;
        foreach (var candidate in current.Projects)
            (await candidate.GetCompilationAsync())!.AssertNoCompilerErrors();
        (await Actions(current.GetDocument(document.Id)!)).Should().BeEmpty();
        workspace.CurrentSolution.Should().BeSameAs(current);
    }

    [Theory]
    [DataInlineUnit("QA")]
    [DataInlineUnit("qa")]
    public async Task Namespace_Segment_Type_Should_Prevent_Both_Dto_Files(DrnTestContextUnit context, string segment)
    {
        using var workspace = new AdhocWorkspace();
        var document = Create(workspace, context);
        var contract = workspace.CurrentSolution.Projects.Single(project => project.Name == "Sample.Contract");
        var collision = contract.AddDocument("Legacy.cs", "namespace Sample.Contract; public class " + segment + ";");
        workspace.TryApplyChanges(collision.Project.Solution).Should().BeTrue();
        (await Actions(workspace.CurrentSolution.GetDocument(document.Id)!)).Should().BeEmpty();
    }

    [Theory]
    [DataInlineUnit("Sample.Domain", "[module: DRN.Conventions.Version(\"1\")]")]
    [DataInlineUnit("Sample.Contract", "[assembly: DRN.Conventions.Version(\"2\")]")]
    [DataInlineUnit("Sample.Contract", "[assembly: DRN.Conventions.Context(\"Sample.Context\")]")]
    public async Task Invalid_Input_In_Either_Project_Should_Prevent_Dto_Generation(DrnTestContextUnit context, string projectName, string input)
    {
        using var workspace = new AdhocWorkspace();
        var document = Create(workspace, context);
        var project = workspace.CurrentSolution.Projects.Single(item => item.Name == projectName);
        var changed = project.AddAdditionalDocument("DrnConventionInputs.g.cs", input).Project.Solution;
        workspace.TryApplyChanges(changed).Should().BeTrue();
        (await Actions(workspace.CurrentSolution.GetDocument(document.Id)!)).Should().BeEmpty();
    }

    [Theory, DataInlineUnit]
    public async Task Explicit_Different_Ownership_Should_Not_Be_Replaced_By_Namespace_Inference(DrnTestContextUnit context)
    {
        using var workspace = new AdhocWorkspace();
        var source = Source.Replace("public class User",
            "[DRN.Framework.SharedKernel.Attributes.SourceKnownSubdomain(\"Identity\", Reason = \"Reviewed authentication owner\")]\npublic class User",
            StringComparison.Ordinal);
        (await Actions(Create(workspace, context, source))).Should().BeEmpty();
    }

    [Theory]
    [DataInlineUnit("[assembly: DRN.Conventions.Role(\"Domain\", NamespacePrefix = \"Sample.Domain\", LogicalPathPrefix = \"Business\")]")]
    [DataInlineUnit("[assembly: DRN.Conventions.Ownership(\"Sample.Domain.QA.User\", Subdomain = \"Identity\", Reason = \"Reviewed owner\")]")]
    public async Task Explicit_Role_Or_Ownership_Inputs_Should_Not_Be_Ignored_By_Dto_Placement(DrnTestContextUnit context, string record)
    {
        using var workspace = new AdhocWorkspace();
        var document = Create(workspace, context);
        var input = document.Project.AddAdditionalDocument("DrnConventionInputs.g.cs",
            "[assembly: DRN.Conventions.Version(\"1\")]\n" + record);
        workspace.TryApplyChanges(input.Project.Solution).Should().BeTrue();
        (await Actions(workspace.CurrentSolution.GetDocument(document.Id)!)).Should().BeEmpty();
    }

    [Theory, DataInlineUnit]
    public async Task Nonstandard_Project_Without_Role_Token_Should_Not_Guess_From_Namespace(DrnTestContextUnit context)
    {
        using var workspace = new AdhocWorkspace();
        var document = Create(workspace, context, contractName: "Sample.Custom");
        var contract = workspace.CurrentSolution.Projects.Single(project => project.Name == "Sample.Custom");
        var configuration = contract.AddAnalyzerConfigDocument("contract.globalconfig", SourceText.From("""
            is_global = true
            build_property.RootNamespace = Sample.Contract
            """), filePath: Path.Combine(context.GetTempPath(), "Sample.Custom", "contract.globalconfig"));
        workspace.TryApplyChanges(configuration.Project.Solution).Should().BeTrue();
        (await Actions(workspace.CurrentSolution.GetDocument(document.Id)!)).Should().BeEmpty();
    }

    [Theory]
    [DataInlineUnit("", "", true)]
    [DataInlineUnit("<T>", "<int>", false)]
    [DataInlineUnit("<T>", "<Sample.Domain.QA.DomainOnly>", false)]
    public async Task Nested_Enum_Selection_Should_Require_A_Type_Resolvable_In_Both_Projects(DrnTestContextUnit context, string declaration, string construction, bool offered)
    {
        using var workspace = new AdhocWorkspace();
        var source = Source.Replace("public int Count", "public Sample.Contract.Types.Container" + construction + ".State Count", StringComparison.Ordinal)
            + "\npublic class DomainOnly;";
        var document = Create(workspace, context, source);
        var contract = workspace.CurrentSolution.Projects.Single(project => project.Name == "Sample.Contract");
        var enumeration = contract.AddDocument("State.cs", "namespace Sample.Contract.Types; public class Container" + declaration +
            " { public enum State { Ready } }", filePath: Path.Combine(context.GetTempPath(), "Sample.Contract", "Types", "State.cs"));
        workspace.TryApplyChanges(enumeration.Project.Solution).Should().BeTrue();
        document = workspace.CurrentSolution.GetDocument(document.Id)!;
        (await document.Project.GetCompilationAsync())!.AssertNoCompilerErrors();
        var actions = await Actions(document);
        if (!offered)
        {
            actions.Should().BeEmpty();
            return;
        }
        var action = actions.Should().ContainSingle().Which;
        var changed = await action.GetChangedSolutionAsync();
        foreach (var project in changed.Projects)
            (await project.GetCompilationAsync())!.AssertNoCompilerErrors();
        var dto = changed.GetProject(contract.Id)!.Documents.Single(item => item.Name == "UserDto.cs");
        (await dto.GetTextAsync()).ToString().Should().Contain("global::Sample.Contract.Types.Container.State Count");
    }

    [Theory, DataInlineUnit]
    public async Task EntitySuffixed_Aggregate_Should_Generate_Stemmed_Dto_And_Mapping(DrnTestContextUnit context)
    {
        using var workspace = new AdhocWorkspace();
        var document = Create(workspace, context, Source
            .Replace("namespace Sample.Domain.QA;", "namespace Sample.Domain.QA.User;", StringComparison.Ordinal)
            .Replace("class User :", "class UserEntity :", StringComparison.Ordinal),
            entityPath: "QA/User/UserEntity.cs");
        var action = (await Actions(document)).Should().ContainSingle().Which;
        action.Title.Should().Contain("QA/Users/UserDto.cs");
        var changed = await action.GetChangedSolutionAsync();
        var contract = changed.Projects.Single(project => project.Name == "Sample.Contract");
        var domain = changed.Projects.Single(project => project.Name == "Sample.Domain");
        (await contract.Documents.Single(item => item.Name == "UserDto.cs").GetTextAsync()).ToString()
            .Should().Contain("public sealed class UserDto");
        (await domain.GetDocument(document.Id)!.GetTextAsync()).ToString()
            .Should().Contain("ToDto() => new(this)");
        (await contract.GetCompilationAsync())!.AssertNoCompilerErrors();
        (await domain.GetCompilationAsync())!.AssertNoCompilerErrors();
    }

    [Theory, DataInlineUnit]
    public async Task Case_Insensitive_Entity_Property_And_Partial_Property_Selection_Should_Be_Declined(DrnTestContextUnit context)
    {
        using var entityNameWorkspace = new AdhocWorkspace();
        var entityNameDoc = Create(entityNameWorkspace, context, Source.Replace("public int Count", "public int ENTITY", StringComparison.Ordinal));
        (await Actions(entityNameDoc)).Should().BeEmpty();

        using var partialWorkspace = new AdhocWorkspace();
        var partialDoc = Create(partialWorkspace, context);
        var text = (await partialDoc.GetTextAsync()).ToString();
        var start = text.IndexOf("public string Name", StringComparison.Ordinal);
        var midCount = text.IndexOf("Count", StringComparison.Ordinal);
        var actions = new List<CodeAction>();
        await new EntityDtoRefactoringProvider().ComputeRefactoringsAsync(new CodeRefactoringContext(partialDoc,
            TextSpan.FromBounds(start, midCount + 2), actions.Add, CancellationToken.None));
        actions.Should().BeEmpty();
    }

    [Theory, DataInlineUnit]
    public async Task Interface_Level_Unresolved_Or_False_Class_Classification_Should_Decline_And_Literal_Justification_Should_Preserve_Spacing(DrnTestContextUnit context)
    {
        using var interfaceWorkspace = new AdhocWorkspace();
        var interfaceSource = """
            namespace Sample.Domain.QA;
            [System.AttributeUsage(System.AttributeTargets.Interface | System.AttributeTargets.Property)]
            public sealed class InterfacePersonalAttribute() : Microsoft.Extensions.Compliance.Classification.DataClassificationAttribute(
                new Microsoft.Extensions.Compliance.Classification.DataClassification("Test", "Personal"));
            [InterfacePersonal]
            public interface IUserProfile { string Name { get; } }
            public class User : DRN.Framework.SharedKernel.Domain.SourceKnownEntity, IUserProfile
            {
                // select-start
                public string Name { get; private set; } = "";
                // select-end
            }
            """;
        var interfaceDocument = Create(interfaceWorkspace, context, interfaceSource);
        (await interfaceDocument.Project.GetCompilationAsync())!.AssertNoCompilerErrors();
        (await Actions(interfaceDocument)).Should().BeEmpty();

        using var unresolvedWorkspace = new AdhocWorkspace();
        var unresolvedSource = Source.Replace("public class User", "[MissingClassification]\npublic class User", StringComparison.Ordinal);
        (await Actions(Create(unresolvedWorkspace, context, unresolvedSource))).Should().BeEmpty();

        using var falseSensitiveWorkspace = new AdhocWorkspace();
        var falseSensitiveSource = Source.Replace("public class User",
            "[DRN.Framework.SharedKernel.Attributes.Sensitive(false, Justification = \"False class exemption.\")]\npublic class User",
            StringComparison.Ordinal);
        (await Actions(Create(falseSensitiveWorkspace, context, falseSensitiveSource))).Should().BeEmpty();

        using var literalWorkspace = new AdhocWorkspace();
        var literalSource = Source.Replace("public class User",
            "[DRN.Framework.SharedKernel.Attributes.Sensitive(Justification = \"Retain    [literal] spacing\")]\npublic class User",
            StringComparison.Ordinal);
        var action = (await Actions(Create(literalWorkspace, context, literalSource))).Should().ContainSingle().Which;
        var changed = await action.GetChangedSolutionAsync();
        var dto = changed.Projects.Single(project => project.Name == "Sample.Contract").Documents.Single();
        (await dto.GetTextAsync()).ToString().Should().Contain("Justification = \"Retain    [literal] spacing\"");
        (await dto.Project.GetCompilationAsync())!.AssertNoCompilerErrors();
    }

    [Theory]
    [DataInlineUnit("public long UserId { get; private set; }", false)]
    [DataInlineUnit("public long? QuestionId { get; private set; }", false)]
    [DataInlineUnit("public long Score { get; private set; }", true)]
    public async Task Internal_Numeric_Foreign_Key_Ids_Should_Decline_While_NonId_Int64_Remains_Allowed(DrnTestContextUnit context, string property, bool allowed)
    {
        using var workspace = new AdhocWorkspace();
        var source = $$"""
            namespace Sample.Domain.QA;
            public class User : DRN.Framework.SharedKernel.Domain.SourceKnownEntity
            {
                // select-start
                {{property}}
                // select-end
            }
            """;
        var actions = await Actions(Create(workspace, context, source));
        if (!allowed)
        {
            actions.Should().BeEmpty();
            return;
        }
        var changed = await actions.Should().ContainSingle().Which.GetChangedSolutionAsync();
        var dto = changed.Projects.Single(project => project.Name == "Sample.Contract").Documents.Single();
        (await dto.GetTextAsync()).ToString().Should().Contain("public global::System.Int64 Score { get; init; }");
    }

    [Theory, DataInlineUnit]
    public async Task Partial_Property_Should_Decline_And_Sibling_Mapper_Name_Should_Not_Block(DrnTestContextUnit context)
    {
        using var partialWorkspace = new AdhocWorkspace();
        var partialSource = """
            #nullable enable
            namespace Sample.Domain.QA;
            public partial class User : DRN.Framework.SharedKernel.Domain.SourceKnownEntity
            {
                private string _name = "";
                // select-start
                public partial string Name { get; set; }
                // select-end
                public partial string Name { get => _name; set => _name = value; }
            }
            """;
        (await Actions(Create(partialWorkspace, context, partialSource))).Should().BeEmpty();

        using var workspace = new AdhocWorkspace();
        var document = Create(workspace, context, Source
            .Replace("namespace Sample.Domain.QA;", "namespace Sample.Domain.QA.User;", StringComparison.Ordinal)
            .Replace("class User :", "class UserEntity :", StringComparison.Ordinal),
            entityPath: "QA/User/UserEntity.cs");
        var sibling = document.Project.AddDocument("BillingMapper.cs", """
            namespace Sample.Domain.Billing.User;
            public class UserEntity : DRN.Framework.SharedKernel.Domain.AggregateRoot;
            public static class BillingUserMapper
            {
                public static object ToDto(this UserEntity entity) => entity;
            }
            """, filePath: Path.Combine(context.GetTempPath(), "Sample.Domain", "Billing", "User", "BillingMapper.cs"));
        workspace.TryApplyChanges(sibling.Project.Solution).Should().BeTrue();
        var current = workspace.CurrentSolution.GetDocument(document.Id)!;
        var action = (await Actions(current)).Should().ContainSingle().Which;
        var changed = await action.GetChangedSolutionAsync();
        (await changed.GetProject(document.Project.Id)!.GetCompilationAsync())!.AssertNoCompilerErrors();
    }

    [Theory]
    [DataInlineUnit("inside-if", true, false)]
    [DataInlineUnit("inside-else", true, false)]
    [DataInlineUnit("tail-if", true, false)]
    [DataInlineUnit("wrapping-class", true, false)]
    [DataInlineUnit("wrapping-attribute", true, false)]
    [DataInlineUnit("wrapping-attribute", false, false)]
    [DataInlineUnit("partial-attribute", true, false)]
    [DataInlineUnit("partial-attribute", false, false)]
    [DataInlineUnit("unrelated-attribute", true, true)]
    [DataInlineUnit("unrelated-attribute", false, true)]
    [DataInlineUnit("method-if", true, true)]
    [DataInlineUnit("region", true, true)]
    public async Task Conditional_Compilation_Around_Selection_Or_Insertion_Point_Should_Decline(
        DrnTestContextUnit context, string scenario, bool feature, bool offered)
    {
        var body = scenario switch
        {
            "inside-if" => """
                public class User : DRN.Framework.SharedKernel.Domain.SourceKnownEntity
                {
                #if FEATURE
                    public int First { get; private set; }
                    // select-start
                    public string Name { get; private set; } = "";
                    // select-end
                #endif
                    public int After { get; private set; }
                }
                """,
            "inside-else" => """
                public class User : DRN.Framework.SharedKernel.Domain.SourceKnownEntity
                {
                #if UNDEFINED
                    public string Disabled { get; private set; } = "";
                #else
                    public int First { get; private set; }
                    // select-start
                    public string Name { get; private set; } = "";
                    // select-end
                #endif
                    public int After { get; private set; }
                }
                """,
            "tail-if" => """
                public class User : DRN.Framework.SharedKernel.Domain.SourceKnownEntity
                {
                    // select-start
                    public string Name { get; private set; } = "";
                    // select-end
                #if FEATURE
                    public int ConditionalTail { get; private set; }
                #endif
                }
                """,
            "wrapping-class" => """
                #if FEATURE
                public class User : DRN.Framework.SharedKernel.Domain.SourceKnownEntity
                {
                    // select-start
                    public string Name { get; private set; } = "";
                    // select-end
                }
                #endif
                """,
            "wrapping-attribute" => """
                #if FEATURE
                [DRN.Framework.SharedKernel.Attributes.Sensitive]
                #endif
                public class User : DRN.Framework.SharedKernel.Domain.SourceKnownEntity
                {
                    // select-start
                    public string Name { get; private set; } = "";
                    // select-end
                }
                """,
            "partial-attribute" => """
                public partial class User : DRN.Framework.SharedKernel.Domain.SourceKnownEntity
                {
                    // select-start
                    public string Name { get; private set; } = "";
                    // select-end
                }
                #if FEATURE
                [DRN.Framework.SharedKernel.Attributes.Sensitive]
                #endif
                public partial class User { }
                """,
            "unrelated-attribute" => """
                #if FEATURE
                [DRN.Framework.SharedKernel.Attributes.Sensitive] public class Earlier { }
                #endif
                public class User : DRN.Framework.SharedKernel.Domain.SourceKnownEntity
                {
                    // select-start
                    public string Name { get; private set; } = "";
                    // select-end
                }
                """,
            "method-if" => """
                public class User : DRN.Framework.SharedKernel.Domain.SourceKnownEntity
                {
                    // select-start
                    public string Name { get; private set; } = "";
                    // select-end
                    public string Format()
                    {
                #if FEATURE
                        return Name;
                #else
                        return "";
                #endif
                    }
                }
                """,
            _ => """
                public class User : DRN.Framework.SharedKernel.Domain.SourceKnownEntity
                {
                    #region Properties
                    public int Earlier { get; private set; }
                    // select-start
                    public string Name { get; private set; } = "";
                    // select-end
                    #endregion
                }
                """
        };
        var source = "#nullable enable\nnamespace Sample.Domain.QA;\n" + body;
        using var workspace = new AdhocWorkspace();
        var document = Create(workspace, context, source);
        var parseOptions = (document.Project.ParseOptions as CSharpParseOptions ?? CSharpParseOptions.Default)
            .WithPreprocessorSymbols(feature ? new[] { "FEATURE" } : Array.Empty<string>());
        workspace.TryApplyChanges(document.Project.WithParseOptions(parseOptions).Solution).Should().BeTrue();
        document = workspace.CurrentSolution.GetDocument(document.Id)!;
        (await document.Project.GetCompilationAsync())!.AssertNoCompilerErrors();
        if (scenario == "inside-if")
        {
            var root = (await document.GetSyntaxRootAsync())!;
            var selected = root.DescendantNodes().OfType<PropertyDeclarationSyntax>()
                .Single(property => property.Identifier.ValueText == "Name");
            selected.ContainsDirectives.Should().BeFalse();
        }
        var actions = await Actions(document);
        if (!offered)
        {
            actions.Should().BeEmpty();
            return;
        }
        var changed = await actions.Should().ContainSingle().Which.GetChangedSolutionAsync();
        foreach (var project in changed.Projects)
            (await project.GetCompilationAsync())!.AssertNoCompilerErrors();
    }

    [Theory]
    [DataInlineUnit("User", false)]
    [DataInlineUnit("UserBase", false)]
    [DataInlineUnit("Earlier", true)]
    public async Task Inactive_Part_In_Another_File_Should_Protect_Type_Classification(
        DrnTestContextUnit context, string disabledType, bool offered)
    {
        using var workspace = new AdhocWorkspace();
        var source = Source.Replace("public class User : DRN.Framework.SharedKernel.Domain.SourceKnownEntity",
            "public partial class UserBase : DRN.Framework.SharedKernel.Domain.SourceKnownEntity;\npublic partial class User : UserBase",
            StringComparison.Ordinal);
        var document = Create(workspace, context, source);
        var part = document.Project.AddDocument("Conditional.cs", $$"""
            namespace Sample.Domain.QA;
            #if FEATURE
            [DRN.Framework.SharedKernel.Attributes.Personal]
            public partial class {{disabledType}} { }
            #endif
            """);
        workspace.TryApplyChanges(part.Project.Solution).Should().BeTrue();
        document = workspace.CurrentSolution.GetDocument(document.Id)!;
        (await document.Project.GetCompilationAsync())!.AssertNoCompilerErrors();
        var actions = await Actions(document);
        if (offered) actions.Should().ContainSingle();
        else actions.Should().BeEmpty();
    }

    [Theory]
    [DataInlineUnit("external-project")]
    [DataInlineUnit("other-contract")]
    [DataInlineUnit("external-metadata")]
    [DataInlineUnit("domain-shadowing")]
    [DataInlineUnit("internal-unrelated")]
    [DataInlineUnit("warnings-as-errors")]
    public async Task Ambiguous_Or_Shadowed_Dto_Return_Type_Should_Decline(DrnTestContextUnit context, string scenario)
    {
        using var workspace = new AdhocWorkspace();
        var document = Create(workspace, context);
        var solution = workspace.CurrentSolution;
        const string collidingDto = "namespace Sample.Contract.QA.Users; public sealed class UserDto;";
        switch (scenario)
        {
            case "external-project":
            case "internal-unrelated":
                var extra = workspace.AddProject(ProjectInfo.Create(ProjectId.CreateNewId(), VersionStamp.Create(),
                        "Legacy.Shared", "Legacy.Shared", LanguageNames.CSharp,
                        filePath: Path.Combine(context.GetTempPath(), "Legacy.Shared", "Legacy.Shared.csproj")))
                    .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
                    .AddMetadataReferences(ClassificationTestFixture.References)
                    .AddDocument("LegacyTypes.cs", scenario == "external-project"
                        ? collidingDto
                        : "namespace Sample.Contract.QA.Users; internal sealed class UserDto;");
                solution = extra.Project.Solution.AddProjectReference(document.Project.Id, new ProjectReference(extra.Project.Id));
                break;
            case "other-contract":
                var otherContract = workspace.AddProject(ProjectInfo.Create(ProjectId.CreateNewId(), VersionStamp.Create(),
                        "Other.Contract", "Other.Contract", LanguageNames.CSharp,
                        filePath: Path.Combine(context.GetTempPath(), "Other.Contract", "Other.Contract.csproj")))
                    .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
                    .AddMetadataReferences(ClassificationTestFixture.References)
                    .AddDocument("LegacyUserDto.cs", collidingDto);
                solution = otherContract.Project.Solution.AddProjectReference(document.Project.Id, new ProjectReference(otherContract.Project.Id));
                break;
            case "external-metadata":
                var compiled = CSharpCompilation.Create("External.Legacy",
                    [CSharpSyntaxTree.ParseText(collidingDto)], ClassificationTestFixture.References,
                    new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
                solution = solution.AddMetadataReference(document.Project.Id, AnalyzerTestCompilation.EmitReference(compiled));
                break;
            case "domain-shadowing":
                solution = document.Project.AddDocument("ShadowUserDto.cs", collidingDto).Project.Solution;
                break;
            case "warnings-as-errors":
                solution = document.Project.WithCompilationOptions(
                    new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, generalDiagnosticOption: ReportDiagnostic.Error)).Solution;
                break;
        }
        workspace.TryApplyChanges(solution).Should().BeTrue();
        document = workspace.CurrentSolution.GetDocument(document.Id)!;
        (await document.Project.GetCompilationAsync())!.AssertNoCompilerErrors();
        var actions = await Actions(document);
        if (scenario is "external-project" or "external-metadata" or "domain-shadowing")
        {
            actions.Should().BeEmpty();
            return;
        }
        var expectedTarget = scenario == "other-contract" ? "Other.Contract" : "Sample.Contract";
        var action = actions.Should().ContainSingle().Which;
        action.Title.Should().Contain(expectedTarget);
        var changed = await action.GetChangedSolutionAsync();
        foreach (var project in changed.Projects)
            (await project.GetCompilationAsync())!.AssertNoCompilerErrors();
    }

    [Theory]
    [DataInlineUnit(true)]
    [DataInlineUnit(false)]
    public async Task Only_New_Compiler_Errors_In_Affected_Projects_Should_Block_The_Projection(
        DrnTestContextUnit context, bool ambiguous)
    {
        using var workspace = new AdhocWorkspace();
        var document = Create(workspace, context, Source + "\nclass Broken { MissingType field; }");
        var contract = document.Project.Solution.Projects.Single(project => project.Name == "Sample.Contract");
        var consumer = workspace.AddProject("Consumer", LanguageNames.CSharp)
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .AddMetadataReferences(ClassificationTestFixture.References)
            .AddProjectReference(new ProjectReference(contract.Id))
            .AddDocument("Caller.cs", $$"""
                using Legacy;
                using Sample.Contract.QA.Users;
                namespace Sample.Contract.QA.Users { public class Existing; }
                namespace Legacy { public class UserDto; }
                class Caller { {{(ambiguous ? "UserDto" : "Legacy.UserDto")}} value; }
                """);
        workspace.TryApplyChanges(consumer.Project.Solution).Should().BeTrue();
        (await workspace.CurrentSolution.GetProject(consumer.Project.Id)!.GetCompilationAsync())!.AssertNoCompilerErrors();
        document = workspace.CurrentSolution.GetDocument(document.Id)!;
        (await document.Project.GetCompilationAsync())!.GetDiagnostics().Should().Contain(d => d.Id == "CS0246");
        var actions = await Actions(document);
        if (ambiguous) actions.Should().BeEmpty();
        else actions.Should().ContainSingle();
    }

    [Theory]
    [DataInlineUnit("Sample.Domain")]
    [DataInlineUnit("Sample.Contract")]
    public async Task Promoted_Documentation_Warnings_Should_Prevent_An_Invalid_Projection(
        DrnTestContextUnit context, string strictProject)
    {
        using var workspace = new AdhocWorkspace();
        var document = Create(workspace, context, """
            namespace Sample.Domain.QA;
            /// <summary>A user.</summary>
            public class User : DRN.Framework.SharedKernel.Domain.SourceKnownEntity
            {
                // select-start
                /// <summary>The public name.</summary>
                public string Name { get; private set; } = "";
                // select-end
            }
            """);
        var project = workspace.CurrentSolution.Projects.Single(p => p.Name == strictProject);
        var parseOptions = ((CSharpParseOptions)project.ParseOptions!).WithDocumentationMode(DocumentationMode.Diagnose);
        var options = (CSharpCompilationOptions)project.CompilationOptions!;
        var warn = options.WithSpecificDiagnosticOptions(options.SpecificDiagnosticOptions.SetItem("CS1591", ReportDiagnostic.Warn));
        var configured = project.WithParseOptions(parseOptions).WithCompilationOptions(warn);
        workspace.TryApplyChanges(configured.Solution).Should().BeTrue();
        (await workspace.CurrentSolution.GetProject(project.Id)!.GetCompilationAsync())!.AssertNoCompilerErrors();
        var action = (await Actions(workspace.CurrentSolution.GetDocument(document.Id)!)).Should().ContainSingle().Which;
        var preview = await action.GetChangedSolutionAsync();
        var previewProject = preview.GetProject(project.Id)!;
        (await previewProject.GetCompilationAsync())!.GetDiagnostics().Should().Contain(d =>
            d.Id == "CS1591" && d.Severity == DiagnosticSeverity.Warning);

        var promote = options.WithSpecificDiagnosticOptions(options.SpecificDiagnosticOptions.SetItem("CS1591", ReportDiagnostic.Error));
        var original = workspace.CurrentSolution.WithProjectCompilationOptions(project.Id, promote);
        workspace.TryApplyChanges(original).Should().BeTrue();
        (await workspace.CurrentSolution.GetProject(project.Id)!.GetCompilationAsync())!.AssertNoCompilerErrors();
        (await Actions(workspace.CurrentSolution.GetDocument(document.Id)!)).Should().BeEmpty();
    }

    private static Document Create(AdhocWorkspace workspace, DrnTestContextUnit context, string source = Source, bool reference = true,
        string contractName = "Sample.Contract", string entityPath = "QA/User.cs")
    {
        var root = context.GetTempPath();
        var domainRoot = Path.Combine(root, "Sample.Domain");
        entityPath = Path.GetFullPath(entityPath, domainRoot);
        Directory.CreateDirectory(Path.GetDirectoryName(entityPath)!);
        File.WriteAllText(entityPath, source);
        var contract = workspace.AddProject(ProjectInfo.Create(ProjectId.CreateNewId(), VersionStamp.Create(),
                contractName, contractName, LanguageNames.CSharp, filePath: Path.Combine(root, contractName, contractName + ".csproj")))
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .AddMetadataReferences(ClassificationTestFixture.References);
        workspace.TryApplyChanges(contract.Solution).Should().BeTrue();
        var domain = workspace.AddProject(ProjectInfo.Create(ProjectId.CreateNewId(), VersionStamp.Create(),
                "Sample.Domain", "Sample.Domain", LanguageNames.CSharp, filePath: Path.Combine(domainRoot, "Sample.Domain.csproj")))
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .AddMetadataReferences(ClassificationTestFixture.References);
        if (reference) domain = domain.AddProjectReference(new ProjectReference(contract.Id));
        var document = domain.AddDocument(Path.GetFileName(entityPath), source, filePath: entityPath);
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
