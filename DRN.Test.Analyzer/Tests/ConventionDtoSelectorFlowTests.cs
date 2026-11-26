using DRN.Framework.SharedKernel.Analyzers.Conventions;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using static DRN.Test.Analyzer.Tests.ClassificationTestFixture;

namespace DRN.Test.Analyzer.Tests;

public sealed class ConventionDtoSelectorFlowTests
{
    private static readonly string[] ExpectedReturnedMethods = ["WithTake", "WithReturnedLocal", "WithReassignedLocalAndTake"];
    private static readonly string[] ExpectedFallbackMethods = ["WithDefaultIfEmpty", "WithFirstOrDefault", "WithSingleOrDefault", "WithLastOrDefault"];
    private static readonly string[] ExpectedWrappedLambdaMethods = ["Parenthesized", "Casted", "DelegateCreated"];
    private static readonly string[] ExpectedReturnedSequenceMethods = ["ConditionalDirect", "ConditionalWithFallback", "DirectArray", "LocalArray"];
    private static readonly string[] ExpectedSharedReturnMessages =
    [
        "Projected property 'Label' requires [Personal] from mapped property 'Secret'.",
        "Projected property 'Label' requires [Sensitive] from mapped property 'PrivateContacts'."
    ];
    private static readonly string[] ExpectedNullConditionalMethods = ["Local", "Inline", "Nested"];

    private static List<Diagnostic> Analyze(Compilation compilation, string role) =>
        ConventionDtoTests.Analyze(compilation, role);

    private static PortableExecutableReference EmitProjectReference(string assemblyName, string path, string source,
        params MetadataReference[] extraReferences) =>
        ConventionDtoTests.EmitProjectReference(assemblyName, path, source, extraReferences);
    [Theory]
    [DataInlineUnit(true)]
    [DataInlineUnit(false)]
    public void Selector_Projections_Should_Follow_Returned_Values_Through_Locals_And_Element_Preserving_Operators(bool missing)
    {
        var annotation = missing ? "" : "[Personal] ";
        var contractRef = EmitProjectReference("Sample.Contract", $"/selector-flow-{missing}/Sample.Contract.dll", $$"""
            using System.Collections.Generic;
            using DRN.Framework.SharedKernel.Attributes;
            namespace Sample.Contract.QA.Users
            {
                public class ContactResponse { {{annotation}}public string Email { get; init; } = ""; }
                public class UserResponse
                {
                    public IReadOnlyList<ContactResponse> Contacts { get; init; } = [];
                    public IReadOnlyList<string> Labels { get; init; } = [];
                }
            }
            """);
        var domainRef = EmitProjectReference("Sample.Domain", $"/selector-flow-{missing}/Sample.Domain.dll", """
            using System.Collections.Generic;
            using DRN.Framework.SharedKernel.Attributes;
            using DRN.Framework.SharedKernel.Domain;
            namespace Sample.Domain.QA.User
            {
                public class ContactModel { [Personal] public string Email => ""; }
                public class UserEntity : AggregateRoot
                {
                    public IReadOnlyList<ContactModel> Contacts { get; } = [];
                    public override Dto ToDto() => new(this);
                }
            }
            """);

        var infra = Compilation("""
            using System;
            using System.Linq;
            using Sample.Contract.QA.Users;
            using Sample.Domain.QA.User;
            namespace Sample.Infra.QA.Users
            {
                public class UserProjection
                {
                    private static void Audit(string value) { }

                    public UserResponse WithTake(UserEntity entity) => new()
                    {
                        Contacts = entity.Contacts.Select(c => new ContactResponse { Email = c.Email }).Take(10).ToArray()
                    };

                    public UserResponse WithReturnedLocal(UserEntity entity) => new()
                    {
                        Contacts = entity.Contacts.Select(c =>
                        {
                            var item = new ContactResponse { Email = c.Email };
                            return item;
                        }).ToArray()
                    };

                    public UserResponse WithReassignedLocalAndTake(UserEntity entity, bool flag) => new()
                    {
                        Contacts = entity.Contacts.Select(c =>
                        {
                            ContactResponse item;
                            if (flag) item = new ContactResponse { Email = c.Email };
                            else item = new ContactResponse { Email = "fallback" };
                            return item;
                        }).Take(5).ToList()
                    };

                    public UserResponse WithAuditOnlyRead(UserEntity entity) => new()
                    {
                        Labels = entity.Contacts.Select(c =>
                        {
                            Audit(c.Email);
                            return "ok";
                        }).Take(10).ToArray()
                    };

                    public UserResponse WithNestedLambdaAuditOnly(UserEntity entity) => new()
                    {
                        Labels = entity.Contacts.Select(c =>
                        {
                            Func<string> reader = () => c.Email;
                            Audit(reader());
                            return "ok";
                        }).ToArray()
                    };
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
        diagnostics.Select(EnclosingMethod).Should().BeEquivalentTo(ExpectedReturnedMethods);
        diagnostics.Should().OnlyContain(d =>
            d.GetMessage() == "Projected property 'Email' requires [Personal] from mapped property 'Email'." &&
            AnalyzerTestCompilation.LocationText(d) == "Email = c.Email");
    }

    [Theory]
    [DataInlineUnit(true)]
    [DataInlineUnit(false)]
    public void Selector_Local_Aliases_Branching_Receivers_And_Standalone_Select_Helpers_Should_Report_Classification(bool missing)
    {
        var annotation = missing ? "" : "[Personal] ";
        var contractRef = EmitProjectReference("Sample.Contract", $"/selector-aliases-{missing}/Sample.Contract.dll", $$"""
            using System.Collections.Generic;
            using DRN.Framework.SharedKernel.Attributes;
            namespace Sample.Contract.QA.Users
            {
                public class ContactResponse { {{annotation}}public string Email { get; init; } = ""; }
                public class GroupResponse { public IReadOnlyList<ContactResponse> Contacts { get; init; } = []; }
                public class UserResponse
                {
                    public IReadOnlyList<ContactResponse> Contacts { get; init; } = [];
                    public IReadOnlyList<GroupResponse> Groups { get; init; } = [];
                }
            }
            """);
        var domainRef = EmitProjectReference("Sample.Domain", $"/selector-aliases-{missing}/Sample.Domain.dll", """
            using System.Collections.Generic;
            using DRN.Framework.SharedKernel.Attributes;
            using DRN.Framework.SharedKernel.Domain;
            namespace Sample.Domain.QA.User
            {
                public class ContactProfile { [Personal] public string Email => ""; }
                public class ContactModel
                {
                    [Personal] public string Email => "";
                    public ContactProfile Profile => new();
                }
                public class GroupModel { public IReadOnlyList<ContactModel> Contacts { get; } = []; }
                public class UserEntity : AggregateRoot
                {
                    public IReadOnlyList<ContactModel> Contacts { get; } = [];
                    public IReadOnlyList<GroupModel> Groups { get; } = [];
                    public override Dto ToDto() => new(this);
                }
            }
            """);

        var infra = Compilation("""
            using System.Collections.Generic;
            using System.Linq;
            using Sample.Contract.QA.Users;
            using Sample.Domain.QA.User;
            namespace Sample.Infra.QA.Users
            {
                public class UserAliasProjection
                {
                    public UserResponse ParameterAlias(UserEntity entity) => new()
                    {
                        Contacts = entity.Contacts.Select(c =>
                        {
                            var copy = c;
                            return new ContactResponse { Email = copy.Email };
                        }).ToArray()
                    };

                    public UserResponse NestedProfileAlias(UserEntity entity) => new()
                    {
                        Contacts = entity.Contacts.Select(c =>
                        {
                            var profile = c.Profile;
                            return new ContactResponse { Email = profile.Email };
                        }).ToArray()
                    };

                    public UserResponse BranchingAlias(UserEntity entity, ContactModel fallback, bool flag) => new()
                    {
                        Contacts = entity.Contacts.Select(c =>
                        {
                            var chosen = fallback;
                            if (flag) chosen = c;
                            return new ContactResponse { Email = chosen.Email };
                        }).ToArray()
                    };

                    public UserResponse NestedSelectOverAliasedLocal(UserEntity entity) => new()
                    {
                        Groups = entity.Groups.Select(g =>
                        {
                            var contacts = g.Contacts;
                            return new GroupResponse
                            {
                                Contacts = contacts.Select(c =>
                                {
                                    var nestedCopy = c;
                                    return new ContactResponse { Email = nestedCopy.Email };
                                }).ToArray()
                            };
                        }).ToArray()
                    };

                    public ContactResponse[] StandaloneAliasHelper(UserEntity entity) =>
                        entity.Contacts.Select(c =>
                        {
                            var standaloneCopy = c;
                            return new ContactResponse { Email = standaloneCopy.Email };
                        }).ToArray();

                    public IEnumerable<ContactResponse> StandaloneScalarHelper(UserEntity entity) =>
                        entity.Contacts.Select(c =>
                        {
                            var email = c.Email;
                            return new ContactResponse { Email = email };
                        });

                    public UserResponse UnrelatedAlias(ContactModel[] contacts) => new()
                    {
                        Contacts = contacts.Select(c =>
                        {
                            var unrelatedCopy = c;
                            return new ContactResponse { Email = unrelatedCopy.Email };
                        }).ToArray()
                    };
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
        diagnostics.Should().OnlyContain(d =>
            d.GetMessage() == "Projected property 'Email' requires [Personal] from mapped property 'Email'.");
        diagnostics.Select(AnalyzerTestCompilation.LocationText).Should().BeEquivalentTo(
            "Email = copy.Email",
            "Email = profile.Email",
            "Email = chosen.Email",
            "Email = nestedCopy.Email",
            "Email = standaloneCopy.Email",
            "Email = email");
    }

    [Fact]
    public void Selector_Local_Property_Access_With_Null_Checks_And_Conditional_Access_Should_Avoid_Sibling_Misattribution()
    {
        var contractRef = EmitProjectReference("Sample.Contract", "/selector-sibling-isolation/Sample.Contract.dll", """
            using System.Collections.Generic;
            using DRN.Framework.SharedKernel.Attributes;
            namespace Sample.Contract.QA.Users
            {
                public record ContactResponse
                {
                    [Personal] public string Email { get; init; } = "";
                    public string Name { get; init; } = "";
                    public string Display { get; init; } = "";
                    public bool IsValid { get; init; }
                }
                public class UserResponse
                {
                    public IReadOnlyList<ContactResponse> Contacts { get; init; } = [];
                }
            }
            """);
        var domainRef = EmitProjectReference("Sample.Domain", "/selector-sibling-isolation/Sample.Domain.dll", """
            using System.Collections.Generic;
            using DRN.Framework.SharedKernel.Attributes;
            using DRN.Framework.SharedKernel.Domain;
            namespace Sample.Domain.QA.User
            {
                public class ContactModel
                {
                    [Personal] public string Email => "";
                    public string Name => "";
                    public bool IsValid => true;
                }
                public class UserEntity : AggregateRoot
                {
                    public IReadOnlyList<ContactModel> Contacts { get; } = [];
                    public override Dto ToDto() => new(this);
                }
            }
            """);

        var infra = Compilation("""
            using System.Linq;
            using Sample.Contract.QA.Users;
            using Sample.Domain.QA.User;
            namespace Sample.Infra.QA.Users
            {
                public class UserSiblingProjection
                {
                    public UserResponse SiblingCondition(UserEntity entity, ContactResponse fallback) => new()
                    {
                        Contacts = entity.Contacts.Select(c =>
                        {
                            var item = new ContactResponse { IsValid = c.IsValid, Email = c.Email };
                            return item.IsValid ? item : fallback;
                        }).ToArray()
                    };

                    public UserResponse WithSafeSibling(UserEntity entity) => new()
                    {
                        Contacts = entity.Contacts.Select(c =>
                        {
                            var item = new ContactResponse { Name = c.Name, Email = c.Email };
                            return item with { Display = item.Name };
                        }).ToArray()
                    };

                    public UserResponse WithPersonalSibling(UserEntity entity) => new()
                    {
                        Contacts = entity.Contacts.Select(c =>
                        {
                            var item = new ContactResponse { Name = c.Name, Email = c.Email };
                            return item with { Display = item.Email };
                        }).ToArray()
                    };

                    public UserResponse ConditionalAccessCondition(UserEntity entity, ContactResponse fallback, bool flag) => new()
                    {
                        Contacts = entity.Contacts.Select(c =>
                        {
                            ContactResponse? item = flag ? new ContactResponse { IsValid = c.IsValid, Email = c.Email } : null;
                            return item?.IsValid == true ? item : fallback;
                        }).ToArray()
                    };

                    public UserResponse WithConditionalSafe(UserEntity entity) => new()
                    {
                        Contacts = entity.Contacts.Select(c =>
                        {
                            var item = new ContactResponse { Name = c.Name, Email = c.Email };
                            return item with { Display = item?.Name ?? "" };
                        }).ToArray()
                    };

                    public UserResponse WithConditionalPersonal(UserEntity entity) => new()
                    {
                        Contacts = entity.Contacts.Select(c =>
                        {
                            var item = new ContactResponse { Name = c.Name, Email = c.Email };
                            return item with { Display = item?.Email ?? "" };
                        }).ToArray()
                    };

                    public UserResponse WithConditionalToString(UserEntity entity) => new()
                    {
                        Contacts = entity.Contacts.Select(c =>
                        {
                            var item = new ContactResponse { Name = c.Name, Email = c.Email };
                            return item with { Display = item?.ToString() ?? "" };
                        }).ToArray()
                    };

                    public UserResponse WithConditionalToStringLength(UserEntity entity) => new()
                    {
                        Contacts = entity.Contacts.Select(c =>
                        {
                            var item = new ContactResponse { Name = c.Name, Email = c.Email };
                            return item with { Display = (item?.ToString().Length ?? 0).ToString() };
                        }).ToArray()
                    };

                    public UserResponse NotNullCheck(UserEntity entity, ContactResponse fallback, bool flag) => new()
                    {
                        Contacts = entity.Contacts.Select(c =>
                        {
                            ContactResponse? item = flag ? new ContactResponse { Email = c.Email } : null;
                            return item != null ? item : fallback;
                        }).ToArray()
                    };

                    public UserResponse PatternNotNullCheck(UserEntity entity, ContactResponse fallback, bool flag) => new()
                    {
                        Contacts = entity.Contacts.Select(c =>
                        {
                            ContactResponse? item = flag ? new ContactResponse { Email = c.Email } : null;
                            return item is not null ? item : fallback;
                        }).ToArray()
                    };

                    public UserResponse ScalarNullCheck(UserEntity entity) => new()
                    {
                        Contacts = entity.Contacts.Select(c =>
                        {
                            string? email = c.Email;
                            return new ContactResponse { IsValid = email != null };
                        }).ToArray()
                    };
                }
            }
            """).WithAssemblyName("Sample.Infra").AddReferences(contractRef, domainRef);
        infra.AssertNoCompilerErrors();
        var diagnostics = Analyze(infra, "Infra").Where(d => d.Id == "DRNDTO003").ToArray();
        diagnostics.Select(d => (AnalyzerTestCompilation.LocationText(d), d.GetMessage())).Should().BeEquivalentTo(
            new[]
            {
                ("Display = item.Email", "Projected property 'Display' requires [Personal] from mapped property 'Email'."),
                ("Display = item?.Email ?? \"\"", "Projected property 'Display' requires [Personal] from mapped property 'Email'."),
                ("Display = item?.ToString() ?? \"\"", "Projected property 'Display' requires [Personal] from mapped property 'Email'."),
                ("Display = (item?.ToString().Length ?? 0).ToString()", "Projected property 'Display' requires [Personal] from mapped property 'Email'."),
                ("IsValid = email != null", "Projected property 'IsValid' requires [Personal] from mapped property 'Email'.")
            });
    }

    [Theory]
    [DataInlineUnit(true)]
    [DataInlineUnit(false)]
    public void Linq_Fallback_Element_Arguments_Should_Target_Destination_Properties(bool missing)
    {
        var annotation = missing ? "" : "[Personal] ";
        var contractRef = EmitProjectReference("Sample.Contract", $"/linq-fallback-{missing}/Sample.Contract.dll", $$"""
            using System.Collections.Generic;
            using DRN.Framework.SharedKernel.Attributes;
            namespace Sample.Contract.QA.Users
            {
                public class ContactResponse { {{annotation}}public string Email { get; init; } = ""; }
                public class UserResponse
                {
                    public IReadOnlyList<ContactResponse> Contacts { get; init; } = [];
                    public ContactResponse? Primary { get; init; }
                }
            }
            """);
        var domainRef = EmitProjectReference("Sample.Domain", $"/linq-fallback-{missing}/Sample.Domain.dll", """
            using System.Collections.Generic;
            using DRN.Framework.SharedKernel.Attributes;
            using DRN.Framework.SharedKernel.Domain;
            namespace Sample.Domain.QA.User
            {
                public class ContactModel { [Personal] public string Email => ""; }
                public class UserEntity : AggregateRoot
                {
                    [Personal] public string Email => "";
                    public IReadOnlyList<ContactModel> Contacts { get; } = [];
                    public override Dto ToDto() => new(this);
                }
            }
            """);

        var infra = Compilation("""
            using System.Linq;
            using Sample.Contract.QA.Users;
            using Sample.Domain.QA.User;
            namespace Sample.Infra.QA.Users
            {
                public class UserFallbackProjection
                {
                    public UserResponse WithDefaultIfEmpty(UserEntity entity) => new()
                    {
                        Contacts = entity.Contacts.Select(_ => new ContactResponse { Email = "ok" })
                            .DefaultIfEmpty(new ContactResponse { Email = entity.Email }).ToArray()
                    };

                    public UserResponse WithFirstOrDefault(UserEntity entity) => new()
                    {
                        Primary = entity.Contacts.Select(_ => new ContactResponse { Email = "ok" })
                            .FirstOrDefault(_ => true, new ContactResponse { Email = entity.Email })
                    };

                    public UserResponse WithSingleOrDefault(UserEntity entity) => new()
                    {
                        Primary = entity.Contacts.Select(_ => new ContactResponse { Email = "ok" })
                            .SingleOrDefault(new ContactResponse { Email = entity.Email })
                    };

                    public UserResponse WithLastOrDefault(UserEntity entity) => new()
                    {
                        Primary = entity.Contacts.Select(_ => new ContactResponse { Email = "ok" })
                            .LastOrDefault(_ => true, new ContactResponse { Email = entity.Email })
                    };
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
        diagnostics.Select(EnclosingMethod).Should().BeEquivalentTo(ExpectedFallbackMethods);
        diagnostics.Should().OnlyContain(d =>
            d.GetMessage() == "Projected property 'Email' requires [Personal] from mapped property 'Email'." &&
            AnalyzerTestCompilation.LocationText(d) == "Email = entity.Email");
    }

    [Fact]
    public void Selector_Flow_Should_Track_Compound_Coalesce_Overwrites_Loops_Captures_And_Diamond_Chains()
    {
        var contractRef = EmitProjectReference("Sample.Contract", "/selector-flow-soundness/Sample.Contract.dll", """
            using System.Collections.Generic;
            namespace Sample.Contract.QA.Users
            {
                public class ContactResponse { public string Label { get; init; } = ""; }
                public class GroupResponse { public IReadOnlyList<ContactResponse> Contacts { get; init; } = []; }
                public class UserResponse
                {
                    public IReadOnlyList<ContactResponse> Contacts { get; init; } = [];
                    public IReadOnlyList<GroupResponse> Groups { get; init; } = [];
                }
            }
            """);
        var domainRef = EmitProjectReference("Sample.Domain", "/selector-flow-soundness/Sample.Domain.dll", """
            using System.Collections.Generic;
            using DRN.Framework.SharedKernel.Attributes;
            using DRN.Framework.SharedKernel.Domain;
            namespace Sample.Domain.QA.User
            {
                public class ContactModel { [Personal] public string Email => ""; }
                public class GroupModel
                {
                    [Personal] public string Secret => "";
                    public IReadOnlyList<ContactModel> Contacts { get; } = [];
                }
                public class UserEntity : AggregateRoot
                {
                    public IReadOnlyList<ContactModel> Contacts { get; } = [];
                    public IReadOnlyList<GroupModel> Groups { get; } = [];
                    public override Dto ToDto() => new(this);
                }
            }
            """);

        var infra = Compilation("""
            using System.Linq;
            using Sample.Contract.QA.Users;
            using Sample.Domain.QA.User;
            namespace Sample.Infra.QA.Users
            {
                public class UserFlowProjection
                {
                    public UserResponse CompoundAssignment(UserEntity entity) => new()
                    {
                        Contacts = entity.Contacts.Select(c =>
                        {
                            var compound = "prefix:";
                            compound += c.Email;
                            return new ContactResponse { Label = compound };
                        }).ToArray()
                    };

                    public UserResponse CoalesceAssignment(UserEntity entity) => new()
                    {
                        Contacts = entity.Contacts.Select(c =>
                        {
                            string? coalesced = null;
                            coalesced ??= c.Email;
                            return new ContactResponse { Label = coalesced };
                        }).ToArray()
                    };

                    public UserResponse UnconditionalOverwrite(UserEntity entity) => new()
                    {
                        Contacts = entity.Contacts.Select(c =>
                        {
                            var overwritten = c.Email;
                            overwritten = "public";
                            return new ContactResponse { Label = overwritten };
                        }).ToArray()
                    };

                    public UserResponse EarlyReturnBeforeLaterPersonal(UserEntity entity, bool flag) => new()
                    {
                        Contacts = entity.Contacts.Select(c =>
                        {
                            var early = "public";
                            if (flag) return new ContactResponse { Label = early };
                            early = c.Email;
                            return new ContactResponse { Label = "done" };
                        }).ToArray()
                    };

                    public UserResponse ConditionalOverwrite(UserEntity entity, bool flag) => new()
                    {
                        Contacts = entity.Contacts.Select(c =>
                        {
                            var conditional = "public";
                            if (flag) conditional = c.Email;
                            return new ContactResponse { Label = conditional };
                        }).ToArray()
                    };

                    public UserResponse PriorAliasBeforeOverwrite(UserEntity entity) => new()
                    {
                        Contacts = entity.Contacts.Select(c =>
                        {
                            var original = c.Email;
                            var kept = original;
                            original = "public";
                            return new ContactResponse { Label = kept };
                        }).ToArray()
                    };

                    public UserResponse LoopBackEdge(UserEntity entity) => new()
                    {
                        Contacts = entity.Contacts.Select(c =>
                        {
                            var looped = "safe";
                            for (var i = 0; i < 2; i++)
                            {
                                if (i == 1) return new ContactResponse { Label = looped };
                                looped = c.Email;
                            }
                            return new ContactResponse { Label = "done" };
                        }).ToArray()
                    };

                    public UserResponse CapturedOuterLocal(UserEntity entity) => new()
                    {
                        Groups = entity.Groups.Select(g =>
                        {
                            var captured = g.Secret;
                            return new GroupResponse
                            {
                                Contacts = g.Contacts.Select(_ =>
                                {
                                    var suffix = "x";
                                    return new ContactResponse { Label = captured + suffix };
                                }).ToArray()
                            };
                        }).ToArray()
                    };

                    public UserResponse DiamondChain(UserEntity entity) => new()
                    {
                        Contacts = entity.Contacts.Select(c =>
                        {
                            var v0 = c.Email;
                            var v1 = v0 + v0;
                            var v2 = v1 + v1;
                            var v3 = v2 + v2;
                            var v4 = v3 + v3;
                            var v5 = v4 + v4;
                            var v6 = v5 + v5;
                            var v7 = v6 + v6;
                            var v8 = v7 + v7;
                            var v9 = v8 + v8;
                            var v10 = v9 + v9;
                            var v11 = v10 + v10;
                            var v12 = v11 + v11;
                            var v13 = v12 + v12;
                            var v14 = v13 + v13;
                            var v15 = v14 + v14;
                            var v16 = v15 + v15;
                            var v17 = v16 + v16;
                            var v18 = v17 + v17;
                            var v19 = v18 + v18;
                            var v20 = v19 + v19;
                            return new ContactResponse { Label = v20 };
                        }).ToArray()
                    };
                }
            }
            """).WithAssemblyName("Sample.Infra").AddReferences(contractRef, domainRef);
        infra.AssertNoCompilerErrors();
        var diagnostics = Analyze(infra, "Infra").Where(d => d.Id == "DRNDTO003").ToArray();
        diagnostics.Select(d => (AnalyzerTestCompilation.LocationText(d), d.GetMessage())).Should().BeEquivalentTo(
            new[]
            {
                ("Label = compound", "Projected property 'Label' requires [Personal] from mapped property 'Email'."),
                ("Label = coalesced", "Projected property 'Label' requires [Personal] from mapped property 'Email'."),
                ("Label = conditional", "Projected property 'Label' requires [Personal] from mapped property 'Email'."),
                ("Label = kept", "Projected property 'Label' requires [Personal] from mapped property 'Email'."),
                ("Label = looped", "Projected property 'Label' requires [Personal] from mapped property 'Email'."),
                ("Label = captured + suffix", "Projected property 'Label' requires [Personal] from mapped property 'Secret'."),
                ("Label = v20", "Projected property 'Label' requires [Personal] from mapped property 'Email'.")
            });
    }

    private static readonly Lazy<(MetadataReference Contract, MetadataReference Domain)> SelectorFlowReferences =
        new(CreateSelectorFlowReferences);

    private static (MetadataReference Contract, MetadataReference Domain) CreateSelectorFlowReferences()
    {
        var contractRef = EmitProjectReference("Sample.Contract", "/selector-flow-edges/Sample.Contract.dll", """
            using System.Collections.Generic;
            using DRN.Framework.SharedKernel.Attributes;
            namespace Sample.Contract.QA.Users
            {
                public record ContactResponse
                {
                    [Personal] public string Email { get; set; } = "";
                    public string Name { get; init; } = "";
                    public string Label { get; set; } = "";
                    public string Display { get; init; } = "";
                    public IReadOnlyList<string> Tags { get; init; } = [];
                    public List<string> MutableTags { get; } = [];
                    [Personal] public List<string> PersonalTags { get; } = [];
                    public string Summary => Email;
                    private string? _fallback;
                    public string Fallback { get => _fallback ?? Email; init => _fallback = value; }
                    public Dictionary<string, string> Extra { get; } = [];
                    public string[] Codes { get; } = new string[1];
                    [Personal] public string[] Values { get; } = new string[1];
                }
                public class CtorBackedResponse
                {
                    [Personal] public string Email { get; }
                    public string Mirror { get; private set; }
                    public CtorBackedResponse(string email) { Email = email; Mirror = email; }
                }
                public class GroupResponse
                {
                    public IReadOnlyList<ContactResponse> Contacts { get; init; } = [];
                    public List<ContactResponse> Children { get; } = [];
                }
                public class UserResponse
                {
                    public IReadOnlyList<ContactResponse> Contacts { get; init; } = [];
                    [Personal] public IReadOnlyList<ContactResponse> AnnotatedContacts { get; init; } = [];
                    public IReadOnlyList<GroupResponse> Groups { get; init; } = [];
                    public IReadOnlyList<bool> Flags { get; init; } = [];
                }
            }
            """);
        var domainRef = EmitProjectReference("Sample.Domain", "/selector-flow-edges/Sample.Domain.dll", """
            using System.Collections.Generic;
            using DRN.Framework.SharedKernel.Attributes;
            using DRN.Framework.SharedKernel.Domain;
            namespace Sample.Domain.QA.User
            {
                public class ContactProfile { [Personal] public string Email => ""; }
                public class ChainNode
                {
                    [Personal] public string Email => "";
                    public void Deconstruct(out ChainNode next, out string value) { next = this; value = Email; }
                }
                public class ContactModel
                {
                    [Personal] public string Email => "";
                    [Personal] public bool IsVip => true;
                    public string Name => "";
                    public ContactProfile? Profile => new();
                    public ChainNode Root => new();
                }
                public class GroupModel
                {
                    [Personal] public string Secret => "";
                    public IReadOnlyList<ContactModel> Contacts { get; } = [];
                    [Sensitive] public IReadOnlyList<ContactModel> PrivateContacts => Contacts;
                    [Sensitive] public IReadOnlyList<ContactModel>? OptionalContacts { get; init; }
                }
                public class UserEntity : AggregateRoot
                {
                    public ContactModel Primary => new();
                    public IReadOnlyList<ContactModel> Contacts { get; } = [];
                    public IReadOnlyList<GroupModel> Groups { get; } = [];
                    public override Dto ToDto() => new(this);
                }
            }
            """);

        return (contractRef, domainRef);
    }

    [Fact]
    public void Selector_Flow_Should_Track_With_Computed_Props_Switch_Guards_Outer_Writes_Patterns_Deconstruction_Out_Mutations_And_Concat_Origin()
    {
        var (contractRef, domainRef) = SelectorFlowReferences.Value;
        var infra = Compilation("""
            using System.Collections.Generic;
            using System.Collections.Immutable;
            using System.Linq;
            using System.Text;
            using Sample.Contract.QA.Users;
            using Sample.Domain.QA.User;
            namespace Sample.Infra.QA.Users
            {
                public class UserEdgeProjection
                {
                    private static void Audit(string value) { }

                    private static bool TryExtract(string input, out string value)
                    {
                        value = input;
                        return true;
                    }

                    private static bool TryWriteConstant(string input, out string value)
                    {
                        value = input;
                        return true;
                    }

                    public UserResponse WithComputedProperty(UserEntity entity) => new()
                    {
                        Contacts = entity.Contacts.Select(c =>
                        {
                            var baseItem = new ContactResponse { Email = "public" };
                            var updated = baseItem with { Email = c.Email };
                            return updated with { Display = updated.Summary };
                        }).ToArray()
                    };

                    public UserResponse SwitchArmGuard(UserEntity entity, bool flag) => new()
                    {
                        Contacts = entity.Contacts.Select(c =>
                        {
                            var chosen = flag switch
                            {
                                true when c.Email.Length > 0 => new ContactResponse { Name = "yes" },
                                _ => new ContactResponse { Name = "no" }
                            };
                            return new ContactResponse { Display = chosen.Name };
                        }).ToArray()
                    };

                    public UserResponse SwitchPropertyPatternExpression(UserEntity entity) => new()
                    {
                        Contacts = entity.Contacts.Select(c =>
                        {
                            var matchedContact = c switch
                            {
                                { IsVip: true } => new ContactResponse { Name = "yes" },
                                _ => new ContactResponse { Name = "no" }
                            };
                            return new ContactResponse { Display = matchedContact.Name };
                        }).ToArray()
                    };

                    public UserResponse SwitchPropertyPatternStatement(UserEntity entity) => new()
                    {
                        Flags = entity.Contacts.Select(c => { switch (c) { case { IsVip: true }: return true; default: return false; } }).ToArray()
                    };

                    public UserResponse SwitchNonBranchingPropertyPattern(UserEntity entity) => new()
                    {
                        Contacts = entity.Contacts.Select(c =>
                        {
                            var safeContact = c switch
                            {
                                { Email: var _ } => new ContactResponse { Name = c.Name },
                                _ => new ContactResponse { Name = "none" }
                            };
                            return new ContactResponse { Display = safeContact.Name };
                        }).ToArray()
                    };

                    public UserResponse OuterSelectLocalWrite(UserEntity entity) => new()
                    {
                        Groups = entity.Groups.Select(g =>
                        {
                            var shared = "safe";
                            return new GroupResponse
                            {
                                Contacts = g.Contacts.Select(c =>
                                {
                                    shared = c.Email;
                                    return new ContactResponse { Label = shared };
                                }).ToArray()
                            };
                        }).ToArray()
                    };

                    public UserResponse OuterMethodLocalWrite(UserEntity entity)
                    {
                        var latest = "safe";
                        return new()
                        {
                            Contacts = entity.Contacts.Select(c =>
                            {
                                latest = c.Email;
                                return new ContactResponse { Label = latest };
                            }).ToArray()
                        };
                    }

                    public UserResponse TupleDeconstruction(UserEntity entity) => new()
                    {
                        Contacts = entity.Contacts.Select(c =>
                        {
                            var (email, name) = (c.Email, c.Name);
                            return new ContactResponse { Name = name, Display = email };
                        }).ToArray()
                    };

                    public UserResponse PatternLocals(UserEntity entity) => new()
                    {
                        Contacts = entity.Contacts.Select(c =>
                        {
                            if (c.Profile is { } profile)
                                return new ContactResponse { Label = profile.Email };
                            if (c is { Email: var matched })
                                return new ContactResponse { Display = matched };
                            return new ContactResponse();
                        }).ToArray()
                    };

                    public UserResponse OutVariable(UserEntity entity) => new()
                    {
                        Contacts = entity.Contacts.Select(c =>
                        {
                            TryExtract(c.Email, out var extracted);
                            return new ContactResponse { Label = extracted };
                        }).ToArray()
                    };

                    public UserResponse ConditionalReturn(UserEntity entity) => new()
                    {
                        Flags = entity.Contacts.Select(c => { if (c.IsVip) return true; return false; }).ToArray()
                    };

                    public UserResponse ConditionalAuditOnly(UserEntity entity) => new()
                    {
                        Flags = entity.Contacts.Select(c => { if (c.IsVip) Audit(c.Email); return false; }).ToArray()
                    };

                    public UserResponse OverwrittenCaptureBeforeInnerSelect(UserEntity entity) => new()
                    {
                        Groups = entity.Groups.Select(g =>
                        {
                            var overwritten = g.Secret;
                            overwritten = "public";
                            return new GroupResponse
                            {
                                Contacts = g.Contacts.Select(_ => new ContactResponse { Label = overwritten }).ToArray()
                            };
                        }).ToArray()
                    };

                    public UserResponse DeferredCaptureOverwrittenAfterInnerSelect(UserEntity entity) => new()
                    {
                        Groups = entity.Groups.Select(g =>
                        {
                            var deferred = "public";
                            var query = g.Contacts.Select(_ => new ContactResponse { Label = deferred });
                            deferred = g.Secret;
                            return new GroupResponse { Contacts = query.ToArray() };
                        }).ToArray()
                    };

                    public UserResponse ReadOnlyCollectionCallIgnored(UserEntity entity) => new()
                    {
                        Contacts = entity.Contacts.Select(c =>
                        {
                            var tags = new List<string>();
                            tags.Contains(c.Email);
                            return new ContactResponse { Tags = tags };
                        }).ToArray()
                    };

                    public UserResponse DirectPropertyCollectionAdd(UserEntity entity) => new()
                    {
                        AnnotatedContacts = entity.Contacts.Select(c =>
                        {
                            var item = new ContactResponse();
                            item.MutableTags.Add(c.Email);
                            return item;
                        }).ToArray(),
                        Contacts = entity.Contacts.Select(c =>
                        {
                            var item = new ContactResponse { Name = c.Name };
                            item.PersonalTags.Add(c.Email);
                            return item with { Display = item.Name };
                        }).ToArray()
                    };

                    public UserResponse TupleLocalDeconstructionSafe(UserEntity entity) => new()
                    {
                        Contacts = entity.Contacts.Select(c =>
                        {
                            var pair = (c.Email, c.Name);
                            var (email, name) = pair;
                            return new ContactResponse { Name = name };
                        }).ToArray()
                    };

                    public UserResponse TupleLocalDeconstructionPersonal(UserEntity entity) => new()
                    {
                        Contacts = entity.Contacts.Select(c =>
                        {
                            var pair = (c.Email, c.Name);
                            var (tupleEmail, tupleName) = pair;
                            return new ContactResponse { Name = tupleName, Display = tupleEmail };
                        }).ToArray()
                    };

                    public UserResponse ImperativePropertyAndCollectionMutation(UserEntity entity) => new()
                    {
                        Contacts = entity.Contacts.Select(c =>
                        {
                            var tags = new List<string>();
                            tags.Add(c.Email);
                            var item = new ContactResponse { Tags = tags };
                            item.Label = c.Email;
                            return item;
                        }).ToArray()
                    };

                    public UserResponse ImperativeSafePropertyAndCollectionMutation(UserEntity entity) => new()
                    {
                        Contacts = entity.Contacts.Select(c =>
                        {
                            var item = new ContactResponse { Name = c.Name };
                            item.Email = c.Email;
                            return item with { Display = item.Name };
                        }).ToArray(),
                        Groups = entity.Groups.Select(g =>
                        {
                            var list = new List<ContactResponse>();
                            list.Add(new ContactResponse { Email = g.Contacts[0].Email });
                            return new GroupResponse { Contacts = list };
                        }).ToArray()
                    };

                    public UserResponse CtorBackedSiblingProperty(UserEntity entity) => new()
                    {
                        Contacts = entity.Contacts.Select(c =>
                        {
                            var backed = new CtorBackedResponse(c.Email);
                            return new ContactResponse { Display = backed.Mirror };
                        }).ToArray()
                    };

                    public UserResponse ConcatAndDefaultIfEmptyOrigin(UserEntity entity, ContactModel[] fallback) => new()
                    {
                        Contacts = fallback.Concat(entity.Contacts)
                            .Select(c => new ContactResponse { Label = c.Email })
                            .ToArray()
                    };

                    public UserResponse DefaultIfEmptyFallbackOrigin(UserEntity entity, ContactModel[] empty) => new()
                    {
                        Contacts = empty.DefaultIfEmpty(entity.Primary)
                            .Select(c => new ContactResponse { Display = c.Email })
                            .ToArray()
                    };

                    public UserResponse RecursiveDeconstructionLoop(UserEntity entity) => new()
                    {
                        Contacts = entity.Contacts.Select(c =>
                        {
                            var node = c.Root;
                            for (var i = 0; i < 2; i++)
                            {
                                (node, _) = node;
                            }
                            return new ContactResponse { Display = node.Email };
                        }).ToArray()
                    };

                    public UserResponse CapturedAliasBeforeOverwrite(UserEntity entity) => new()
                    {
                        Groups = entity.Groups.Select(g =>
                        {
                            var original = g.Secret;
                            var copy = original;
                            original = "public";
                            return new GroupResponse
                            {
                                Contacts = g.Contacts.Select(_ => new ContactResponse { Label = copy }).ToArray()
                            };
                        }).ToArray()
                    };

                    public UserResponse MaterializedCaptureOverwrittenAfterSelect(UserEntity entity) => new()
                    {
                        Groups = entity.Groups.Select(g =>
                        {
                            var materialized = "public";
                            ContactResponse[] items = [.. g.Contacts.Select(_ => new ContactResponse { Label = materialized })];
                            var list = g.Contacts.Select(_ => new ContactResponse { Display = materialized }).ToArray();
                            materialized = g.Secret;
                            return new GroupResponse { Contacts = items.Concat(list).ToArray() };
                        }).ToArray()
                    };

                    public UserResponse UnconditionalOutOverwriteClearsPersonal(UserEntity entity) => new()
                    {
                        Contacts = entity.Contacts.Select(c =>
                        {
                            var cleared = c.Email;
                            TryWriteConstant("public", out cleared);
                            return new ContactResponse { Label = cleared };
                        }).ToArray()
                    };

                    public UserResponse ConditionalOutPreservesPersonal(UserEntity entity, bool flag) => new()
                    {
                        Contacts = entity.Contacts.Select(c =>
                        {
                            var maybeCleared = c.Email;
                            if (flag && TryWriteConstant("public", out maybeCleared)) { }
                            return new ContactResponse { Display = maybeCleared };
                        }).ToArray()
                    };

                    public UserResponse SelfFeedingOutPreservesInput(UserEntity entity) => new()
                    {
                        Contacts = entity.Contacts.Select(c =>
                        {
                            var reused = c.Email;
                            TryExtract(reused, out reused);
                            return new ContactResponse { Label = reused };
                        }).ToArray()
                    };

                    public UserResponse ConditionalLocalAssignment(UserEntity entity) => new()
                    {
                        Flags = entity.Contacts.Select(c => { var isVip = false; if (c.IsVip) isVip = true; return isVip; }).ToArray()
                    };

                    public UserResponse ConditionalLocalAuditOnly(UserEntity entity) => new()
                    {
                        Flags = entity.Contacts.Select(c =>
                        {
                            var auditTag = "none";
                            if (c.IsVip)
                                auditTag = "vip";
                            Audit(auditTag);
                            return false;
                        }).ToArray()
                    };

                    public UserResponse ImmutableReceiverCallsIgnored(UserEntity entity) => new()
                    {
                        Contacts = entity.Contacts.Select(c =>
                        {
                            var text = "public";
                            text.Replace("p", c.Email);
                            var immutableTags = ImmutableList<string>.Empty;
                            immutableTags.Add(c.Email);
                            return new ContactResponse { Label = text, Tags = immutableTags };
                        }).ToArray()
                    };

                    public UserResponse StringBuilderAppendTracked(UserEntity entity) => new()
                    {
                        Contacts = entity.Contacts.Select(c =>
                        {
                            var builder = new StringBuilder();
                            builder.Append(c.Email);
                            return new ContactResponse { Label = builder.ToString() };
                        }).ToArray()
                    };

                    public UserResponse CollectionWriteUsedAsCondition(UserEntity entity) => new()
                    {
                        Contacts = entity.Contacts.Select(c =>
                        {
                            var seen = new HashSet<string>();
                            var added = seen.Add(c.Email);
                            if (!added) Audit("duplicate");
                            return new ContactResponse { Tags = seen.ToArray() };
                        }).ToArray()
                    };

                    public UserResponse SwitchSetsReturnedLocal(UserEntity entity) => new()
                    {
                        Flags = entity.Contacts.Select(c => { var vip = false; switch (c.IsVip) { case true: vip = true; break; } return vip; }).ToArray()
                    };

                    public UserResponse SwitchGuardSetsReturnedLocal(UserEntity entity) => new()
                    {
                        Flags = entity.Contacts.Select(c => { var vip = true; switch (c) { case var _ when c.IsVip: break; default: vip = false; break; } return vip; }).ToArray()
                    };

                    public UserResponse SwitchChangesOnlyAuditLocal(UserEntity entity) => new()
                    {
                        Flags = entity.Contacts.Select(c =>
                        {
                            var tag = "public";
                            switch (c.IsVip) { case true: tag = "vip"; break; }
                            Audit(tag);
                            return false;
                        }).ToArray()
                    };

                    public UserResponse MaterializedQueryLocal(UserEntity entity) => new()
                    {
                        Groups = entity.Groups.Select(g =>
                        {
                            var label = g.Secret;
                            var query = g.Contacts.Select(_ => new ContactResponse { Label = label });
                            query.TryGetNonEnumeratedCount(out _);
                            var alias = query.Take(5);
                            label = "public";
                            var rows = alias.ToArray();
                            label = g.Secret;
                            return new GroupResponse { Contacts = rows };
                        }).ToArray()
                    };

                    public UserResponse MaterializedTwice(UserEntity entity) => new()
                    {
                        Groups = entity.Groups.Select(g =>
                        {
                            var repeated = "public";
                            var query = g.Contacts.Select(_ => new ContactResponse { Label = repeated });
                            var first = query.ToList();
                            repeated = g.Secret;
                            return new GroupResponse { Contacts = first.Concat(query.ToArray()).ToArray() };
                        }).ToArray()
                    };

                    public UserResponse CapturedLocalOverwrite(UserEntity entity, bool clear)
                    {
                        var shared = "public";
                        return new UserResponse
                        {
                            Contacts = entity.Contacts.Select(c =>
                            {
                                shared = c.Email;
                                if (clear) shared = "public";
                                var before = shared;
                                shared = "public";
                                return new ContactResponse { Label = shared, Display = before };
                            }).ToArray()
                        };
                    }

                    public UserResponse PreviousElementCapture(UserEntity entity) => new()
                    {
                        Groups = entity.Groups.Select(g =>
                        {
                            var shared = "public";
                            return new GroupResponse
                            {
                                Contacts = g.Contacts.Select(c =>
                                {
                                    var earlier = shared;
                                    shared = c.Email;
                                    return new ContactResponse { Label = earlier };
                                }).ToArray()
                            };
                        }).ToArray()
                    };

                    public UserResponse PropertyOverwrites(UserEntity entity, bool clear) => new()
                    {
                        Contacts = entity.Contacts.Select(c =>
                        {
                            var rewritten = new ContactResponse();
                            rewritten.Label = c.Email;
                            rewritten.MutableTags.Add(c.Email);
                            rewritten.Label = "public";
                            return rewritten;
                        }).ToArray(),
                        AnnotatedContacts = entity.Contacts.Select(c =>
                        {
                            var conditional = new ContactResponse();
                            conditional.Label = c.Email;
                            if (clear) conditional.Label = "public";
                            return conditional;
                        }).ToArray()
                    };
                }
            }
            """).WithAssemblyName("Sample.Infra").AddReferences(contractRef, domainRef);
        infra.AssertNoCompilerErrors();
        var diagnostics = Analyze(infra, "Infra").Where(d => d.Id == "DRNDTO003").ToArray();
        const string labelFromEmail = "Projected property 'Label' requires [Personal] from mapped property 'Email'.";
        const string displayFromEmail = "Projected property 'Display' requires [Personal] from mapped property 'Email'.";
        const string flagsFromIsVip = "Projected property 'Flags' requires [Personal] from mapped property 'IsVip'.";
        const string labelFromSecret = "Projected property 'Label' requires [Personal] from mapped property 'Secret'.";
        const string tagsFromEmail = "Projected property 'Tags' requires [Personal] from mapped property 'Email'.";
        const string mutableTagsFromEmail = "Projected property 'MutableTags' requires [Personal] from mapped property 'Email'.";
        diagnostics.Select(d => (EnclosingMethod(d), AnalyzerTestCompilation.LocationText(d), d.GetMessage()))
            .Should().BeEquivalentTo(new[]
            {
                ("WithComputedProperty", "Display = updated.Summary", displayFromEmail),
                ("SwitchArmGuard", "Display = chosen.Name", displayFromEmail),
                ("SwitchPropertyPatternExpression", "Display = matchedContact.Name", "Projected property 'Display' requires [Personal] from mapped property 'IsVip'."),
                ("SwitchPropertyPatternStatement", "Flags = entity.Contacts.Select(c => { switch (c) { case { IsVip: true }: return true; default: return false; } }).ToArray()", flagsFromIsVip),
                ("OuterSelectLocalWrite", "Label = shared", labelFromEmail),
                ("OuterMethodLocalWrite", "Label = latest", labelFromEmail),
                ("TupleDeconstruction", "Display = email", displayFromEmail),
                ("PatternLocals", "Label = profile.Email", labelFromEmail),
                ("PatternLocals", "Display = matched", displayFromEmail),
                ("OutVariable", "Label = extracted", labelFromEmail),
                ("ConditionalReturn", "Flags = entity.Contacts.Select(c => { if (c.IsVip) return true; return false; }).ToArray()", flagsFromIsVip),
                ("DeferredCaptureOverwrittenAfterInnerSelect", "Label = deferred", labelFromSecret),
                ("DirectPropertyCollectionAdd", "item.MutableTags.Add(c.Email)", mutableTagsFromEmail),
                ("TupleLocalDeconstructionPersonal", "Display = tupleEmail", displayFromEmail),
                ("ImperativePropertyAndCollectionMutation", "Tags = tags", tagsFromEmail),
                ("ImperativePropertyAndCollectionMutation", "item.Label = c.Email", labelFromEmail),
                ("CtorBackedSiblingProperty", "Display = backed.Mirror", displayFromEmail),
                ("ConcatAndDefaultIfEmptyOrigin", "Label = c.Email", labelFromEmail),
                ("DefaultIfEmptyFallbackOrigin", "Display = c.Email", displayFromEmail),
                ("RecursiveDeconstructionLoop", "Display = node.Email", displayFromEmail),
                ("CapturedAliasBeforeOverwrite", "Label = copy", labelFromSecret),
                ("ConditionalOutPreservesPersonal", "Display = maybeCleared", displayFromEmail),
                ("SelfFeedingOutPreservesInput", "Label = reused", labelFromEmail),
                ("ConditionalLocalAssignment", "Flags = entity.Contacts.Select(c => { var isVip = false; if (c.IsVip) isVip = true; return isVip; }).ToArray()", flagsFromIsVip),
                ("StringBuilderAppendTracked", "Label = builder.ToString()", labelFromEmail),
                ("CollectionWriteUsedAsCondition", "Tags = seen.ToArray()", tagsFromEmail),
                ("SwitchSetsReturnedLocal", "Flags = entity.Contacts.Select(c => { var vip = false; switch (c.IsVip) { case true: vip = true; break; } return vip; }).ToArray()", flagsFromIsVip),
                ("SwitchGuardSetsReturnedLocal", "Flags = entity.Contacts.Select(c => { var vip = true; switch (c) { case var _ when c.IsVip: break; default: vip = false; break; } return vip; }).ToArray()", flagsFromIsVip),
                ("MaterializedTwice", "Label = repeated", labelFromSecret),
                ("CapturedLocalOverwrite", "Display = before", displayFromEmail),
                ("PreviousElementCapture", "Label = earlier", labelFromEmail),
                ("PropertyOverwrites", "rewritten.MutableTags.Add(c.Email)", mutableTagsFromEmail),
                ("PropertyOverwrites", "conditional.Label = c.Email", labelFromEmail)
            });
    }

    [Fact]
    public void Imperative_Selectors_Should_Report_Only_The_Classified_Destination()
    {
        var (contract, domain) = SelectorFlowReferences.Value;
        var infra = Compilation("""
            using System.Collections.Generic;
            using System.Linq;
            using System.Text;
            using Sample.Contract.QA.Users;
            using Sample.Domain.QA.User;
            namespace Sample.Infra.QA.Users;
            public class Projection
            {
                public ContactResponse[] Indexed(UserEntity e) => e.Contacts.Select(c =>
                {
                    var item = new ContactResponse { Name = c.Name };
                    item.Extra["email"] = c.Email;
                    item.Values[0] = c.Email;
                    return item with { Display = item.Name };
                }).ToArray();

                public UserResponse IndexedWrapper(UserEntity e) => new() { AnnotatedContacts = e.Contacts.Select(c =>
                {
                    var row = new ContactResponse();
                    row.Extra[c.Email] = "present";
                    row.Codes[0] = c.Email;
                    return row;
                }).ToArray() };

                public GroupResponse[] Range(UserEntity e) => e.Groups.Select(g =>
                {
                    var group = new GroupResponse();
                    group.Children.AddRange(g.Contacts.Select(c => new ContactResponse { Email = c.Email }));
                    group.Children.Insert(0, new ContactResponse { Label = g.Secret });
                    return group;
                }).ToArray();

                public ContactResponse[] Deconstruct(UserEntity e) => e.Contacts.Select(c =>
                {
                    var dto = new ContactResponse();
                    var pair = (c.Name, c.Email);
                    (dto.Email, dto.Label) = pair;
                    return dto;
                }).ToArray();

                public ContactResponse[] Loops(UserEntity e) => e.Groups.Select(g =>
                {
                    var direct = new List<string>();
                    foreach (var contact in g.Contacts) direct.Add(contact.Email);
                    var builder = new StringBuilder();
                    foreach (var email in g.Contacts.Select(c => c.Email)) builder.Append("[").Append(email);
                    var names = new List<string>();
                    foreach (var (_, name) in g.Contacts.Select(c => (c.Email, c.Name))) names.Add(name);
                    return new ContactResponse { Tags = direct, Label = builder.ToString(), Name = string.Join(",", names) };
                }).ToArray();

                public ContactResponse[] Guard(UserEntity e) => e.Contacts.Select(c =>
                {
                    var message = "normal";
                    try { if (c.IsVip) message = "vip"; } catch { }
                    return new ContactResponse { Display = message };
                }).ToArray();

                public UserResponse LoopReturn(UserEntity e) => new()
                {
                    Flags = e.Contacts.Select(c => { while (c.IsVip) return true; return false; }).ToArray()
                };

                public ContactResponse[] Siblings(UserEntity e) => e.Contacts.Select(c =>
                {
                    var item = new ContactResponse { Name = c.Name };
                    if (c.IsVip) item.Email = c.Email;
                    var first = item?.Name[0];
                    var tag = item?.Tags.FirstOrDefault();
                    return item! with { Display = first + tag };
                }).ToArray();

                public ContactResponse[] Getter(UserEntity e) => e.Contacts.Select(c =>
                {
                    var item = new ContactResponse { Email = c.Email };
                    return new ContactResponse { Label = item.Fallback };
                }).ToArray();

                public ContactResponse[] ConditionalProperty(UserEntity e) => e.Contacts.Select(c =>
                {
                    var item = new ContactResponse();
                    if (c.IsVip) item.Label = "vip";
                    return item;
                }).ToArray();

                public ContactResponse[] ExclusiveBranches(UserEntity e, bool flag) => e.Contacts.Select(c =>
                {
                    var scratch = "public";
                    var output = "public";
                    _ = flag ? (scratch = c.Email) : (output = scratch);
                    return new ContactResponse { Label = output };
                }).ToArray();

                public ContactResponse[] TupleFields(UserEntity e) => e.Contacts.Select(c =>
                {
                    var pair = (Secret: c.Email, Public: c.Name);
                    return new ContactResponse { Name = pair.Item2, Label = pair.Secret };
                }).ToArray();

                public ContactResponse[] ConditionalTupleField(UserEntity e) => e.Contacts.Select(c =>
                {
                    var pair = (Box: (Box?)new Box { Email = c.Email, Name = c.Name }, Tag: "ok");
                    return new ContactResponse
                    {
                        Label = pair.Box?.Email ?? "",
                        Name = pair.Box?.Name ?? "",
                        Display = pair.Box is null ? "missing" : "present"
                    };
                }).ToArray();

                public ContactResponse[] NamedTuplePattern(UserEntity e) => e.Contacts.Select(c =>
                {
                    var pair = (Secret: c.Email, Public: c.Name);
                    if (pair is { Secret: var secret, Public: var visible })
                        return new ContactResponse { Label = secret, Name = visible };
                    return new ContactResponse();
                }).ToArray();

                public ContactResponse[] NullableTuple(UserEntity e) => e.Contacts.Select(c =>
                {
                    (string Secret, string Public)? pair = (c.Email, c.Name);
                    return new ContactResponse { Label = pair.Value.Secret, Name = pair.Value.Public };
                }).ToArray();

                public ContactResponse[] CustomTupleGetter(UserEntity e) => e.Contacts.Select(c =>
                {
                    var box = new Box { Email = c.Email, Name = c.Name };
                    return new ContactResponse { Label = box.Value.Secret, Name = box.Name };
                }).ToArray();

                public ContactResponse[] TupleModelFields(UserEntity e) => e.Contacts.Select(c =>
                {
                    var pair = (Contact: c, Tag: "ok");
                    var nested = (Tag: "ok", Inner: (Flag: true, Contact: c));
                    var (_, extracted) = nested.Inner;
                    return new ContactResponse
                    {
                        Name = pair.Contact.Name,
                        Label = pair.Contact.Email,
                        Display = nested.Inner.Contact.Email,
                        Tags = [extracted.Email]
                    };
                }).ToArray();

                public ContactResponse[] NestedTupleFieldLocal(UserEntity e) => e.Contacts.Select(c =>
                {
                    var outer = (Inner: (Secret: c.Email, Public: c.Name), Tag: "ok");
                    var inner = outer.Inner;
                    return new ContactResponse { Name = inner.Public, Label = inner.Secret };
                }).ToArray();

                public ContactResponse[] PositionalPropertyPatterns(UserEntity e, ContactModel other) => e.Contacts.Select(c =>
                {
                    var actual = (other, (Contact: c, Tag: 0));
                    var unrelated = (c, (Contact: other, Tag: 0));
                    var actualVip = actual is (_, { Contact: { IsVip: true } });
                    var unrelatedVip = unrelated is (_, { Contact: { IsVip: true } });
                    return new ContactResponse { Label = actualVip.ToString(), Name = unrelatedVip.ToString() };
                }).ToArray();

                private sealed record LocalPositional(string Email, string Name)
                {
                    public string Summary => Email;
                }

                public ContactResponse[] SameAssemblyPositionalRecord(UserEntity e) => e.Contacts.Select(c =>
                {
                    var updated = new LocalPositional("", c.Name) with { Email = c.Email };
                    return new ContactResponse { Name = updated.Name, Label = updated.Summary };
                }).ToArray();

                private sealed class Box
                {
                    public string Email { get; init; } = "";
                    public string Name { get; init; } = "";
                    public (string Public, string Secret) Value => (Name, Email);
                }
                private sealed class View { public string Name { get; init; } = ""; }
                private class ForwardingHolder<T> where T : new()
                {
                    public T Data { get; set; } = new();
                    public T Forwarded => Data;
                    public T ViaBlock { get { return this.Forwarded; } }
                }
                private sealed class Holder : ForwardingHolder<Box>
                {
                    public View Summary => new View { Name = Data.Email };
                }

                public ContactResponse[] ChainedGetter(UserEntity e) => e.Contacts.Select(c =>
                {
                    var holder = new Holder();
                    holder.Data = new Box { Email = c.Email };
                    return new ContactResponse { Label = holder.Summary.Name };
                }).ToArray();

                public ContactResponse[] ForwardingGetters(UserEntity e) => e.Contacts.Select(c =>
                {
                    var written = new Holder();
                    written.Data = new Box { Email = c.Email, Name = c.Name };
                    var initialized = new Holder { Data = new Box { Email = c.Email, Name = c.Name } };
                    return new ContactResponse
                    {
                        Name = written.Forwarded.Name,
                        Label = written.Forwarded.Email,
                        Display = initialized.ViaBlock.Name,
                        Tags = [initialized.ViaBlock.Email]
                    };
                }).ToArray();

                private interface ICard
                {
                    string Name { get; }
                    string Email { get; set; }
                }
                private abstract class Card : ICard
                {
                    public string Email { get; set; } = "";
                    public abstract string Name { get; }
                }
                private sealed class PublicCard : Card { public override string Name { get; } = ""; }
                private sealed class EmailCard : Card { public override string Name => Email; }

                public ContactResponse[] ConcreteGetters(UserEntity e) => e.Contacts.Select(c =>
                {
                    Card direct = new PublicCard { Email = c.Email };
                    ICard contract = new PublicCard { Email = c.Email };
                    ICard classified = new EmailCard { Email = c.Email };
                    Card written = new PublicCard();
                    written.Email = c.Email;
                    ICard computed = new EmailCard();
                    computed.Email = c.Email;
                    return new ContactResponse
                    {
                        Name = direct.Name + contract.Name + written.Name,
                        Label = classified.Name,
                        Display = computed.Name
                    };
                }).ToArray();

                private static Card CreateCard(bool useEmail) => useEmail ? new EmailCard() : new PublicCard();

                public ContactResponse[] UnknownGetter(UserEntity e, bool useEmail) => e.Contacts.Select(c =>
                {
                    Card unknown = CreateCard(useEmail);
                    unknown.Email = c.Email;
                    return new ContactResponse { Label = unknown.Name };
                }).ToArray();

                public ContactResponse[] TuplePatterns(UserEntity e) => e.Contacts.Select(c =>
                {
                    if ((c.Email, c.Name) is (var email, var name))
                        return new ContactResponse { Name = name, Display = email };
                    return new ContactResponse();
                }).ToArray();

                public ContactResponse[] TupleSwitch(UserEntity e) => e.Contacts.Select(c =>
                    (c.Email, c.Name) switch { (var email, var name) => new ContactResponse { Name = name, Email = email } }
                ).ToArray();
            }
            """).WithAssemblyName("Sample.Infra").AddReferences(contract, domain);
        infra.AssertNoCompilerErrors();
        var diagnostics = Analyze(infra, "Infra").Where(d => d.Id == "DRNDTO003");
        diagnostics.Select(d => (AnalyzerTestCompilation.LocationText(d), d.GetMessage())).Should().BeEquivalentTo(
            new[]
            {
                ("item.Extra[\"email\"] = c.Email", "Projected property 'Extra' requires [Personal] from mapped property 'Email'."),
                ("row.Extra[c.Email] = \"present\"", "Projected property 'Extra' requires [Personal] from mapped property 'Email'."),
                ("row.Codes[0] = c.Email", "Projected property 'Codes' requires [Personal] from mapped property 'Email'."),
                ("Label = g.Secret", "Projected property 'Label' requires [Personal] from mapped property 'Secret'."),
                ("dto.Label", "Projected property 'Label' requires [Personal] from mapped property 'Email'."),
                ("Tags = direct", "Projected property 'Tags' requires [Personal] from mapped property 'Email'."),
                ("Label = builder.ToString()", "Projected property 'Label' requires [Personal] from mapped property 'Email'."),
                ("Display = message", "Projected property 'Display' requires [Personal] from mapped property 'IsVip'."),
                ("Flags = e.Contacts.Select(c => { while (c.IsVip) return true; return false; }).ToArray()", "Projected property 'Flags' requires [Personal] from mapped property 'IsVip'."),
                ("Label = item.Fallback", "Projected property 'Label' requires [Personal] from mapped property 'Email'."),
                ("item.Label = \"vip\"", "Projected property 'Label' requires [Personal] from mapped property 'IsVip'."),
                ("Label = pair.Secret", "Projected property 'Label' requires [Personal] from mapped property 'Email'."),
                ("Label = pair.Box?.Email ?? \"\"", "Projected property 'Label' requires [Personal] from mapped property 'Email'."),
                ("Label = secret", "Projected property 'Label' requires [Personal] from mapped property 'Email'."),
                ("Label = pair.Value.Secret", "Projected property 'Label' requires [Personal] from mapped property 'Email'."),
                ("Label = box.Value.Secret", "Projected property 'Label' requires [Personal] from mapped property 'Email'."),
                ("Label = pair.Contact.Email", "Projected property 'Label' requires [Personal] from mapped property 'Email'."),
                ("Display = nested.Inner.Contact.Email", "Projected property 'Display' requires [Personal] from mapped property 'Email'."),
                ("Tags = [extracted.Email]", "Projected property 'Tags' requires [Personal] from mapped property 'Email'."),
                ("Label = inner.Secret", "Projected property 'Label' requires [Personal] from mapped property 'Email'."),
                ("Label = actualVip.ToString()", "Projected property 'Label' requires [Personal] from mapped property 'IsVip'."),
                ("Label = updated.Summary", "Projected property 'Label' requires [Personal] from mapped property 'Email'."),
                ("Label = holder.Summary.Name", "Projected property 'Label' requires [Personal] from mapped property 'Email'."),
                ("Label = written.Forwarded.Email", "Projected property 'Label' requires [Personal] from mapped property 'Email'."),
                ("Tags = [initialized.ViaBlock.Email]", "Projected property 'Tags' requires [Personal] from mapped property 'Email'."),
                ("Label = classified.Name", "Projected property 'Label' requires [Personal] from mapped property 'Email'."),
                ("Display = computed.Name", "Projected property 'Display' requires [Personal] from mapped property 'Email'."),
                ("Label = unknown.Name", "Projected property 'Label' requires [Personal] from mapped property 'Email'."),
                ("Display = email", "Projected property 'Display' requires [Personal] from mapped property 'Email'.")
            });
    }

    private static (string, string)[] AnalyzeStandaloneResponseArraySelect(string members) =>
        AnalyzeResponseDiagnostics(members).Select(d => (AnalyzerTestCompilation.LocationText(d), d.GetMessage())).ToArray();

    private static Diagnostic[] AnalyzeResponseDiagnostics(string members)
    {
        var (contract, domain) = SelectorFlowReferences.Value;
        var infra = Compilation($$"""
            using System;
            using System.Collections.Generic;
            using System.Linq;
            using System.Text;
            using Sample.Contract.QA.Users;
            using Sample.Domain.QA.User;
            namespace Sample.Infra.QA.Users;
            public class Projection
            {
            {{members}}
            }
            """).WithAssemblyName("Sample.Infra").AddReferences(contract, domain);
        infra.AssertNoCompilerErrors();
        return Analyze(infra, "Infra").Where(d => d.Id == "DRNDTO003").ToArray();
    }

    [Theory]
    [DataInlineUnit("""
        public ContactResponse[] DirectReturn(UserEntity e) => e.Contacts.Select(c =>
        {
            var row = new ContactResponse();
            row.Label = c.Email;
            return row;
        }).ToArray();
        """, true)]
    [DataInlineUnit("""
        public ContactResponse[] ReturnedLocal(UserEntity e)
        {
            var rows = e.Contacts.Select(c =>
            {
                var row = new ContactResponse();
                row.Label = c.Email;
                return row;
            }).ToArray();
            return rows;
        }
        """, true)]
    [DataInlineUnit("""
        public ContactResponse[] ReassignedBeforeReturn(UserEntity e)
        {
            var rows = e.Contacts.Select(c =>
            {
                var row = new ContactResponse();
                row.Label = c.Email;
                return row;
            }).ToArray();
            rows = [];
            return rows;
        }
        """, false)]
    [DataInlineUnit("""
        public ContactResponse[] ConditionallyReassignedBeforeReturn(UserEntity e, bool clear)
        {
            var rows = e.Contacts.Select(c =>
            {
                var row = new ContactResponse();
                row.Label = c.Email;
                return row;
            }).ToArray();
            if (clear) rows = [];
            return rows;
        }
        """, true)]
    [DataInlineUnit("""
        public ContactResponse[] ReassignedOnlyInNestedLocalFunction(UserEntity e)
        {
            var rows = e.Contacts.Select(c =>
            {
                var row = new ContactResponse();
                row.Label = c.Email;
                return row;
            }).ToArray();
            void Clear() => rows = [];
            _ = (Action)Clear;
            return rows;
        }
        """, true)]
    [DataInlineUnit("""
        public ContactResponse[] ReturnedOnlyInNestedLambda(UserEntity e)
        {
            var rows = e.Contacts.Select(c =>
            {
                var row = new ContactResponse();
                row.Label = c.Email;
                return row;
            }).ToArray();
            Func<ContactResponse[]> deferred = () => rows;
            _ = deferred;
            return [];
        }
        """, false)]
    [DataInlineUnit("""
        public ContactResponse[] ReturnedLocalInsideLocalFunction(UserEntity e)
        {
            ContactResponse[] Build()
            {
                var rows = e.Contacts.Select(c =>
                {
                    var row = new ContactResponse();
                    row.Label = c.Email;
                    return row;
                }).ToArray();
                return rows;
            }
            return Build();
        }
        """, true)]
    [DataInlineUnit("""
        public ContactResponse[] ReturnedLocalInsideLambda(UserEntity e)
        {
            Func<ContactResponse[]> deferred = () =>
            {
                var rows = e.Contacts.Select(c =>
                {
                    var row = new ContactResponse();
                    row.Label = c.Email;
                    return row;
                }).ToArray();
                return rows;
            };
            _ = deferred;
            return [];
        }
        """, false)]
    public void Materialized_Select_Returned_Through_Local_Should_Respect_Reaching_Assignments_And_Function_Boundaries(
        string method, bool shouldReport)
    {
        var diagnostics = AnalyzeStandaloneResponseArraySelect(method);
        if (!shouldReport)
        {
            diagnostics.Should().BeEmpty();
            return;
        }
        diagnostics.Should().BeEquivalentTo(new[]
        {
            ("row.Label = c.Email", "Projected property 'Label' requires [Personal] from mapped property 'Email'.")
        });
    }

    [Theory]
    [DataInlineUnit("&&", false)]
    [DataInlineUnit("||", false)]
    [DataInlineUnit("&&", true)]
    [DataInlineUnit("||", true)]
    public void Short_Circuit_Operators_Should_Carry_Left_Operand_Classification_Into_Right_Operand_Writes(
        string op, bool overwriteAfter)
    {
        var overwrite = overwriteAfter ? "flag = false;" : "";
        var diagnostics = AnalyzeStandaloneResponseArraySelect($$"""
            public ContactResponse[] ShortCircuit(UserEntity e) => e.Contacts.Select(c =>
            {
                var flag = false;
                var independent = "public";
                _ = c.IsVip {{op}} (flag = true);
                {{overwrite}}
                return new ContactResponse
                {
                    Label = flag ? "vip" : "standard",
                    Name = c.Name,
                    Display = independent
                };
            }).ToArray();
            """);
        if (overwriteAfter)
        {
            diagnostics.Should().BeEmpty();
            return;
        }
        diagnostics.Should().BeEquivalentTo(new[]
        {
            ("Label = flag ? \"vip\" : \"standard\"", "Projected property 'Label' requires [Personal] from mapped property 'IsVip'.")
        });
    }

    [Fact]
    public void Tuple_Field_Writes_Should_Match_Reads_By_Ordered_Field_Ordinals()
    {
        var flat = AnalyzeStandaloneResponseArraySelect("""
            public ContactResponse[] FlatTupleWrite(UserEntity e) => e.Contacts.Select(c =>
            {
                var pair = (Secret: "", Public: c.Name);
                pair.Secret = c.Email;
                return new ContactResponse
                {
                    Label = pair.Secret,
                    Name = pair.Public
                };
            }).ToArray();
            """);
        flat.Should().BeEquivalentTo(new[]
        {
            ("Label = pair.Secret", "Projected property 'Label' requires [Personal] from mapped property 'Email'.")
        });

        var nested = AnalyzeStandaloneResponseArraySelect("""
            public ContactResponse[] NestedTupleSiblingWrite(UserEntity e) => e.Contacts.Select(c =>
            {
                var outer = (First: (Value: "", Other: c.Name), Second: (Value: c.Name, Other: ""));
                outer.First.Value = c.Email;
                return new ContactResponse
                {
                    Label = outer.First.Value,
                    Name = outer.Second.Value,
                    Display = outer.First.Other
                };
            }).ToArray();
            """);
        nested.Should().BeEquivalentTo(new[]
        {
            ("Label = outer.First.Value", "Projected property 'Label' requires [Personal] from mapped property 'Email'.")
        });

        var cyclic = AnalyzeStandaloneResponseArraySelect("""
            public ContactResponse[] CyclicTupleFieldWrite(UserEntity e) => e.Contacts.Select(c =>
            {
                var pair = (Secret: c.Email, Public: c.Name);
                for (var i = 0; i < 2; i++)
                {
                    var next = pair.Secret;
                    pair.Secret = next;
                }
                return new ContactResponse
                {
                    Label = pair.Secret,
                    Name = pair.Public
                };
            }).ToArray();
            """);
        cyclic.Should().BeEquivalentTo(new[]
        {
            ("Label = pair.Secret", "Projected property 'Label' requires [Personal] from mapped property 'Email'.")
        });
    }

    [Fact]
    public void Unconditional_Local_Property_Write_Should_Supersede_Object_Initializer()
    {
        const string helper = """
            private class MutableDraft
            {
                public string Text { get; set; } = "";
                public string Sibling { get; set; } = "";
            }
            """;

        var overwritten = AnalyzeStandaloneResponseArraySelect($$"""
            {{helper}}
            public ContactResponse[] OverwrittenInitializer(UserEntity e) => e.Contacts.Select(c =>
            {
                var draft = new MutableDraft { Text = c.Email };
                draft.Text = c.Name;
                return new ContactResponse { Label = draft.Text };
            }).ToArray();
            """);
        overwritten.Should().BeEmpty();

        var conditional = AnalyzeStandaloneResponseArraySelect($$"""
            {{helper}}
            public ContactResponse[] ConditionalOverwrite(UserEntity e, bool clear) => e.Contacts.Select(c =>
            {
                var draft = new MutableDraft { Text = c.Email };
                if (clear) draft.Text = c.Name;
                return new ContactResponse { Label = draft.Text };
            }).ToArray();
            """);
        conditional.Should().BeEquivalentTo(new[]
        {
            ("Label = draft.Text", "Projected property 'Label' requires [Personal] from mapped property 'Email'.")
        });

        var beforeAndSibling = AnalyzeStandaloneResponseArraySelect($$"""
            {{helper}}
            public ContactResponse[] ReadBeforeOverwriteAndSibling(UserEntity e) => e.Contacts.Select(c =>
            {
                var draft = new MutableDraft { Text = c.Email, Sibling = c.Email };
                var before = draft.Text;
                draft.Text = c.Name;
                return new ContactResponse
                {
                    Label = draft.Text,
                    Display = before,
                    Name = draft.Sibling
                };
            }).ToArray();
            """);
        beforeAndSibling.Should().BeEquivalentTo(new[]
        {
            ("Display = before", "Projected property 'Display' requires [Personal] from mapped property 'Email'."),
            ("Name = draft.Sibling", "Projected property 'Name' requires [Personal] from mapped property 'Email'.")
        });
    }

    [Fact]
    public void Custom_Or_Virtual_Setter_Overwrite_Should_Preserve_Earlier_Classified_Write()
    {
        var diagnostics = AnalyzeStandaloneResponseArraySelect("""
            private sealed class SideEffectBox
            {
                private string _backup = "";
                private string _label = "";
                public string Backup => _backup;
                public string Label
                {
                    get => _label;
                    set
                    {
                        if (value.Length > 0) _backup = value;
                        _label = value;
                    }
                }
            }

            private class VirtualBox
            {
                protected string _backup = "";
                public virtual string Label { get; set; } = "";
                public string Backup => _backup;
            }

            private sealed class RetainingBox : VirtualBox
            {
                private string _label = "";
                public override string Label
                {
                    get => _label;
                    set
                    {
                        if (value.Length > 0) _backup = value;
                        _label = value;
                    }
                }
            }

            private static VirtualBox CreateBox(bool retain) => retain ? new RetainingBox() : new VirtualBox();

            public ContactResponse[] CustomSetterOverwrite(UserEntity e) => e.Contacts.Select(c =>
            {
                var box = new SideEffectBox();
                box.Label = c.Email;
                box.Label = "";
                return new ContactResponse { Name = box.Backup };
            }).ToArray();

            public ContactResponse[] VirtualSetterOverwrite(UserEntity e, bool retain) => e.Contacts.Select(c =>
            {
                var box = CreateBox(retain);
                box.Label = c.Email;
                box.Label = "";
                return new ContactResponse { Display = box.Backup };
            }).ToArray();
            """);

        diagnostics.Should().BeEquivalentTo(new[]
        {
            ("Name = box.Backup", "Projected property 'Name' requires [Personal] from mapped property 'Email'."),
            ("Display = box.Backup", "Projected property 'Display' requires [Personal] from mapped property 'Email'.")
        });
    }

    [Fact]
    public void Parenthesized_Cast_And_Delegate_Wrapped_Block_Select_Should_Report_Classification()
    {
        var diagnostics = AnalyzeResponseDiagnostics("""
            public ContactResponse[] Parenthesized(UserEntity e) => e.Contacts.Select((c =>
            {
                var row = new ContactResponse();
                row.Label = c.Email;
                return row;
            })).ToArray();

            public ContactResponse[] Casted(UserEntity e) => e.Contacts.Select((Func<ContactModel, ContactResponse>)(c =>
            {
                var row = new ContactResponse();
                row.Label = c.Email;
                return row;
            })).ToArray();

            public ContactResponse[] DelegateCreated(UserEntity e) => e.Contacts.Select(new Func<ContactModel, ContactResponse>(c =>
            {
                var row = new ContactResponse();
                row.Label = c.Email;
                return row;
            })).ToArray();
            """);

        diagnostics.Select(EnclosingMethod).Should().BeEquivalentTo(ExpectedWrappedLambdaMethods);
        diagnostics.Should().OnlyContain(d =>
            AnalyzerTestCompilation.LocationText(d) == "row.Label = c.Email" &&
            d.GetMessage() == "Projected property 'Label' requires [Personal] from mapped property 'Email'.");
    }

    [Fact]
    public void Out_And_Ref_Invocation_Null_Checks_Should_Report_Classification()
    {
        var diagnostics = AnalyzeStandaloneResponseArraySelect("""
            private static bool TryExtract(string input, out string? value)
            {
                value = input.Length > 0 ? input : null;
                return value is not null;
            }

            private static void TryMutate(string input, ref string? value)
            {
                if (input.Length > 0) value = input;
            }

            public ContactResponse[] OutAndRefNullChecks(UserEntity e) => e.Contacts.Select(c =>
            {
                TryExtract(c.Email, out var extracted);
                string? mutated = null;
                TryMutate(c.Email, ref mutated);
                return new ContactResponse
                {
                    Label = (extracted is not null).ToString(),
                    Display = (mutated != null).ToString()
                };
            }).ToArray();
            """);

        diagnostics.Should().BeEquivalentTo(new[]
        {
            ("Label = (extracted is not null).ToString()", "Projected property 'Label' requires [Personal] from mapped property 'Email'."),
            ("Display = (mutated != null).ToString()", "Projected property 'Display' requires [Personal] from mapped property 'Email'.")
        });
    }

    [Theory]
    [DataInlineUnit("==")]
    [DataInlineUnit("!=")]
    public void Overloaded_Equality_Null_Checks_Should_Respect_The_Record_Receiver(string equality)
    {
        var diagnostics = AnalyzeStandaloneResponseArraySelect($$"""
            private sealed class CustomProbe
            {
                public string Email { get; init; } = "";
                public static bool operator ==(CustomProbe? left, CustomProbe? right) =>
                    left is not null && right is null && left.Email.Length > 0;
                public static bool operator !=(CustomProbe? left, CustomProbe? right) => !(left == right);
                public override bool Equals(object? obj) => ReferenceEquals(this, obj);
                public override int GetHashCode() => 0;
            }

            private sealed record CustomEqualsRecord
            {
                public string Email { get; init; } = "";
                [System.Runtime.CompilerServices.CompilerGenerated]
                public bool Equals(CustomEqualsRecord? other) => other is null && Email.Length > 0;
                public override int GetHashCode() => 0;
            }

            public ContactResponse[] OverloadedOperators(UserEntity e) => e.Contacts.Select(c =>
            {
                CustomProbe? probe = new CustomProbe { Email = c.Email };
                CustomEqualsRecord? rec = new CustomEqualsRecord { Email = c.Email };
                return new ContactResponse
                {
                    Name = (probe is null).ToString() + (rec is null).ToString() + (null {{equality}} rec).ToString(),
                    Label = (probe == null).ToString(),
                    Display = (rec {{equality}} null).ToString()
                };
            }).ToArray();
            """);

        diagnostics.Should().BeEquivalentTo(new[]
        {
            ("Label = (probe == null).ToString()", "Projected property 'Label' requires [Personal] from mapped property 'Email'."),
            ($"Display = (rec {equality} null).ToString()", "Projected property 'Display' requires [Personal] from mapped property 'Email'.")
        });
    }

    [Fact]
    public void Nested_Member_Initializers_And_Non_Direct_Setters_Should_Not_Suppress_Prior_State()
    {
        var diagnostics = AnalyzeStandaloneResponseArraySelect("""
            private sealed class Inner
            {
                public string Label { get; set; } = "";
                public string Name { get; set; } = "";
            }

            private sealed record Holder(Inner Inner)
            {
                public List<string> Tags { get; init; } = [];
            }

            private sealed class Outer
            {
                public Inner Inner { get; }
                public Outer(string email) => Inner = new Inner { Label = email };
            }

            private sealed class MergingHolder
            {
                private readonly string _secret;
                private string _label = "";
                public MergingHolder(string secret) => _secret = secret;
                public string Label { get => _secret + _label; set => _label = value; }
            }

            private sealed class RetainingHolder
            {
                private string _label;
                public RetainingHolder(string initial) => _label = initial;
                public string Label
                {
                    get => _label;
                    set { if (value.Length > 0) _label = value; }
                }
            }

            private sealed class PlainAutoHolder
            {
                public PlainAutoHolder(string email) => Label = email;
                public string Label { get; set; }
            }

            public ContactResponse[] NestedWithInitializers(UserEntity e) => e.Contacts.Select(c =>
            {
                var first = new Holder(new Inner { Label = c.Email })
                {
                    Inner = { Name = "safe" },
                    Tags = { c.Email, "safe" }
                };
                var second = first with { };
                var overwrittenAuto = new PlainAutoHolder(c.Email) { Label = "safe" };
                return new ContactResponse
                {
                    Name = overwrittenAuto.Label,
                    Label = second.Inner.Label,
                    Tags = second.Tags
                };
            }).ToArray();

            public ContactResponse[] NestedCtorInitializer(UserEntity e) => e.Contacts.Select(c =>
            {
                var outer = new Outer(c.Email) { Inner = { Name = "safe" } };
                return new ContactResponse { Label = outer.Inner.Label };
            }).ToArray();

            public ContactResponse[] MergingGetterInitializer(UserEntity e) => e.Contacts.Select(c =>
            {
                var merging = new MergingHolder(c.Email) { Label = "safe" };
                return new ContactResponse { Display = merging.Label };
            }).ToArray();

            public ContactResponse[] RetainingSetterInitializer(UserEntity e) => e.Contacts.Select(c =>
            {
                var retaining = new RetainingHolder(c.Email) { Label = "" };
                return new ContactResponse { Name = retaining.Label };
            }).ToArray();
            """);

        diagnostics.Should().BeEquivalentTo(new[]
        {
            ("Label = second.Inner.Label", "Projected property 'Label' requires [Personal] from mapped property 'Email'."),
            ("Tags = second.Tags", "Projected property 'Tags' requires [Personal] from mapped property 'Email'."),
            ("Label = outer.Inner.Label", "Projected property 'Label' requires [Personal] from mapped property 'Email'."),
            ("Display = merging.Label", "Projected property 'Display' requires [Personal] from mapped property 'Email'."),
            ("Name = retaining.Label", "Projected property 'Name' requires [Personal] from mapped property 'Email'.")
        });
    }

    [Fact]
    public void Unverified_Constructor_Parameter_Names_Should_Not_Suppress_Other_Arguments()
    {
        var diagnostics = AnalyzeStandaloneResponseArraySelect("""
            private sealed class SwappedBox
            {
                public string Name { get; }
                public string Email { get; }
                public SwappedBox(string name, string email)
                {
                    Name = email;
                    Email = name;
                }
            }

            private sealed record OverriddenRecord(string Name, string Email)
            {
                public string Name { get; } = Email;
            }

            private sealed record VerifiedRecord(string Email, string Name);

            public ContactResponse[] ConstructorFlow(UserEntity e) => e.Contacts.Select(c =>
            {
                var swapped = new SwappedBox(c.Name, c.Email);
                var overridden = new OverriddenRecord(c.Name, c.Email);
                var verified = new VerifiedRecord(c.Email, c.Name);
                return new ContactResponse
                {
                    Name = verified.Name,
                    Label = swapped.Name,
                    Display = overridden.Name
                };
            }).ToArray();
            """);

        diagnostics.Should().BeEquivalentTo(new[]
        {
            ("Label = swapped.Name", "Projected property 'Label' requires [Personal] from mapped property 'Email'."),
            ("Display = overridden.Name", "Projected property 'Display' requires [Personal] from mapped property 'Email'.")
        });
    }

    [Fact]
    public void Interface_And_Base_Writes_On_Unknown_Concrete_Receivers_Should_Report_Through_Concrete_Reads()
    {
        var diagnostics = AnalyzeStandaloneResponseArraySelect("""
            private interface IHolder { string Data { get; set; } }
            private class Holder : IHolder { public string Data { get; set; } = ""; }
            private class BaseHolder { public virtual string Data { get; set; } = ""; }
            private class MidHolder : BaseHolder { public sealed override string Data { get; set; } = ""; }
            private class InheritedBase { public string Data { get; set; } = ""; }
            private class DerivedHolder : InheritedBase, IHolder { }

            private static Holder GetHolder() => new Holder();
            private static MidHolder GetMid() => new MidHolder();
            private static DerivedHolder GetDerived() => new DerivedHolder();

            public ContactResponse[] PolymorphicWrites(UserEntity e) => e.Contacts.Select(c =>
            {
                var h = GetHolder();
                ((IHolder)h).Data = c.Email;
                var m = GetMid();
                ((BaseHolder)m).Data = c.Email;
                var d = GetDerived();
                ((IHolder)d).Data = c.Email;
                return new ContactResponse
                {
                    Label = h.Data,
                    Display = m.Data,
                    Name = d.Data
                };
            }).ToArray();
            """);

        diagnostics.Should().BeEquivalentTo(new[]
        {
            ("Label = h.Data", "Projected property 'Label' requires [Personal] from mapped property 'Email'."),
            ("Display = m.Data", "Projected property 'Display' requires [Personal] from mapped property 'Email'."),
            ("Name = d.Data", "Projected property 'Name' requires [Personal] from mapped property 'Email'.")
        });
    }

    [Fact]
    public void Conditional_Access_And_Array_Creation_Returned_Selects_Should_Report_Classification()
    {
        var diagnostics = AnalyzeResponseDiagnostics("""
            public ContactResponse[]? ConditionalDirect(UserEntity? e) => e?.Contacts?.Select(c =>
            {
                var row = new ContactResponse();
                row.Label = c.Email;
                return row;
            }).ToArray();

            public UserResponse ConditionalWithFallback(UserEntity? e) => new()
            {
                Contacts = e?.Contacts?.Select(c =>
                {
                    var row = new ContactResponse();
                    row.Label = c.Email;
                    return row;
                }).ToArray() ?? Array.Empty<ContactResponse>()
            };

            public ContactResponse[][] DirectArray(UserEntity e) => new[]
            {
                e.Contacts.Select(c =>
                {
                    var row = new ContactResponse();
                    row.Label = c.Email;
                    return row;
                }).ToArray()
            };

            public ContactResponse[][] LocalArray(UserEntity e)
            {
                var rows = new[]
                {
                    e.Contacts.Select(c =>
                    {
                        var row = new ContactResponse();
                        row.Label = c.Email;
                        return row;
                    }).ToArray()
                };
                return rows;
            }

            public int[] DimensionNotReturned(UserEntity e) => new int[e.Contacts.Select(c =>
            {
                var row = new ContactResponse();
                row.Label = c.Email;
                return row;
            }).Count()];
            """);

        diagnostics.Select(EnclosingMethod).Should().BeEquivalentTo(ExpectedReturnedSequenceMethods);
        diagnostics.Should().OnlyContain(d =>
            AnalyzerTestCompilation.LocationText(d) == "row.Label = c.Email" &&
            d.GetMessage() == "Projected property 'Label' requires [Personal] from mapped property 'Email'.");
    }

    [Fact]
    public void Standalone_Conditional_Returns_And_Early_Return_Mutations_Should_Report_Target_Properties()
    {
        var diagnostics = AnalyzeResponseDiagnostics("""
            public ContactResponse[] StandaloneConditionalReturn(UserEntity e) => e.Contacts.Select(c =>
            {
                if (c.IsVip)
                    return new ContactResponse { Display = "Contact", Label = "vip" };
                return new ContactResponse { Label = "standard", Display = "Con" + "tact" };
            }).ToArray();

            public ContactResponse[] EarlyReturnLocalMutation(UserEntity e) => e.Contacts.Select(c =>
            {
                var row = new ContactResponse { Name = c.Name };
                if (c.IsVip)
                {
                    row.Label = "vip";
                    return row;
                }
                return row;
            }).ToArray();

            public ContactResponse[] ContinuationAfterEarlyReturn(UserEntity e) => e.Contacts.Select(c =>
            {
                var row = new ContactResponse { Name = c.Name };
                if (c.IsVip) return row;
                row.Label = "standard";
                return row;
            }).ToArray();

            public UserResponse AnnotatedCollectionEarlyReturn(UserEntity e) => new()
            {
                AnnotatedContacts = e.Contacts.Select(c =>
                {
                    var row = new ContactResponse { Name = c.Name };
                    if (c.IsVip)
                    {
                        row.Label = "vip";
                        return row;
                    }
                    return row;
                }).ToArray()
            };
            """);

        const string expectedMessage = "Projected property 'Label' requires [Personal] from mapped property 'IsVip'.";
        diagnostics.Select(d => (EnclosingMethod(d), AnalyzerTestCompilation.LocationText(d), d.GetMessage()))
            .Should().BeEquivalentTo(new[]
            {
                ("StandaloneConditionalReturn", "Label = \"vip\"", expectedMessage),
                ("EarlyReturnLocalMutation", "row.Label = \"vip\"", expectedMessage),
                ("ContinuationAfterEarlyReturn", "row.Label = \"standard\"", expectedMessage),
                ("AnnotatedCollectionEarlyReturn", "row.Label = \"vip\"", expectedMessage)
            });
    }

    [Theory]
    [DataInlineUnit(false)]
    [DataInlineUnit(true)]
    public void Ternary_Response_Should_Classify_Only_The_Changing_Property(bool useBlock)
    {
        const string expression = """
            c.IsVip ? new ContactResponse { Label = "yes", Name = "Contact" } : new ContactResponse { Label = "no", Name = "Contact" }
            """;
        var body = useBlock ? $"{{ return {expression}; }}" : expression;
        var diagnostics = AnalyzeResponseDiagnostics($$"""
            public ContactResponse[] Map(UserEntity e) => e.Contacts.Select(c => {{body}}).ToArray();
            """);

        diagnostics.Should().ContainSingle().Which.GetMessage().Should().Be(
            "Projected property 'Label' requires [Personal] from mapped property 'IsVip'.");
    }

    [Fact]
    public void Shared_Return_Value_Should_Keep_Each_Guard()
    {
        var diagnostics = AnalyzeResponseDiagnostics("""
            public ContactResponse[] Map(UserEntity e) => e.Groups.Select(g =>
            {
                var row = new ContactResponse { Label = "yes" };
                if (g.Secret.Length > 0) return row;
                if (g.PrivateContacts.Count > 0) return row;
                return new ContactResponse();
            }).ToArray();
            """);
        diagnostics.Select(d => d.GetMessage()).Should().BeEquivalentTo(ExpectedSharedReturnMessages);
    }

    [Fact]
    public void Collection_Add_Should_Use_The_Same_Destination_As_The_Initializer()
    {
        var diagnostics = AnalyzeResponseDiagnostics("""
            private class Labels : System.Collections.ObjectModel.Collection<ContactResponse>
            {
                protected override void InsertItem(int index, ContactResponse item) =>
                    base.InsertItem(index, new ContactResponse { Label = item.Email });
            }
            public GroupResponse[] DefaultAdd(UserEntity e) => e.Contacts.Select(c =>
            {
                var rows = new System.Collections.ObjectModel.Collection<ContactResponse>();
                rows.Add(new ContactResponse { Email = c.Email });
                return new GroupResponse { Contacts = rows };
            }).ToArray();
            public GroupResponse[] DefaultInitializer(UserEntity e) => e.Contacts.Select(c =>
                new GroupResponse { Contacts = new System.Collections.ObjectModel.Collection<ContactResponse>
                    { new ContactResponse { Email = c.Email } } }).ToArray();
            public GroupResponse[] DefaultExpression(UserEntity e) => e.Contacts.Select(c =>
            {
                System.Collections.ObjectModel.Collection<ContactResponse> rows = [];
                rows.Add(new ContactResponse { Email = c.Email });
                return new GroupResponse { Contacts = rows };
            }).ToArray();
            public GroupResponse[] CustomAdd(UserEntity e) => e.Contacts.Select(c =>
            {
                System.Collections.ObjectModel.Collection<ContactResponse> rows = new Labels();
                rows.Add(new ContactResponse { Email = c.Email });
                return new GroupResponse { Contacts = rows };
            }).ToArray();
            """);
        diagnostics.Select(d => (EnclosingMethod(d), d.GetMessage())).Should().BeEquivalentTo(new[]
        {
            ("CustomAdd", "Projected property 'Contacts' requires [Personal] from mapped property 'Email'.")
        });
    }

    [Fact]
    public void Returning_The_Same_Local_Should_Only_Classify_Observable_Changes()
    {
        var diagnostics = AnalyzeResponseDiagnostics("""
            public UserResponse Unchanged(UserEntity e) => new()
            {
                Contacts = e.Contacts.Select(c =>
                {
                    var row = new ContactResponse { Name = c.Name };
                    if (c.IsVip) return row;
                    return row;
                }).ToArray()
            };
            public UserResponse MayThrow(UserEntity e) => new()
            {
                Contacts = e.Contacts.Select(c =>
                {
                    var row = new ContactResponse { Name = c.Name };
                    if (c.IsVip) throw new InvalidOperationException();
                    return row;
                }).ToArray()
            };
            """);
        diagnostics.Select(d => (EnclosingMethod(d), d.GetMessage())).Should().BeEquivalentTo(new[]
        {
            ("MayThrow", "Projected property 'Contacts' requires [Personal] from mapped property 'IsVip'.")
        });
    }

    [Fact]
    public void Null_Conditional_Query_Returns_Should_Preserve_Writes_Only_When_Elements_Are_Returned()
    {
        const string selector = """
            c =>
            {
                var row = new ContactResponse();
                row.Label = c.Email;
                return row;
            }
            """;
        var diagnostics = AnalyzeResponseDiagnostics($$"""
            public ContactResponse[]? Local(UserEntity e)
            {
                var query = e.Contacts.Select({{selector}});
                return query?.ToArray();
            }

            public ContactResponse[]? Inline(UserEntity e) => e.Contacts.Select({{selector}})?.ToArray();
            public ContactResponse[]? Nested(UserEntity? e) => (e?.Contacts?.Select({{selector}}))?.ToArray();
            public int? CountOnly(UserEntity e) => e.Contacts.Select({{selector}})?.Count();
            """);
        diagnostics.Select(EnclosingMethod).Should().BeEquivalentTo(ExpectedNullConditionalMethods);
        diagnostics.Should().OnlyContain(d => AnalyzerTestCompilation.LocationText(d) == "row.Label = c.Email" &&
            d.GetMessage() == "Projected property 'Label' requires [Personal] from mapped property 'Email'.");
    }

    [Theory]
    [DataInlineUnit("query.ToArray()", true)]
    [DataInlineUnit("query.Any()", true)]
    [DataInlineUnit("query.Any(_ => true)", true)]
    [DataInlineUnit("query.TryGetNonEnumeratedCount(out _)", false)]
    public void Conditional_Query_Writes_Should_Keep_The_Collection_Condition_At_Enumeration(string use, bool enumerates)
    {
        var diagnostics = AnalyzeResponseDiagnostics($$"""
            public ContactResponse[] Map(UserEntity e, bool active) => e.Groups.Select(g =>
            {
                var value = g.Secret;
                var query = active ? g.PrivateContacts.Select(c => { value = "present"; return c.Name; })
                    : Enumerable.Empty<string>();
                _ = {{use}};
                return new ContactResponse { Label = value };
            }).ToArray();
            """);
        const string incoming = "Projected property 'Label' requires [Personal] from mapped property 'Secret'.";
        const string collection = "Projected property 'Label' requires [Sensitive] from mapped property 'PrivateContacts'.";
        diagnostics.Select(d => d.GetMessage()).Should().BeEquivalentTo(enumerates ? new[] { incoming, collection } : new[] { incoming });
    }

    [Theory]
    [DataInlineUnit("g.Secret.Length > 0 ? writes : g.PrivateContacts", "selected.ToArray()", "Personal", "Secret")]
    [DataInlineUnit("g.Secret.Length > 0 ? g.PrivateContacts : writes", "selected.ToArray()", "Personal", "Secret")]
    [DataInlineUnit("g.OptionalContacts ?? writes", "selected.ToArray()", "Sensitive", "OptionalContacts")]
    [DataInlineUnit("g.Secret.Length > 0 ? writes : g.Contacts", "selected.TryGetNonEnumeratedCount(out _)", null, null)]
    [DataInlineUnit("active ? writes : g.PrivateContacts", "selected.ToArray()", null, null)]
    [DataInlineUnit("writes", "g.Secret.Length > 0 ? selected.ToArray() : Array.Empty<ContactModel>()", "Personal", "Secret")]
    public void Deferred_Writes_Should_Depend_On_The_Condition_Selecting_The_Enumerated_Query(
        string selection, string use, string? classification, string? property)
    {
        var diagnostics = AnalyzeStandaloneResponseArraySelect($$"""
            public ContactResponse[] Map(UserEntity e, bool active) => e.Groups.Select(g =>
            {
                var value = "empty";
                var writes = g.Contacts.Select(c => { value = "present"; return c; });
                var selected = {{selection}};
                var before = value;
                _ = {{use}};
                return new ContactResponse { Label = value, Display = before };
            }).ToArray();
            """);
        if (classification is null) diagnostics.Should().BeEmpty();
        else diagnostics.Should().ContainSingle().Which.Should().Be(
            ("Label = value", $"Projected property 'Label' requires [{classification}] from mapped property '{property}'."));
    }

    [Theory]
    [DataInlineUnit("g.OptionalContacts?.Select(c => { value = \"present\"; return c; })?.ToArray()", "OptionalContacts")]
    [DataInlineUnit("(g.OptionalContacts?.Select(c => { value = \"present\"; return c; }) ?? Enumerable.Empty<ContactModel>()).ToArray()", "OptionalContacts")]
    [DataInlineUnit("g.PrivateContacts.Where(c => { value = \"present\"; return true; }).ToArray()", "PrivateContacts")]
    [DataInlineUnit("g.PrivateContacts.Any(c => { value = \"present\"; return true; })", "PrivateContacts")]
    [DataInlineUnit("g.Contacts.Zip(g.PrivateContacts, (a, b) => { value = \"present\"; return a; }).ToArray()", "PrivateContacts")]
    [DataInlineUnit("g.Contacts.GroupJoin(g.PrivateContacts, a => a.Name, b => b.Name, (a, group) => { value = \"present\"; return a; }).ToArray()", null)]
    [DataInlineUnit("g.PrivateContacts.Where(c => { value = \"present\"; return true; }).TryGetNonEnumeratedCount(out _)", null)]
    [DataInlineUnit("g.PrivateContacts.Aggregate(0, (n, c) => n + 1, n => { value = \"present\"; return n; })", null)]
    public void Captured_Writes_Should_Require_Classification_From_The_Sequence_Supplying_Callback_Elements(
        string use, string? property)
    {
        var diagnostics = AnalyzeStandaloneResponseArraySelect($$"""
            public ContactResponse[] Map(UserEntity e) => e.Groups.Select(g =>
            {
                var value = "empty";
                _ = {{use}};
                return new ContactResponse { Label = value };
            }).ToArray();
            """);
        if (property is null) diagnostics.Should().BeEmpty();
        else diagnostics.Should().ContainSingle().Which.Should().Be(
            ("Label = value", $"Projected property 'Label' requires [Sensitive] from mapped property '{property}'."));
    }

    [Fact]
    public void Each_Enumeration_Should_Apply_Only_Its_Own_Selection_Condition()
    {
        var diagnostics = AnalyzeStandaloneResponseArraySelect("""
            public ContactResponse[] Map(UserEntity e) => e.Groups.Select(g =>
            {
                var value = "empty";
                var writes = g.Contacts.Select(c => { value = "present"; return c; });
                var first = g.Secret.Length > 0 ? writes : Enumerable.Empty<ContactModel>();
                var second = g.OptionalContacts ?? writes;
                _ = first.ToArray();
                var earlier = value;
                _ = second.ToArray();
                return new ContactResponse { Display = earlier, Label = value };
            }).ToArray();
            """);
        diagnostics.Should().BeEquivalentTo(new[]
        {
            ("Display = earlier", "Projected property 'Display' requires [Personal] from mapped property 'Secret'."),
            ("Label = value", "Projected property 'Label' requires [Personal] from mapped property 'Secret'."),
            ("Label = value", "Projected property 'Label' requires [Sensitive] from mapped property 'OptionalContacts'.")
        });
    }

    [Fact]
    public void Conditional_Return_Constants_Must_Match_On_Every_Path()
    {
        var diagnostics = AnalyzeResponseDiagnostics("""
            public ContactResponse[] OmittedOnOnePath(UserEntity e, bool other) => e.Contacts.Select(c =>
            {
                if (c.IsVip) return new ContactResponse { Label = "vip", Display = "Contact", Name = "User" };
                if (other) return new ContactResponse { Label = "vip", Display = "Contact", Name = "User" };
                return new ContactResponse { Label = "standard", Display = "Contact" };
            }).ToArray();

            public ContactResponse[] NonConstantOnOnePath(UserEntity e) => e.Contacts.Select(c =>
            {
                if (c.IsVip) return new ContactResponse { Display = "Contact", Label = "vip" };
                return new ContactResponse { Display = c.Name, Label = "standard" };
            }).ToArray();

            public ContactResponse[] DifferentConstants(UserEntity e) => e.Contacts.Select(c =>
            {
                if (c.IsVip) return new ContactResponse { Display = "Contact", Name = "User", Label = "vip" };
                return new ContactResponse { Display = "Other", Name = "User", Label = "standard" };
            }).ToArray();

            public ContactResponse[] MatchingConstants(UserEntity e) => e.Contacts.Select(c =>
            {
                if (c.IsVip) return new ContactResponse { Display = "Con" + "tact", Label = "vip" };
                return new ContactResponse { Display = "Contact", Label = "standard" };
            }).ToArray();
            """);

        const string labelFromIsVip = "Projected property 'Label' requires [Personal] from mapped property 'IsVip'.";
        const string displayFromIsVip = "Projected property 'Display' requires [Personal] from mapped property 'IsVip'.";
        const string nameFromIsVip = "Projected property 'Name' requires [Personal] from mapped property 'IsVip'.";
        diagnostics.Select(d => (EnclosingMethod(d), AnalyzerTestCompilation.LocationText(d), d.GetMessage())).Should().BeEquivalentTo(new[]
        {
            ("OmittedOnOnePath", "Label = \"vip\"", labelFromIsVip),
            ("OmittedOnOnePath", "Name = \"User\"", nameFromIsVip),
            ("NonConstantOnOnePath", "Display = \"Contact\"", displayFromIsVip),
            ("NonConstantOnOnePath", "Label = \"vip\"", labelFromIsVip),
            ("DifferentConstants", "Display = \"Contact\"", displayFromIsVip),
            ("DifferentConstants", "Label = \"vip\"", labelFromIsVip),
            ("MatchingConstants", "Label = \"vip\"", labelFromIsVip)
        });
    }

    [Fact]
    public void Side_Effecting_Coalesce_ConditionalAccess_CoalesceAssign_And_Switch_Expressions_Should_Carry_Guards()
    {
        var diagnostics = AnalyzeStandaloneResponseArraySelect("""
            public ContactResponse[] CoalesceGuard(UserEntity e) => e.Contacts.Select(c =>
            {
                var fromCoalesce = "ok";
                _ = ((string?)c.Email) ?? (fromCoalesce = "missing");
                var pureCoalesce = "ok";
                _ = ((string?)c.Name) ?? "fallback";
                return new ContactResponse { Label = fromCoalesce, Name = pureCoalesce };
            }).ToArray();

            public ContactResponse[] ConditionalAccessGuard(UserEntity e) => e.Contacts.Select(c =>
            {
                var fromConditionalAccess = "ok";
                _ = ((string?)c.Email)?.Replace("a", fromConditionalAccess = "present");
                return new ContactResponse { Display = fromConditionalAccess };
            }).ToArray();

            public ContactResponse[] CoalesceAssignGuard(UserEntity e) => e.Contacts.Select(c =>
            {
                var fromCoalesceAssign = "ok";
                string? probe = c.Email;
                probe ??= (fromCoalesceAssign = "missing");
                return new ContactResponse { Label = fromCoalesceAssign };
            }).ToArray();

            public ContactResponse[] SwitchExpressionGuards(UserEntity e, bool flag) => e.Contacts.Select(c =>
            {
                var fromSwitchValue = "ok";
                _ = c.Email switch { "vip" => fromSwitchValue = "vip", _ => "" };

                var fromSwitchGuard = "ok";
                _ = flag switch { true when c.IsVip => "", _ => fromSwitchGuard = "std" };

                return new ContactResponse
                {
                    Display = fromSwitchValue,
                    Name = fromSwitchGuard
                };
            }).ToArray();
            """);

        diagnostics.Should().BeEquivalentTo(new[]
        {
            ("Label = fromCoalesce", "Projected property 'Label' requires [Personal] from mapped property 'Email'."),
            ("Display = fromConditionalAccess", "Projected property 'Display' requires [Personal] from mapped property 'Email'."),
            ("Label = fromCoalesceAssign", "Projected property 'Label' requires [Personal] from mapped property 'Email'."),
            ("Display = fromSwitchValue", "Projected property 'Display' requires [Personal] from mapped property 'Email'."),
            ("Name = fromSwitchGuard", "Projected property 'Name' requires [Personal] from mapped property 'IsVip'.")
        });
    }

    [Fact]
    public void Do_While_Loop_Return_Conditions_Should_Report_Only_When_Later_Iteration_Can_Reach_Return()
    {
        var diagnostics = AnalyzeResponseDiagnostics("""
            public UserResponse LaterIterationReturn(UserEntity e) => new()
            {
                Flags = e.Contacts.Select(c =>
                {
                    var step = 0;
                    do
                    {
                        if (step++ > 0) return true;
                    } while (c.IsVip);
                    return false;
                }).ToArray()
            };

            public UserResponse GuaranteedFirstIterationReturn(UserEntity e) => new()
            {
                Flags = e.Contacts.Select(c =>
                {
                    do
                    {
                        return false;
                    } while (c.IsVip);
                }).ToArray()
            };

            public UserResponse NoReturnInLoop(UserEntity e) => new()
            {
                Flags = e.Contacts.Select(c =>
                {
                    var steps = 0;
                    do { steps++; } while (steps < 2 && c.IsVip);
                    return false;
                }).ToArray()
            };
            """);

        var diagnostic = diagnostics.Should().ContainSingle().Which;
        EnclosingMethod(diagnostic).Should().Be("LaterIterationReturn");
        AnalyzerTestCompilation.LocationText(diagnostic).Should().Contain("step++ > 0");
        diagnostic.GetMessage().Should().Be("Projected property 'Flags' requires [Personal] from mapped property 'IsVip'.");
    }

    [Theory]
    [DataInlineUnit("e.Contacts", 0)]
    [DataInlineUnit("contacts", 1)]
    public void Capture_Free_Select_Should_Only_Collect_The_Enclosing_Body_For_A_Local_Source(
        string source, int expectedBodyCount)
    {
        var (contract, domain) = SelectorFlowReferences.Value;
        var compilation = Compilation($$"""
            using System.Linq;
            using Sample.Contract.QA.Users;
            using Sample.Domain.QA.User;
            namespace Sample.Infra.QA.Users;
            public class Projection
            {
                public ContactResponse[] Map(UserEntity e)
                {
                    var contacts = e.Contacts;
                    return {{source}}.Select(static c => new ContactResponse { Label = c.Email }).ToArray();
                }
            }
            """).WithAssemblyName("Sample.Infra").AddReferences(contract, domain);
        compilation.AssertNoCompilerErrors();

        var tree = compilation.SyntaxTrees.Single();
        var invocation = tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>()
            .Single(call => call.Expression is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Select" });
        var select = (IInvocationOperation)compilation.GetSemanticModel(tree).GetOperation(invocation)!;
        var options = AnalyzerTestCompilation.ConventionOptionsProvider("Sample.Infra");
        var facts = new ConventionFacts(compilation, ConventionProject.Read(compilation, options.GlobalOptions),
            new([], []), options, CancellationToken.None);
        var bodyFlows = new Dictionary<IBlockOperation, SelectorFlowResult>();
        SelectorFlowCollector.ResolveEnclosingBodyScope(select, facts, bodyFlows);
        bodyFlows.Should().HaveCount(expectedBodyCount);

        Analyze(compilation, "Infra").Should().ContainSingle(d => d.Id == "DRNDTO003")
            .Which.GetMessage().Should().Be("Projected property 'Label' requires [Personal] from mapped property 'Email'.");
    }

    [Fact]
    public void Captured_Method_Locals_Should_Bind_At_Sequence_Enumeration_Time()
    {
        var diagnostics = AnalyzeStandaloneResponseArraySelect("""
            public ContactResponse[] MutatedBeforeEnumeration(UserEntity e)
            {
                var shared = "public";
                var query = e.Contacts.Select(_ => new ContactResponse { Label = shared });
                shared = e.Primary.Email;
                return query.ToArray();
            }

            public ContactResponse[] ClearedBeforeEnumeration(UserEntity e)
            {
                var shared = e.Primary.Email;
                var query = e.Contacts.Select(_ => new ContactResponse { Display = shared });
                shared = "public";
                return query.ToArray();
            }

            public ContactResponse[] MutatedAfterEnumeration(UserEntity e)
            {
                var shared = "public";
                var rows = e.Contacts.Select(_ => new ContactResponse { Name = shared }).ToArray();
                shared = e.Primary.Email;
                return rows;
            }

            public ContactResponse[] DiscardedFirstResult(UserEntity e)
            {
                var captured = e.Primary.Email;
                var query = e.Contacts.Select(_ => new ContactResponse { Name = captured });
                _ = query.ToArray();
                captured = "public";
                return query.ToArray();
            }

            public ContactResponse[] UncapturedMethodLocalUnchanged(UserEntity e)
            {
                var uncaptured = e.Primary.Email;
                return [new ContactResponse { Label = uncaptured }];
            }
            """);

        diagnostics.Should().BeEquivalentTo(new[]
        {
            ("Label = shared", "Projected property 'Label' requires [Personal] from mapped property 'Email'."),
            ("Name = captured", "Projected property 'Name' requires [Personal] from mapped property 'Email'.")
        });
    }

    [Fact]
    public void Materialized_Child_Selector_And_Sibling_Linq_Lambda_Writes_Should_Propagate_At_Materialization()
    {
        var diagnostics = AnalyzeStandaloneResponseArraySelect("""
            public ContactResponse[] ChildSelectorMutatesOuterLocal(UserEntity e) => e.Groups.Select(g =>
            {
                var leaked = "public";
                var query = g.Contacts.Select(c =>
                {
                    var temp = c.Email;
                    leaked = temp;
                    return new ContactResponse { Name = c.Name };
                });
                var before = leaked;
                _ = query.ToArray();
                return new ContactResponse { Display = before, Label = leaked };
            }).ToArray();

            public ContactResponse[] ChildSelectorOverwriteOnPossiblyEmptySequence(UserEntity e) => e.Groups.Select(g =>
            {
                var maybeKept = g.Secret;
                _ = g.Contacts.Select(c =>
                {
                    maybeKept = "public";
                    return new ContactResponse { Name = c.Name };
                }).ToArray();
                return new ContactResponse { Label = maybeKept };
            }).ToArray();

            public UserResponse SiblingWhereAfterSelect(UserEntity e)
            {
                var afterLabel = "public";
                var query = e.Contacts.Select(_ => new ContactResponse { Label = afterLabel });
                var rows = query.Where(_ => { afterLabel = e.Primary.Email; return true; }).ToArray();
                return new UserResponse { AnnotatedContacts = rows };
            }

            public UserResponse SiblingWhereBeforeSelect(UserEntity e)
            {
                var beforeLabel = "public";
                var rows = e.Contacts
                    .Where(_ => { beforeLabel = e.Primary.Email; return true; })
                    .Select(_ => new ContactResponse { Display = beforeLabel })
                    .ToArray();
                return new UserResponse { AnnotatedContacts = rows };
            }

            public UserResponse MultiHopOuterLocalLag(UserEntity e) => new()
            {
                Groups = e.Groups.Select(g =>
                {
                    var first = "public";
                    var second = "public";
                    return new GroupResponse
                    {
                        Contacts = g.Contacts.Select(c =>
                        {
                            var lagged = first;
                            first = second;
                            second = c.Email;
                            return new ContactResponse { Name = lagged };
                        }).ToArray()
                    };
                }).ToArray()
            };
            """);

        diagnostics.Should().BeEquivalentTo(new[]
        {
            ("Label = leaked", "Projected property 'Label' requires [Personal] from mapped property 'Email'."),
            ("Label = maybeKept", "Projected property 'Label' requires [Personal] from mapped property 'Secret'."),
            ("Label = afterLabel", "Projected property 'Label' requires [Personal] from mapped property 'Email'."),
            ("Display = beforeLabel", "Projected property 'Display' requires [Personal] from mapped property 'Email'."),
            ("Name = lagged", "Projected property 'Name' requires [Personal] from mapped property 'Email'.")
        });
    }

    [Fact]
    public void Returned_Response_Local_Auto_Property_Overwrite_Should_Supersede_Object_Initializer()
    {
        var diagnostics = AnalyzeResponseDiagnostics("""
            public ContactResponse[] StraightLineOverwrite(UserEntity e) => e.Contacts.Select(c =>
            {
                var row = new ContactResponse { Label = c.Email, Display = c.Email };
                row.Label = "public";
                return row;
            }).ToArray();

            public ContactResponse[] ConditionalOverwriteKeepsInitializer(UserEntity e, bool clear) => e.Contacts.Select(c =>
            {
                var row = new ContactResponse { Label = c.Email };
                if (clear) row.Label = "public";
                return row;
            }).ToArray();
            """);

        diagnostics.Select(d => (EnclosingMethod(d), AnalyzerTestCompilation.LocationText(d), d.GetMessage())).Should().BeEquivalentTo(new[]
        {
            ("StraightLineOverwrite", "Display = c.Email", "Projected property 'Display' requires [Personal] from mapped property 'Email'."),
            ("ConditionalOverwriteKeepsInitializer", "Label = c.Email", "Projected property 'Label' requires [Personal] from mapped property 'Email'.")
        });
    }

    [Fact]
    public void Mixed_Use_Query_Should_Preserve_Known_Materialization_Outer_Writes_Alongside_Escape()
    {
        var diagnostics = AnalyzeStandaloneResponseArraySelect("""
            public ContactResponse[] MaterializedAndEscaped(UserEntity e) => e.Groups.Select(g =>
            {
                var leaked = "public";
                var query = g.Contacts.Select(c =>
                {
                    var temp = c.Email;
                    leaked = temp;
                    return new ContactResponse { Name = c.Name };
                });
                var before = leaked;
                _ = query.ToArray();
                GC.KeepAlive(query);
                return new ContactResponse { Display = before, Label = leaked };
            }).ToArray();
            """);

        diagnostics.Should().BeEquivalentTo(new[]
        {
            ("Label = leaked", "Projected property 'Label' requires [Personal] from mapped property 'Email'.")
        });
    }

    [Fact]
    public void Failed_Switch_Expression_Guard_Writes_Should_Flow_Into_Later_Arms_Without_Leaking_Arm_Values()
    {
        var diagnostics = AnalyzeStandaloneResponseArraySelect("""
            public ContactResponse[] FailedGuardFlowsToNextArm(UserEntity e) => e.Contacts.Select(c =>
            {
                var saved = "public";
                return c.Name switch
                {
                    _ when (saved = c.Email).Length < 0 => new ContactResponse { Label = "first" },
                    _ => new ContactResponse { Label = saved }
                };
            }).ToArray();

            public ContactResponse[] PatternGatedFailedGuardFlowsToNextArm(UserEntity e) => e.Contacts.Select(c =>
            {
                var gated = "public";
                return c.Email switch
                {
                    "vip" when (gated = "hit").Length < 0 => new ContactResponse { Display = "first" },
                    _ => new ContactResponse { Display = gated }
                };
            }).ToArray();

            public ContactResponse[] EarlierArmValueWriteDoesNotLeakToLaterArm(UserEntity e, bool flag) => e.Contacts.Select(c =>
            {
                var isolated = "public";
                return flag switch
                {
                    true => new ContactResponse { Email = (isolated = c.Email) },
                    false => new ContactResponse { Name = isolated }
                };
            }).ToArray();
            """);

        diagnostics.Should().BeEquivalentTo(new[]
        {
            ("Label = saved", "Projected property 'Label' requires [Personal] from mapped property 'Email'."),
            ("Display = gated", "Projected property 'Display' requires [Personal] from mapped property 'Email'.")
        });
    }

    [Fact]
    public void Deeply_Nested_Materialized_Selectors_Writing_Parent_Locals_Should_Remain_Bounded_And_Accurate()
    {
        var diagnostics = AnalyzeStandaloneResponseArraySelect("""
            public ContactResponse[] DeepNestedMaterializations(UserEntity e) => e.Groups.Select(g1 =>
            {
                var x1 = "public";
                _ = g1.Contacts.Select(c2 =>
                {
                    x1 = c2.Email;
                    var x2 = "public";
                    _ = g1.Contacts.Select(c3 =>
                    {
                        x2 = c3.Name;
                        var x3 = "public";
                        _ = g1.Contacts.Select(c4 =>
                        {
                            x3 = c4.Name;
                            var x4 = "public";
                            _ = g1.Contacts.Select(c5 =>
                            {
                                x4 = c5.Name;
                                var x5 = "public";
                                _ = g1.Contacts.Select(c6 =>
                                {
                                    x5 = c6.Name;
                                    var x6 = "public";
                                    _ = g1.Contacts.Select(c7 =>
                                    {
                                        x6 = c7.Name;
                                        var x7 = "public";
                                        _ = g1.Contacts.Select(c8 =>
                                        {
                                            x7 = c8.Name;
                                            var x8 = "public";
                                            _ = g1.Contacts.Select(c9 =>
                                            {
                                                x8 = c9.Name;
                                                return c9.Name;
                                            }).ToArray();
                                            return x8;
                                        }).ToArray();
                                        return x7;
                                    }).ToArray();
                                    return x6;
                                }).ToArray();
                                return x5;
                            }).ToArray();
                            return x4;
                        }).ToArray();
                        return x3;
                    }).ToArray();
                    return x2;
                }).ToArray();
                return new ContactResponse { Label = x1 };
            }).ToArray();
            """);

        diagnostics.Should().BeEquivalentTo(new[]
        {
            ("Label = x1", "Projected property 'Label' requires [Personal] from mapped property 'Email'.")
        });
    }

    [Fact]
    public void Conditional_Constructor_Targets_Should_Reuse_Common_Constant_Initializer_Guards()
    {
        var (_, domainRef) = SelectorFlowReferences.Value;
        var contractRef = EmitProjectReference("Sample.Contract", "/ctor-common-constant/Sample.Contract.dll", """
            namespace Sample.Contract.QA.Users
            {
                public record PositionalContactResponse(string Name, string Label);
            }
            """);
        var infra = Compilation("""
            using System.Linq;
            using Sample.Contract.QA.Users;
            using Sample.Domain.QA.User;
            namespace Sample.Infra.QA.Users;
            public class Projection
            {
                public PositionalContactResponse[] OverwrittenByCommonInitializer(UserEntity e) => e.Contacts.Select(c =>
                    c.IsVip
                        ? new PositionalContactResponse("a", "vip") { Name = "Contact" }
                        : new PositionalContactResponse("b", "std") { Name = "Contact" }).ToArray();

                public PositionalContactResponse[] PreservesEnclosingGuard(UserEntity e) => e.Groups.Select(g =>
                    g.PrivateContacts.Count > 0
                        ? (e.Primary.IsVip
                            ? new PositionalContactResponse("a", "vip") { Name = "Contact" }
                            : new PositionalContactResponse("b", "std") { Name = "Contact" })
                        : new PositionalContactResponse("c", "other") { Name = "Other" }).ToArray();

                public PositionalContactResponse[] RetainsGuardWhenFinalValueCanVary(UserEntity e) => e.Contacts.Select(c =>
                    c.IsVip
                        ? new PositionalContactResponse("a", "vip") { Name = "One" }
                        : new PositionalContactResponse("b", "std") { Name = "Two" }).ToArray();
            }
            """).WithAssemblyName("Sample.Infra").AddReferences(contractRef, domainRef);
        infra.AssertNoCompilerErrors();

        var diagnostics = Analyze(infra, "Infra").Where(d => d.Id == "DRNDTO003").ToArray();
        diagnostics.Select(d => (EnclosingMethod(d), d.GetMessage())).Should().BeEquivalentTo(new[]
        {
            ("OverwrittenByCommonInitializer", "Projected property 'Label' requires [Personal] from mapped property 'IsVip'."),
            ("PreservesEnclosingGuard", "Projected property 'Name' requires [Sensitive] from mapped property 'PrivateContacts'."),
            ("PreservesEnclosingGuard", "Projected property 'Label' requires [Sensitive] from mapped property 'PrivateContacts'."),
            ("PreservesEnclosingGuard", "Projected property 'Label' requires [Personal] from mapped property 'IsVip'."),
            ("RetainsGuardWhenFinalValueCanVary", "Projected property 'Name' requires [Personal] from mapped property 'IsVip'."),
            ("RetainsGuardWhenFinalValueCanVary", "Projected property 'Label' requires [Personal] from mapped property 'IsVip'.")
        });
    }

    [Fact]
    public void Conditional_Response_Should_Skip_Enclosing_Target_Guard_Only_When_Branches_Are_Equivalent()
    {
        var diagnostics = AnalyzeResponseDiagnostics("""
            public UserResponse EquivalentBranches(UserEntity e) => new()
            {
                Contacts = e.Contacts.Select(c =>
                    c.IsVip
                        ? new ContactResponse { Name = "Contact", Label = "same" }
                        : new ContactResponse { Label = "same", Name = "Con" + "tact" }).ToArray()
            };

            public UserResponse EquivalentBranchesKeepSequenceSource(UserEntity e) => new()
            {
                Contacts = e.Groups[0].PrivateContacts.Select(c =>
                    c.IsVip
                        ? new ContactResponse { Name = "Contact" }
                        : new ContactResponse { Name = "Contact" }).ToArray()
            };

            public UserResponse ChangingElementRetainsGuard(UserEntity e) => new()
            {
                Contacts = e.Contacts.Select(c =>
                    c.IsVip
                        ? new ContactResponse { Email = "vip" }
                        : new ContactResponse { Email = "std" }).ToArray()
            };

            public UserResponse NullableBranchRetainsGuard(UserEntity e) => new()
            {
                Contacts = e.Contacts.Select(c =>
                    c.IsVip
                        ? new ContactResponse { Email = "Contact" }
                        : null!).ToArray()
            };
            """);

        diagnostics.Select(d => (EnclosingMethod(d), d.GetMessage())).Should().BeEquivalentTo(new[]
        {
            ("EquivalentBranchesKeepSequenceSource", "Projected property 'Contacts' requires [Sensitive] from mapped property 'PrivateContacts'."),
            ("ChangingElementRetainsGuard", "Projected property 'Contacts' requires [Personal] from mapped property 'IsVip'."),
            ("NullableBranchRetainsGuard", "Projected property 'Contacts' requires [Personal] from mapped property 'IsVip'.")
        });
    }

    [Fact]
    public void Foreach_Select_Callback_That_Can_Throw_Should_Retain_Loop_And_Return_Guards()
    {
        var diagnostics = AnalyzeResponseDiagnostics("""
            public ContactResponse[] ThrowingSelectLoopGuard(UserEntity e) => e.Contacts.Select(c =>
            {
                var label = "default";
                foreach (var item in new[] { 1 }.Select(_ =>
                         {
                             if (c.IsVip) throw new InvalidOperationException();
                             return 1;
                         }))
                {
                    label = "reached";
                }
                return new ContactResponse { Label = label };
            }).ToArray();

            public UserResponse ThrowingSelectReturnCondition(UserEntity e) => new()
            {
                Flags = e.Contacts.Select(c =>
                {
                    foreach (var item in new[] { 1 }.Select(_ => c.IsVip ? throw new InvalidOperationException() : 1))
                        return true;
                    return false;
                }).ToArray()
            };

            public ContactResponse[] FixedLiteralWithoutThrowOmitted(UserEntity e) => e.Contacts.Select(c =>
            {
                var label = "default";
                var display = "default";
                foreach (var item in new[] { c.Email }.Select(_ => "ok"))
                    label = "reached";
                foreach (var item in new[] { c.Email })
                    display = "reached";
                return new ContactResponse { Label = label, Display = display };
            }).ToArray();
            """);

        diagnostics.Select(d => (EnclosingMethod(d), d.GetMessage())).Should().BeEquivalentTo(new[]
        {
            ("ThrowingSelectLoopGuard", "Projected property 'Label' requires [Personal] from mapped property 'IsVip'."),
            ("ThrowingSelectReturnCondition", "Projected property 'Flags' requires [Personal] from mapped property 'IsVip'.")
        });
    }
}
