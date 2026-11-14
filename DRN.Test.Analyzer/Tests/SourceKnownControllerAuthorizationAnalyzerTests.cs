using System.IO;

namespace DRN.Test.Analyzer.Tests;

public class SourceKnownControllerAuthorizationAnalyzerTests
{
    private const string Imports = """
        using System;
        using Microsoft.AspNetCore.Authorization;
        using Microsoft.AspNetCore.Mvc;
        using DRN.Framework.Hosting.Controllers;
        """;

    private const string Framework = """
        namespace Microsoft.AspNetCore.Authorization
        {
            public interface IAuthorizeData
            {
                string? Policy { get; set; }
                string? Roles { get; set; }
                string? AuthenticationSchemes { get; set; }
            }
            public interface IAllowAnonymous { }
            [System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
            public class AuthorizeAttribute : System.Attribute, IAuthorizeData
            {
                public AuthorizeAttribute() { }
                public AuthorizeAttribute(string? policy) { Policy = policy; }
                public string? Policy { get; set; }
                public string? Roles { get; set; }
                public string? AuthenticationSchemes { get; set; }
            }
            [System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Method, Inherited = true)]
            public sealed class AllowAnonymousAttribute : System.Attribute, IAllowAnonymous { }
        }
        namespace Microsoft.AspNetCore.Mvc
        {
            [System.AttributeUsage(System.AttributeTargets.Method, Inherited = true)]
            public sealed class NonActionAttribute : System.Attribute { }
            [System.AttributeUsage(System.AttributeTargets.Class, Inherited = true)]
            public sealed class NonControllerAttribute : System.Attribute { }
            [System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Method)]
            public sealed class ApiExplorerSettingsAttribute : System.Attribute { public bool IgnoreApi { get; set; } }
            public abstract class ControllerBase
            {
                [NonAction] public object Ok() => new object();
                [NonAction] public virtual object Utility() => new object();
            }
        }
        namespace DRN.Framework.Hosting.Controllers
        {
            [Microsoft.AspNetCore.Authorization.Authorize]
            public abstract class SourceKnownControllerBase<TEntity, TDto> : Microsoft.AspNetCore.Mvc.ControllerBase { }
        }
        """;

    [Theory]
    [InlineData("[Authorize(\"Readers\")]", "")]
    [InlineData("[Authorize(Policy = \"Readers\")]", "")]
    [InlineData("", "[Authorize(\"Readers\")]")]
    [InlineData("", "[Authorize(null, Policy = \"Readers\")]")]
    [InlineData("[Authorize(Roles = \"Admin\")][Authorize(\"Readers\")]", "")]
    public async Task Explicit_Class_Or_Action_Policy_Should_Qualify(string classAttribute, string methodAttribute)
    {
        var diagnostics = await Analyze(classAttribute + """
            public class TestController : SourceKnownControllerBase<object, object>
            {
            """ + methodAttribute + " public void Read() { } }");
        diagnostics.Should().BeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("[Authorize]")]
    [InlineData("[Authorize(\"\")]")]
    [InlineData("[Authorize(\"  \t\")]")]
    [InlineData("[Authorize(null)]")]
    [InlineData("[Authorize(\"Readers\", Policy = null)]")]
    [InlineData("[Authorize(\"Readers\", Policy = \" \")]")]
    [InlineData("[Authorize(Roles = \"Admin\", AuthenticationSchemes = \"Bearer\")]")]
    [InlineData("[ApiExplorerSettings(IgnoreApi = true)]")]
    public async Task Missing_Named_Policy_Should_Report_Ordinary_Custom_Action(string attribute)
    {
        var diagnostics = await Analyze("public class TestController : SourceKnownControllerBase<object, object> { " +
                                        attribute + " public void Read() { } }");
        AssertSingle(diagnostics, "Read");
    }

    [Theory]
    [InlineData("[AllowAnonymous]", "[Authorize(\"Readers\")]")]
    [InlineData("[Authorize(\"Readers\")]", "[AllowAnonymous]")]
    [InlineData("[AllowAnonymous][Authorize(\"Readers\")]", "")]
    public async Task Anonymous_Metadata_Should_Override_Named_Policy(string classAttribute, string methodAttribute)
    {
        var diagnostics = await Analyze(classAttribute +
            "public class TestController : SourceKnownControllerBase<object, object> { " + methodAttribute + " public void Read() { } }");
        AssertSingle(diagnostics, "Read");
        diagnostics[0].GetMessage().Should().Contain("anonymous access bypasses authorization");
    }

    [Fact]
    public async Task Override_Should_Inherit_Policy_And_NonAction_But_Hidden_Method_Should_Not()
    {
        var diagnostics = await Analyze("""
            public abstract class Base : SourceKnownControllerBase<object, object>
            {
                [Authorize("Readers")] public virtual void Read() { }
                [NonAction] public virtual void Disabled() { }
                [Authorize("Readers")] public void Hidden() { }
                [NonAction] public void HiddenDisabled() { }
            }
            public class TestController : Base
            {
                public override void Read() { }
                public override void Disabled() { }
                public new void Hidden() { }
                public new void HiddenDisabled() { }
                public override object Utility() => new object();
            }
            """);
        diagnostics.Select(LocationText).Should().BeEquivalentTo("Hidden", "HiddenDisabled");
    }

    [Fact]
    public async Task Inherited_Class_Policy_Should_Cover_Inherited_And_Custom_Actions()
    {
        var diagnostics = await Analyze("""
            [Authorize("Readers")]
            public abstract class Base : SourceKnownControllerBase<object, object> { public void Read() { } }
            public class TestController : Base { public void Custom() { } }
            """);
        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task Inherited_Anonymous_Metadata_Should_Be_Rejected()
    {
        var diagnostics = await Analyze("""
            public class PublicAccessAttribute : Attribute, IAllowAnonymous { }
            [PublicAccess]
            public abstract class Base : SourceKnownControllerBase<object, object>
            {
                [Authorize("Readers")] public virtual void Read() { }
                [NonAction] public void Disabled() { }
            }
            [Authorize("Readers")]
            public class TestController : Base { public override void Read() { } }
            """);
        AssertSingle(diagnostics, "Read");
    }

    [Fact]
    public async Task Non_Actions_And_Ineligible_Controllers_Should_Not_Report()
    {
        var diagnostics = await Analyze("""
            public abstract class AbstractController : SourceKnownControllerBase<object, object> { public void Read() { } }
            public class GenericController<T> : SourceKnownControllerBase<object, object> { public void Read() { } }
            internal class InternalController : SourceKnownControllerBase<object, object> { public void Read() { } }
            [NonController] public class ExcludedController : SourceKnownControllerBase<object, object> { public void Read() { } }
            public class ExcludedChildController : ExcludedController { }
            public class UnrelatedController { public void Read() { } }
            public class TestController : SourceKnownControllerBase<object, object>, IDisposable
            {
                public void Dispose() { }
                public override string ToString() => "test";
                public static void Static() { }
                public void Generic<T>() { }
                public int Property => 1;
                protected void Protected() { }
                [NonAction] public void Disabled() { }
            }
            internal class Outer { public class NestedController : SourceKnownControllerBase<object, object> { public void Read() { } } }
            public class GenericOuter<T> { public class NestedController : SourceKnownControllerBase<object, object> { public void Read() { } } }
            """);
        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task Public_Nested_Controller_And_Unrelated_Dispose_Should_Be_Checked()
    {
        var diagnostics = await Analyze("""
            public class Outer
            {
                public class NestedController : SourceKnownControllerBase<object, object> { public void Dispose() { } }
            }
            """);
        AssertSingle(diagnostics, "Dispose");
    }

    [Fact]
    public async Task Derived_Attribute_Requires_Explicit_Framework_Policy_Property()
    {
        var diagnostics = await Analyze("""
            public class ExplicitPolicyAttribute : AuthorizeAttribute { }
            public class ImplicitPolicyAttribute : AuthorizeAttribute { public ImplicitPolicyAttribute() : base("Readers") { } }
            public class HiddenPolicyAttribute : AuthorizeAttribute { public new string? Policy { get; set; } }
            public class RemappedPolicyAttribute : AuthorizeAttribute, IAuthorizeData
            {
                string? IAuthorizeData.Policy { get; set; }
            }
            public class TestController : SourceKnownControllerBase<object, object>
            {
                [ExplicitPolicy(Policy = "Readers")] public void Explicit() { }
                [ImplicitPolicy] public void Implicit() { }
                [HiddenPolicy(Policy = "Readers")] public void Hidden() { }
                [RemappedPolicy(Policy = "Readers")] public void Remapped() { }
            }
            """);
        diagnostics.Select(LocationText).Should().BeEquivalentTo("Implicit", "Hidden", "Remapped");
    }

    [Fact]
    public async Task AttributeUsage_Should_Control_Inheritance_And_Replacement()
    {
        var diagnostics = await Analyze("""
            [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = false)]
            public class LocalPolicyAttribute : AuthorizeAttribute { }
            [AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
            public class SinglePolicyAttribute : AuthorizeAttribute { }
            [LocalPolicy(Policy = "Readers")]
            public abstract class Base : SourceKnownControllerBase<object, object>
            {
                [LocalPolicy(Policy = "Readers")] public virtual void Local() { }
                [SinglePolicy(Policy = "Readers")] public virtual void Replaced() { }
            }
            public class TestController : Base
            {
                public override void Local() { }
                [SinglePolicy] public override void Replaced() { }
            }
            """);
        diagnostics.Select(LocationText).Should().BeEquivalentTo("Local", "Replaced");
    }

    [Fact]
    public async Task Conditional_Policy_Attribute_Should_Not_Qualify_As_Runtime_Policy()
    {
        var diagnostics = await Analyze("""
            [System.Diagnostics.Conditional("UNDEFINED_POLICY")]
            public class ConditionalPolicyAttribute : AuthorizeAttribute { }
            public class TestController : SourceKnownControllerBase<object, object>
            {
                [ConditionalPolicy(Policy = "Readers")] public void Read() { }
            }
            """);
        AssertSingle(diagnostics, "Read");
    }

    [Fact]
    public async Task Hiding_Should_Respect_Constructed_Generic_Parameters_And_Return_Types()
    {
        var diagnostics = await Analyze("""
            public class Outer<T> { public class Nested { } }
            public abstract class Base<T> : SourceKnownControllerBase<object, object>
            {
                public void Read(T value) { }
                public void Read(int value) { }
                public void Nested(Outer<int>.Nested value) { }
                public object ReturnType() => new object();
            }
            public class TestController : Base<string>
            {
                [Authorize("Readers")] public new void Read(string value) { }
                [Authorize("Readers")] public void Nested(Outer<string>.Nested value) { }
                [Authorize("Readers")] public new string ReturnType() => "value";
            }
            """);
        diagnostics.Should().HaveCount(3);
        diagnostics.Select(LocationText).Should().OnlyContain(location => location == "TestController");
        diagnostics.Select(diagnostic => diagnostic.GetMessage()).Should().Contain(message => message.Contains("'Read'"));
        diagnostics.Select(diagnostic => diagnostic.GetMessage()).Should().Contain(message => message.Contains("'Nested'"));
        diagnostics.Select(diagnostic => diagnostic.GetMessage()).Should().Contain(message => message.Contains("'ReturnType'"));
    }

    [Fact]
    public async Task Referenced_Base_Metadata_Should_Preserve_Policy_And_Disabled_Action_Inheritance()
    {
        var framework = EmitReference(Compile("Framework", Framework));
        var library = EmitReference(Compile("Controllers", Imports + """
            public abstract class Base<T> : SourceKnownControllerBase<object, object>
            {
                [Authorize("Readers")] public virtual void Read(T value) { }
                [NonAction] public virtual void Disabled() { }
                public void Unprotected() { }
            }
            """, framework));
        var compilation = Compile("Consumer", Imports + """
            public class TestController : Base<string>
            {
                public override void Read(string value) { }
                public override void Disabled() { }
            }
            """, framework, library);
        var diagnostics = await Analyze(compilation);
        AssertSingle(diagnostics, "TestController");
        diagnostics[0].GetMessage().Should().Contain("'Unprotected'");
    }

    [Fact]
    public async Task Generated_Controller_Should_Still_Require_Policy()
    {
        var compilation = Compile("Generated", "// <auto-generated/>\n" + Imports + """
            public class TestController : SourceKnownControllerBase<object, object> { public void Read() { } }
            """ + Framework);
        AssertSingle(await Analyze(compilation), "Read");
    }

    private static Task<ImmutableArray<Diagnostic>> Analyze(string source)
        => Analyze(Compile("Test", Imports + source + Framework));

    private static async Task<ImmutableArray<Diagnostic>> Analyze(CSharpCompilation compilation)
    {
        compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).Should().BeEmpty();
        return await compilation.WithAnalyzers([new SourceKnownControllerAuthorizationAnalyzer()]).GetAnalyzerDiagnosticsAsync();
    }

    private static CSharpCompilation Compile(string name, string source, params MetadataReference[] extraReferences)
    {
        var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(Path.Combine(Path.GetDirectoryName(typeof(object).Assembly.Location)!, "System.Runtime.dll"))
        };
        references.AddRange(extraReferences);
        return CSharpCompilation.Create(name, [CSharpSyntaxTree.ParseText(source)], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
    }

    private static MetadataReference EmitReference(CSharpCompilation compilation)
    {
        using var stream = new MemoryStream();
        var result = compilation.Emit(stream);
        result.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).Should().BeEmpty();
        result.Success.Should().BeTrue();
        return MetadataReference.CreateFromImage(stream.ToArray());
    }

    private static string LocationText(Diagnostic diagnostic)
        => diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan);

    private static void AssertSingle(ImmutableArray<Diagnostic> diagnostics, string identifier)
    {
        diagnostics.Should().ContainSingle();
        diagnostics[0].Id.Should().Be("DRN0026");
        diagnostics[0].Severity.Should().Be(DiagnosticSeverity.Error);
        LocationText(diagnostics[0]).Should().Be(identifier);
    }
}
