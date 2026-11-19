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
            if (input.Fields.Keys.Any(field => !ConventionInputSyntax.IsField(input.Kind, field)))
            {
                InvalidInput(input, "declared with unknown fields", "record using documented fields for " + input.Kind);
                continue;
            }
            Records.Add(input);
            ProcessInputRecord(input, roleKeys);
        }

        FindRoleOverlaps(ConventionInputSyntax.NamespacePrefixField, '.');
        FindRoleOverlaps(ConventionInputSyntax.LogicalPathPrefixField, '/');
        _roles.RemoveAll(role => _invalidRoles.Contains(role));

        PrepareAssemblyOwnershipAttributes();
    }

    private void ProcessInputRecord(ConventionInput input,
        HashSet<(string Role, string Namespace, string Path, string Reason)> roleKeys)
    {
        switch (input.Kind)
        {
            case ConventionInputSyntax.ReferenceKind:
                ValidateReferenceInput(input);
                break;
            case ConventionInputSyntax.PathKind:
                ProcessPathInput(input);
                break;
            case ConventionInputSyntax.RoleKind:
                ProcessRoleInput(input, roleKeys);
                break;
            case ConventionInputSyntax.OwnershipKind:
                ProcessOwnershipInput(input);
                break;
            case ConventionInputSyntax.ContextKind:
                ProcessContextInput(input);
                break;
        }
    }

    private void ValidateReferenceInput(ConventionInput input)
    {
        var stage = input.Get(ConventionInputSyntax.StageField);
        if (stage is not (ConventionInputSyntax.ProjectField or ConventionInputSyntax.PackageField or
                "Resolved" or ConventionInputSyntax.CompleteStage) ||
            stage == ConventionInputSyntax.CompleteStage && input.Identity != ConventionInputSyntax.InventoryIdentity)
            InvalidInput(input, "declared with unknown provenance", "Project, Package, Resolved or complete Inventory record");
    }

    private void ProcessPathInput(ConventionInput input)
    {
        var path = ConventionProject.NormalizeSeparators(input.Identity);
        if (!ConventionProject.IsLogicalPath(input.Get(ConventionInputSyntax.LogicalPathField)))
        {
            InvalidInput(input, "declared with an invalid logical path", "project-relative Compile or Link path");
            _invalidPaths.Add(path);
            return;
        }
        AddUnique(_paths, _invalidPaths, path, input);
    }

    private void ProcessRoleInput(ConventionInput input,
        HashSet<(string Role, string Namespace, string Path, string Reason)> roleKeys)
    {
        var namespacePrefix = input.Get(ConventionInputSyntax.NamespacePrefixField);
        var logicalPathPrefix = input.Get(ConventionInputSyntax.LogicalPathPrefixField);
        if (!ConventionProject.IsRole(input.Identity) || !ConventionProject.IsNamespace(namespacePrefix) ||
            !ConventionProject.IsLogicalPath(logicalPathPrefix))
        {
            InvalidInput(input, "declared with an invalid role or prefix", "role with namespace and logical-path prefixes");
            _invalidRoles.Add(input);
            return;
        }
        if (roleKeys.Add((input.Identity, namespacePrefix, logicalPathPrefix, input.Get(ConventionInputSyntax.ReasonField))))
            _roles.Add(input);
    }

    private void ProcessOwnershipInput(ConventionInput input)
    {
        var type = Compilation.GetTypeByMetadataName(input.Identity);
        var kind = input.Get(ConventionInputSyntax.KindField);
        if (type is null || !SyntaxFacts.IsValidIdentifier(input.Get(ConventionInputSyntax.SubdomainField)) ||
            kind is not ("" or ConventionInputSyntax.EntityField or "Value") ||
            kind == "Value" && Symbols.IsPersistenceType(type, Cancellation))
        {
            InvalidInput(input, "declared with an invalid symbol, subdomain or classification", "exact typed ownership mapping");
            _invalidOwnershipNames.Add(input.Identity);
            return;
        }
        AddUnique(_ownership, _invalidOwnershipNames, input.Identity, input);
    }

    private void ProcessContextInput(ConventionInput input)
    {
        var context = Compilation.GetTypeByMetadataName(input.Identity);
        if (context is null || !Symbols.IsContext(context, Cancellation))
        {
            InvalidInput(input, "not identifying a known persistence context", "exact context metadata name");
            _invalidContextNames.Add(input.Identity);
            return;
        }
        AddUnique(_contexts, _invalidContextNames, input.Identity, input);
    }

    private void PrepareAssemblyOwnershipAttributes()
    {
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
            if (_externalOwners.TryGetValue(external.OriginalDefinition, out var previous) && previous.ConstructorArguments[1].Value as string != owner)
            {
                InvalidOwner(external, ConventionSymbols.MetadataName(external), "contradictory external ownership", location);
                continue;
            }
            _externalOwners[external.OriginalDefinition] = attribute;
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
        ReportOverlappingRoles(overlaps, groups);
    }

    [SuppressMessage("SonarQube", "S3267", Justification = "A Where predicate would mutate _invalidRoles as a side effect during filtering.")]
    private void ReportOverlappingRoles(HashSet<string> overlaps, Dictionary<string, ConventionInput[]> groups)
    {
        foreach (var prefix in overlaps)
        {
            foreach (var input in groups[prefix])
            {
                if (_invalidRoles.Add(input))
                    InvalidInput(input, "overlapping another role mapping", "set of nonoverlapping namespace and logical-path prefixes");
            }
        }
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
        Diagnostics.Add(ConventionProject.InputIssue(input.Kind == ConventionInputSyntax.PathKind ? "Path:Compile/Link" : input.Kind + ":" + input.Identity,
            problem, remedy, input.Location));
}
