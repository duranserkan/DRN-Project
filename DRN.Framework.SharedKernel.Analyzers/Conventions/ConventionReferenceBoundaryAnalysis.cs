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
        var target = inventory.DeclaredProject(resolved);
        var kind = target is null ? "package" : "project";
        if (target is null && !inventory.Packages.TryGetValue(resolved.Get(ConventionInputSyntax.PackageField), out target)) return;
        if (!inventory.Metadata.TryGetValue(ConventionProject.NormalizeSeparators(resolved.Identity), out var reference) ||
            facts.Compilation.GetAssemblyOrModuleSymbol(reference) is not IAssemblySymbol assembly) return;
        var project = facts.ReferenceProject(assembly);
        if (project is null)
        {
            if (FrameworkAssembly(facts, assembly) is { } framework)
                foreach (var role in inventory.Roles)
                    ReportFrameworkReference(facts, resolved, target, framework, role, kind, reported);
            return;
        }
        ReportDisallowedRoleReferences(facts, inventory.Roles, resolved, assembly, project, (kind, target), reported);
    }

    private static void ReportDisallowedRoleReferences(ConventionFacts facts, string[] roles, ConventionInput resolved,
        IAssemblySymbol assembly, ConventionProject project, (string Kind, string Target) declaration, HashSet<string> reported)
    {
        foreach (var role in roles)
        {
            if (ConventionArchitecture.Allows(role, project.Role)) continue;
            var key = role + ":" + assembly.Identity.Name;
            if (!reported.Add(key)) continue;
            facts.Diagnostics.Add(Diagnostic.Create(ConventionDiagnosticDescriptors.Reference, resolved.Location,
                ImmutableDictionary<string, string?>.Empty.Add(ConventionInputSyntax.TargetField, declaration.Target),
                declaration.Kind, facts.Project.Name, project.Name));
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
    internal static string? FrameworkPackage(string package)
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
        if (SymbolEqualityComparer.Default.Equals(facts.Symbols.SourceKnownReadOnlyController?.ContainingAssembly, assembly)) return HostingPackage;
        return null;
    }

    private sealed class ReferenceInventory
    {
        internal ConventionInput[] References { get; }
        private readonly Dictionary<string, int> _projects = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _projectItems = new(StringComparer.Ordinal);
        internal HashSet<string> Packages { get; }
        internal Dictionary<string, PortableExecutableReference> Metadata { get; }
        internal string[] Roles { get; }

        private ReferenceInventory(ConventionInput[] references, HashSet<string> packages,
            Dictionary<string, PortableExecutableReference> metadata, string[] roles)
        {
            References = references;
            for (var index = 0; index < references.Length; index++)
            {
                var record = references[index];
                if (record.Get(ConventionInputSyntax.StageField) != ConventionInputSyntax.ProjectField) continue;
                var project = ConventionProject.NormalizeSeparators(record.Identity);
                var item = ConventionProject.NormalizeSeparators(record.Get(ConventionInputSyntax.SourceField));
                if (project.Length > 0) _projects.TryAdd(project, index);
                if (item.Length > 0) _projectItems.TryAdd(item, index);
            }
            Packages = packages;
            Metadata = metadata;
            Roles = roles;
        }

        internal string? DeclaredProject(ConventionInput resolved)
        {
            var project = ConventionProject.NormalizeSeparators(resolved.Get(ConventionInputSyntax.ProjectField));
            var original = ConventionProject.NormalizeSeparators(resolved.Get(ConventionInputSyntax.OriginalProjectField));
            var first = Math.Min(_projects.GetValueOrDefault(project, int.MaxValue), _projectItems.GetValueOrDefault(original, int.MaxValue));
            return first == int.MaxValue ? null : References[first].Identity;
        }

        internal static ReferenceInventory Create(ConventionFacts facts)
        {
            var references = facts.Records.Where(record => record.Kind == ConventionInputSyntax.ReferenceKind).ToArray();
            var packages = references.Where(record => record.Get(ConventionInputSyntax.StageField) == ConventionInputSyntax.PackageField)
                .Select(record => record.Identity).Where(value => value.Length > 0).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var metadata = facts.Compilation.References.OfType<PortableExecutableReference>()
                .Where(reference => reference.FilePath is not null)
                .GroupBy(reference => ConventionProject.NormalizeSeparators(reference.FilePath!), StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            var roles = facts.Records.Where(record => record.Kind == ConventionInputSyntax.RoleKind).Select(record => record.Identity)
                .Append(facts.Project.Role).Where(ConventionProject.IsRole).Distinct(StringComparer.Ordinal).ToArray();
            return new(references, packages, metadata, roles);
        }
    }
}
