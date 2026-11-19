using DRN.Framework.SharedKernel.Analyzers.Conventions;
using static DRN.Test.Analyzer.Tests.ClassificationTestFixture;

namespace DRN.Test.Analyzer.Tests;

public sealed class ConventionDtoTests
{
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
        var mapper = body.Length == 0 ? "" : "public UserDto ToDto() " + body;
        var domain = Compilation($$"""
            using DRN.Framework.SharedKernel.Attributes;
            using DRN.Framework.SharedKernel.Domain;
            using Shared.Contract.QA.Users;
            namespace Shop.Domain.QA.User;
            public class UserEntity : SourceKnownEntity
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
    [DataInline("public static UserDto ToDto() => new();")]
    [DataInline("private UserDto ToDto() => new(this);")]
    [DataInline("public UserDto ToDto(int unused = 0) => new(this);")]
    [DataInline("public UserDto ToDto<T>() => new(this);")]
    [DataInline("public Dto ToDto() => new(this);")]
    public void Mapper_Should_Be_A_Public_Parameterless_Typed_Instance_Method(string method)
    {
        var contract = Compilation("""
            using DRN.Framework.SharedKernel.Domain;
            namespace Shared.Contract.QA.Users;
            public class UserDto(SourceKnownEntity? entity = null) : Dto(entity);
            """).WithAssemblyName("Shared.Contract");
        var domain = Compilation($$"""
            using DRN.Framework.SharedKernel.Domain;
            using Shared.Contract.QA.Users;
            namespace Shop.Domain.QA.User;
            public class UserEntity : SourceKnownEntity { {{method}} }
            """).WithAssemblyName("Shop.Domain").AddReferences(contract.ToMetadataReference());
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
                public UserDto ToDto() => new({{arguments}}) { Display = Name };
            }
            """);
        if (expected.Length == 0) diagnostics.Should().BeEmpty();
        else diagnostics.Should().ContainSingle().Which.Id.Should().Be(expected);
    }

    [Theory]
    [DataInline("public", "ProfileDto", "QA.Users")]
    [DataInline("public", "UserDto", "QA.Other")]
    [DataInline("internal", "UserDto", "QA.Users")]
    public void Mapper_Contract_And_Projection_Should_Report_Independently(string access, string dto, string area)
    {
        var diagnostics = Project($$"""
            {{access}} class {{dto}}(SourceKnownEntity? entity = null) : Dto(entity)
            {
                public string Label { get; init; } = "";
            }
            """, $$"""
            internal class UserEntity : SourceKnownEntity
            {
                [Personal] public string Email => "";
                public {{dto}} ToDto() => new(this) { Label = Email };
            }
            """, area);
        diagnostics.Select(diagnostic => diagnostic.Id).Should().BeEquivalentTo("DRNDTO001", "DRNDTO003");
        diagnostics.Single(item => item.Id == "DRNDTO003").GetMessage().Should()
            .Be("DTO property 'Label' requires [Personal] from mapped property 'Email'.");
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
        var batch = batchDto ? "namespace Shop.Contract.QA.ProductBatches { public class ProductBatchDto : Dto; }" : "";
        var compilation = Compilation($$"""
            using DRN.Framework.SharedKernel.Domain;
            namespace Shop.Contract.QA.Orders { public class OrderDto : Dto; }
            namespace Shop.Contract.QA.Products { public class ProductDto : Dto; }
            namespace Shop.Contract.Other.Products { public class ProductDto : Dto; }
            {{batch}}
            namespace Shop.Contract.{{area}} { public record {{name}}; }
            """, "Unrelated.cs").WithAssemblyName("Shop.Contract");
        compilation.AssertNoCompilerErrors();
        var path = PathInput("Unrelated.cs", directory + "/Unrelated.cs");
        var diagnostics = Analyze(compilation, "Contract", path);
        if (misplaced) diagnostics.Should().ContainSingle().Which.Id.Should().Be("DRNDTO004");
        else diagnostics.Should().BeEmpty();
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
                public UserDto ToDto() => new(this) { Display = Name };
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
                public UserDto ToDto() => new(this) { Details = {{construction}} { Label = {{value}} } };
            }
            """);
        if (!missing) diagnostics.Should().BeEmpty();
        else diagnostics.Should().ContainSingle().Which.GetMessage().Should().Be("DTO property 'Label' requires [Personal] from mapped property 'Email'.");
    }

    private const string MissingKeyClassification = "DTO property 'Extra' requires [Personal] from mapped property 'Email'.";
    private const string MissingValueClassification = "DTO property 'Extra' requires [Sensitive] from mapped property 'Secret'.";

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
                public UserDto ToDto() => new(this) { Contacts = new() { {{value}} } };
            }
            """);
        if (missing.Length == 0) diagnostics.Should().BeEmpty();
        else diagnostics.Should().ContainSingle().Which.GetMessage().Should()
            .Be($"DTO property '{missing}' requires [Personal] from mapped property 'Email'.");
    }

    [Theory]
    [DataInline("System.Collections.Generic.List<string>", "Email")]
    [DataInline("Labels", "new ContactDto { Label = Email }")]
    public void Scalar_Elements_And_Custom_Add_Inputs_Should_Classify_The_Collection(string collection, string element)
    {
        var diagnostics = Project($$"""
            public class ContactDto { [Personal] public string Label { get; init; } = ""; }
            public class Labels : System.Collections.Generic.List<string>
            {
                public void Add(ContactDto contact) => Add(contact.Label);
            }
            public class UserDto(SourceKnownEntity? e = null) : Dto(e)
            {
                public {{collection}} Contacts { get; init; } = new();
            }
            """, $$"""
            public class UserEntity : SourceKnownEntity
            {
                [Personal] public string Email => "";
                public UserDto ToDto() => new(this) { Contacts = new() { {{element}} } };
            }
            """);
        diagnostics.Should().ContainSingle().Which.GetMessage().Should()
            .Be("DTO property 'Contacts' requires [Personal] from mapped property 'Email'.");
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
                public UserDto ToDto() => new(this) { Extra = {{construction}} { [{{key}}] = {{value}} } };
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
                public UserDto ToDto() => new(this) { Details = new() { Display = Format(new Options { Prefix = Name }) } };
            }
            """);
        if (!missing) diagnostics.Should().BeEmpty();
        else
        {
            var diagnostic = diagnostics.Should().ContainSingle().Which;
            diagnostic.Id.Should().Be("DRNDTO003");
            diagnostic.GetMessage().Should().Be("DTO property 'Display' requires [Personal] from mapped property 'Name'.");
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
                public UserDto ToDto() => new(this) { Name = Name };
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
                public UserDto ToDto() => new(this) { Display = {{value}} };
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
                public UserDto ToDto() => new(this) { Display = {{value}} };
            }
            """);
        var diagnostic = diagnostics.Should().ContainSingle().Which;
        diagnostic.Id.Should().Be("DRNDTO003");
        diagnostic.GetMessage().Should().Be($"DTO property 'Display' requires [{kind}] from mapped property '{property}'.");
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
                public UserDto ToDto() => new(this) { Display = {{value}} };
            }
            """);
        if (missingFrom is null)
        {
            diagnostics.Should().BeEmpty();
            return;
        }
        var diagnostic = diagnostics.Should().ContainSingle().Which;
        diagnostic.Id.Should().Be("DRNDTO003");
        diagnostic.GetMessage().Should().Be($"DTO property 'Display' requires [Personal] from mapped property '{missingFrom}'.");
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
                public UserDto ToDto() => new(this) { Display = {{value}} };
            }
            """);
        if (missingFrom is null)
        {
            diagnostics.Should().BeEmpty();
            return;
        }
        var diagnostic = diagnostics.Should().ContainSingle().Which;
        diagnostic.Id.Should().Be("DRNDTO003");
        diagnostic.GetMessage().Should().Be($"DTO property 'Display' requires [Personal] from mapped property '{missingFrom}'.");
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
                public UserDto ToDto() => new(this) { Display = Number };
            }
            """;
        Project(contract, domain).Should().BeEmpty();
        var annotated = domain.Replace("public Value? Number", "[Sensitive] public Value? Number", StringComparison.Ordinal);
        Project(contract, annotated).Should().ContainSingle().Which.GetMessage().Should()
            .Be("DTO property 'Display' requires [Sensitive] from mapped property 'Number'.");
    }

    [Fact]
    public void Closed_Generic_Projection_Should_Use_The_Same_Contract_Placement_Rules()
    {
        Project("public class UserDto<T>(SourceKnownEntity? e = null) : Dto(e);", """
            public class UserEntity : SourceKnownEntity { public UserDto<int> ToDto() => new(this); }
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
            public partial class UserEntity : SourceKnownEntity { public partial UserDto ToDto(); }
            public partial class UserEntity { public partial UserDto ToDto() => new(this); }
            """).Should().BeEmpty();
    }

    [Fact]
    public void Invalid_Owner_Should_Report_The_Input_Problem_Alone()
    {
        Project("public class UserDto(SourceKnownEntity? e = null) : Dto(e);", """
            [SourceKnownSubdomain("Other")]
            public class UserEntity : SourceKnownEntity;
            """).Should().ContainSingle().Which.Id.Should().Be("DRNCONVENTIONINPUT001");
    }

    [Fact]
    public void Public_Dto_Inside_Internal_Type_Should_Not_Satisfy_The_Public_Contract()
    {
        Project("internal class Container { public class UserDto(SourceKnownEntity? e = null) : Dto(e); }", """
            internal class UserEntity : SourceKnownEntity { public Container.UserDto ToDto() => new(this); }
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
            internal class UserEntity : SourceKnownEntity { public {{type}} ToDto() => new(this); }
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
                public UserDto ToDto() => new(this) { Display = {{value}} };
            }
            """);
        if (kind is null)
        {
            diagnostics.Should().BeEmpty();
            return;
        }
        var diagnostic = diagnostics.Should().ContainSingle().Which;
        diagnostic.Id.Should().Be("DRNDTO003");
        diagnostic.GetMessage().Should().Be($"DTO property 'Display' requires [{kind}] from mapped property '{property}'.");
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
                public UserDto ToDto() => new(this) { Display = {{value}} };
            }
            """);
        if (kind is null)
        {
            diagnostics.Should().BeEmpty();
            return;
        }
        var diagnostic = diagnostics.Should().ContainSingle().Which;
        diagnostic.Id.Should().Be("DRNDTO003");
        diagnostic.GetMessage().Should().Be($"DTO property 'Display' requires [{kind}] from mapped property '{property}'.");
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
                public UserDto ToDto() => new(this) { {{assignment}} };
            }
            """);
        if (missingFrom is null)
        {
            diagnostics.Should().BeEmpty();
            return;
        }
        var diagnostic = diagnostics.Should().ContainSingle().Which;
        diagnostic.Id.Should().Be("DRNDTO003");
        diagnostic.GetMessage().Should().Be($"DTO property '{missingFrom}' requires [Personal] from mapped property 'Email'.");
    }

    [Theory]
    [DataInline("Labels", "new ContactDto { Label = Email }", "", "[Personal]", "Contacts")]
    [DataInline("Labels", "new ContactDto { Label = Email }", "[Personal]", "", null)]
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
                public UserDto ToDto() => new(this) { Contacts = [{{elements}}] };
            }
            """);
        if (missing is null) diagnostics.Should().BeEmpty();
        else
        {
            var diagnostic = diagnostics.Should().ContainSingle().Which;
            diagnostic.Id.Should().Be("DRNDTO003");
            diagnostic.GetMessage().Should().Be($"DTO property '{missing}' requires [Personal] from mapped property 'Email'.");
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
                public UserDto ToDto() => new(this) { {{assignment}} };
            }
            """);
        if (missing is null) diagnostics.Should().BeEmpty();
        else
        {
            var diagnostic = diagnostics.Should().ContainSingle().Which;
            diagnostic.Id.Should().Be("DRNDTO003");
            diagnostic.GetMessage().Should().Be($"DTO property '{missing}' requires [Personal] from mapped property 'Email'.");
        }
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
}
