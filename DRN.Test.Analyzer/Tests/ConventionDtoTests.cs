using DRN.Framework.SharedKernel.Analyzers.Conventions;
using static DRN.Test.Analyzer.Tests.ClassificationTestFixture;

namespace DRN.Test.Analyzer.Tests;

public sealed class ConventionDtoTests
{
    private static readonly Lazy<MetadataReference> SharedUserDtoContract = new(() =>
    {
        var contract = Compilation("""
            using DRN.Framework.SharedKernel.Domain;
            namespace Shared.Contract.QA.Users;
            public class UserDto(SourceKnownEntity? entity = null) : Dto(entity);
            """).WithAssemblyName("Shared.Contract");
        contract.AssertNoCompilerErrors();
        return AnalyzerTestCompilation.EmitReference(contract, "/shared-dto/Shared.Contract.dll", AnalyzerTestCompilation.ReferenceAssemblyOptions);
    });

    [Theory]
    [DataInline("", "", "=> new(this);", "")]
    [DataInline("[Personal]", "Display", "=> new(this) { Display = Name };", "")]
    [DataInline("[Sensitive]", "Display", "=> new(this) { Display = Secret };", "")]
    [DataInline("", "Display", "=> new(this) { Display = nameof(Secret) };", "")]
    [DataInline("", "Display", "=> new(this) { Display = Secret };", "DRNDTO003")]
    [DataInline("[Personal]", "Display", "=> new(this) { Display = Secret };", "DRNDTO003")]
    [DataInline("[Sensitive(false)]", "Display", "=> new(this) { Display = Secret };", "DRNDTO003")]
    [DataInline("", "Display", "=> new(this) { Display = Name.Trim() };", "DRNDTO003")]
    [DataInline("[Personal]", "Display", "=> new(this) { Display = Name + Secret };", "DRNDTO003")]
    [DataInline("[Personal, Sensitive]", "Display", "=> new(this) { Display = Name + Secret };", "")]
    [DataInline("[Personal]", "Display", "{ return new UserDto(this) { Display = Name }; }", "")]
    [DataInline("", "Name", "=> new(this);", "DRNDTO003")]
    [DataInline("", "Name", "=> new(this) { Name = string.Empty };", "")]
    [DataInline("[Sensitive]", "Name", "=> new(this) { Name = Secret };", "")]
    [DataInline("", "", "=> new();", "DRNDTO001")]
    [DataInline("", "", "=> new(null);", "DRNDTO001")]
    [DataInline("", "", "", "DRNDTO001")]
    public void Projection_Should_Require_The_Source_And_Only_Exposed_Classifications(
        string attribute, string property, string body, string expected)
    {
        var member = property.Length == 0 ? "" : $"{attribute} public string {property} {{ get; init; }} = string.Empty;";
        var contract = Compilation($$"""
            using DRN.Framework.SharedKernel.Attributes;
            using DRN.Framework.SharedKernel.Domain;
            namespace Shared.Contract.QA.Users;
            public class UserDto(SourceKnownEntity? entity = null) : Dto(entity) { {{member}} }
            """).WithAssemblyName("Shared.Contract");
        contract.AssertNoCompilerErrors();
        var baseDeclaration = body.Length == 0
            ? "public abstract class UserBase : SourceKnownEntity { public override Dto ToDto() => new(this); }"
            : "";
        var baseType = body.Length == 0 ? "UserBase" : "SourceKnownEntity";
        var mapper = body.Length == 0 ? "" : "public override UserDto ToDto() " + body;
        var domain = Compilation($$"""
            using DRN.Framework.SharedKernel.Attributes;
            using DRN.Framework.SharedKernel.Domain;
            using Shared.Contract.QA.Users;
            namespace Shop.Domain.QA.User;
            {{baseDeclaration}}
            public class UserEntity : {{baseType}}
            {
                [Personal] public string Name => "";
                [Sensitive] public string Secret => "";
                {{mapper}}
            }
            """).WithAssemblyName("Shop.Domain").AddReferences(contract.ToMetadataReference());
        domain.AssertNoCompilerErrors();
        var diagnostics = Analyze(domain, "Domain");
        if (expected.Length == 0) diagnostics.Should().BeEmpty();
        else diagnostics.Should().ContainSingle().Which.Id.Should().Be(expected);
    }

    [Theory]
    [DataInline("QA.Categories", false)]
    [DataInline("QA", true)]
    [DataInline("QA.Tags", true)]
    public void Dto_Namespace_Should_Include_Subdomain_And_Entity_Folder(string area, bool misplaced)
    {
        var contract = Compilation($$"""
            using DRN.Framework.SharedKernel.Domain;
            namespace Shop.Contract.{{area}};
            public class CategoryDto(SourceKnownEntity? entity = null) : Dto(entity);
            """).WithAssemblyName("Shop.Contract");
        contract.AssertNoCompilerErrors();
        var diagnostics = Analyze(contract, "Contract");
        if (misplaced) diagnostics.Should().ContainSingle().Which.Id.Should().Be("DRNDTO002");
        else diagnostics.Should().BeEmpty();
    }

    [Theory]
    [DataInlineUnit("public static new UserDto ToDto() => new();")]
    [DataInlineUnit("private new UserDto ToDto() => new(this);")]
    [DataInlineUnit("public UserDto ToDto(int unused = 0) => new(this);")]
    [DataInlineUnit("public UserDto ToDto<T>() => new(this);")]
    [DataInlineUnit("public override Dto ToDto() => new(this);")]
    [DataInlineUnit("public new UserDto ToDto() => new(this);")]
    public void Mapper_Should_Be_A_Public_Parameterless_Typed_Instance_Override(string method)
    {
        var domain = Compilation($$"""
            using DRN.Framework.SharedKernel.Domain;
            using Shared.Contract.QA.Users;
            namespace Shop.Domain.QA.User;
            public abstract class UserBase : SourceKnownEntity { public override Dto ToDto() => new(this); }
            public class UserEntity : UserBase { {{method}} }
            """).WithAssemblyName("Shop.Domain").AddReferences(SharedUserDtoContract.Value);
        domain.AssertNoCompilerErrors();
        Analyze(domain, "Domain").Should().ContainSingle().Which.Id.Should().Be("DRNDTO001");
    }

    [Theory]
    [DataInline("this", "[Personal]", "")]
    [DataInline("this", "", "DRNDTO003")]
    [DataInline("", "[Personal]", "DRNDTO001")]
    [DataInline("null", "[Personal]", "DRNDTO001")]
    [DataInline("this, 2", "[Personal]", "DRNDTO001")]
    public void Mapper_Should_Allow_Omitted_Optional_Constructor_Arguments(string arguments, string annotation, string expected)
    {
        var diagnostics = Project($$"""
            public class UserDto(SourceKnownEntity? entity = null, int version = 1) : Dto(entity)
            {
                {{annotation}} public string Display { get; init; } = "";
            }
            """, $$"""
            public class UserEntity : SourceKnownEntity
            {
                [Personal] public string Name => "";
                public override UserDto ToDto() => new({{arguments}}) { Display = Name };
            }
            """);
        if (expected.Length == 0) diagnostics.Should().BeEmpty();
        else diagnostics.Should().ContainSingle().Which.Id.Should().Be(expected);
    }

    [Theory]
    [DataInline("public", "ProfileDto", "QA.Users", "this")]
    [DataInline("public", "UserDto", "QA.Other", "this")]
    [DataInline("internal", "UserDto", "QA.Users", "this")]
    [DataInline("public", "UserDto", "QA.Users", "this, 2")]
    public void Mapper_Contract_And_Projection_Should_Report_Independently(string access, string dto, string area, string arguments)
    {
        var diagnostics = Project($$"""
            {{access}} class {{dto}}(SourceKnownEntity? entity = null, int version = 1) : Dto(entity)
            {
                public string Label { get; init; } = "";
            }
            """, $$"""
            internal class UserEntity : SourceKnownEntity
            {
                [Personal] public string Email => "";
                public override {{dto}} ToDto() => new({{arguments}}) { Label = Email };
            }
            """, area);
        diagnostics.Select(diagnostic => diagnostic.Id).Should().BeEquivalentTo("DRNDTO001", "DRNDTO003");
        diagnostics.Single(item => item.Id == "DRNDTO003").GetMessage().Should()
            .Be("Projected property 'Label' requires [Personal] from mapped property 'Email'.");
    }

    [Theory]
    [DataInline("OrderRequest", "QA.Orders", "QA/Orders", false, false)]
    [DataInline("OrderResponse", "QA.Orders", "QA/Other", false, true)]
    [DataInline("OrderRequest", "QA", "QA/Orders", false, true)]
    [DataInline("ProductBatchRequest", "QA.Products", "QA/Products", false, false)]
    [DataInline("ProductBatchResponse", "QA.Products", "QA/Products", true, true)]
    [DataInline("ProductBatchRequest", "QA.ProductBatches", "QA/ProductBatches", true, false)]
    [DataInline("OrderingRequest", "QA", "QA", false, false)]
    [DataInline("SearchResponse", "QA", "QA", false, false)]
    [DataInline("OrderRequestOptions", "QA", "QA", false, false)]
    [DataInline("ProductBatchResponse", "Other.Products", "Other/Products", true, false)]
    public void Messages_Should_Use_The_Longest_Entity_Prefix_In_Their_Subdomain(
        string name, string area, string directory, bool batchDto, bool misplaced)
    {
        var compilation = Compilation($"namespace Shop.Contract.{area}; public record {name};", "Unrelated.cs").WithAssemblyName("Shop.Contract");
        var paths = new List<ConventionInput> { PathInput("Unrelated.cs", directory + "/Unrelated.cs") };
        AddDto("QA.Orders", "OrderDto");
        AddDto("QA.Products", "ProductDto");
        AddDto("Other.Products", "ProductDto");
        if (batchDto) AddDto("QA.ProductBatches", "ProductBatchDto");
        compilation.AssertNoCompilerErrors();
        var diagnostics = Analyze(compilation, "Contract", [.. paths]);
        if (misplaced) diagnostics.Should().ContainSingle().Which.Id.Should().Be("DRNDTO004");
        else diagnostics.Should().BeEmpty();

        void AddDto(string dtoArea, string dtoName)
        {
            var file = dtoArea.Replace('.', '/') + "/" + dtoName + ".cs";
            compilation = compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText(
                $"namespace Shop.Contract.{dtoArea}; public class {dtoName} : DRN.Framework.SharedKernel.Domain.Dto;", path: file));
            paths.Add(PathInput(file, file));
        }
    }

    [Fact]
    public void Messages_Should_Follow_The_Actual_Namespace_And_Logical_Folder_Of_A_Misplaced_Dto()
    {
        var compilation = Compilation("""
            namespace Shop.Contract.QA.Legacy;
            public class OrderDto : DRN.Framework.SharedKernel.Domain.Dto;
            public record OrderRequest;
            """, "Dto.cs").WithAssemblyName("Shop.Contract").AddSyntaxTrees(CSharpSyntaxTree.ParseText(
                "namespace Shop.Contract.QA.Orders; public record OrderResponse;", path: "Response.cs"));
        compilation.AssertNoCompilerErrors();
        var diagnostics = Analyze(compilation, "Contract", PathInput("Dto.cs", "QA/Linked/Dto.cs"),
            PathInput("Response.cs", "QA/Orders/Response.cs"));
        diagnostics.Select(item => item.Id).Should().BeEquivalentTo("DRNDTO002", "DRNDTO004");
        diagnostics.Single(item => item.Id == "DRNDTO004").GetMessage().Should()
            .Be("Place 'OrderResponse' in 'QA/Linked' with namespace 'Shop.Contract.QA.Legacy' alongside its entity DTO.");
    }

    [Fact]
    public void Every_Public_Request_Part_Should_Use_Its_Logical_Compile_Folder()
    {
        var compilation = Compilation("""
            using DRN.Framework.SharedKernel.Domain;
            namespace Shop.Contract.QA.Orders { public class OrderDto : Dto; public partial class OrderRequest; }
            namespace Shop.Contract.QA { internal record OrderResponse; }
            """, "First.cs").WithAssemblyName("Shop.Contract")
            .AddSyntaxTrees(CSharpSyntaxTree.ParseText("namespace Shop.Contract.QA.Orders; public partial class OrderRequest;",
                path: "Linked.cs"));
        compilation.AssertNoCompilerErrors();
        var diagnostics = Analyze(compilation, "Contract", PathInput("First.cs", "QA/Orders/First.cs"),
            PathInput("Linked.cs", "QA/Shared/Linked.cs"));
        diagnostics.Should().ContainSingle().Which.Id.Should().Be("DRNDTO004");
    }

    [Theory]
    [DataInline("[Personal]", "", "", "", "DRNDTO003")]
    [DataInline("[Personal]", "", "[Personal]", "", "")]
    [DataInline("", "[Personal]", "[Personal]", "", "DRNDTO003")]
    [DataInline("[Sensitive]", "[Personal]", "[Sensitive]", "[Personal]", "")]
    [DataInline("[Sensitive]", "[Personal]", "[Personal]", "[Sensitive]", "DRNDTO003")]
    public void Type_Protection_Cannot_Replace_The_Source_Property_Annotation(string entityAttribute,
        string sourceAttribute, string dtoAttribute, string targetAttribute, string expected)
    {
        var diagnostics = Project($$"""
            {{dtoAttribute}} public class UserDto(SourceKnownEntity? entity = null) : Dto(entity)
            {
                {{targetAttribute}} public string Display { get; init; } = "";
            }
            """, $$"""
            {{entityAttribute}} public class UserEntity : SourceKnownEntity
            {
                {{sourceAttribute}} public string Name => "";
                public override UserDto ToDto() => new(this) { Display = Name };
            }
            """);
        if (expected.Length == 0) diagnostics.Should().BeEmpty();
        else diagnostics.Should().ContainSingle().Which.Id.Should().Be(expected);
    }

    [Theory]
    [DataInline("[Personal]", "Contact.Email", "new()", false)]
    [DataInline("", "Contact.Email", "new()", true)]
    [DataInline("[Personal]", "\"public literal\"", "new()", false)]
    [DataInline("", "Contact.Email", "", true)]
    public void Nested_Assignments_Should_Check_The_Leaf_Destination(string annotation, string value, string construction, bool missing)
    {
        var diagnostics = Project($$"""
            public class ContactView { {{annotation}} public string Label { get; set; } = ""; }
            public class UserDto(SourceKnownEntity? entity = null) : Dto(entity)
            {
                public ContactView Details { get; init; } = new();
            }
            """, $$"""
            public class ContactInfo { [Personal] public string Email => ""; }
            public class UserEntity : SourceKnownEntity
            {
                public ContactInfo Contact { get; } = new();
                public override UserDto ToDto() => new(this) { Details = {{construction}} { Label = {{value}} } };
            }
            """);
        if (!missing) diagnostics.Should().BeEmpty();
        else diagnostics.Should().ContainSingle().Which.GetMessage().Should().Be("Projected property 'Label' requires [Personal] from mapped property 'Email'.");
    }

    private const string MissingKeyClassification = "Projected property 'Extra' requires [Personal] from mapped property 'Email'.";
    private const string MissingValueClassification = "Projected property 'Extra' requires [Sensitive] from mapped property 'Secret'.";

    [Theory]
    [DataInline("Generic.List<ContactDto>", "", "[Personal]", "new ContactDto { Label = Email }", "")]
    [DataInline("Generic.List<ContactDto>", "[Personal]", "", "new ContactDto { Label = Email }", "Label")]
    [DataInline("Generic.HashSet<ContactDto>", "[Personal]", "", "new ContactDto { Label = Email }", "Label")]
    [DataInline("ArrayList", "[Personal]", "", "new ContactDto { Label = Email }", "Label")]
    [DataInline("Generic.List<ContactDto>", "", "[Personal]", "Copy(new ContactDto { Label = Email })", "Contacts")]
    [DataInline("Generic.List<ContactDto>", "[Personal]", "", "Copy(new ContactDto { Label = Email })", "")]
    public void Collection_Elements_And_Helper_Results_Should_Classify_Their_Destination(
        string collection, string collectionAnnotation, string elementAnnotation, string value, string missing)
    {
        var diagnostics = Project($$"""
            public class ContactDto { {{elementAnnotation}} public string Label { get; init; } = ""; }
            public class UserDto(SourceKnownEntity? e = null) : Dto(e)
            {
                {{collectionAnnotation}} public System.Collections.{{collection}} Contacts { get; init; } = new();
            }
            """, $$"""
            public class UserEntity : SourceKnownEntity
            {
                [Personal] public string Email => "";
                private static ContactDto Copy(ContactDto input) => new() { Label = input.Label };
                public override UserDto ToDto() => new(this) { Contacts = new() { {{value}} } };
            }
            """);
        if (missing.Length == 0) diagnostics.Should().BeEmpty();
        else diagnostics.Should().ContainSingle().Which.GetMessage().Should()
            .Be($"Projected property '{missing}' requires [Personal] from mapped property 'Email'.");
    }

    [Theory]
    [DataInline("System.Collections.Generic.List<string>", "Email")]
    [DataInline("Labels", "new ContactDto { Label = Email }")]
    [DataInline("RelistedLabels", "new ContactDto { Label = Email }")]
    public void Scalar_Elements_And_Custom_Add_Inputs_Should_Classify_The_Collection(string collection, string element)
    {
        var diagnostics = Project($$"""
            public class ContactDto { [Personal] public string Label { get; init; } = ""; }
            public class Labels : System.Collections.Generic.List<string>
            {
                public void Add(ContactDto contact) => Add(contact.Label);
            }
            public class RelistedLabels : System.Collections.Generic.List<object>, System.Collections.Generic.ICollection<object>
            {
                public new void Add(object item) => base.Add(((ContactDto)item).Label);
            }
            public class UserDto(SourceKnownEntity? e = null) : Dto(e)
            {
                public {{collection}} Contacts { get; init; } = new();
            }
            """, $$"""
            public class UserEntity : SourceKnownEntity
            {
                [Personal] public string Email => "";
                public override UserDto ToDto() => new(this) { Contacts = new() { {{element}} } };
            }
            """);
        var diagnostic = diagnostics.Should().ContainSingle().Which;
        diagnostic.Id.Should().Be("DRNDTO003");
        diagnostic.GetMessage().Should().Be("Projected property 'Contacts' requires [Personal] from mapped property 'Email'.");
    }

    [Theory]
    [DataInline("new()", "[Personal, Sensitive]", "Email", "Secret", "")]
    [DataInline("", "[Personal, Sensitive]", "Email", "Secret", "")]
    [DataInline("new()", "[Sensitive]", "Email", "Secret", MissingKeyClassification)]
    [DataInline("", "[Sensitive]", "Email", "Secret", MissingKeyClassification)]
    [DataInline("new()", "[Personal]", "Email", "Secret", MissingValueClassification)]
    [DataInline("", "[Personal]", "Email", "Secret", MissingValueClassification)]
    [DataInline("new()", "", "\"key\"", "\"value\"", "")]
    public void Dictionary_Keys_And_Values_Should_Classify_The_Collection_Property(
        string construction, string annotation, string key, string value, string missing)
    {
        var diagnostics = Project($$"""
            public class UserDto(SourceKnownEntity? e = null) : Dto(e)
            {
                {{annotation}} public System.Collections.Generic.Dictionary<string,string> Extra { get; init; } = new();
            }
            """, $$"""
            public class UserEntity : SourceKnownEntity
            {
                [Personal] public string Email => "";
                [Sensitive] public string Secret => "";
                public override UserDto ToDto() => new(this) { Extra = {{construction}} { [{{key}}] = {{value}} } };
            }
            """);
        if (missing.Length == 0)
        {
            diagnostics.Should().BeEmpty();
            return;
        }
        var diagnostic = diagnostics.Should().ContainSingle().Which;
        diagnostic.Id.Should().Be("DRNDTO003");
        diagnostic.GetMessage().Should().Be(missing);
    }

    [Theory]
    [DataInline("[Personal]", "", false)]
    [DataInline("", "[Personal]", true)]
    [DataInline("[Sensitive]", "[Personal]", true)]
    public void Helper_Inputs_Should_Classify_The_Property_Receiving_The_Result(
        string destinationAnnotation, string inputAnnotation, bool missing)
    {
        var diagnostics = Project($$"""
            public class DetailsView { {{destinationAnnotation}} public string Display { get; init; } = ""; }
            public class UserDto(SourceKnownEntity? e = null) : Dto(e) { public DetailsView Details { get; init; } = new(); }
            """, $$"""
            public class Options { {{inputAnnotation}} public string Prefix { get; init; } = ""; }
            public class UserEntity : SourceKnownEntity
            {
                [Personal] public string Name => "";
                private static string Format(Options options) => options.Prefix;
                public override UserDto ToDto() => new(this) { Details = new() { Display = Format(new Options { Prefix = Name }) } };
            }
            """);
        if (!missing) diagnostics.Should().BeEmpty();
        else
        {
            var diagnostic = diagnostics.Should().ContainSingle().Which;
            diagnostic.Id.Should().Be("DRNDTO003");
            diagnostic.GetMessage().Should().Be("Projected property 'Display' requires [Personal] from mapped property 'Name'.");
        }
    }

    [Theory]
    [DataInline("[Personal]", false)]
    [DataInline("", true)]
    public void Override_Should_Be_Checked_Once(string annotation, bool missing)
    {
        var diagnostics = Project($$"""
            public abstract class UserBase(SourceKnownEntity? e) : Dto(e) { public virtual string Name { get; init; } = ""; }
            public class UserDto(SourceKnownEntity? e = null) : UserBase(e) { {{annotation}} public override string Name { get; init; } = ""; }
            """, """
            public class UserEntity : SourceKnownEntity
            {
                [Personal] public string Name => "";
                public override UserDto ToDto() => new(this) { Name = Name };
            }
            """);
        if (missing) diagnostics.Should().ContainSingle().Which.Id.Should().Be("DRNDTO003");
        else diagnostics.Should().BeEmpty();
    }

    [Theory]
    [DataInline("Code.ToString()", "Sensitive")]
    [DataInline("Contact.Label", "Personal")]
    [DataInline("Alias", "Personal")]
    [DataInline("((IProfile)this).Alias", "Personal")]
    [DataInline("((IProfile)this).Detail", "Sensitive")]
    [DataInline("CodeName", "Sensitive")]
    public void Projection_Should_Recognize_Classified_Value_Types_Receivers_And_Interface_Members(string value, string kind)
    {
        var diagnostics = Project("""
            public class UserDto(SourceKnownEntity? e = null) : Dto(e) { public string Display { get; init; } = ""; }
            """, $$"""
            [Sensitive] public struct CodeValue { }
            [Personal] public class ContactInfo { public string Label => ""; }
            public interface IProfile { [Personal] string Alias { get; } string Detail { get; } }
            [System.AttributeUsage(System.AttributeTargets.Interface)]
            public class ClassifiedProfileAttribute : SensitiveAttribute;
            [ClassifiedProfile] public interface ICodes { string CodeName { get; } }
            public class UserEntity : SourceKnownEntity, IProfile, ICodes
            {
                public CodeValue? Code => null;
                public ContactInfo Contact { get; } = new();
                public string Alias => "";
                [Sensitive] string IProfile.Detail => "";
                public string CodeName => "";
                public override UserDto ToDto() => new(this) { Display = {{value}} };
            }
            """);
        diagnostics.Should().ContainSingle().Which.GetMessage().Should().Contain($"'Display' requires [{kind}]");
    }

    [Theory]
    [DataInline("((IProfile)Profile).Alias", "Alias", "Personal")]
    [DataInline("((IProfile)Profile)?.Alias ?? \"\"", "Alias", "Personal")]
    [DataInline("((IProfile?)OptionalProfile)?.Alias ?? \"\"", "Alias", "Personal")]
    [DataInline("((IProfile?)OptionalProfile)?.Alias?.Trim() ?? \"\"", "Alias", "Personal")]
    [DataInline("((IClassified)Profile).Title", "Title", "Sensitive")]
    [DataInline("((IClassified?)OptionalProfile)?.Title ?? \"\"", "Title", "Sensitive")]
    [DataInline("((ClassifiedProfile)View).Label", "Label", "Sensitive")]
    public void Receiver_Should_Keep_The_Known_Concrete_Type_Across_A_Cast(string value, string property, string kind)
    {
        var diagnostics = Project("""
            public class UserDto(SourceKnownEntity? e = null) : Dto(e) { public string Display { get; init; } = ""; }
            """, $$"""
            public interface IProfile { string Alias { get; } string Title { get; } }
            [System.AttributeUsage(System.AttributeTargets.Interface)]
            public class ClassifiedAttribute : SensitiveAttribute;
            [Classified] public interface IClassified : IProfile;
            public class ProfileBase { public string Label => ""; }
            public class ProfileInfo : ProfileBase, IClassified
            {
                [Personal] string IProfile.Alias => "";
                public string Title => "";
            }
            [Sensitive] public class ClassifiedProfile : ProfileInfo;
            public class UserEntity : SourceKnownEntity
            {
                public ProfileInfo Profile => new();
                public ProfileInfo? OptionalProfile { get; init; }
                public IProfile View => new ClassifiedProfile();
                public override UserDto ToDto() => new(this) { Display = {{value}} };
            }
            """);
        var diagnostic = diagnostics.Should().ContainSingle().Which;
        diagnostic.Id.Should().Be("DRNDTO003");
        diagnostic.GetMessage().Should().Be($"Projected property 'Display' requires [{kind}] from mapped property '{property}'.");
    }

    [Theory]
    [DataInline("((ProfileBase)Profile).Alias", "", "Alias")]
    [DataInline("((ProfileBase)Profile).Alias", "[Personal]", null)]
    [DataInline("((IProfile)Profile).Alias", "", "Alias")]
    [DataInline("((IProfile)Profile)?.Alias ?? \"\"", "[Personal]", null)]
    [DataInline("((ProfileBase)Profile).Label", "", null)]
    [DataInline("((ProfileBase)Profile).Code", "", null)]
    [DataInline("((ContactProfile)Profile).Code", "", "Code")]
    public void Projection_Should_Follow_Only_The_Selected_Virtual_Property(
        string value, string annotation, string? missingFrom)
    {
        var diagnostics = Project($$"""
            public class UserDto(SourceKnownEntity? e = null) : Dto(e) { {{annotation}} public string Display { get; init; } = ""; }
            """, $$"""
            public interface IProfile { string Alias { get; } }
            public class ProfileBase : IProfile
            {
                public virtual string Alias => "";
                public string Label => "";
                public virtual string Code => "";
            }
            public class ContactProfile : ProfileBase
            {
                [Personal] public override string Alias => "";
                [Personal] public new string Label => "";
                public new virtual string Code => "";
            }
            public sealed class CustomerProfile : ContactProfile { [Personal] public override string Code => ""; }
            public class UserEntity : SourceKnownEntity
            {
                public CustomerProfile Profile => new();
                public override UserDto ToDto() => new(this) { Display = {{value}} };
            }
            """);
        if (missingFrom is null)
        {
            diagnostics.Should().BeEmpty();
            return;
        }
        var diagnostic = diagnostics.Should().ContainSingle().Which;
        diagnostic.Id.Should().Be("DRNDTO003");
        diagnostic.GetMessage().Should().Be($"Projected property 'Display' requires [Personal] from mapped property '{missingFrom}'.");
    }

    [Theory]
    [DataInline("((ProfileBase)Profile).Alias", "", "Alias")]
    [DataInline("((ProfileBase)Profile).Alias", "[Personal]", null)]
    [DataInline("((IBaseView)Profile).Alias", "", "Alias")]
    [DataInline("((IBaseView)Profile).Alias", "[Personal]", null)]
    [DataInline("((ProfileBase)Profile).Label", "", null)]
    public void Upcast_Receiver_Should_Preserve_Interface_Classifications_On_Resolved_Member(
        string value, string annotation, string? missingFrom)
    {
        var diagnostics = Project($$"""
            public class UserDto(SourceKnownEntity? e = null) : Dto(e) { {{annotation}} public string Display { get; init; } = ""; }
            """, $$"""
            public interface IBaseView { string Alias { get; } }
            public interface IAnnotated { [Personal] string Alias { get; } [Personal] string Label { get; } }
            public class ProfileBase
            {
                public virtual string Alias => "";
                public string Label => "";
            }
            public sealed class DerivedProfile : ProfileBase, IBaseView, IAnnotated
            {
                public override string Alias => "";
                public new string Label => "";
            }
            public class UserEntity : SourceKnownEntity
            {
                public DerivedProfile Profile => new();
                public override UserDto ToDto() => new(this) { Display = {{value}} };
            }
            """);
        if (missingFrom is null)
        {
            diagnostics.Should().BeEmpty();
            return;
        }
        var diagnostic = diagnostics.Should().ContainSingle().Which;
        diagnostic.Id.Should().Be("DRNDTO003");
        diagnostic.GetMessage().Should().Be($"Projected property 'Display' requires [Personal] from mapped property '{missingFrom}'.");
    }

    [Fact]
    public void Shared_Classified_Value_Type_Protects_Context_But_Cannot_Replace_A_Property_Annotation()
    {
        const string contract = """
            [Sensitive] public struct Value { }
            public class UserDto(SourceKnownEntity? e = null) : Dto(e) { public Value? Display { get; init; } }
            """;
        const string domain = """
            public class UserEntity : SourceKnownEntity
            {
                public Value? Number => null;
                public override UserDto ToDto() => new(this) { Display = Number };
            }
            """;
        Project(contract, domain).Should().BeEmpty();
        var annotated = domain.Replace("public Value? Number", "[Sensitive] public Value? Number", StringComparison.Ordinal);
        Project(contract, annotated).Should().ContainSingle().Which.GetMessage().Should()
            .Be("Projected property 'Display' requires [Sensitive] from mapped property 'Number'.");
    }

    [Fact]
    public void Closed_Generic_Projection_Should_Use_The_Same_Contract_Placement_Rules()
    {
        Project("public class UserDto<T>(SourceKnownEntity? e = null) : Dto(e);", """
            public class UserEntity : SourceKnownEntity { public override UserDto<int> ToDto() => new(this); }
            """).Should().BeEmpty();
        var misplaced = Compilation("""
            using DRN.Framework.SharedKernel.Domain;
            namespace Shared.Contract.QA;
            public class UserDto<T>(SourceKnownEntity? e = null) : Dto(e);
            """).WithAssemblyName("Shared.Contract");
        misplaced.AssertNoCompilerErrors();
        Analyze(misplaced, "Contract").Should().ContainSingle().Which.Id.Should().Be("DRNDTO002");
    }

    [Fact]
    public void Partial_Mapper_Should_Use_The_Implementation_Body()
    {
        Project("public class UserDto(SourceKnownEntity? e = null) : Dto(e);", """
            public partial class UserEntity : SourceKnownEntity { public override partial UserDto ToDto(); }
            public partial class UserEntity { public override partial UserDto ToDto() => new(this); }
            """).Should().BeEmpty();
    }

    [Fact]
    public void Invalid_Owner_Should_Report_The_Input_Problem_Alone()
    {
        Project("public class UserDto(SourceKnownEntity? e = null) : Dto(e);", """
            [SourceKnownSubdomain("Other")]
            public class UserEntity : SourceKnownEntity { public override Dto ToDto() => new(this); }
            """).Should().ContainSingle().Which.Id.Should().Be("DRNCONVENTIONINPUT001");
    }

    [Fact]
    public void Public_Dto_Inside_Internal_Type_Should_Not_Satisfy_The_Public_Contract()
    {
        Project("internal class Container { public class UserDto(SourceKnownEntity? e = null) : Dto(e); }", """
            internal class UserEntity : SourceKnownEntity { public override Container.UserDto ToDto() => new(this); }
            """).Should().ContainSingle().Which.Id.Should().Be("DRNDTO001");
    }

    [Theory]
    [DataInline("UserDto<int>", true)]
    [DataInline("UserDto<InternalDetail[]>", false)]
    [DataInline("Container<int[]>.UserDto", true)]
    [DataInline("Container<InternalDetail[]>.UserDto", false)]
    public void Constructed_Dto_Should_Have_Public_Type_Arguments(string type, bool isPublic)
    {
        var diagnostics = Project("""
            internal class InternalDetail;
            public class UserDto<T>(SourceKnownEntity? e = null) : Dto(e);
            public class Container<T> { public class UserDto(SourceKnownEntity? e = null) : Dto(e); }
            """, $$"""
            internal class UserEntity : SourceKnownEntity { public override {{type}} ToDto() => new(this); }
            """);
        if (isPublic) diagnostics.Should().BeEmpty();
        else diagnostics.Should().ContainSingle().Which.Id.Should().Be("DRNDTO001");
    }

    [Theory]
    [DataInline("((IProfile)Profile).Alias", "", "Personal", "Alias")]
    [DataInline("((IProfile)Profile).Alias", "[Personal]", null, null)]
    [DataInline("((IProfile)Profile).Title", "", "Sensitive", "Title")]
    [DataInline("((IProfile)Profile)?.Alias ?? \"\"", "", "Personal", "Alias")]
    [DataInline("((IProfile?)OptionalProfile)?.Alias ?? \"\"", "", "Personal", "Alias")]
    [DataInline("(Profile as IProfile)?.Alias ?? \"\"", "", "Personal", "Alias")]
    [DataInline("((IProfile)ClassifiedProfile).Plain", "", "Sensitive", "Plain")]
    [DataInline("((IProfile)ClassifiedProfile).Plain", "[Sensitive]", null, null)]
    [DataInline("((IProfile)PlainProfile).Plain", "", null, null)]
    public void Boxed_Struct_Receiver_Should_Preserve_Struct_Interface_Classifications(
        string value, string annotation, string? kind, string? property)
    {
        var diagnostics = Project($$"""
            public class UserDto(SourceKnownEntity? e = null) : Dto(e) { {{annotation}} public string Display { get; init; } = ""; }
            """, $$"""
            public interface IProfile { string Alias { get; } string Title { get; } string Plain { get; } }
            public struct ProfileStruct : IProfile
            {
                [Personal] string IProfile.Alias => "";
                [Sensitive] public string Title => "";
                public string Plain => "";
            }
            [Sensitive] public struct ClassifiedStruct : IProfile
            {
                string IProfile.Alias => "";
                public string Title => "";
                public string Plain => "";
            }
            public struct PlainStruct : IProfile
            {
                string IProfile.Alias => "";
                public string Title => "";
                public string Plain => "";
            }
            public class UserEntity : SourceKnownEntity
            {
                public ProfileStruct Profile => default;
                public ProfileStruct? OptionalProfile { get; init; }
                public ClassifiedStruct ClassifiedProfile => default;
                public PlainStruct PlainProfile => default;
                public override UserDto ToDto() => new(this) { Display = {{value}} };
            }
            """);
        if (kind is null)
        {
            diagnostics.Should().BeEmpty();
            return;
        }
        var diagnostic = diagnostics.Should().ContainSingle().Which;
        diagnostic.Id.Should().Be("DRNDTO003");
        diagnostic.GetMessage().Should().Be($"Projected property 'Display' requires [{kind}] from mapped property '{property}'.");
    }

    [Theory]
    [DataInline("new Options { Prefix = Email } + Suffix", "", "Personal", "Email")]
    [DataInline("new Options { Prefix = Email } + Suffix", "[Personal]", null, null)]
    [DataInline("new Options { Prefix = Email } + Suffix", "[Sensitive]", "Personal", "Email")]
    [DataInline("new TextOptions { Prefix = Email } + \"\"", "", "Personal", "Email")]
    [DataInline("!new Options { Prefix = Email }", "", "Personal", "Email")]
    [DataInline("$\"{new Options { Prefix = Email }}\"", "", "Personal", "Email")]
    [DataInline("new Options { Prefix = Email }.Formatted", "", "Personal", "Email")]
    public void Operator_And_Expression_Inputs_Should_Classify_The_Outer_Destination(
        string value, string annotation, string? kind, string? property)
    {
        var diagnostics = Project($$"""
            public class UserDto(SourceKnownEntity? e = null) : Dto(e) { {{annotation}} public string Display { get; init; } = ""; }
            """, $$"""
            public class Options
            {
                [Personal] public string Prefix { get; init; } = "";
                public string Formatted => Prefix;
                public override string ToString() => Prefix;
                public static string operator +(Options left, string right) => left.Prefix + right;
                public static string operator !(Options value) => value.Prefix;
            }
            public class TextOptions
            {
                [Personal] public string Prefix { get; init; } = "";
                public override string ToString() => Prefix;
            }
            public class UserEntity : SourceKnownEntity
            {
                [Personal] public string Email => "";
                public string Suffix => "";
                public override UserDto ToDto() => new(this) { Display = {{value}} };
            }
            """);
        if (kind is null)
        {
            diagnostics.Should().BeEmpty();
            return;
        }
        var diagnostic = diagnostics.Should().ContainSingle().Which;
        diagnostic.Id.Should().Be("DRNDTO003");
        diagnostic.GetMessage().Should().Be($"Projected property 'Display' requires [{kind}] from mapped property '{property}'.");
    }

    [Theory]
    [DataInline("", "", "Details = Flag ? new ContactView { Label = Email } : new ContactView { Label = \"\" }", "Label")]
    [DataInline("", "[Personal]", "Details = Flag ? new ContactView { Label = Email } : new ContactView { Label = \"\" }", null)]
    [DataInline("[Personal]", "", "Contacts = [new ContactDto { Label = Email }]", "Label")]
    [DataInline("", "[Personal]", "Contacts = [new ContactDto { Label = Email }]", null)]
    [DataInline("[Personal]", "", "Contacts = [.. new ContactDto[] { new() { Label = Email } }]", "Label")]
    [DataInline("", "[Personal]", "Contacts = [.. new ContactDto[] { new() { Label = Email } }]", null)]
    [DataInline("", "[Personal]", "Contacts = [with(capacity: Email.Length), new ContactDto { Label = Email }]", "Contacts")]
    [DataInline("[Personal]", "", "Contacts = [with(capacity: Email.Length), new ContactDto { Label = Email }]", "Label")]
    [DataInline("", "", "Details = (ContactView?)null ?? new ContactView { Label = Email }", "Label")]
    public void Conditional_And_Collection_Expressions_Should_Preserve_Nested_Dto_Destinations(
        string collectionAnnotation, string elementAnnotation, string assignment, string? missingFrom)
    {
        var diagnostics = Project($$"""
            public class ContactView { {{elementAnnotation}} public string Label { get; init; } = ""; }
            public class ContactDto { {{elementAnnotation}} public string Label { get; init; } = ""; }
            public class UserDto(SourceKnownEntity? e = null) : Dto(e)
            {
                public ContactView Details { get; init; } = new();
                {{collectionAnnotation}} public System.Collections.Generic.List<ContactDto> Contacts { get; init; } = [];
            }
            """, $$"""
            public class UserEntity : SourceKnownEntity
            {
                public bool Flag => true;
                [Personal] public string Email => "";
                public override UserDto ToDto() => new(this) { {{assignment}} };
            }
            """);
        if (missingFrom is null)
        {
            diagnostics.Should().BeEmpty();
            return;
        }
        var diagnostic = diagnostics.Should().ContainSingle().Which;
        diagnostic.Id.Should().Be("DRNDTO003");
        diagnostic.GetMessage().Should().Be($"Projected property '{missingFrom}' requires [Personal] from mapped property 'Email'.");
    }

    [Theory]
    [DataInline("Labels", "new ContactDto { Label = Email }", "", "[Personal]", "Contacts")]
    [DataInline("Labels", "new ContactDto { Label = Email }", "[Personal]", "", null)]
    [DataInline("Relisted", "new ContactDto { Label = Email }", "", "[Personal]", "Contacts")]
    [DataInline("Mixed", "new ContactDto { Label = Email }", "", "[Personal]", "Contacts")]
    [DataInline("Mixed", "(object)new ContactDto { Label = Email }", "[Personal]", "", "Label")]
    [DataInline("Plain", "new ContactDto { Label = Email }", "", "[Personal]", null)]
    [DataInline("Plain", "new ContactDto { Label = Email }", "[Personal]", "", "Label")]
    [DataInline("Mixed", "(object)new ContactDto { Label = Email }, new ContactDto { Label = Email }", "", "[Personal]", "Contacts")]
    public void Collection_Expression_Should_Follow_The_Add_Selected_For_Each_Element(
        string collection, string elements, string collectionAnnotation, string leafAnnotation, string? missing)
    {
        var diagnostics = Project($$"""
            public class ContactDto { {{leafAnnotation}} public string Label { get; init; } = ""; }
            public class Plain : System.Collections.Generic.List<object>;
            public class Relisted : Plain, System.Collections.Generic.ICollection<object>
            {
                public new void Add(object item) => base.Add(((ContactDto)item).Label);
            }
            public class Labels : Plain
            {
                public new void Add(object item) => base.Add(((ContactDto)item).Label);
            }
            public class Mixed : Plain
            {
                public void Add(ContactDto item) => base.Add(item.Label);
            }
            public class UserDto(SourceKnownEntity? e = null) : Dto(e)
            {
                {{collectionAnnotation}} public {{collection}} Contacts { get; init; } = [];
            }
            """, $$"""
            public class UserEntity : SourceKnownEntity
            {
                [Personal] public string Email => "";
                public override UserDto ToDto() => new(this) { Contacts = [{{elements}}] };
            }
            """);
        if (missing is null) diagnostics.Should().BeEmpty();
        else
        {
            var diagnostic = diagnostics.Should().ContainSingle().Which;
            diagnostic.Id.Should().Be("DRNDTO003");
            diagnostic.GetMessage().Should().Be($"Projected property '{missing}' requires [Personal] from mapped property 'Email'.");
        }
    }

    [Theory]
    [DataInline("Display = (Flag ? new Options { Prefix = Email } : null) ?? \"\"", "", "[Personal]", "Display")]
    [DataInline("Display = (Flag ? new Options { Prefix = Email } : null) ?? \"\"", "[Personal]", "", null)]
    [DataInline("Labels = [.. new Options[] { new() { Prefix = Email } }]", "", "[Personal]", "Labels")]
    [DataInline("Labels = [.. new Options[] { new() { Prefix = Email } }]", "[Personal]", "", null)]
    public void User_Defined_Conversions_In_Coalescing_And_Spread_Should_Classify_The_Result(
        string assignment, string resultAnnotation, string inputAnnotation, string? missing)
    {
        var diagnostics = Project($$"""
            public class UserDto(SourceKnownEntity? e = null) : Dto(e)
            {
                {{resultAnnotation}} public string Display { get; init; } = "";
                {{resultAnnotation}} public string[] Labels { get; init; } = [];
            }
            """, $$"""
            public class Options
            {
                {{inputAnnotation}} public string Prefix { get; init; } = "";
                public static implicit operator string(Options value) => value.Prefix;
            }
            public class UserEntity : SourceKnownEntity
            {
                public bool Flag => true;
                [Personal] public string Email => "";
                public override UserDto ToDto() => new(this) { {{assignment}} };
            }
            """);
        if (missing is null) diagnostics.Should().BeEmpty();
        else
        {
            var diagnostic = diagnostics.Should().ContainSingle().Which;
            diagnostic.Id.Should().Be("DRNDTO003");
            diagnostic.GetMessage().Should().Be($"Projected property '{missing}' requires [Personal] from mapped property 'Email'.");
        }
    }

    [Fact]
    public void AnswerEntity_TagEntity_And_Abstract_Descendant_Should_Require_Override_ToDto_And_Pass_With_Explicit_Construction()
    {
        var contract = Compilation("""
            using DRN.Framework.SharedKernel.Domain;
            namespace Shared.Contract.QA.Answers
            {
                public class AnswerDto(SourceKnownEntity? entity = null) : Dto(entity)
                {
                    public string Body { get; init; } = string.Empty;
                    public bool IsAccepted { get; init; }
                }
            }
            namespace Shared.Contract.QA.Tags
            {
                public class TagValueModel { public string Label { get; set; } = string.Empty; }
                public class TagDto(SourceKnownEntity? entity = null) : Dto(entity)
                {
                    public string Name { get; init; } = string.Empty;
                    public TagValueModel Model { get; init; } = new();
                }
            }
            namespace Shared.Contract.QA.Notes
            {
                public class NoteDto(SourceKnownEntity? entity = null) : Dto(entity)
                {
                    public string Text { get; init; } = string.Empty;
                }
            }
            """).WithAssemblyName("Shared.Contract");
        contract.AssertNoCompilerErrors();

        var domain = Compilation("""
            using DRN.Framework.SharedKernel.Domain;
            using Shared.Contract.QA.Answers;
            using Shared.Contract.QA.Tags;
            using Shared.Contract.QA.Notes;
            namespace Shop.Domain.QA.Answer
            {
                public class AnswerEntity : AggregateRoot
                {
                    public string Body => "";
                    public bool IsAccepted => false;
                    public override AnswerDto ToDto() => new AnswerDto(this) { Body = Body, IsAccepted = IsAccepted };
                }
            }
            namespace Shop.Domain.QA.Tag
            {
                public class TagEntity : AggregateRoot<TagValueModel>
                {
                    public string Name => "";
                    public override TagDto ToDto() => new(this) { Name = Name, Model = Model };
                }
            }
            namespace Shop.Domain.QA.Note
            {
                public abstract class NoteBase : SourceKnownEntity
                {
                    public string Text => "";
                    public override Dto ToDto() => new(this);
                }
                public class NoteEntity : NoteBase
                {
                    public new NoteDto ToDto() => new(this) { Text = Text };
                }
            }
            """).WithAssemblyName("Shop.Domain").AddReferences(contract.ToMetadataReference());
        domain.AssertNoCompilerErrors();

        var diagnostics = Analyze(domain, "Domain");
        var missing = diagnostics.Should().ContainSingle().Which;
        missing.Id.Should().Be("DRNDTO001");
        missing.GetMessage().Should().Contain("NoteEntity").And.Contain("override public ToDto()").And.Contain("NoteDto");
    }

    [Fact]
    public void Dto_Exposure_And_Explicit_Message_Rules_Should_Diagnose_Violations_And_Honor_NonAction_And_Exemptions()
    {
        var contract = Compilation("""
            using System.Collections.Generic;
            using DRN.Framework.SharedKernel.Domain;
            namespace Sample.Contract.QA.Orders
            {
                public class OrderDto(SourceKnownEntity? entity = null) : Dto(entity);
                public class InheritedOrderResponse : OrderDto;
                public class NestedOrderRequest { public List<OrderDto> Items { get; init; } = []; }
                public class OrderRequest { public string Name { get; init; } = ""; }
                public class OrderResponse { public string Name { get; init; } = ""; }
                public class OrderView { public string Name { get; init; } = ""; }
            }
            """).WithAssemblyName("Sample.Contract");
        contract.AssertNoCompilerErrors();
        var contractDiagnostics = Analyze(contract, "Contract");
        contractDiagnostics.Where(d => d.Id == "DRNDTO005").Select(d => d.Properties["Target"]).Should().BeEquivalentTo(
            "T:Sample.Contract.QA.Orders.InheritedOrderResponse",
            "P:Sample.Contract.QA.Orders.NestedOrderRequest.Items");

        var hosted = Compilation("""
            #nullable enable
            using System;
            using System.Threading;
            using System.Threading.Tasks;
            using DRN.Framework.SharedKernel.Domain;
            using DRN.Framework.SharedKernel.Domain.Pagination;
            using Microsoft.AspNetCore.Mvc;
            using Sample.Contract.QA.Orders;
            namespace Sample.Hosted.Controllers.QA;
            public class OrdersController : ControllerBase
            {
                [HttpGet("{id:guid}")] public Task<OrderResponse> Get(Guid id) => Task.FromResult(new OrderResponse());
                [HttpDelete("{id:guid}")] public Task<NoContentResult> Delete(Guid id) => Task.FromResult(NoContent());
                [HttpPost("ok")] public Task<ActionResult<OrderResponse>> Create([FromBody] OrderRequest request) => Task.FromResult<ActionResult<OrderResponse>>(new OrderResponse());
                [HttpGet("dto")] public Task<PaginationResultModel<OrderDto>> LeakedDtos() => throw new NotImplementedException();
                [NonAction] public Task<OrderDto> DisabledDto() => throw new NotImplementedException();
                [HttpPost("bad")] public Task<OrderView> Bad([FromBody] OrderView payload) => throw new NotImplementedException();
            }
            """).WithAssemblyName("Sample.Hosted").AddReferences(contract.ToMetadataReference());
        hosted.AssertNoCompilerErrors();
        var hostedDiagnostics = Analyze(hosted, "Hosted");
        hostedDiagnostics.Where(d => d.Id == "DRNDTO005").Select(d => d.Properties["Target"])
            .Should().Equal("M:Sample.Hosted.Controllers.QA.OrdersController.LeakedDtos");
        hostedDiagnostics.Where(d => d.Id == "DRNDTO006").Select(d => d.GetMessage()).Should().HaveCount(2)
            .And.Contain(m => m.Contains("Response") && m.Contains("OrderView"))
            .And.Contain(m => m.Contains("Request") && m.Contains("OrderView"));
    }

    [Fact]
    public void Response_Projection_Should_Preserve_Entity_And_Dto_Classifications()
    {
        var contract = Compilation("""
            using DRN.Framework.SharedKernel.Attributes;
            using DRN.Framework.SharedKernel.Domain;
            namespace Sample.Contract.QA.Users
            {
                public class UserDto(SourceKnownEntity? e = null) : Dto(e)
                {
                    [Sensitive] public string Token { get; init; } = "";
                }
                public class UserResponse
                {
                    public string Email { get; init; } = "";
                    public string Token { get; init; } = "";
                }
            }
            """).WithAssemblyName("Sample.Contract");
        var domain = Compilation("""
            using DRN.Framework.SharedKernel.Attributes;
            using DRN.Framework.SharedKernel.Domain;
            using Sample.Contract.QA.Users;
            namespace Sample.Domain.QA.User
            {
                public class UserEntity : AggregateRoot
                {
                    [Personal] public string Email => "";
                    public override UserDto ToDto() => new(this);
                }
            }
            """).WithAssemblyName("Sample.Domain").AddReferences(contract.ToMetadataReference());
        var hosted = Compilation("""
            using Sample.Contract.QA.Users;
            using Sample.Domain.QA.User;
            namespace Sample.Hosted.Controllers.QA
            {
                public class UserMapper
                {
                    public UserResponse Project(UserEntity entity, UserDto dto) => new() { Email = entity.Email, Token = dto.Token };
                }
            }
            """).WithAssemblyName("Sample.Hosted").AddReferences(contract.ToMetadataReference(), domain.ToMetadataReference());
        hosted.AssertNoCompilerErrors();
        var diagnostics = Analyze(hosted, "Hosted");
        diagnostics.Where(d => d.Id == "DRNDTO003").Select(d => d.GetMessage()).Should().BeEquivalentTo(
            "Projected property 'Email' requires [Personal] from mapped property 'Email'.",
            "Projected property 'Token' requires [Sensitive] from mapped property 'Token'.");
    }

    [Theory]
    [DataInlineUnit("IOrderEnvelope.g.cs", "OrderResponse")]
    [DataInlineUnit("IOrderEnvelope.cs", "IOrderEnvelope")]
    public void Dto_Exposure_Through_A_Local_Interface_Should_Report_Once_On_The_Handwritten_Declaration(
        string interfaceFile, string expectedTarget)
    {
        var contract = Compilation("""
            using DRN.Framework.SharedKernel.Domain;
            namespace Sample.Contract.QA.Orders;
            public class OrderDto : Dto;
            public interface IMarker<T>;
            public sealed class OrderResponse : IOrderEnvelope;
            """, "OrderResponse.cs").WithAssemblyName("Sample.Contract").AddSyntaxTrees(CSharpSyntaxTree.ParseText("""
            namespace Sample.Contract.QA.Orders;
            public interface IOrderEnvelope : IMarker<OrderDto>;
            """, path: interfaceFile));
        contract.AssertNoCompilerErrors();

        var diagnostic = Analyze(contract, "Contract").Should().ContainSingle(d => d.Id == "DRNDTO005").Which;
        diagnostic.Properties["Entity"].Should().Be("Sample.Contract.QA.Orders.OrderDto");
        diagnostic.Location.SourceTree!.FilePath.Should().Be(expectedTarget + ".cs");
        AnalyzerTestCompilation.LocationText(diagnostic).Should().Be(expectedTarget);
    }

    [Fact]
    public void Dto_Exposure_Should_Check_Inherited_Bases_And_ProducesResponseType_Attributes()
    {
        var sharedRef = EmitProjectReference("Shared.Contract", "/dto-inherited/Shared.Contract.dll", """
            using DRN.Framework.SharedKernel.Domain;
            namespace Shared.Contract.QA.Orders
            {
                public class OrderDto(SourceKnownEntity? entity = null) : Dto(entity);
                public class BaseOrderResponse { public OrderDto Item { get; init; } = new(); }
            }
            """);

        var contract = Compilation("""
            using Shared.Contract.QA.Orders;
            namespace Sample.Contract.QA.Orders
            {
                public class Envelope<T> { public T Payload { get; init; } = default!; }
                public class ClosedGenericOrderResponse : Envelope<OrderDto>;
                public interface IOrderEnvelope<T> { T Payload { get; } }
                public interface IClosedOrderResponse : IOrderEnvelope<OrderDto>;
                public class DerivedOrderResponse : BaseOrderResponse;
                public class OrderResponse { public string Name { get; init; } = ""; }
            }
            """).WithAssemblyName("Sample.Contract").AddReferences(sharedRef);
        contract.AssertNoCompilerErrors();
        var contractDiagnostics = Analyze(contract, "Contract");
        contractDiagnostics.Where(d => d.Id == "DRNDTO005").Select(d => d.Properties["Target"]).Should().BeEquivalentTo(
            "T:Sample.Contract.QA.Orders.ClosedGenericOrderResponse",
            "T:Sample.Contract.QA.Orders.IClosedOrderResponse",
            "T:Sample.Contract.QA.Orders.DerivedOrderResponse");

        var hosted = Compilation("""
            using Microsoft.AspNetCore.Mvc;
            using Sample.Contract.QA.Orders;
            using Shared.Contract.QA.Orders;
            namespace Sample.Hosted.Controllers.QA;
            public class OrdersController : ControllerBase
            {
                [HttpGet("untyped"), ProducesResponseType(typeof(OrderDto), 200, "application/json", "application/xml")]
                public IActionResult Untyped() => Ok();

                [HttpGet("generic"), ProducesResponseType<OrderDto>(200)]
                public ActionResult GenericAttr() => Ok();

                [HttpGet("named"), ProducesResponseType(200, Type = typeof(OrderDto))]
                public IActionResult NamedType() => Ok();

                [HttpGet("ok"), ProducesResponseType(typeof(OrderResponse), 200, "application/json")]
                public IActionResult Good() => Ok();
            }
            """).WithAssemblyName("Sample.Hosted").AddReferences(sharedRef, contract.ToMetadataReference());
        hosted.AssertNoCompilerErrors();
        var hostedDiagnostics = Analyze(hosted, "Hosted");
        hostedDiagnostics.Where(d => d.Id == "DRNDTO005").Select(d => d.Properties["Target"]).Should().BeEquivalentTo(
            "M:Sample.Hosted.Controllers.QA.OrdersController.Untyped",
            "M:Sample.Hosted.Controllers.QA.OrdersController.GenericAttr",
            "M:Sample.Hosted.Controllers.QA.OrdersController.NamedType");
    }

    [Fact]
    public void Explicit_Contract_Messages_Should_Check_Inferred_Body_On_SourceKnown_And_Application_Payloads_While_Preserving_Hosted_Local_Models()
    {
        var contractRef = EmitProjectReference("Sample.Contract", "/dto-explicit/Sample.Contract.dll", """
            using DRN.Framework.SharedKernel.Domain;
            namespace Sample.Contract.QA.Orders
            {
                public class OrderDto(SourceKnownEntity? entity = null) : Dto(entity);
                public class OrderResponse { public string Name { get; init; } = ""; }
            }
            """);

        var domainRef = EmitProjectReference("Sample.Domain", "/dto-explicit/Sample.Domain.dll", """
            using DRN.Framework.SharedKernel.Domain;
            using Sample.Contract.QA.Orders;
            namespace Sample.Domain.QA.Order
            {
                public class OrderEntity : AggregateRoot
                {
                    public override OrderDto ToDto() => new(this);
                }
            }
            """, contractRef);

        var appRef = EmitProjectReference("Sample.Application", "/dto-explicit/Sample.Application.dll", """
            namespace Sample.Application.QA
            {
                public class AppCommand { public string Title { get; init; } = ""; }
                public class AppSummary { public string Title { get; init; } = ""; }
            }
            """);

        var hosted = Compilation("""
            #nullable enable
            using System;
            using System.Threading;
            using System.Threading.Tasks;
            using DRN.Framework.Hosting.Controllers;
            using DRN.Framework.SharedKernel.Domain;
            using Microsoft.AspNetCore.Mvc;
            using Microsoft.Extensions.DependencyInjection;
            using Sample.Application.QA;
            using Sample.Contract.QA.Orders;
            using Sample.Domain.QA.Order;
            namespace Sample.Hosted.Controllers.QA
            {
                public sealed record ClientErrorPayload(string Message);
                public interface IOrderLookup;

                public class ClientErrorController : ControllerBase
                {
                    [HttpPost("client-error")]
                    public IActionResult Report([FromBody] ClientErrorPayload? payload) => NoContent();

                    [HttpPost("app")]
                    public AppSummary Run([FromBody] AppCommand command) => new();
                }

                public class OrdersController(ISourceKnownService<OrderEntity, OrderResponse> service)
                    : SourceKnownReadOnlyController<OrderEntity, OrderResponse>(service)
                {
                    [HttpPost("inferred")]
                    public Task<OrderResponse> Update(ClientErrorPayload payload, [FromKeyedServices("orders")] IOrderLookup lookup)
                        => Task.FromResult(new OrderResponse());

                    [HttpGet("route/{id:guid}")]
                    public Task<OrderResponse> ById([FromRoute] Guid id, [FromQuery] string? q, CancellationToken ct) =>
                        Task.FromResult(new OrderResponse());
                }
            }
            """).WithAssemblyName("Sample.Hosted")
            .AddReferences(AnalyzerTestCompilation.HostingReference, AnalyzerTestCompilation.UtilsReference, contractRef, domainRef, appRef);
        hosted.AssertNoCompilerErrors();

        var diagnostics = Analyze(hosted, "Hosted");
        var explicitMessages = diagnostics.Where(d => d.Id == "DRNDTO006").ToArray();
        explicitMessages.Select(d => d.GetMessage()).Should().BeEquivalentTo(
            "Public endpoint 'Sample.Hosted.Controllers.QA.ClientErrorController.Run(Sample.Application.QA.AppCommand)' must use an explicit Contract Response type instead of 'Sample.Application.QA.AppSummary'.",
            "Public endpoint 'Sample.Hosted.Controllers.QA.ClientErrorController.Run(Sample.Application.QA.AppCommand)' must use an explicit Contract Request type instead of 'Sample.Application.QA.AppCommand'.",
            "Public endpoint 'Sample.Hosted.Controllers.QA.OrdersController.Update(Sample.Hosted.Controllers.QA.ClientErrorPayload, Sample.Hosted.Controllers.QA.IOrderLookup)' must use an explicit Contract Request type instead of 'Sample.Hosted.Controllers.QA.ClientErrorPayload'.");
    }

    [Fact]
    public void Response_Projection_Should_Cover_Positional_Records_Nested_Reads_Single_Nested_Diagnostic_And_Abstract_Base()
    {
        var contract = Compilation("""
            #nullable enable
            using DRN.Framework.SharedKernel.Attributes;
            using DRN.Framework.SharedKernel.Domain;
            namespace Sample.Contract.QA.Users
            {
                public record PositionalUserResponse(string Email);
                public record AnnotatedPositionalUserResponse([property: Personal] string Email);
                public class TagModelResponse { public string StringValue { get; init; } = ""; }
                public class TagResponse { public TagModelResponse Model { get; init; } = new(); }
                public class ContactDto { [Personal] public string Email { get; init; } = ""; }
                public class UserDto(SourceKnownEntity? e = null) : Dto(e)
                {
                    public ContactDto? Contact { get; init; }
                }
                public class NestedUserResponse { public string Display { get; init; } = ""; }
            }
            """).WithAssemblyName("Sample.Contract");
        contract.AssertNoCompilerErrors();
        var domain = Compilation("""
            using DRN.Framework.SharedKernel.Attributes;
            using DRN.Framework.SharedKernel.Domain;
            using Sample.Contract.QA.Users;
            namespace Sample.Domain.QA.User
            {
                public class TagModel { [Sensitive] public string StringValue { get; set; } = ""; }
                public class UserEntity : AggregateRoot
                {
                    [Personal] public string Email => "";
                    public TagModel Model { get; } = new();
                    public override UserDto ToDto() => new(this);
                }
            }
            """).WithAssemblyName("Sample.Domain").AddReferences(contract.ToMetadataReference());
        domain.AssertNoCompilerErrors();
        var hosted = Compilation("""
            using Sample.Contract.QA.Users;
            using Sample.Domain.QA.User;
            namespace Sample.Hosted.Controllers.QA
            {
                public abstract class AbstractMapperBase
                {
                    public PositionalUserResponse MapPositional(UserEntity entity) => new(entity.Email);
                    public AnnotatedPositionalUserResponse MapAnnotated(UserEntity entity) => new(entity.Email);
                }

                public class ConcreteMapper
                {
                    public TagResponse MapNested(UserEntity entity) =>
                        new() { Model = new TagModelResponse { StringValue = entity.Model.StringValue } };

                    public NestedUserResponse MapDtoNested(UserDto dto) =>
                        new() { Display = dto.Contact?.Email ?? "" };

                    public class InnerMapper
                    {
                        public PositionalUserResponse MapInner(UserEntity entity) => new(entity.Email);
                    }
                }
            }
            """).WithAssemblyName("Sample.Hosted").AddReferences(contract.ToMetadataReference(), domain.ToMetadataReference());
        hosted.AssertNoCompilerErrors();

        var diagnostics = Analyze(hosted, "Hosted").Where(d => d.Id == "DRNDTO003").ToArray();
        diagnostics.Select(d => (d.Properties["Target"], d.GetMessage())).Should().BeEquivalentTo(new (string?, string)[]
        {
            ("T:Sample.Hosted.Controllers.QA.AbstractMapperBase", "Projected property 'Email' requires [Personal] from mapped property 'Email'."),
            ("T:Sample.Hosted.Controllers.QA.ConcreteMapper", "Projected property 'StringValue' requires [Sensitive] from mapped property 'StringValue'."),
            ("T:Sample.Hosted.Controllers.QA.ConcreteMapper", "Projected property 'Display' requires [Personal] from mapped property 'Email'."),
            ("T:Sample.Hosted.Controllers.QA.ConcreteMapper.InnerMapper", "Projected property 'Email' requires [Personal] from mapped property 'Email'.")
        });
    }

    [Fact]
    public void Dto_Exposure_Should_Inspect_WriteOnly_Properties_And_Report_DRNARCHITECTURE001_On_Budget_Overflow()
    {
        var wideTree = AnalyzerTestCompilation.WideModelTree("Sample.Contract.QA.Orders");
        var contract = Compilation("""
            using DRN.Framework.SharedKernel.Domain;
            namespace Sample.Contract.QA.Orders
            {
                public class OrderDto(SourceKnownEntity? entity = null) : Dto(entity);
                public class NestedInput { public OrderDto Item { set { _ = value; } } }
                public class OrderRequest
                {
                    public NestedInput Input { get; init; } = new();
                    public Wide Payload { get; init; } = new();
                }
            }
            """).AddSyntaxTrees(wideTree).WithAssemblyName("Sample.Contract");
        contract.AssertNoCompilerErrors();
        var diagnostics = Analyze(contract, "Contract");

        diagnostics.Where(d => d.Id == "DRNDTO005").Select(d => d.Properties["Target"]).Should().BeEquivalentTo(
            "P:Sample.Contract.QA.Orders.NestedInput.Item",
            "P:Sample.Contract.QA.Orders.OrderRequest.Input");
        diagnostics.Where(d => d.Id == "DRNARCHITECTURE001").Should().ContainSingle()
            .Which.Properties["Target"].Should().Be("P:Sample.Contract.QA.Orders.OrderRequest.Payload");
    }

    [Fact]
    public void Open_Generic_And_Nested_Generic_Response_Projections_Should_Report_DRNDTO003()
    {
        var contract = Compilation("""
            #nullable enable
            using DRN.Framework.SharedKernel.Attributes;
            using DRN.Framework.SharedKernel.Domain;
            namespace Sample.Contract.QA.Users
            {
                public class UserDto(SourceKnownEntity? e = null) : Dto(e)
                {
                    [Personal] public string Email { get; init; } = "";
                }
                public class UserResponse
                {
                    public string Email { get; init; } = "";
                }
            }
            """).WithAssemblyName("Sample.Contract");
        contract.AssertNoCompilerErrors();

        var domain = Compilation("""
            #nullable enable
            using DRN.Framework.SharedKernel.Attributes;
            using DRN.Framework.SharedKernel.Domain;
            using Sample.Contract.QA.Users;
            namespace Sample.Domain.QA.User
            {
                public class ProfileModel { [Sensitive] public string Secret { get; init; } = ""; }
                public class UserEntity : AggregateRoot
                {
                    [Personal] public string Email { get; init; } = "";
                    public ProfileModel Profile { get; init; } = new();
                    public override UserDto ToDto() => new(this) { Email = Email };
                }
            }
            """).WithAssemblyName("Sample.Domain").AddReferences(contract.ToMetadataReference());
        domain.AssertNoCompilerErrors();

        var hosted = Compilation("""
            #nullable enable
            using Sample.Contract.QA.Users;
            using Sample.Domain.QA.User;
            namespace Sample.Hosted.Controllers.QA
            {
                public class GenericMapper<T>
                {
                    public UserResponse FromEntity(UserEntity entity) => new() { Email = entity.Email };

                    public class NestedInsideGeneric
                    {
                        public UserResponse FromDto(UserDto dto) => new() { Email = dto.Email };
                    }
                }

                public class ConstrainedMapper<TEntity> where TEntity : UserEntity
                {
                    public UserResponse FromConstrained(TEntity entity) => new() { Email = entity.Profile.Secret };
                }
            }
            """).WithAssemblyName("Sample.Hosted").AddReferences(contract.ToMetadataReference(), domain.ToMetadataReference());
        hosted.AssertNoCompilerErrors();

        var diagnostics = Analyze(hosted, "Hosted").Where(d => d.Id == "DRNDTO003").ToArray();
        diagnostics.Select(d => (d.Properties["Target"], d.GetMessage())).Should().BeEquivalentTo(new (string?, string)[]
        {
            ("T:Sample.Hosted.Controllers.QA.GenericMapper`1", "Projected property 'Email' requires [Personal] from mapped property 'Email'."),
            ("T:Sample.Hosted.Controllers.QA.GenericMapper`1.NestedInsideGeneric", "Projected property 'Email' requires [Personal] from mapped property 'Email'."),
            ("T:Sample.Hosted.Controllers.QA.ConstrainedMapper`1", "Projected property 'Email' requires [Sensitive] from mapped property 'Secret'.")
        });
    }

    [Fact]
    public void Controller_Boundaries_Should_Check_ProducesResponseType_Complex_FromQuery_App_Wrappers_And_Exempt_Injected_Services_And_Status_Results()
    {
        var contractRef = EmitProjectReference("Sample.Contract", "/dto-boundaries/Sample.Contract.dll", """
            using DRN.Framework.SharedKernel.Domain;
            namespace Sample.Contract.QA.Orders
            {
                public class OrderDto(SourceKnownEntity? entity = null) : Dto(entity);
                public class OrderView { public string Title { get; init; } = ""; }
                public class OrderFilter { public string Query { get; init; } = ""; }
                public class OrderResponse { public string Name { get; init; } = ""; }
            }
            """);

        var domainRef = EmitProjectReference("Sample.Domain", "/dto-boundaries/Sample.Domain.dll", """
            using DRN.Framework.SharedKernel.Domain;
            using Sample.Contract.QA.Orders;
            namespace Sample.Domain.QA.Order
            {
                public class OrderEntity : AggregateRoot
                {
                    public override OrderDto ToDto() => new(this);
                }
            }
            """, contractRef);

        var appRef = EmitProjectReference("Sample.Application", "/dto-boundaries/Sample.Application.dll", """
            using Sample.Contract.QA.Orders;
            namespace Sample.Application.QA
            {
                public class AppDtoWrapper { public OrderDto Item { get; init; } = new(); }
                public class AppFilter { public string Query { get; init; } = ""; }
            }
            """, contractRef);

        var hosted = Compilation("""
            #nullable enable
            using System;
            using System.Threading;
            using System.Threading.Tasks;
            using DRN.Framework.Hosting.Controllers;
            using DRN.Framework.SharedKernel.Domain;
            using DRN.Framework.SharedKernel.Domain.Pagination;
            using Microsoft.AspNetCore.Http;
            using Microsoft.AspNetCore.Mvc;
            using Microsoft.Extensions.DependencyInjection;
            using Sample.Application.QA;
            using Sample.Contract.QA.Orders;
            using Sample.Domain.QA.Order;
            namespace Sample.Hosted.Controllers.QA
            {
                public class OrdersController(ISourceKnownService<OrderEntity, OrderResponse> service)
                    : SourceKnownReadOnlyController<OrderEntity, OrderResponse>(service)
                {
                    [HttpGet("ok-result")]
                    public Task<OkResult> PingOk() => Task.FromResult(Ok());

                    [HttpGet("status-result")]
                    public Task<StatusCodeResult> PingStatus() => Task.FromResult(StatusCode(202));

                    [HttpGet("minimal-ok"), ProducesResponseType(typeof(OrderResponse), 200)]
                    public Task<IResult> MinimalOk() => Task.FromResult<IResult>(Results.Ok(new OrderResponse()));

                    [HttpGet("minimal-bad"), ProducesResponseType(typeof(OrderView), 200)]
                    public Task<IResult> MinimalBad() => Task.FromResult<IResult>(Results.Ok(new OrderView()));

                    [HttpGet("injected")]
                    public Task<OrderResponse> WithInjectedServices(
                        [FromRoute] Guid id,
                        [FromQuery] string? q,
                        [FromQuery] PaginationRequest? page,
                        [FromQuery] PaginationResultInfo? pageInfo,
                        IFormFileCollection files,
                        [FromServices] ISourceKnownService<OrderEntity, OrderDto> scopedService,
                        [FromKeyedServices("orders")] ISourceKnownService<OrderEntity, OrderDto> keyedService,
                        CancellationToken ct)
                        => Task.FromResult(new OrderResponse());

                    [HttpGet("bad-query")]
                    public Task<OrderResponse> BadQuery([FromQuery] OrderFilter filter, [FromQuery] AppFilter appFilter)
                        => Task.FromResult(new OrderResponse());

                    [HttpGet("leaked-app-wrapper")]
                    public AppDtoWrapper LeakedAppWrapper() => new();

                    [HttpGet("mixed-dto-and-filter"), ProducesResponseType(typeof(OrderDto), 200)]
                    public IActionResult MixedDtoAndFilter([FromQuery] OrderFilter filter) => Ok();
                }
            }
            """).WithAssemblyName("Sample.Hosted")
            .AddReferences(AnalyzerTestCompilation.HostingReference, AnalyzerTestCompilation.UtilsReference, contractRef, domainRef, appRef);
        hosted.AssertNoCompilerErrors();

        var name = hosted.AssemblyName!;
        var options = AnalyzerTestCompilation.ConventionOptionsProvider(name);
        var facts = new ConventionFacts(hosted, new(true, name, "Hosted", name, name, []),
            new([], []), options, CancellationToken.None);
        ConventionBoundaryAnalysis.Analyze(facts);
        ConventionDtoAnalysis.Analyze(facts);

        facts.Diagnostics.Where(d => d.Id == "DRNARCHITECTURE002").Should().BeEmpty();
        facts.Diagnostics.Where(d => d.Id == "DRNDTO005").Select(d => d.Properties["Target"]).Should().BeEquivalentTo(
            "M:Sample.Hosted.Controllers.QA.OrdersController.LeakedAppWrapper",
            "M:Sample.Hosted.Controllers.QA.OrdersController.MixedDtoAndFilter(Sample.Contract.QA.Orders.OrderFilter)");
        facts.Diagnostics.Where(d => d.Id == "DRNDTO006").Select(d => (d.Properties["Target"], d.GetMessage())).Should().BeEquivalentTo(
            new (string?, string)[]
            {
                ("M:Sample.Hosted.Controllers.QA.OrdersController.MinimalBad",
                    "Public endpoint 'Sample.Hosted.Controllers.QA.OrdersController.MinimalBad()' must use an explicit Contract Response type instead of 'Sample.Contract.QA.Orders.OrderView'."),
                ("M:Sample.Hosted.Controllers.QA.OrdersController.BadQuery(Sample.Contract.QA.Orders.OrderFilter,Sample.Application.QA.AppFilter)",
                    "Public endpoint 'Sample.Hosted.Controllers.QA.OrdersController.BadQuery(Sample.Contract.QA.Orders.OrderFilter, Sample.Application.QA.AppFilter)' must use an explicit Contract Request type instead of 'Sample.Contract.QA.Orders.OrderFilter'."),
                ("M:Sample.Hosted.Controllers.QA.OrdersController.BadQuery(Sample.Contract.QA.Orders.OrderFilter,Sample.Application.QA.AppFilter)",
                    "Public endpoint 'Sample.Hosted.Controllers.QA.OrdersController.BadQuery(Sample.Contract.QA.Orders.OrderFilter, Sample.Application.QA.AppFilter)' must use an explicit Contract Request type instead of 'Sample.Application.QA.AppFilter'."),
                ("M:Sample.Hosted.Controllers.QA.OrdersController.LeakedAppWrapper",
                    "Public endpoint 'Sample.Hosted.Controllers.QA.OrdersController.LeakedAppWrapper()' must use an explicit Contract Response type instead of 'Sample.Application.QA.AppDtoWrapper'."),
                ("M:Sample.Hosted.Controllers.QA.OrdersController.MixedDtoAndFilter(Sample.Contract.QA.Orders.OrderFilter)",
                    "Public endpoint 'Sample.Hosted.Controllers.QA.OrdersController.MixedDtoAndFilter(Sample.Contract.QA.Orders.OrderFilter)' must use an explicit Contract Request type instead of 'Sample.Contract.QA.Orders.OrderFilter'.")
            });
    }

    [Fact]
    public void Response_Projection_Syntax_Filter_Should_Preserve_Local_And_Global_Using_Aliases_And_Nested_Creations()
    {
        var contract = Compilation("""
            #nullable enable
            using DRN.Framework.SharedKernel.Attributes;
            using DRN.Framework.SharedKernel.Domain;
            namespace Sample.Contract.QA.Users
            {
                public class UserDto(SourceKnownEntity? e = null) : Dto(e);
                public class UserResponse { public string Email { get; init; } = ""; }
            }
            """).WithAssemblyName("Sample.Contract");
        contract.AssertNoCompilerErrors();

        var domain = Compilation("""
            #nullable enable
            using DRN.Framework.SharedKernel.Attributes;
            using DRN.Framework.SharedKernel.Domain;
            using Sample.Contract.QA.Users;
            namespace Sample.Domain.QA.User
            {
                public class UserEntity : AggregateRoot
                {
                    [Personal] public string Email { get; init; } = "";
                    public override UserDto ToDto() => new(this);
                }
            }
            """).WithAssemblyName("Sample.Domain").AddReferences(contract.ToMetadataReference());
        domain.AssertNoCompilerErrors();

        var localAliasHosted = Compilation("""
            #nullable enable
            using Sample.Domain.QA.User;
            namespace Sample.Hosted.Controllers.QA
            {
                using RespAlias = Sample.Contract.QA.Users.UserResponse;

                public class AliasMapper
                {
                    public RespAlias FromLocalAlias(UserEntity entity) => new RespAlias { Email = entity.Email };
                }
            }
            """, "AliasMapper.cs")
            .AddSyntaxTrees(CSharpSyntaxTree.ParseText("""
                #nullable enable
                using UserAlias = Sample.Domain.QA.User.UserEntity;
                using Sample.Contract.QA.Users;
                namespace Sample.Hosted.Controllers.QA
                {
                    public sealed record Wrapper(UserResponse Value);

                    public class NestedMapper
                    {
                        public Wrapper FromNested(UserAlias entity) => new Wrapper(new UserResponse { Email = entity.Email });
                        public UserResponse Empty() => new UserResponse();
                    }
                }
                """, path: "NestedMapper.cs"))
            .WithAssemblyName("Sample.Hosted").AddReferences(contract.ToMetadataReference(), domain.ToMetadataReference());
        localAliasHosted.AssertNoCompilerErrors();

        var localOptions = AnalyzerTestCompilation.ConventionOptionsProvider("Sample.Hosted");
        var localFacts = new ConventionFacts(localAliasHosted, new(true, "Sample.Hosted", "Hosted", "Sample.Hosted", "Sample.Hosted", []),
            new([], []), localOptions, CancellationToken.None);
        localFacts.HasResponseUsingAlias(localAliasHosted.SyntaxTrees[0]).Should().BeTrue();
        localFacts.HasResponseUsingAlias(localAliasHosted.SyntaxTrees[1]).Should().BeFalse();
        ConventionDtoAnalysis.Analyze(localFacts);
        var localDiagnostics = localFacts.Diagnostics.Where(d => d.Id == "DRNDTO003").ToArray();
        localDiagnostics.Select(d => d.Properties["Target"]).Should().BeEquivalentTo(
            "T:Sample.Hosted.Controllers.QA.AliasMapper",
            "T:Sample.Hosted.Controllers.QA.NestedMapper");
        localDiagnostics.Select(d => d.GetMessage()).Should().AllBe("Projected property 'Email' requires [Personal] from mapped property 'Email'.");

        var globalAliasHosted = Compilation("""
            global using GlobalResp = Sample.Contract.QA.Users.UserResponse;
            """, "Usings.cs")
            .AddSyntaxTrees(CSharpSyntaxTree.ParseText("""
                #nullable enable
                using Sample.Domain.QA.User;
                namespace Sample.Hosted.Controllers.QA
                {
                    public class GlobalAliasMapper
                    {
                        public GlobalResp FromGlobalAlias(UserEntity entity) => new GlobalResp { Email = entity.Email };
                    }
                }
                """, path: "GlobalAliasMapper.cs"))
            .WithAssemblyName("Sample.Hosted").AddReferences(contract.ToMetadataReference(), domain.ToMetadataReference());
        globalAliasHosted.AssertNoCompilerErrors();

        Analyze(globalAliasHosted, "Hosted").Where(d => d.Id == "DRNDTO003").Should().ContainSingle()
            .Which.GetMessage().Should().Be("Projected property 'Email' requires [Personal] from mapped property 'Email'.");
    }

    [Fact]
    public void Public_Shape_Clean_Cache_Should_Keep_Generic_Arguments_And_Budget_Limits_Isolated()
    {
        var wideTree = AnalyzerTestCompilation.WideModelTree("Sample.Contract.QA.Orders");
        var contract = Compilation("""
            using System.Collections.Generic;
            using DRN.Framework.SharedKernel.Domain;
            namespace Sample.Contract.QA.Orders
            {
                public class OrderDto(SourceKnownEntity? entity = null) : Dto(entity);
                public class OrderResponse { public int Count { get; init; } }
                public class CleanContainer
                {
                    public List<OrderResponse> CleanItems { get; init; } = [];
                }
                public class MixedContainer
                {
                    public List<OrderResponse> RepeatedClean { get; init; } = [];
                    public List<OrderDto> LeakedItems { get; init; } = [];
                    public Wide Overflow { get; init; } = new();
                }
            }
            """).AddSyntaxTrees(wideTree).WithAssemblyName("Sample.Contract");
        contract.AssertNoCompilerErrors();

        var diagnostics = Analyze(contract, "Contract");
        diagnostics.Where(d => d.Id == "DRNDTO005").Select(d => d.Properties["Target"]).Should().BeEquivalentTo(
            "P:Sample.Contract.QA.Orders.MixedContainer.LeakedItems");
        diagnostics.Where(d => d.Id == "DRNARCHITECTURE001").Select(d => d.Properties["Target"]).Should().BeEquivalentTo(
            "P:Sample.Contract.QA.Orders.MixedContainer.Overflow");
    }

    [Fact]
    public void Response_With_Expressions_Should_Preserve_Classifications_Without_Duplicate_Diagnostics()
    {
        var contract = Compilation("""
            #nullable enable
            using DRN.Framework.SharedKernel.Attributes;
            using DRN.Framework.SharedKernel.Domain;
            namespace Sample.Contract.QA.Users
            {
                public class UserDto(SourceKnownEntity? e = null) : Dto(e)
                {
                    [Sensitive] public string Token { get; init; } = "";
                }
                public sealed record UserResponse
                {
                    public string Email { get; init; } = "";
                    [Personal] public string ClassifiedEmail { get; init; } = "";
                    public string Token { get; init; } = "";
                }
                public sealed record WrapperResponse
                {
                    public UserResponse Inner { get; init; } = new();
                }
            }
            """).WithAssemblyName("Sample.Contract");
        contract.AssertNoCompilerErrors();

        var domain = Compilation("""
            #nullable enable
            using DRN.Framework.SharedKernel.Attributes;
            using DRN.Framework.SharedKernel.Domain;
            using Sample.Contract.QA.Users;
            namespace Sample.Domain.QA.User
            {
                public class UserEntity : AggregateRoot
                {
                    [Personal] public string Email { get; init; } = "";
                    public override UserDto ToDto() => new(this);
                }
            }
            """).WithAssemblyName("Sample.Domain").AddReferences(contract.ToMetadataReference());
        domain.AssertNoCompilerErrors();

        var hosted = Compilation("""
            #nullable enable
            using Sample.Contract.QA.Users;
            using Sample.Domain.QA.User;
            namespace Sample.Hosted.Controllers.QA
            {
                public class WithMapper
                {
                    public UserResponse Unclassified(UserResponse existing, UserEntity entity) => existing with { Email = entity.Email };
                    public UserResponse Classified(UserResponse existing, UserEntity entity) => existing with { ClassifiedEmail = entity.Email };
                    public UserResponse FromDto(UserResponse existing, UserDto dto) => existing with { Token = dto.Token };
                    public UserResponse Chained(UserResponse existing, UserEntity entity, UserDto dto) =>
                        existing with { Email = entity.Email } with { Token = dto.Token };
                    public WrapperResponse Nested(UserResponse existing, UserEntity entity) =>
                        new WrapperResponse { Inner = existing with { Email = entity.Email } };
                    public UserResponse Empty(UserResponse existing) => existing with { };
                }
            }
            """).WithAssemblyName("Sample.Hosted").AddReferences(contract.ToMetadataReference(), domain.ToMetadataReference());
        hosted.AssertNoCompilerErrors();

        var diagnostics = Analyze(hosted, "Hosted").Where(d => d.Id == "DRNDTO003").ToArray();
        diagnostics.Select(d => (
            Line: d.Location.SourceTree!.GetText().Lines[d.Location.GetLineSpan().StartLinePosition.Line].ToString().Trim(),
            Span: AnalyzerTestCompilation.LocationText(d),
            Message: d.GetMessage())).Should().BeEquivalentTo(new[]
        {
            ("public UserResponse Unclassified(UserResponse existing, UserEntity entity) => existing with { Email = entity.Email };",
                "Email = entity.Email",
                "Projected property 'Email' requires [Personal] from mapped property 'Email'."),
            ("public UserResponse FromDto(UserResponse existing, UserDto dto) => existing with { Token = dto.Token };",
                "Token = dto.Token",
                "Projected property 'Token' requires [Sensitive] from mapped property 'Token'."),
            ("existing with { Email = entity.Email } with { Token = dto.Token };",
                "Email = entity.Email",
                "Projected property 'Email' requires [Personal] from mapped property 'Email'."),
            ("existing with { Email = entity.Email } with { Token = dto.Token };",
                "Token = dto.Token",
                "Projected property 'Token' requires [Sensitive] from mapped property 'Token'."),
            ("new WrapperResponse { Inner = existing with { Email = entity.Email } };",
                "Email = entity.Email",
                "Projected property 'Email' requires [Personal] from mapped property 'Email'.")
        });
    }

    [Fact]
    public void Folders_Layout_Should_Enforce_DRNDTO001_DRNDTO002_And_DRNDTO004()
    {
        var contract = Compilation("""
            using DRN.Framework.SharedKernel.Domain;
            namespace Sample.Contract.QA.Common.Orders
            {
                public class OrderDto(SourceKnownEntity? entity = null) : Dto(entity);
                public record OrderResponse;
            }
            """, "QA/Common/Orders/OrderDto.cs")
            .AddSyntaxTrees(
                CSharpSyntaxTree.ParseText("""
                    [assembly: System.Reflection.AssemblyMetadata("DRN.Conventions.ProjectName", "Sample.Contract")]
                    [assembly: System.Reflection.AssemblyMetadata("DRN.Conventions.RootNamespace", "Sample.Contract")]
                    [assembly: System.Reflection.AssemblyMetadata("DRN.Conventions.DomainLayout", "Folders")]
                    """, path: "AssemblyInfo.g.cs"),
                CSharpSyntaxTree.ParseText("using DRN.Framework.SharedKernel.Domain; namespace Sample.Contract.QA.Orders; public class MissingSubdomainDto(SourceKnownEntity? entity = null) : Dto(entity);", path: "QA/Orders/MissingSubdomainDto.cs"),
                CSharpSyntaxTree.ParseText("namespace Sample.Contract.QA.Common; public record OrderRequest;", path: "QA/Common/OrderRequest.cs"))
            .WithAssemblyName("Sample.Contract");
        contract.AssertNoCompilerErrors();
        var contractDiagnostics = AnalyzeFolders(contract,
            PathInput("QA/Common/Orders/OrderDto.cs", "QA/Common/Orders/OrderDto.cs"),
            PathInput("QA/Orders/MissingSubdomainDto.cs", "QA/Orders/MissingSubdomainDto.cs"),
            PathInput("QA/Common/OrderRequest.cs", "QA/Common/OrderRequest.cs"));
        contractDiagnostics.Select(d => (d.Id, d.Properties["Target"])).Should().BeEquivalentTo(new (string, string?)[]
        {
            ("DRNDTO002", "T:Sample.Contract.QA.Orders.MissingSubdomainDto"),
            ("DRNDTO004", "T:Sample.Contract.QA.Common.OrderRequest")
        });

        var contractRef = AnalyzerTestCompilation.EmitReference(contract, "/folders-dto/Sample.Contract.dll", AnalyzerTestCompilation.ReferenceAssemblyOptions);
        var domain = Compilation("""
            using DRN.Framework.SharedKernel.Domain;
            using Sample.Contract.QA.Common.Orders;
            namespace Sample.Domain.QA.Common.Order
            {
                public class OrderEntity : AggregateRoot { public override OrderDto ToDto() => new(this); }
            }
            namespace Sample.Domain.Billing.Common.Order
            {
                public class OrderEntity : AggregateRoot { public override OrderDto ToDto() => new(this); }
            }
            """).WithAssemblyName("Sample.Domain").AddReferences(contractRef);
        domain.AssertNoCompilerErrors();
        var domainDiagnostics = AnalyzeFolders(domain);
        domainDiagnostics.Where(d => d.Id == "DRNDTO001").Should().ContainSingle()
            .Which.Properties["Target"].Should().Be("T:Sample.Domain.Billing.Common.Order.OrderEntity");
    }

    [Fact]
    public async Task Unresolved_Error_Types_Should_Degrade_Cleanly_Without_Contract_Diagnostics()
    {
        var hosted = Compilation("""
            using System.Threading.Tasks;
            using Microsoft.AspNetCore.Mvc;
            namespace Sample.Hosted.Controllers.QA
            {
                public class BrokenController : ControllerBase
                {
                    [HttpGet("missing-return")] public Task<MissingResponse> GetMissing() => default!;
                    [HttpGet("missing-meta"), ProducesResponseType(typeof(MissingResponse), 200)] public IActionResult GetMeta() => Ok();
                    [HttpPost("missing-param")] public IActionResult Create([FromBody] MissingRequest request) => Ok();
                    public MissingResponse MapMissing(MissingEntity entity) => new() { Name = entity.Name };
                }
            }
            """).WithAssemblyName("Sample.Hosted");
        hosted.GetDiagnostics().Should().Contain(d => d.Id == "CS0246");
        var diagnostics = await hosted.WithAnalyzers([new SourceKnownConventionAnalyzer()],
            AnalyzerTestCompilation.ConventionOptions("Sample.Hosted")).GetAnalyzerDiagnosticsAsync();
        diagnostics.Should().NotContain(d => d.Id == "AD0001" || d.Id == "DRNDTO003" || d.Id == "DRNDTO005" ||
            d.Id == "DRNDTO006" || d.Id == "DRNARCHITECTURE002" || d.Id == "DRNENUM003");
    }

    [Fact]
    public void Dto_Exposure_Should_Cover_Generics_Request_Params_Domain_Infra_Wrappers_And_Allow_Nested_Dto()
    {
        var contractRef = EmitProjectReference("Sample.Contract", "/dto-m1-m3/Sample.Contract.dll", """
            using System.Collections.Generic;
            using System.Threading;
            using DRN.Framework.SharedKernel.Domain;
            namespace Sample.Contract.QA.Orders
            {
                public class ChildDto(SourceKnownEntity? entity = null) : Dto(entity);
                public class OrderDto(SourceKnownEntity? entity = null) : Dto(entity)
                {
                    public ChildDto Child { get; init; } = new();
                }
                public class OrderQueue : Queue<OrderDto>;
                public class OrderStream : IAsyncEnumerable<OrderDto>
                {
                    public IAsyncEnumerator<OrderDto> GetAsyncEnumerator(CancellationToken cancellationToken = default) => throw new System.NotImplementedException();
                }
                public class Envelope<T> { public class Metadata { public string Label { get; init; } = ""; } }
                public class NestedDtoResponse { public Envelope<OrderDto>.Metadata Meta { get; init; } = new(); }
                public class OrderResponse { public string Name { get; init; } = ""; }
            }
            """);
        var contract = Compilation("""
            using System.Collections.Generic;
            using System.Threading;
            using DRN.Framework.SharedKernel.Domain;
            namespace Sample.Contract.QA.Orders
            {
                public class ChildDto(SourceKnownEntity? entity = null) : Dto(entity);
                public class OrderDto(SourceKnownEntity? entity = null) : Dto(entity)
                {
                    public ChildDto Child { get; init; } = new();
                }
                public class OrderQueue : Queue<OrderDto>;
                public class OrderStream : IAsyncEnumerable<OrderDto>
                {
                    public IAsyncEnumerator<OrderDto> GetAsyncEnumerator(CancellationToken cancellationToken = default) => throw new System.NotImplementedException();
                }
                internal interface IIndirectOrderStream : IAsyncEnumerable<OrderDto>;
                public class IndirectOrderStream : IIndirectOrderStream
                {
                    IAsyncEnumerator<OrderDto> IAsyncEnumerable<OrderDto>.GetAsyncEnumerator(CancellationToken cancellationToken) => throw new System.NotImplementedException();
                }
                internal interface IInternalOnlyDtoMarker<T>;
                public class InternalOnlyDtoMarker : IInternalOnlyDtoMarker<OrderDto>;
                public class Envelope<T> { public class Metadata { public string Label { get; init; } = ""; } }
                public class NestedDtoResponse { public Envelope<OrderDto>.Metadata Meta { get; init; } = new(); }
            }
            """).WithAssemblyName("Sample.Contract");
        contract.AssertNoCompilerErrors();
        Analyze(contract, "Contract").Where(d => d.Id == "DRNDTO005")
            .Select(d => (d.Properties["Target"], d.Properties["Entity"])).Should().BeEquivalentTo(
                new (string?, string?)[]
                {
                    ("T:Sample.Contract.QA.Orders.OrderQueue", "Sample.Contract.QA.Orders.OrderDto"),
                    ("T:Sample.Contract.QA.Orders.OrderQueue", "Sample.Contract.QA.Orders.ChildDto"),
                    ("T:Sample.Contract.QA.Orders.OrderStream", "Sample.Contract.QA.Orders.OrderDto"),
                    ("T:Sample.Contract.QA.Orders.OrderStream", "Sample.Contract.QA.Orders.ChildDto"),
                    ("M:Sample.Contract.QA.Orders.OrderStream.GetAsyncEnumerator(System.Threading.CancellationToken)", "Sample.Contract.QA.Orders.OrderDto"),
                    ("M:Sample.Contract.QA.Orders.OrderStream.GetAsyncEnumerator(System.Threading.CancellationToken)", "Sample.Contract.QA.Orders.ChildDto"),
                    ("T:Sample.Contract.QA.Orders.IndirectOrderStream", "Sample.Contract.QA.Orders.OrderDto"),
                    ("T:Sample.Contract.QA.Orders.IndirectOrderStream", "Sample.Contract.QA.Orders.ChildDto"),
                    ("P:Sample.Contract.QA.Orders.NestedDtoResponse.Meta", "Sample.Contract.QA.Orders.OrderDto"),
                    ("P:Sample.Contract.QA.Orders.NestedDtoResponse.Meta", "Sample.Contract.QA.Orders.ChildDto")
                });

        var domainRef = EmitProjectReference("Sample.Domain", "/dto-m1-m3/Sample.Domain.dll", """
            using Sample.Contract.QA.Orders;
            namespace Sample.Domain.QA.Order
            {
                public class DomainDtoWrapper { public OrderDto Item { get; init; } = new(); }
            }
            """, contractRef);
        var infraRef = EmitProjectReference("Sample.Infra", "/dto-m1-m3/Sample.Infra.dll", """
            using Sample.Contract.QA.Orders;
            namespace Sample.Infra.QA
            {
                public class InfraDtoWrapper { public OrderDto Item { get; init; } = new(); }
            }
            """, contractRef);
        var hosted = Compilation("""
            using Microsoft.AspNetCore.Mvc;
            using Sample.Contract.QA.Orders;
            using Sample.Domain.QA.Order;
            using Sample.Infra.QA;
            namespace Sample.Hosted.Controllers.QA
            {
                public class OrdersController : ControllerBase
                {
                    [HttpPost("param")] public OrderResponse FromDtoParam([FromBody] OrderDto dto) => new();
                    [HttpGet("domain-wrap")] public DomainDtoWrapper FromDomainWrap() => new();
                    [HttpGet("infra-wrap")] public InfraDtoWrapper FromInfraWrap() => new();
                }
            }
            """).WithAssemblyName("Sample.Hosted").AddReferences(contractRef, domainRef, infraRef);
        hosted.AssertNoCompilerErrors();
        Analyze(hosted, "Hosted").Where(d => d.Id == "DRNDTO005")
            .Select(d => (d.Properties["Target"], d.Properties["Entity"])).Should().BeEquivalentTo(
                new (string?, string?)[]
                {
                    ("M:Sample.Hosted.Controllers.QA.OrdersController.FromDtoParam(Sample.Contract.QA.Orders.OrderDto)", "Sample.Contract.QA.Orders.OrderDto"),
                    ("M:Sample.Hosted.Controllers.QA.OrdersController.FromDtoParam(Sample.Contract.QA.Orders.OrderDto)", "Sample.Contract.QA.Orders.ChildDto"),
                    ("M:Sample.Hosted.Controllers.QA.OrdersController.FromDomainWrap", "Sample.Contract.QA.Orders.OrderDto"),
                    ("M:Sample.Hosted.Controllers.QA.OrdersController.FromDomainWrap", "Sample.Contract.QA.Orders.ChildDto"),
                    ("M:Sample.Hosted.Controllers.QA.OrdersController.FromInfraWrap", "Sample.Contract.QA.Orders.OrderDto"),
                    ("M:Sample.Hosted.Controllers.QA.OrdersController.FromInfraWrap", "Sample.Contract.QA.Orders.ChildDto")
                });
    }

    [Fact]
    public void Explicit_Contract_Messages_Should_Unwrap_Collections_And_Reject_Object_Dynamic_And_Wrapped_NonResponses()
    {
        var contractRef = EmitProjectReference("Sample.Contract", "/dto-m2/Sample.Contract.dll", """
            namespace Sample.Contract.QA.Orders
            {
                public class OrderResponse { public string Name { get; init; } = ""; }
                public class OrderView { public string Name { get; init; } = ""; }
                public class OrderInput { public string Name { get; init; } = ""; }
            }
            """);
        var domainRef = EmitProjectReference("Sample.Domain", "/dto-m2/Sample.Domain.dll", """
            using DRN.Framework.SharedKernel.Domain;
            namespace Sample.Domain.QA.Order
            {
                public class OrderEntity : AggregateRoot { public override Dto ToDto() => new(this); }
            }
            """);
        var hosted = Compilation("""
            using System.Collections.Generic;
            using System.Collections.Immutable;
            using System.Collections.ObjectModel;
            using System.Threading.Tasks;
            using DRN.Framework.Hosting.Controllers;
            using DRN.Framework.SharedKernel.Domain;
            using Microsoft.AspNetCore.Mvc;
            using Sample.Contract.QA.Orders;
            using Sample.Domain.QA.Order;
            namespace Sample.Hosted.Controllers.QA
            {
                public class OrdersController : ControllerBase
                {
                    [HttpGet("async")] public IAsyncEnumerable<OrderResponse> StreamOk() => null!;
                    [HttpGet("imm-array")] public ImmutableArray<OrderResponse> ImmutableArrayOk() => default;
                    [HttpGet("imm-list")] public ImmutableList<OrderResponse> ImmutableListOk() => null!;
                    [HttpGet("set")] public ISet<OrderResponse> SetOk() => null!;
                    [HttpGet("ro-set")] public IReadOnlySet<OrderResponse> ReadOnlySetOk() => null!;
                    [HttpGet("hashset")] public HashSet<OrderResponse> HashSetOk() => null!;
                    [HttpGet("collection")] public Collection<OrderResponse> CollectionOk() => null!;
                    [HttpGet("ro-collection")] public ReadOnlyCollection<OrderResponse> ReadOnlyCollectionOk() => null!;
                    [HttpGet("bad-object")] public ActionResult<object> BadObjectReturn() => null!;
                    [HttpPost("bad-object-body")] public OrderResponse BadObjectBody([FromBody] object payload) => new();
                    [HttpGet("bad-dynamic")] public Task<dynamic> BadDynamicReturn() => null!;
                    [HttpPost("bad-dynamic-body")] public OrderResponse BadDynamicBody([FromBody] dynamic payload) => new();
                    [HttpGet("bad-dynamic-list")] public List<dynamic> BadDynamicList() => null!;
                    [HttpGet("bad-stream")] public IAsyncEnumerable<OrderView> BadStream() => null!;
                    [HttpGet("bad-list")] public List<OrderView> BadList() => null!;
                    [HttpGet("bad-array")] public OrderView[] BadArray() => [];
                }

                public class NonContractOrdersController(ISourceKnownService<OrderEntity, OrderView, OrderInput> service)
                    : SourceKnownController<OrderEntity, OrderView, OrderInput>(service)
                {
                    [HttpGet("own-view")] public OrderView OwnView() => new();
                    [HttpGet("own-dynamic")] public dynamic OwnDynamic() => null!;
                }
            }
            """).WithAssemblyName("Sample.Hosted").AddReferences(contractRef, domainRef, AnalyzerTestCompilation.HostingReference);
        hosted.AssertNoCompilerErrors();
        var diagnostics = Analyze(hosted, "Hosted").Where(d => d.Id == "DRNDTO006").ToArray();
        diagnostics.Select(d => (d.Properties["Target"], d.Properties["Entity"])).Should().BeEquivalentTo(new (string?, string?)[]
        {
            ("M:Sample.Hosted.Controllers.QA.OrdersController.BadObjectReturn", "System.Object"),
            ("M:Sample.Hosted.Controllers.QA.OrdersController.BadObjectBody(System.Object)", "System.Object"),
            ("M:Sample.Hosted.Controllers.QA.OrdersController.BadDynamicReturn", "System.Object"),
            ("M:Sample.Hosted.Controllers.QA.OrdersController.BadDynamicBody(System.Object)", "System.Object"),
            ("M:Sample.Hosted.Controllers.QA.OrdersController.BadDynamicList", "System.Object"),
            ("M:Sample.Hosted.Controllers.QA.OrdersController.BadStream", "Sample.Contract.QA.Orders.OrderView"),
            ("M:Sample.Hosted.Controllers.QA.OrdersController.BadList", "Sample.Contract.QA.Orders.OrderView"),
            ("M:Sample.Hosted.Controllers.QA.OrdersController.BadArray", "Sample.Contract.QA.Orders.OrderView"),
            ("T:Sample.Hosted.Controllers.QA.NonContractOrdersController", "Sample.Contract.QA.Orders.OrderView"),
            ("T:Sample.Hosted.Controllers.QA.NonContractOrdersController", "Sample.Contract.QA.Orders.OrderInput"),
            ("M:Sample.Hosted.Controllers.QA.NonContractOrdersController.OwnView", "Sample.Contract.QA.Orders.OrderView"),
            ("M:Sample.Hosted.Controllers.QA.NonContractOrdersController.OwnDynamic", "System.Object")
        });
    }

    [Fact]
    public void Inherited_Application_Actions_Should_Report_Contracts_For_Each_Endpoint()
    {
        var contract = EmitProjectReference("Sample.Contract", "/dto-inherited/Sample.Contract.dll",
            "namespace Sample.Contract.QA.Orders; public class OrderResponse;");
        var hosted = Compilation("""
            using DRN.Framework.Hosting.Controllers;
            using DRN.Framework.SharedKernel.Domain;
            using Microsoft.AspNetCore.Mvc;
            using Sample.Contract.QA.Orders;
            namespace Sample.Hosted.Controllers.QA;
            public class OrderEntity : AggregateRoot { public override Dto ToDto() => new(this); }
            public abstract class OrderOperations(ISourceKnownService<OrderEntity, OrderResponse> service)
                : SourceKnownReadOnlyController<OrderEntity, OrderResponse>(service)
            {
                [HttpPost("preview")] public object Preview([FromBody] object input) => input;
                [HttpPost("submit")] public object Submit([FromBody] object input) => input;
            }
            public class OrdersController(ISourceKnownService<OrderEntity, OrderResponse> service) : OrderOperations(service);
            """).WithAssemblyName("Sample.Hosted").AddReferences(contract, AnalyzerTestCompilation.HostingReference);
        hosted.AssertNoCompilerErrors();

        var diagnostics = Analyze(hosted, "Hosted").Where(d => d.Id == "DRNDTO006").ToArray();
        diagnostics.Should().OnlyContain(d => d.Properties["Target"] == "T:Sample.Hosted.Controllers.QA.OrdersController");
        diagnostics.Select(d => d.GetMessage()).Should().BeEquivalentTo(
            "Public endpoint 'Sample.Hosted.Controllers.QA.OrderOperations.Preview(object)' must use an explicit Contract Request type instead of 'object'.",
            "Public endpoint 'Sample.Hosted.Controllers.QA.OrderOperations.Preview(object)' must use an explicit Contract Response type instead of 'object'.",
            "Public endpoint 'Sample.Hosted.Controllers.QA.OrderOperations.Submit(object)' must use an explicit Contract Request type instead of 'object'.",
            "Public endpoint 'Sample.Hosted.Controllers.QA.OrderOperations.Submit(object)' must use an explicit Contract Response type instead of 'object'.");
    }

    [Theory]
    [DataInlineUnit("", true)]
    [DataInlineUnit("[Personal]", false)]
    public void Response_Projections_In_Infra_Should_Preserve_Classification_Through_Linq_Extension_Chains(
        string destinationAnnotation, bool missing)
    {
        var contract = Compilation($$"""
            using DRN.Framework.SharedKernel.Attributes;
            using DRN.Framework.SharedKernel.Domain;
            namespace Sample.Contract.QA.Users
            {
                public class UserDto(SourceKnownEntity? e = null) : Dto(e);
                public record ContactSummary({{(missing ? "" : "[property: Personal]")}} string Label);
                public class ContactResponse { {{destinationAnnotation}} public string Email { get; init; } = ""; }
                public class OtherModel { public string Secret { get; init; } = ""; }
                public class UserResponse
                {
                    {{destinationAnnotation}} public string Email { get; init; } = "";
                    public System.Collections.Generic.IReadOnlyList<ContactResponse> Contacts { get; init; } = [];
                    public ContactSummary? Summary { get; init; }
                }
            }
            """).WithAssemblyName("Sample.Contract");
        contract.AssertNoCompilerErrors();
        var contractRef = AnalyzerTestCompilation.EmitReference(contract, "/dto-m4/Sample.Contract.dll", AnalyzerTestCompilation.ReferenceAssemblyOptions);

        var domain = Compilation("""
            using System.Collections.Generic;
            using DRN.Framework.SharedKernel.Attributes;
            using DRN.Framework.SharedKernel.Domain;
            using Sample.Contract.QA.Users;
            namespace Sample.Domain.QA.User
            {
                public class ContactModel { [Personal] public string Email { get; init; } = ""; }
                public class UserEntity : AggregateRoot
                {
                    [Personal] public string Email { get; init; } = "";
                    public List<ContactModel> Contacts { get; init; } = [];
                    public System.Collections.Immutable.ImmutableArray<ContactModel> ImmutableContacts { get; init; } = [];
                    public override UserDto ToDto() => new(this);
                }
            }
            """).WithAssemblyName("Sample.Domain").AddReferences(contractRef);
        domain.AssertNoCompilerErrors();
        var domainRef = AnalyzerTestCompilation.EmitReference(domain, "/dto-m4/Sample.Domain.dll", AnalyzerTestCompilation.ReferenceAssemblyOptions);

        var infra = Compilation("""
            using System.Collections.Immutable;
            using System.Linq;
            using Sample.Contract.QA.Users;
            using Sample.Domain.QA.User;
            namespace Sample.Infra.QA
            {
                public class UserProjection
                {
                    public UserResponse Direct(UserEntity entity) => new() { Email = entity.Email };
                    public UserResponse FirstContact(UserEntity entity) => new() { Email = entity.Contacts.First().Email };
                    public UserResponse Selected(UserEntity entity) =>
                        new() { Email = entity.Contacts.Select(c => c.Email).First() };
                    public UserResponse Immutable(UserEntity entity) =>
                        new() { Email = entity.ImmutableContacts.Select(c => c.Email).First() };
                    public UserResponse ProjectChildren(UserEntity entity) =>
                        new() { Contacts = entity.Contacts.Select(c => new ContactResponse { Email = c.Email }).ToArray() };
                    public UserResponse ProjectList(UserEntity entity) =>
                        new() { Contacts = entity.Contacts.Select(item => new ContactResponse { Email = item.Email }).ToList() };
                    public UserResponse ProjectImmutable(UserEntity entity) =>
                        new() { Contacts = entity.ImmutableContacts.Select(contact => new ContactResponse { Email = contact.Email }).ToImmutableArray() };
                    public UserResponse FromLocal(UserEntity entity)
                    {
                        var local = new ContactSummary(entity.Email);
                        return new() { Summary = local };
                    }
                    public UserResponse FromHelper(UserEntity entity) => new() { Summary = SummaryFor(entity) };
                    private static ContactSummary SummaryFor(UserEntity entity) => new(Label: entity.Email);
                    public OtherModel UnrelatedModel(UserEntity entity) => new() { Secret = entity.Email };
                    public UserResponse Unrelated(ContactModel[] contacts) =>
                        new() { Email = contacts.Select(c => c.Email).First() };
                    public UserResponse WithPredicate(UserEntity entity) =>
                        new() { Email = entity.Contacts.First(c => true).Email };
                    public UserResponse WithNamedArguments(UserEntity entity) =>
                        new() { Email = Enumerable.First(predicate: c => true, source: entity.Contacts).Email };
                }
            }
            """).WithAssemblyName("Sample.Infra").AddReferences(contractRef, domainRef);
        infra.AssertNoCompilerErrors();
        var diagnostics = Analyze(infra, "Infra").Where(d => d.Id == "DRNDTO003").ToArray();
        if (!missing)
        {
            diagnostics.Should().BeEmpty();
            return;
        }
        diagnostics.Select(d => AnalyzerTestCompilation.LocationText(d)).Should().BeEquivalentTo(
            "Email = entity.Email",
            "Email = entity.Contacts.First().Email",
            "Email = entity.Contacts.Select(c => c.Email).First()",
            "Email = entity.ImmutableContacts.Select(c => c.Email).First()",
            "Email = c.Email",
            "Email = item.Email",
            "Email = contact.Email",
            "entity.Email",
            "Label: entity.Email",
            "Email = entity.Contacts.First(c => true).Email",
            "Email = Enumerable.First(predicate: c => true, source: entity.Contacts).Email");
    }

    [Fact]
    public void Response_Model_Index_Should_Include_Arguments_Of_Containing_Types()
    {
        var contract = Compilation("""
            namespace Sample.Contract;
            public class ContactSummary;
            public class Envelope<T> { public class Section { public class Item; } }
            public class OrderResponse { public Envelope<ContactSummary>.Section.Item Details { get; } = new(); }
            """).WithAssemblyName("Sample.Contract");
        contract.AssertNoCompilerErrors();
        var options = AnalyzerTestCompilation.ConventionOptionsProvider("Sample.Contract");
        var facts = new ConventionFacts(contract, ConventionProject.Read(contract, options.GlobalOptions),
            new([], []), options, CancellationToken.None);
        facts.MayBeResponseModel("ContactSummary").Should().BeTrue();
        facts.IsResponseModel(contract.GetTypeByMetadataName("Sample.Contract.ContactSummary")!).Should().BeTrue();
    }

    [Fact]
    public void Response_Model_Index_Should_Follow_Generic_Constraints_For_Helper_Projections()
    {
        var contractRef = EmitProjectReference("Sample.Contract", "/constrained-response/Sample.Contract.dll", """
            namespace Sample.Contract.QA.Users
            {
                public class ChildModel { public string Email { get; init; } = ""; }
                public class ConstrainedResponse<T> where T : ChildModel
                {
                    public T Item { get; init; } = default!;
                }
            }
            """);
        var domainRef = EmitProjectReference("Sample.Domain", "/constrained-response/Sample.Domain.dll", """
            using DRN.Framework.SharedKernel.Attributes;
            using DRN.Framework.SharedKernel.Domain;
            namespace Sample.Domain.QA.User
            {
                public class UserEntity : AggregateRoot
                {
                    [Personal] public string Email => "";
                    public override Dto ToDto() => new(this);
                }
            }
            """);

        var app = Compilation("""
            using Sample.Contract.QA.Users;
            using Sample.Domain.QA.User;
            namespace Sample.Application.Services.QA.Users
            {
                public class UserMapper
                {
                    public ChildModel BuildChild(UserEntity entity) => new ChildModel { Email = entity.Email };
                }
            }
            """).WithAssemblyName("Sample.Application").AddReferences(contractRef, domainRef);
        app.AssertNoCompilerErrors();
        var diagnostic = Analyze(app, "Application").Where(d => d.Id == "DRNDTO003").Should().ContainSingle().Which;
        diagnostic.GetMessage().Should().Be("Projected property 'Email' requires [Personal] from mapped property 'Email'.");
        AnalyzerTestCompilation.LocationText(diagnostic).Should().Be("Email = entity.Email");
    }

    [Fact]
    public void Dto_Exposure_Should_Check_Type_Parameter_Constraints_Distinct_Member_Dtos_And_Distinct_Inherited_Controller_Dtos()
    {
        var contractRef = EmitProjectReference("Sample.Contract", "/dto-header-inherited/Sample.Contract.dll", """
            using DRN.Framework.SharedKernel.Domain;
            namespace Sample.Contract.QA.Orders
            {
                public class OrderDto(SourceKnownEntity? entity = null) : Dto(entity);
                public class CustomerDto(SourceKnownEntity? entity = null) : Dto(entity);
            }
            """);

        var contract = Compilation("""
            using DRN.Framework.SharedKernel.Domain;
            namespace Sample.Contract.QA.Orders
            {
                public class OrderDto(SourceKnownEntity? entity = null) : Dto(entity);
                public class CustomerDto(SourceKnownEntity? entity = null) : Dto(entity);
                public class ConstrainedDtoMarker<T> where T : OrderDto;
                public class EnvelopeDto<T>() : Dto(null);
                public class OrderResponse { public EnvelopeDto<OrderDto> Item { get; init; } = new(); }
                public interface IMarker<T1, T2>;
                public class PairMarker : IMarker<OrderDto, CustomerDto>;
                public class UnconstrainedMarker<T> { public T Value { get; init; } = default!; }
                public interface IPair<T1, T2>
                {
                    T1 First();
                    T2 Second();
                }
                public class DtoPair : IPair<OrderDto, CustomerDto>
                {
                    public (OrderDto, CustomerDto) Both { get; init; }
                    public OrderDto First() => new();
                    public CustomerDto Second() => new();
                }
            }
            """).WithAssemblyName("Sample.Contract");
        contract.AssertNoCompilerErrors();
        var contractDiagnostics = Analyze(contract, "Contract").Where(d => d.Id == "DRNDTO005").ToArray();
        contractDiagnostics.Where(d => d.Properties["Target"] == "P:Sample.Contract.QA.Orders.OrderResponse.Item")
            .Select(d => d.Properties["Entity"]).Should().BeEquivalentTo(
                "Sample.Contract.QA.Orders.EnvelopeDto`1", "Sample.Contract.QA.Orders.OrderDto");
        contractDiagnostics.Where(d => d.Properties["Target"] == "T:Sample.Contract.QA.Orders.ConstrainedDtoMarker`1")
            .Should().ContainSingle().Which.Properties["Entity"].Should().Be("Sample.Contract.QA.Orders.OrderDto");
        contractDiagnostics.Should().NotContain(d => d.Properties["Target"] == "T:Sample.Contract.QA.Orders.UnconstrainedMarker`1");
        var markerDiagnostics = contractDiagnostics.Where(d => d.Properties["Target"] == "T:Sample.Contract.QA.Orders.PairMarker").ToArray();
        markerDiagnostics.Select(d => d.Properties["Entity"]).Should().BeEquivalentTo(
            "Sample.Contract.QA.Orders.OrderDto", "Sample.Contract.QA.Orders.CustomerDto");
        var exception = new ConventionInput("Exception", "DRNDTO005", ImmutableDictionary<string, string>.Empty
            .Add("Target", "T:Sample.Contract.QA.Orders.PairMarker").Add("Entity", "Sample.Contract.QA.Orders.OrderDto")
            .Add("Owner", "Contracts").Add("Reason", "Reviewed marker argument").Add("Review", "2030-01-01"), Location.None);
        var options = AnalyzerTestCompilation.ConventionOptionsProvider("Sample.Contract");
        var facts = new ConventionFacts(contract, ConventionProject.Read(contract, options.GlobalOptions),
            new([exception], []), options, CancellationToken.None);
        var exceptions = new ConventionExceptions(facts, new DateOnly(2026, 1, 1));
        markerDiagnostics.Where(d => !exceptions.Applies(d)).Should().ContainSingle()
            .Which.Properties["Entity"].Should().Be("Sample.Contract.QA.Orders.CustomerDto");
        const string pair = "Sample.Contract.QA.Orders.DtoPair";
        const string order = "Sample.Contract.QA.Orders.OrderDto";
        const string customer = "Sample.Contract.QA.Orders.CustomerDto";
        contractDiagnostics.Where(d => d.Properties["Target"]!.Contains("DtoPair", StringComparison.Ordinal))
            .Select(d => (d.Properties["Target"], d.Properties["Entity"])).Should().BeEquivalentTo(
                new (string?, string?)[] { ("T:" + pair, order), ("T:" + pair, customer), ("M:" + pair + ".First", order),
                    ("M:" + pair + ".Second", customer), ("P:" + pair + ".Both", order), ("P:" + pair + ".Both", customer) });

        var hosted = Compilation("""
            using Microsoft.AspNetCore.Mvc;
            using Sample.Contract.QA.Orders;
            namespace Sample.Hosted.Controllers.QA
            {
                public abstract class BaseDtoController : ControllerBase
                {
                    [HttpGet("order")] public OrderDto GetOrder() => new();
                    [HttpGet("customer")] public CustomerDto GetCustomer() => new();
                    [HttpGet("order-again")] public OrderDto GetOrderAgain() => new();
                }
                public class ConcreteDtoController : BaseDtoController
                {
                    [HttpGet("own-order")] public OrderDto OwnOrder() => new();
                    [HttpPost("both")] public OrderDto Both(CustomerDto input) => new();
                }
            }
            """).WithAssemblyName("Sample.Hosted").AddReferences(contractRef);
        hosted.AssertNoCompilerErrors();
        Analyze(hosted, "Hosted").Where(d => d.Id == "DRNDTO005")
            .Select(d => (d.Properties["Target"], d.Properties["Entity"])).Should().BeEquivalentTo(
                new (string?, string?)[]
                {
                    ("T:Sample.Hosted.Controllers.QA.ConcreteDtoController", "Sample.Contract.QA.Orders.OrderDto"),
                    ("T:Sample.Hosted.Controllers.QA.ConcreteDtoController", "Sample.Contract.QA.Orders.CustomerDto"),
                    ("M:Sample.Hosted.Controllers.QA.ConcreteDtoController.Both(Sample.Contract.QA.Orders.CustomerDto)", order),
                    ("M:Sample.Hosted.Controllers.QA.ConcreteDtoController.Both(Sample.Contract.QA.Orders.CustomerDto)", customer),
                    ("M:Sample.Hosted.Controllers.QA.ConcreteDtoController.OwnOrder", "Sample.Contract.QA.Orders.OrderDto")
                });
    }

    [Fact]
    public void Dto_Exposure_Should_Traverse_Nested_Dto_Members_Message_Base_Args_And_Deduplicate_Closed_Constructions()
    {
        var contractRef = EmitProjectReference("Sample.Contract", "/dto-nested-members/Sample.Contract.dll", """
            using DRN.Framework.SharedKernel.Domain;
            namespace Sample.Contract.QA.Orders
            {
                public class CustomerDto(SourceKnownEntity? entity = null) : Dto(entity);
                public class OrderDto(SourceKnownEntity? entity = null) : Dto(entity)
                {
                    public CustomerDto Customer { get; init; } = new();
                }
            }
            """);

        var hosted = Compilation("""
            using Microsoft.AspNetCore.Mvc;
            using Sample.Contract.QA.Orders;
            namespace Sample.Hosted.Controllers.QA;
            public class OrdersController : ControllerBase
            {
                [HttpGet("order")] public OrderDto GetOrder() => new();
            }
            """).WithAssemblyName("Sample.Hosted").AddReferences(contractRef);
        hosted.AssertNoCompilerErrors();
        var endpointDiagnostics = Analyze(hosted, "Hosted").Where(d => d.Id == "DRNDTO005").ToArray();
        endpointDiagnostics.Select(d => (d.Properties["Target"], d.Properties["Entity"])).Should().BeEquivalentTo(
            new (string?, string?)[]
            {
                ("M:Sample.Hosted.Controllers.QA.OrdersController.GetOrder", "Sample.Contract.QA.Orders.OrderDto"),
                ("M:Sample.Hosted.Controllers.QA.OrdersController.GetOrder", "Sample.Contract.QA.Orders.CustomerDto")
            });
        var endpointException = new ConventionInput("Exception", "DRNDTO005", ImmutableDictionary<string, string>.Empty
            .Add("Target", "M:Sample.Hosted.Controllers.QA.OrdersController.GetOrder")
            .Add("Entity", "Sample.Contract.QA.Orders.OrderDto")
            .Add("Owner", "Contracts").Add("Reason", "Reviewed outer DTO").Add("Review", "2030-01-01"), Location.None);
        var hostedOptions = AnalyzerTestCompilation.ConventionOptionsProvider("Sample.Hosted");
        var hostedFacts = new ConventionFacts(hosted, ConventionProject.Read(hosted, hostedOptions.GlobalOptions),
            new([endpointException], []), hostedOptions, CancellationToken.None);
        var hostedExceptions = new ConventionExceptions(hostedFacts, new DateOnly(2026, 1, 1));
        endpointDiagnostics.Where(d => !hostedExceptions.Applies(d)).Should().ContainSingle()
            .Which.Properties["Entity"].Should().Be("Sample.Contract.QA.Orders.CustomerDto");

        var contract = Compilation("""
            using DRN.Framework.SharedKernel.Domain;
            namespace Sample.Contract.QA.Orders
            {
                public class CustomerDto(SourceKnownEntity? entity = null) : Dto(entity);
                public class OrderDto(SourceKnownEntity? entity = null) : Dto(entity);
                public class PaymentDto(SourceKnownEntity? entity = null) : Dto(entity);
                public class EnvelopeDto<T>() : Dto(null);
                public class PaymentSourceDto() : Dto(null)
                {
                    public PaymentDto GetPayment() => new();
                    public event System.Action<CustomerDto> Changed { add { } remove { } }
                }
                public class PaymentSourceResponse : PaymentSourceDto;
                public class OrderResponse : EnvelopeDto<OrderDto>
                {
                    public PaymentDto Payment { get; init; } = new();
                }
                public class DuplicateEnvelopeResponse
                {
                    public (EnvelopeDto<OrderDto> First, EnvelopeDto<CustomerDto> Second) Pair { get; init; }
                }
            }
            """).WithAssemblyName("Sample.Contract");
        contract.AssertNoCompilerErrors();
        var contractDiagnostics = Analyze(contract, "Contract").Where(d => d.Id == "DRNDTO005").ToArray();
        contractDiagnostics.Where(d => d.Properties["Target"] == "T:Sample.Contract.QA.Orders.PaymentSourceResponse")
            .Select(d => d.Properties["Entity"]).Should().BeEquivalentTo(
                "Sample.Contract.QA.Orders.PaymentSourceDto", "Sample.Contract.QA.Orders.PaymentDto", "Sample.Contract.QA.Orders.CustomerDto");
        var baseDiagnostics = contractDiagnostics
            .Where(d => d.Properties["Target"] == "T:Sample.Contract.QA.Orders.OrderResponse").ToArray();
        baseDiagnostics.Select(d => d.Properties["Entity"]).Should().BeEquivalentTo(
            "Sample.Contract.QA.Orders.EnvelopeDto`1",
            "Sample.Contract.QA.Orders.OrderDto");
        contractDiagnostics.Where(d => d.Properties["Target"] == "P:Sample.Contract.QA.Orders.OrderResponse.Payment")
            .Should().ContainSingle().Which.Properties["Entity"].Should().Be("Sample.Contract.QA.Orders.PaymentDto");

        var baseException = new ConventionInput("Exception", "DRNDTO005", ImmutableDictionary<string, string>.Empty
            .Add("Target", "T:Sample.Contract.QA.Orders.OrderResponse")
            .Add("Entity", "Sample.Contract.QA.Orders.EnvelopeDto`1")
            .Add("Owner", "Contracts").Add("Reason", "Reviewed envelope base").Add("Review", "2030-01-01"), Location.None);
        var contractOptions = AnalyzerTestCompilation.ConventionOptionsProvider("Sample.Contract");
        var contractFacts = new ConventionFacts(contract, ConventionProject.Read(contract, contractOptions.GlobalOptions),
            new([baseException], []), contractOptions, CancellationToken.None);
        var contractExceptions = new ConventionExceptions(contractFacts, new DateOnly(2026, 1, 1));
        baseDiagnostics.Where(d => !contractExceptions.Applies(d)).Should().ContainSingle()
            .Which.Properties["Entity"].Should().Be("Sample.Contract.QA.Orders.OrderDto");

        var pairDiagnostics = contractDiagnostics
            .Where(d => d.Properties["Target"] == "P:Sample.Contract.QA.Orders.DuplicateEnvelopeResponse.Pair").ToArray();
        pairDiagnostics.Select(d => d.Properties["Entity"]).Should().BeEquivalentTo(
            "Sample.Contract.QA.Orders.EnvelopeDto`1",
            "Sample.Contract.QA.Orders.OrderDto",
            "Sample.Contract.QA.Orders.CustomerDto");
    }

    [Fact]
    public void Deeply_Wrapped_Request_Bodies_Should_Reject_Object_While_Allowing_Named_Contract_Requests()
    {
        var contractRef = EmitProjectReference("Sample.Contract", "/dto-deep-request/Sample.Contract.dll", """
            namespace Sample.Contract.QA.Orders
            {
                public class OrderRequest { public string Name { get; init; } = ""; }
                public class OrderResponse { public string Name { get; init; } = ""; }
            }
            """);
        var hosted = Compilation("""
            using System.Collections.Generic;
            using Microsoft.AspNetCore.Mvc;
            using Sample.Contract.QA.Orders;
            namespace Sample.Hosted.Controllers.QA
            {
                public class OrdersController : ControllerBase
                {
                    [HttpPost("four-obj")]
                    public OrderResponse FourObjectWrappers([FromBody] List<List<List<List<object>>>> payload) => new();

                    [HttpPost("five-obj")]
                    public OrderResponse FiveObjectWrappers([FromBody] List<List<List<List<List<object>>>>> payload) => new();

                    [HttpPost("five-arr")]
                    public OrderResponse FiveObjectArrays([FromBody] object[][][][][] payload) => new();

                    [HttpPost("four-req")]
                    public OrderResponse FourRequestWrappers([FromBody] List<List<List<List<OrderRequest>>>> request) => new();

                    [HttpPost("five-req")]
                    public OrderResponse FiveRequestWrappers([FromBody] List<List<List<List<List<OrderRequest>>>>> request) => new();

                    [HttpPost("five-req-arr")]
                    public OrderResponse FiveRequestArrays([FromBody] OrderRequest[][][][][] request) => new();
                }
            }
            """).WithAssemblyName("Sample.Hosted").AddReferences(contractRef);
        hosted.AssertNoCompilerErrors();

        Analyze(hosted, "Hosted").Where(d => d.Id == "DRNDTO006")
            .Select(d => (d.Properties["Target"], d.Properties["Entity"])).Should().BeEquivalentTo(
                new (string?, string?)[]
                {
                    ("M:Sample.Hosted.Controllers.QA.OrdersController.FourObjectWrappers(System.Collections.Generic.List{System.Collections.Generic.List{System.Collections.Generic.List{System.Collections.Generic.List{System.Object}}}})", "System.Object"),
                    ("M:Sample.Hosted.Controllers.QA.OrdersController.FiveObjectWrappers(System.Collections.Generic.List{System.Collections.Generic.List{System.Collections.Generic.List{System.Collections.Generic.List{System.Collections.Generic.List{System.Object}}}}})", "System.Object"),
                    ("M:Sample.Hosted.Controllers.QA.OrdersController.FiveObjectArrays(System.Object[][][][][])", "System.Object")
                });
    }

    [Theory]
    [DataInlineUnit(64, false)]
    [DataInlineUnit(65, true)]
    public void Explicit_Contract_Request_Should_Check_The_Wrapper_Depth_Boundary(int depth, bool exceedsLimit)
    {
        var contract = EmitProjectReference("Sample.Contract", "/depth/Sample.Contract.dll",
            "namespace Sample.Contract.QA; public class SearchRequest;");
        var arrays = string.Concat(Enumerable.Repeat("[]", depth));
        var hosted = Compilation($$"""
            using Microsoft.AspNetCore.Mvc;
            namespace Sample.Hosted;
            public class SearchController : ControllerBase
            {
                [HttpPost] public IActionResult Find([FromBody] Sample.Contract.QA.SearchRequest{{arrays}} input) => Ok();
            }
            """).WithAssemblyName("Sample.Hosted").AddReferences(contract);
        hosted.AssertNoCompilerErrors();
        var diagnostics = Analyze(hosted, "Hosted").Where(d => d.Id == "DRNDTO006");
        if (exceedsLimit) diagnostics.Should().ContainSingle().Which.Properties["Entity"].Should().Be("System.Object");
        else diagnostics.Should().BeEmpty();
    }

    private static List<Diagnostic> Project(string contractTypes, string domainTypes, string area = "QA.Users")
    {
        var contract = Compilation($$"""
            using DRN.Framework.SharedKernel.Attributes;
            using DRN.Framework.SharedKernel.Domain;
            [assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Shop.Domain")]
            namespace Shared.Contract.{{area}};
            {{contractTypes}}
            """).WithAssemblyName("Shared.Contract");
        contract.AssertNoCompilerErrors();
        var domain = Compilation($$"""
            using DRN.Framework.SharedKernel.Attributes;
            using DRN.Framework.SharedKernel.Domain;
            using Shared.Contract.{{area}};
            namespace Shop.Domain.QA.User;
            {{domainTypes}}
            """).WithAssemblyName("Shop.Domain").AddReferences(contract.ToMetadataReference());
        domain.AssertNoCompilerErrors();
        return Analyze(domain, "Domain");
    }

    internal static PortableExecutableReference EmitProjectReference(string assemblyName, string path, string source,
        params MetadataReference[] extraReferences)
    {
        var metadata = CSharpSyntaxTree.ParseText($$"""
            [assembly: System.Reflection.AssemblyMetadata("DRN.Conventions.ProjectName", "{{assemblyName}}")]
            [assembly: System.Reflection.AssemblyMetadata("DRN.Conventions.RootNamespace", "{{assemblyName}}")]
            """, path: "AssemblyInfo.g.cs");
        var compilation = Compilation(source).AddSyntaxTrees(metadata)
            .WithAssemblyName(assemblyName).AddReferences(extraReferences);
        compilation.AssertNoCompilerErrors();
        return AnalyzerTestCompilation.EmitReference(compilation, path, AnalyzerTestCompilation.ReferenceAssemblyOptions);
    }

    private static ConventionInput PathInput(string source, string logical) => new("Path", source,
        ImmutableDictionary<string, string>.Empty.Add("LogicalPath", logical), Location.None);

    internal static List<Diagnostic> Analyze(Compilation compilation, string role, params ConventionInput[] inputs)
    {
        var name = compilation.AssemblyName!;
        var options = AnalyzerTestCompilation.ConventionOptionsProvider(name);
        var facts = new ConventionFacts(compilation, new(true, name, role, name, name, []),
            new([.. inputs], []), options, CancellationToken.None);
        ConventionDtoAnalysis.Analyze(facts);
        return facts.Diagnostics;
    }

    private static List<Diagnostic> AnalyzeFolders(Compilation compilation, params ConventionInput[] inputs)
    {
        var name = compilation.AssemblyName!;
        var options = AnalyzerTestCompilation.ConventionOptionsWithProperties(name, ("DrnDomainLayout", "Folders"));
        var project = ConventionProject.Read(compilation, options.GlobalOptions);
        var facts = new ConventionFacts(compilation, project, new([.. inputs], []), options, CancellationToken.None);
        ConventionDtoAnalysis.Analyze(facts);
        return facts.Diagnostics;
    }
}
