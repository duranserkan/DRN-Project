using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;

namespace DRN.Framework.SharedKernel.Analyzers.Conventions;

internal static class ConventionReferenceBoundaryAnalysis
{
    private const string SharedKernelPackage = "DRN.Framework.SharedKernel";
    private const string UtilsPackage = "DRN.Framework.Utils";
    private const string EntityFrameworkPackage = "DRN.Framework.EntityFramework";
    private const string HostingPackage = "DRN.Framework.Hosting";

    private static readonly string[] FrameworkPackages =
    [
        SharedKernelPackage, UtilsPackage, EntityFrameworkPackage,
        HostingPackage, "DRN.Framework.Testing", "DRN.Framework.Jobs", "DRN.Framework.MassTransit"
    ];

    internal static void Analyze(ConventionFacts facts)
    {
        var inventory = ReferenceInventory.Create(facts);
        var reported = new HashSet<string>(StringComparer.Ordinal);
        ReportDeclaredPackageReferences(facts, inventory, reported);
        foreach (var resolved in inventory.References.Where(record => record.Get(ConventionInputSyntax.StageField) == "Resolved"))
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            AnalyzeResolvedReference(facts, inventory, resolved, reported);
        }
    }

    private static void ReportDeclaredPackageReferences(ConventionFacts facts, ReferenceInventory inventory, HashSet<string> reported)
    {
        foreach (var declared in inventory.References.Where(record =>
                     record.Get(ConventionInputSyntax.StageField) == ConventionInputSyntax.PackageField))
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            if (FrameworkPackage(declared.Identity) is not { } framework) continue;
            foreach (var role in inventory.Roles)
                ReportFrameworkReference(facts, declared, declared.Identity, framework, role, "package", reported);
        }
    }

    private static void AnalyzeResolvedReference(ConventionFacts facts, ReferenceInventory inventory,
        ConventionInput resolved, HashSet<string> reported)
    {
        var directProject = inventory.Projects.Contains(ConventionProject.NormalizeSeparators(resolved.Get(ConventionInputSyntax.ProjectField))) ||
            inventory.ProjectItems.Contains(ConventionProject.NormalizeSeparators(resolved.Get(ConventionInputSyntax.OriginalProjectField)));
        var directPackage = inventory.Packages.Contains(resolved.Get(ConventionInputSyntax.PackageField));
        if (!directProject && !directPackage ||
            !inventory.Metadata.TryGetValue(ConventionProject.NormalizeSeparators(resolved.Identity), out var reference) ||
            facts.Compilation.GetAssemblyOrModuleSymbol(reference) is not IAssemblySymbol assembly) return;
        var project = facts.ReferenceProject(assembly);
        var target = ResolveDeclaredReferenceTarget(inventory.References, resolved, directProject);
        var kind = directProject ? "project" : "package";
        if (project is null)
        {
            if (FrameworkAssembly(facts, assembly) is { } framework)
                foreach (var role in inventory.Roles)
                    ReportFrameworkReference(facts, resolved, target, framework, role, kind, reported);
            return;
        }
        ReportDisallowedRoleReferences(facts, inventory.Roles, resolved, assembly, project, target, kind, reported);
    }

    private static string ResolveDeclaredReferenceTarget(ConventionInput[] references, ConventionInput resolved, bool directProject)
    {
        if (directProject)
        {
            var projectPath = ConventionProject.NormalizeSeparators(resolved.Get(ConventionInputSyntax.ProjectField));
            var originalPath = ConventionProject.NormalizeSeparators(resolved.Get(ConventionInputSyntax.OriginalProjectField));
            return references.First(record => record.Get(ConventionInputSyntax.StageField) == ConventionInputSyntax.ProjectField &&
                (ConventionProject.NormalizeSeparators(record.Identity) == projectPath ||
                 originalPath.Length > 0 && ConventionProject.NormalizeSeparators(record.Get(ConventionInputSyntax.SourceField)) == originalPath)).Identity;
        }
        return references.First(record => record.Get(ConventionInputSyntax.StageField) == ConventionInputSyntax.PackageField &&
            string.Equals(record.Identity, resolved.Get(ConventionInputSyntax.PackageField), StringComparison.OrdinalIgnoreCase)).Identity;
    }

    private static void ReportDisallowedRoleReferences(ConventionFacts facts, string[] roles, ConventionInput resolved,
        IAssemblySymbol assembly, ConventionProject project, string target, string kind, HashSet<string> reported)
    {
        foreach (var role in roles)
        {
            if (ConventionArchitecture.Allows(role, project.Role)) continue;
            var key = role + ":" + assembly.Identity.Name;
            if (!reported.Add(key)) continue;
            facts.Diagnostics.Add(Diagnostic.Create(ConventionDiagnosticDescriptors.Reference, resolved.Location,
                ImmutableDictionary<string, string?>.Empty.Add(ConventionInputSyntax.TargetField, target),
                kind, facts.Project.Name, project.Name));
        }
    }

    private static void ReportFrameworkReference(ConventionFacts facts, ConventionInput input, string target, string framework,
        string role, string kind, HashSet<string> reported)
    {
        var forbidden = role switch
        {
            ConventionArchitecture.Contract or ConventionArchitecture.SharedKernel => framework != SharedKernelPackage,
            ConventionArchitecture.Domain or ConventionArchitecture.Utils => framework is not (SharedKernelPackage or UtilsPackage),
            _ => false
        };
        if (!forbidden || !reported.Add(role + ":" + framework)) return;
        facts.Diagnostics.Add(Diagnostic.Create(ConventionDiagnosticDescriptors.Reference, input.Location,
            ImmutableDictionary<string, string?>.Empty.Add(ConventionInputSyntax.TargetField, target), kind,
            facts.Project.Name, framework));
    }

    [SuppressMessage("SonarQube", "S3267", Justification = "The direct loop avoids a predicate delegate capturing package on each lookup.")]
    private static string? FrameworkPackage(string package)
    {
        foreach (var known in FrameworkPackages)
            if (string.Equals(package, known, StringComparison.OrdinalIgnoreCase)) return known;
        return null;
    }

    private static string? FrameworkAssembly(ConventionFacts facts, IAssemblySymbol assembly)
    {
        if (SymbolEqualityComparer.Default.Equals(facts.Symbols.Entity?.ContainingAssembly, assembly)) return SharedKernelPackage;
        if (SymbolEqualityComparer.Default.Equals(facts.Symbols.Context?.ContainingAssembly, assembly)) return EntityFrameworkPackage;
        if (SymbolEqualityComparer.Default.Equals(facts.Symbols.RegistrationAttribute?.ContainingAssembly, assembly)) return UtilsPackage;
        if (SymbolEqualityComparer.Default.Equals(facts.Symbols.SourceKnownController?.ContainingAssembly, assembly)) return HostingPackage;
        return null;
    }

    private sealed class ReferenceInventory
    {
        internal ConventionInput[] References { get; }
        internal HashSet<string> Projects { get; }
        internal HashSet<string> ProjectItems { get; }
        internal HashSet<string> Packages { get; }
        internal Dictionary<string, PortableExecutableReference> Metadata { get; }
        internal string[] Roles { get; }

        private ReferenceInventory(ConventionInput[] references, HashSet<string> projects, HashSet<string> projectItems,
            HashSet<string> packages, Dictionary<string, PortableExecutableReference> metadata, string[] roles)
        {
            References = references;
            Projects = projects;
            ProjectItems = projectItems;
            Packages = packages;
            Metadata = metadata;
            Roles = roles;
        }

        internal static ReferenceInventory Create(ConventionFacts facts)
        {
            var references = facts.Records.Where(record => record.Kind == ConventionInputSyntax.ReferenceKind).ToArray();
            var projects = references.Where(record => record.Get(ConventionInputSyntax.StageField) == ConventionInputSyntax.ProjectField)
                .Select(record => ConventionProject.NormalizeSeparators(record.Identity))
                .Where(value => value.Length > 0).ToHashSet(StringComparer.Ordinal);
            var projectItems = references.Where(record => record.Get(ConventionInputSyntax.StageField) == ConventionInputSyntax.ProjectField)
                .Select(record => ConventionProject.NormalizeSeparators(record.Get(ConventionInputSyntax.SourceField)))
                .Where(value => value.Length > 0).ToHashSet(StringComparer.Ordinal);
            var packages = references.Where(record => record.Get(ConventionInputSyntax.StageField) == ConventionInputSyntax.PackageField)
                .Select(record => record.Identity).Where(value => value.Length > 0).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var metadata = facts.Compilation.References.OfType<PortableExecutableReference>()
                .Where(reference => reference.FilePath is not null)
                .GroupBy(reference => ConventionProject.NormalizeSeparators(reference.FilePath!), StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            var roles = facts.Records.Where(record => record.Kind == ConventionInputSyntax.RoleKind).Select(record => record.Identity)
                .Append(facts.Project.Role).Where(ConventionProject.IsRole).Distinct(StringComparer.Ordinal).ToArray();
            return new(references, projects, projectItems, packages, metadata, roles);
        }
    }
}
