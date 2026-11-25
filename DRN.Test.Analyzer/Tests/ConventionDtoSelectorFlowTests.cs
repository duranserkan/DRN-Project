using static DRN.Test.Analyzer.Tests.ClassificationTestFixture;

namespace DRN.Test.Analyzer.Tests;

public sealed class ConventionDtoSelectorFlowTests
{
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
        diagnostics.Should().HaveCount(3);
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
        diagnostics.Should().HaveCount(4);
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
        diagnostics.Select(d => (AnalyzerTestCompilation.LocationText(d), d.GetMessage())).Should().BeEquivalentTo(
            new[]
            {
                ("Display = updated.Summary", "Projected property 'Display' requires [Personal] from mapped property 'Email'."),
                ("Display = chosen.Name", "Projected property 'Display' requires [Personal] from mapped property 'Email'."),
                ("Display = matchedContact.Name", "Projected property 'Display' requires [Personal] from mapped property 'IsVip'."),
                ("Flags = entity.Contacts.Select(c => { switch (c) { case { IsVip: true }: return true; default: return false; } }).ToArray()", "Projected property 'Flags' requires [Personal] from mapped property 'IsVip'."),
                ("Label = shared", "Projected property 'Label' requires [Personal] from mapped property 'Email'."),
                ("Label = latest", "Projected property 'Label' requires [Personal] from mapped property 'Email'."),
                ("Display = email", "Projected property 'Display' requires [Personal] from mapped property 'Email'."),
                ("Label = profile.Email", "Projected property 'Label' requires [Personal] from mapped property 'Email'."),
                ("Display = matched", "Projected property 'Display' requires [Personal] from mapped property 'Email'."),
                ("Label = extracted", "Projected property 'Label' requires [Personal] from mapped property 'Email'."),
                ("Flags = entity.Contacts.Select(c => { if (c.IsVip) return true; return false; }).ToArray()", "Projected property 'Flags' requires [Personal] from mapped property 'IsVip'."),
                ("Label = deferred", "Projected property 'Label' requires [Personal] from mapped property 'Secret'."),
                ("item.MutableTags.Add(c.Email)", "Projected property 'MutableTags' requires [Personal] from mapped property 'Email'."),
                ("Display = tupleEmail", "Projected property 'Display' requires [Personal] from mapped property 'Email'."),
                ("Tags = tags", "Projected property 'Tags' requires [Personal] from mapped property 'Email'."),
                ("item.Label = c.Email", "Projected property 'Label' requires [Personal] from mapped property 'Email'."),
                ("Display = backed.Mirror", "Projected property 'Display' requires [Personal] from mapped property 'Email'."),
                ("Label = c.Email", "Projected property 'Label' requires [Personal] from mapped property 'Email'."),
                ("Display = c.Email", "Projected property 'Display' requires [Personal] from mapped property 'Email'."),
                ("Display = node.Email", "Projected property 'Display' requires [Personal] from mapped property 'Email'."),
                ("Label = copy", "Projected property 'Label' requires [Personal] from mapped property 'Secret'."),
                ("Display = maybeCleared", "Projected property 'Display' requires [Personal] from mapped property 'Email'."),
                ("Label = reused", "Projected property 'Label' requires [Personal] from mapped property 'Email'."),
                ("Flags = entity.Contacts.Select(c => { var isVip = false; if (c.IsVip) isVip = true; return isVip; }).ToArray()", "Projected property 'Flags' requires [Personal] from mapped property 'IsVip'."),
                ("Label = builder.ToString()", "Projected property 'Label' requires [Personal] from mapped property 'Email'."),
                ("Tags = seen.ToArray()", "Projected property 'Tags' requires [Personal] from mapped property 'Email'."),
                ("Flags = entity.Contacts.Select(c => { var vip = false; switch (c.IsVip) { case true: vip = true; break; } return vip; }).ToArray()", "Projected property 'Flags' requires [Personal] from mapped property 'IsVip'."),
                ("Flags = entity.Contacts.Select(c => { var vip = true; switch (c) { case var _ when c.IsVip: break; default: vip = false; break; } return vip; }).ToArray()", "Projected property 'Flags' requires [Personal] from mapped property 'IsVip'."),
                ("Label = repeated", "Projected property 'Label' requires [Personal] from mapped property 'Secret'."),
                ("Display = before", "Projected property 'Display' requires [Personal] from mapped property 'Email'."),
                ("Label = earlier", "Projected property 'Label' requires [Personal] from mapped property 'Email'."),
                ("rewritten.MutableTags.Add(c.Email)", "Projected property 'MutableTags' requires [Personal] from mapped property 'Email'."),
                ("conditional.Label = c.Email", "Projected property 'Label' requires [Personal] from mapped property 'Email'.")
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

    private static (string, string)[] AnalyzeStandaloneResponseArraySelect(string members)
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
        return Analyze(infra, "Infra")
            .Where(d => d.Id == "DRNDTO003")
            .Select(d => (AnalyzerTestCompilation.LocationText(d), d.GetMessage()))
            .ToArray();
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
        var diagnostics = AnalyzeStandaloneResponseArraySelect("""
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

        diagnostics.Should().HaveCount(3);
        diagnostics.Should().OnlyContain(item =>
            item.Item1 == "row.Label = c.Email" &&
            item.Item2 == "Projected property 'Label' requires [Personal] from mapped property 'Email'.");
    }
}
