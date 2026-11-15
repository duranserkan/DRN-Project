using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace DRN.Framework.SharedKernel.Analyzers.Conventions;

internal sealed partial class ConventionFacts
{
    private void PrepareInputs()
    {
        var roleKeys = new HashSet<(string Role, string Namespace, string Path, string Reason)>();
        foreach (var input in Inputs.Records)
        {
            Cancellation.ThrowIfCancellationRequested();
            if (input.Fields.Keys.Any(field => !IsField(input.Kind, field)))
            {
                InvalidInput(input, "declared with unknown fields", "record using documented fields for " + input.Kind);
                continue;
            }
            Records.Add(input);
            switch (input.Kind)
            {
                case "Reference":
                    if (input.Get("Stage") is not ("Project" or "Package" or "Resolved" or "Complete") ||
                        input.Get("Stage") == "Complete" && input.Identity != "Inventory")
                        InvalidInput(input, "declared with unknown provenance", "Project, Package, Resolved or complete Inventory record");
                    break;
                case "Path":
                    var path = ConventionProject.NormalizeSeparators(input.Identity);
                    if (!ConventionProject.IsLogicalPath(input.Get("LogicalPath")))
                    {
                        InvalidInput(input, "declared with an invalid logical path", "project-relative Compile or Link path");
                        _invalidPaths.Add(path);
                        break;
                    }
                    AddUnique(_paths, _invalidPaths, path, input);
                    break;
                case "Role":
                    if (!ConventionProject.IsRole(input.Identity) || !ConventionProject.IsNamespace(input.Get("NamespacePrefix")) ||
                        !ConventionProject.IsLogicalPath(input.Get("LogicalPathPrefix")))
                    {
                        InvalidInput(input, "declared with an invalid role or prefix", "role with namespace and logical-path prefixes");
                        _invalidRoles.Add(input);
                        break;
                    }
                    if (roleKeys.Add((input.Identity, input.Get("NamespacePrefix"),
                            input.Get("LogicalPathPrefix"), input.Get("Reason")))) _roles.Add(input);
                    break;
                case "Ownership":
                    var type = Compilation.GetTypeByMetadataName(input.Identity);
                    if (type is null || !SyntaxFacts.IsValidIdentifier(input.Get("Subdomain")) ||
                        input.Get("Kind") is not ("" or "Entity" or "Value") ||
                        input.Get("Kind") == "Value" && Symbols.IsPersistenceType(type, Cancellation))
                    {
                        InvalidInput(input, "declared with an invalid symbol, subdomain or classification", "exact typed ownership mapping");
                        _invalidOwnershipNames.Add(input.Identity);
                        break;
                    }
                    AddUnique(_ownership, _invalidOwnershipNames, input.Identity, input);
                    break;
                case "Context":
                    var context = Compilation.GetTypeByMetadataName(input.Identity);
                    if (context is null || !Symbols.IsContext(context, Cancellation))
                    {
                        InvalidInput(input, "not identifying a known persistence context", "exact context metadata name");
                        _invalidContextNames.Add(input.Identity);
                        break;
                    }
                    AddUnique(_contexts, _invalidContextNames, input.Identity, input);
                    break;
            }
        }

        FindRoleOverlaps("NamespacePrefix", '.');
        FindRoleOverlaps("LogicalPathPrefix", '/');
        _roles.RemoveAll(role => _invalidRoles.Contains(role));

        foreach (var attribute in Compilation.Assembly.GetAttributes())
        {
            Cancellation.ThrowIfCancellationRequested();
            if (!ConventionSymbols.Same(attribute.AttributeClass, Symbols.OwnershipAttribute)) continue;
            if (attribute.AttributeConstructor is null || attribute.ConstructorArguments.Any(argument => argument.Kind == TypedConstantKind.Error)) continue;
            var location = attribute.ApplicationSyntaxReference?.GetSyntax(Cancellation).GetLocation() ?? Microsoft.CodeAnalysis.Location.None;
            if (attribute.ConstructorArguments is not [{ Value: INamedTypeSymbol external }, { Value: string owner }] ||
                IsLocal(external) || !SyntaxFacts.IsValidIdentifier(owner) || string.IsNullOrWhiteSpace(Reason(attribute)))
            {
                Diagnostics.Add(ConventionProject.InputIssue("assembly ownership", "malformed", "reasoned exact external type owner", location));
                continue;
            }
            if (_externalOwners.TryGetValue(external, out var previous) && previous.ConstructorArguments[1].Value as string != owner)
            {
                InvalidOwner(external, ConventionSymbols.MetadataName(external), "contradictory external ownership", location);
                continue;
            }
            _externalOwners[external] = attribute;
        }
    }

    private void FindRoleOverlaps(string field, char separator)
    {
        var groups = _roles.GroupBy(input => ConventionProject.NormalizeSeparators(input.Get(field)), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        var overlaps = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (prefix, inputs) in groups)
        {
            Cancellation.ThrowIfCancellationRequested();
            if (inputs.Length > 1) overlaps.Add(prefix);
            for (var end = prefix.LastIndexOf(separator); end >= 0; end = end == 0 ? -1 : prefix.LastIndexOf(separator, end - 1))
            {
                Cancellation.ThrowIfCancellationRequested();
                var ancestor = prefix[..end];
                if (!groups.ContainsKey(ancestor)) continue;
                overlaps.Add(prefix);
                overlaps.Add(ancestor);
            }
        }
        foreach (var prefix in overlaps)
        foreach (var input in groups[prefix])
            if (_invalidRoles.Add(input))
                InvalidInput(input, "overlapping another role mapping", "set of nonoverlapping namespace and logical-path prefixes");
    }

    private void AddUnique(Dictionary<string, ConventionInput> index, HashSet<string> invalid, string key, ConventionInput input)
    {
        if (invalid.Contains(key)) return;
        if (index.TryGetValue(key, out var previous) && !SameInput(previous, input))
        {
            InvalidInput(input, "contradictory", "consistent declaration for the target");
            index.Remove(key);
            invalid.Add(key);
            return;
        }
        index[key] = input;
    }

    private static bool SameInput(ConventionInput left, ConventionInput right) =>
        left.Kind == right.Kind && left.Identity == right.Identity && left.Fields.Count == right.Fields.Count &&
        left.Fields.All(field => right.Fields.TryGetValue(field.Key, out var value) && value == field.Value);

    private void InvalidInput(ConventionInput input, string problem, string remedy) =>
        Diagnostics.Add(ConventionProject.InputIssue(input.Kind == "Path" ? "Path:Compile/Link" : input.Kind + ":" + input.Identity,
            problem, remedy, input.Location));

    private static bool IsField(string kind, string field) => kind switch
    {
        "Path" => field == "LogicalPath",
        "Role" => field is "NamespacePrefix" or "LogicalPathPrefix" or "Reason",
        "Context" => field is "Subdomain" or "Repositories" or "Migrations" or "MigrationsAssembly" or "Factory" or "Provider" or "Reason",
        "Ownership" => field is "Subdomain" or "Kind" or "Reason",
        "Exception" => field is "Target" or "Entity" or "Context" or "Reason" or "Owner" or "Review" or "Fingerprint",
        "Reference" => field is "Stage" or "Source" or "Project" or "OriginalProject" or "Package",
        "Registration" => field is "Service" or "Key" or "Reason",
        "Configuration" => field is "Context" or "Reason",
        _ => false
    };
}
