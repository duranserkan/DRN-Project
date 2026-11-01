using DRN.Framework.SharedKernel.CodeFixes;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace DRN.Test.Analyzer.Tests;

public sealed class ClassificationCodeFixTests
{
    private static Project CreateProject(AdhocWorkspace workspace) => workspace.AddProject("Consumer", LanguageNames.CSharp)
        .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
        .AddMetadataReferences(DiagnosticClassificationAnalyzerTests.References);

    private static async Task<List<CodeAction>> GetActions(Document document, Diagnostic diagnostic, CodeFixProvider provider)
    {
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new CodeFixContext(document, diagnostic,
            (action, _) => actions.Add(action), CancellationToken.None));
        return actions;
    }

    private static async Task<Solution> Apply(CodeAction action)
    {
        var operations = await action.GetOperationsAsync(CancellationToken.None);
        return operations.OfType<ApplyChangesOperation>().Single().ChangedSolution;
    }

    private static async Task<Document> Apply(Document document, CodeAction action) =>
        (await Apply(action)).GetDocument(document.Id)!;

    private static CodeFixProvider ClassificationProvider(string selection) => selection == "SensitiveFalse"
        ? new NameExemptionCodeFixProvider() : new ClassificationCodeFixProvider();

    [Fact]
    public async Task Project_Rule_Diagnostic_Should_Offer_A_Compiling_Classification_Fix()
    {
        using var workspace = new AdhocWorkspace();
        var project = CreateProject(workspace);
        var document = project.AddDocument("Model.cs", "public record Model(string MembershipNumber);");
        var file = new ProjectDiagnosticRulesTests.RuleFile("data-classification-rules.json", ProjectDiagnosticRulesTests.Fixture());
        var diagnostic = (await DiagnosticClassificationAnalyzerTests.Analyze(
            (CSharpCompilation)(await document.Project.GetCompilationAsync())!, file)).Single();
        diagnostic.Id.Should().Be("DRN0015");
        var actions = await GetActions(document, diagnostic, new ClassificationCodeFixProvider());
        var changed = await Apply(document, actions.Single(action => action.EquivalenceKey == "Personal"));
        var after = (CSharpCompilation)(await changed.Project.GetCompilationAsync())!;
        (await DiagnosticClassificationAnalyzerTests.Analyze(after, file)).Should().BeEmpty();
        after.GetTypeByMetadataName("Model")!.GetMembers("MembershipNumber").OfType<IPropertySymbol>().Single()
            .GetAttributes().Should().ContainSingle().Which.AttributeClass!.ToDisplayString()
            .Should().Be("DRN.Framework.SharedKernel.Attributes.PersonalAttribute");
    }

    [Fact]
    public async Task Catalog_Field_Fixes_Should_Apply_The_Selected_Classification_And_Compile()
    {
        foreach (var (name, classification) in new[]
                 {
                     ("UserName", "Personal"), ("FirstName", "Personal"), ("LastName", "Personal"),
                     ("DateOfBirth", "Personal"), ("BirthDate", "Personal"), ("IBAN", "Personal"),
                     ("Cvv", "Sensitive"), ("Cvc", "Sensitive"), ("PinCode", "Sensitive")
                 })
        {
            using var workspace = new AdhocWorkspace();
            var project = CreateProject(workspace);
            var document = project.AddDocument("Model.cs",
                "public class Model { public string " + name + " { get; set; } = \"\"; }");
            var diagnostic = (await DiagnosticClassificationAnalyzerTests.Analyze(
                (CSharpCompilation)(await document.Project.GetCompilationAsync())!)).Single();
            diagnostic.Id.Should().Be("DRN0015");
            var actions = await GetActions(document, diagnostic, new ClassificationCodeFixProvider());
            var changed = await Apply(document, actions.Single(action => action.EquivalenceKey == classification));
            var after = (CSharpCompilation)(await changed.Project.GetCompilationAsync())!;
            (await DiagnosticClassificationAnalyzerTests.Analyze(after)).Should().BeEmpty();
            var property = after.GetTypeByMetadataName("Model")!.GetMembers(name).OfType<IPropertySymbol>().Single();
            property.GetAttributes().Should().ContainSingle().Which.AttributeClass!.ToDisplayString()
                .Should().Be("DRN.Framework.SharedKernel.Attributes." + classification + "Attribute");
        }
    }

    [Fact]
    public async Task FixAll_Should_Classify_Catalog_Fields_Across_Project_Documents()
    {
        foreach (var (classification, first, second) in new[]
                 {
                     ("Personal", "UserName", "BirthDate"),
                     ("Sensitive", "Cvv", "PinCode")
                 })
        {
            using var workspace = new AdhocWorkspace();
            var project = CreateProject(workspace);
            var model = project.AddDocument("Model.cs",
                "public class Model { public string " + first + " { get; set; } = \"\"; public string FileName { get; set; } = \"\"; }");
            var record = model.Project.AddDocument("Details.cs", "public record Details(string " + second + ");");
            var document = record.Project.GetDocument(model.Id)!;
            var before = await DiagnosticClassificationAnalyzerTests.Analyze(
                (CSharpCompilation)(await document.Project.GetCompilationAsync())!);
            before.Should().HaveCount(2).And.OnlyContain(diagnostic => diagnostic.Id == "DRN0015");
            var provider = new ClassificationCodeFixProvider();
            var context = new FixAllContext(document, provider, FixAllScope.Project, classification,
                provider.FixableDiagnosticIds, new ClassificationDiagnostics(), CancellationToken.None);
            var action = await provider.GetFixAllProvider().GetFixAsync(context);
            action.Should().NotBeNull();
            var changed = (await Apply(action!)).GetProject(project.Id)!;
            var after = (CSharpCompilation)(await changed.GetCompilationAsync())!;
            (await DiagnosticClassificationAnalyzerTests.Analyze(after)).Should().BeEmpty();
            foreach (var (type, name) in new[] { ("Model", first), ("Details", second) })
            {
                var property = after.GetTypeByMetadataName(type)!.GetMembers(name).OfType<IPropertySymbol>().Single();
                property.GetAttributes().Should().ContainSingle().Which.AttributeClass!.ToDisplayString()
                    .Should().Be("DRN.Framework.SharedKernel.Attributes." + classification + "Attribute");
            }
            after.GetTypeByMetadataName("Model")!.GetMembers("FileName").OfType<IPropertySymbol>().Single()
                .GetAttributes().Should().BeEmpty();
        }
    }

    [Fact]
    public async Task Invalid_Exemption_Fix_Should_Enable_Classification_Without_Changing_Constants()
    {
        foreach (var declaration in new[]
                 {
                     "[Sensitive(false)] public class Model { }",
                     "public class Model { [Sensitive(sensitive: false)] public string Value = \"\"; }",
                     "public record Model([field: Sensitive(false)] string Value);",
                     "[Sensitive(Flags.Disabled)] public class Model { } public static class Flags { public const bool Disabled = false; }",
                     "public class Model : SensitiveAttribute { public Model() : base(/* classification */ false) { } }",
                     "public class Model() : SensitiveAttribute(sensitive: false);"
                 })
        {
            using var workspace = new AdhocWorkspace();
            var project = CreateProject(workspace);
            var document = project.AddDocument("Model.cs", "using DRN.Framework.SharedKernel.Attributes;\n" + declaration);
            var diagnostic = (await DiagnosticClassificationAnalyzerTests.Analyze(
                (CSharpCompilation)(await document.Project.GetCompilationAsync())!)).Single();
            diagnostic.Id.Should().Be("DRN0016");
            var provider = new InvalidExemptionCodeFixProvider();
            provider.GetFixAllProvider().Should().BeNull();
            var actions = await GetActions(document, diagnostic, provider);
            actions.Should().ContainSingle();
            var action = actions.Single();
            action.Title.Should().Be("Use Sensitive(true)");
            var changed = await Apply(document, action);
            (await DiagnosticClassificationAnalyzerTests.Analyze((CSharpCompilation)(await changed.Project.GetCompilationAsync())!))
                .Should().BeEmpty();
            var text = (await changed.GetTextAsync()).ToString();
            if (declaration.Contains("Flags.Disabled", StringComparison.Ordinal))
                text.Should().Contain("Sensitive(true)").And.Contain("const bool Disabled = false");
            if (declaration.Contains("/* classification */", StringComparison.Ordinal))
                text.Should().Contain("/* classification */ true");
        }
    }

    [Fact]
    public async Task Invalid_Exemption_Fix_Should_Skip_Generated_Declarations()
    {
        foreach (var (name, source) in new[]
                 {
                     ("Model.g.cs", "[Sensitive(false)] public class Model { }"),
                     ("Model.cs", "// <auto-generated/>\n[Sensitive(false)] public class Model { }"),
                     ("Model.cs", "[System.CodeDom.Compiler.GeneratedCode(\"Generator\", \"1\")] public class Model { [Sensitive(false)] public string Value = \"\"; }"),
                     ("Model.cs", "public class Model { [System.CodeDom.Compiler.GeneratedCode(\"Generator\", \"1\"), Sensitive(false)] public string Value = \"\"; }")
                 })
        {
            using var workspace = new AdhocWorkspace();
            var project = CreateProject(workspace);
            var document = project.AddDocument(name, source.Replace("Sensitive(", "DRN.Framework.SharedKernel.Attributes.Sensitive(", StringComparison.Ordinal));
            (await DiagnosticClassificationAnalyzerTests.Analyze(
                (CSharpCompilation)(await document.Project.GetCompilationAsync())!)).Should().BeEmpty();
            var attribute = (await document.GetSyntaxRootAsync())!.DescendantNodes()
                .OfType<AttributeSyntax>()
                .First(item => item.Name.ToString().Contains("Sensitive"));
            var diagnostic = Diagnostic.Create(DiagnosticClassificationAnalyzer.InvalidExemption, attribute.GetLocation(), "Invalid exemption");
            var actions = await GetActions(document, diagnostic, new InvalidExemptionCodeFixProvider());
            actions.Should().BeEmpty();
        }
    }

    [Fact]
    public async Task Field_Classifications_Should_Not_Block_Property_Code_Fixes()
    {
        foreach (var selection in new[] { "Personal", "Sensitive", "SensitiveFalse" })
        foreach (var declaration in new[]
                 {
                     "public class Model { [field: CLASSIFICATION] public string Password { get; set; } = \"\"; }",
                     "public record Model([field: CLASSIFICATION] string Password);"
                 })
        {
            var classification = selection == "Personal" ? "Personal" : "Sensitive";
            var source = "using DRN.Framework.SharedKernel.Attributes;\n" + declaration.Replace("CLASSIFICATION", classification);
            using var workspace = new AdhocWorkspace();
            var project = CreateProject(workspace);
            var document = project.AddDocument("Model.cs", source);
            var compilation = (CSharpCompilation)(await document.Project.GetCompilationAsync())!;
            var diagnostic = (await DiagnosticClassificationAnalyzerTests.Analyze(compilation)).Single();
            diagnostic.Id.Should().Be("DRN0015");
            var provider = ClassificationProvider(selection);
            var actions = await GetActions(document, diagnostic, provider);
            var action = actions.Single(item => item.EquivalenceKey == selection);
            var changed = await Apply(document, action);
            var after = (CSharpCompilation)(await changed.Project.GetCompilationAsync())!;
            (await DiagnosticClassificationAnalyzerTests.Analyze(after)).Should().BeEmpty();
            var text = (await changed.GetTextAsync()).ToString();
            text.Should().Contain("[field: " + classification + "]");
            var property = after.GetTypeByMetadataName("Model")!.GetMembers("Password").OfType<IPropertySymbol>().Single();
            var attribute = property.GetAttributes().Single();
            attribute.AttributeClass!.Name.Should().Be(classification + "Attribute");
            if (selection == "SensitiveFalse") attribute.ConstructorArguments.Single().Value.Should().Be(false);
        }
    }

    [Fact]
    public async Task FixAll_Should_Apply_Only_The_Selected_Classification_To_Properties_And_Records()
    {
        using var workspace = new AdhocWorkspace();
        var project = CreateProject(workspace);
        var document = project.AddDocument("Model.cs", """
            public record Contact(string Email);
            public class Model
            {
                public string Password { get; set; } = "";
                [DRN.Framework.SharedKernel.Attributes.Personal]
                public string Phone { get; set; } = "";
            }
            """);
        var provider = new ClassificationCodeFixProvider();
        var context = new FixAllContext(document, provider, FixAllScope.Document, "Sensitive",
            provider.FixableDiagnosticIds, new ClassificationDiagnostics(), CancellationToken.None);
        var action = await provider.GetFixAllProvider().GetFixAsync(context);
        action.Should().NotBeNull();
        var changed = await Apply(document, action!);
        (await DiagnosticClassificationAnalyzerTests.Analyze((CSharpCompilation)(await changed.Project.GetCompilationAsync())!))
            .Should().BeEmpty();
        var text = (await changed.GetTextAsync()).ToString();
        text.Split("SensitiveAttribute").Should().HaveCount(3);
        text.Split(".Personal]").Should().HaveCount(2);
        text.Should().Contain("property:");
    }

    private sealed class ClassificationDiagnostics : FixAllContext.DiagnosticProvider
    {
        public override async Task<IEnumerable<Diagnostic>> GetDocumentDiagnosticsAsync(Document document, CancellationToken cancellationToken)
        {
            var tree = await document.GetSyntaxTreeAsync(cancellationToken);
            var diagnostics = await GetAllDiagnosticsAsync(document.Project, cancellationToken);
            return diagnostics.Where(diagnostic => diagnostic.Location.SourceTree == tree);
        }

        public override Task<IEnumerable<Diagnostic>> GetProjectDiagnosticsAsync(Project project, CancellationToken cancellationToken) =>
            Task.FromResult(Enumerable.Empty<Diagnostic>());

        public override async Task<IEnumerable<Diagnostic>> GetAllDiagnosticsAsync(Project project, CancellationToken cancellationToken) =>
            await DiagnosticClassificationAnalyzerTests.Analyze((CSharpCompilation)(await project.GetCompilationAsync(cancellationToken))!);
    }

    [Fact]
    public async Task Separate_Classification_And_Exemption_Fixes_Should_Compile_With_Name_Conflicts()
    {
        const string source = """
            public class SensitiveAttribute : System.Attribute;
            public class PersonalAttribute : System.Attribute;
            public class Model { public string Password { get; set; } = ""; }
            """;
        foreach (var selection in new[] { "Personal", "Sensitive", "SensitiveFalse" })
        {
            using var workspace = new AdhocWorkspace();
            var project = CreateProject(workspace);
            var document = project.AddDocument("Model.cs", SourceText.From(source));
            var compilation = (CSharpCompilation)(await document.Project.GetCompilationAsync())!;
            var diagnostic = (await DiagnosticClassificationAnalyzerTests.Analyze(compilation)).Single();
            var provider = ClassificationProvider(selection);
            var actions = await GetActions(document, diagnostic, provider);
            var action = actions.Single(item => item.EquivalenceKey == selection);
            var changed = await Apply(document, action);
            var after = (CSharpCompilation)(await changed.Project.GetCompilationAsync())!;
            (await DiagnosticClassificationAnalyzerTests.Analyze(after)).Should().BeEmpty();
            var text = (await changed.GetTextAsync()).ToString();
            text.Should().Contain("global::DRN.Framework.SharedKernel.Attributes.");
            text.Split("global::DRN.Framework.SharedKernel.Attributes.").Should().HaveCount(2);
        }
        new ClassificationCodeFixProvider().GetFixAllProvider().Should().NotBeNull();
        new NameExemptionCodeFixProvider().GetFixAllProvider().Should().BeNull();
    }

    [Fact]
    public async Task Generated_Property_Should_Not_Offer_A_Source_Edit()
    {
        using var workspace = new AdhocWorkspace();
        var project = CreateProject(workspace);
        var document = project.AddDocument("Model.g.cs", "public class Model { public string Password { get; set; } }");
        var diagnostics = await DiagnosticClassificationAnalyzerTests.Analyze((CSharpCompilation)(await document.Project.GetCompilationAsync())!);
        diagnostics.Should().BeEmpty();
        var property = (await document.GetSyntaxRootAsync())!.DescendantNodes()
            .OfType<PropertyDeclarationSyntax>().Single();
        var diagnostic = Diagnostic.Create(DiagnosticClassificationAnalyzer.MissingClassification, property.Identifier.GetLocation(), "Password");
        var actions = await GetActions(document, diagnostic, new ClassificationCodeFixProvider());
        actions.Should().BeEmpty();
    }
}
