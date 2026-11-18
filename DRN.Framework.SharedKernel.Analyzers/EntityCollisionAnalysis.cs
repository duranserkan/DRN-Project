using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DRN.Framework.SharedKernel.Analyzers;

internal sealed record EntityDeclaration(INamedTypeSymbol Symbol, byte AppId, byte Value, Location NameLocation, Location AttributeLocation)
{
    internal bool IsLocal => NameLocation.IsInSource;
    internal string ValueDiagnosticName => IsLocal ? Symbol.Name : $"{Symbol.ContainingAssembly.Name}::{Symbol.Name}";

    internal static EntityDeclaration? Read(INamedTypeSymbol symbol, AttributeData attribute, Location nameLocation, Location attributeLocation,
        EntityAnalyzerHelper.CompilationMetadata metadata, CancellationToken cancellation, out Diagnostic? problem)
    {
        problem = null;
        var status = metadata.Read(attribute, cancellation, out var value, out var appId);
        if (status == EntityAnalyzerHelper.MetadataStatus.UnsupportedAttribute)
        {
            var attributeName = nameLocation.IsInSource ? attribute.AttributeClass?.Name : attribute.AttributeClass?.ToDisplayString();
            problem = Diagnostic.Create(DiagnosticDescriptors.UnsupportedEntityAttribute, attributeLocation, attributeName ?? symbol.Name);
            return null;
        }
        var resolved = status == EntityAnalyzerHelper.MetadataStatus.Resolved;
        if (resolved && appId <= EntityAnalyzerHelper.MaxAppId)
            return new(symbol, appId, value, nameLocation, attributeLocation);

        var name = nameLocation.IsInSource ? symbol.Name : $"{symbol.ContainingAssembly.Name}::{symbol.Name}";
        problem = resolved
            ? Diagnostic.Create(DiagnosticDescriptors.AppIdOutOfRange, attributeLocation, name, appId)
            : Diagnostic.Create(DiagnosticDescriptors.UnresolvableAppId, attributeLocation, name);
        return null;
    }
}

internal static class EntityCollisionAnalysis
{
    internal static void Report(CompilationAnalysisContext context, List<EntityDeclaration> entities)
    {
        ReportCollisions(context, entities.GroupBy(entity => (entity.AppId, entity.Value)),
            static entity => entity.AttributeLocation, keepReferencedAnchor: true, DuplicateValue);
        ReportCollisions(context, entities.GroupBy(entity => (entity.AppId, entity.Symbol.Name)),
            static entity => entity.NameLocation, keepReferencedAnchor: false, DuplicateName);
        ReportMultiplePartitions(context, entities);
    }

    private static void ReportCollisions<TKey>(CompilationAnalysisContext context, IEnumerable<IGrouping<TKey, EntityDeclaration>> groups,
        Func<EntityDeclaration, Location> location, bool keepReferencedAnchor, Func<EntityDeclaration, EntityDeclaration, Diagnostic> diagnostic)
    {
        foreach (var group in groups)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            if (group.Count() < 2) continue;
            var firstReference = ReportReferencedCollisions(context, group, diagnostic);
            EntityDeclaration? firstLocal = null;
            foreach (var local in OrderByLocation(group.Where(entity => entity.IsLocal), location))
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                // Value conflicts cite the referenced owner. Name warnings cite the first local declaration once one exists.
                var anchor = keepReferencedAnchor ? firstReference ?? firstLocal : firstLocal ?? firstReference;
                if (anchor != null) context.ReportDiagnostic(diagnostic(local, anchor));
                firstLocal ??= local;
            }
        }
    }

    private static EntityDeclaration? ReportReferencedCollisions(CompilationAnalysisContext context, IEnumerable<EntityDeclaration> group,
        Func<EntityDeclaration, EntityDeclaration, Diagnostic> diagnostic)
    {
        EntityDeclaration? firstReference = null;
        foreach (var reference in group.Where(entity => !entity.IsLocal)
                     .OrderBy(entity => entity.Symbol.ContainingAssembly.Name, StringComparer.Ordinal)
                     .ThenBy(entity => entity.Symbol.ToDisplayString(), StringComparer.Ordinal))
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            if (firstReference is null) firstReference = reference;
            else context.ReportDiagnostic(diagnostic(reference, firstReference));
        }
        return firstReference;
    }

    private static Diagnostic DuplicateValue(EntityDeclaration entity, EntityDeclaration owner) =>
        Diagnostic.Create(DiagnosticDescriptors.DuplicateEntityTypeValue, entity.AttributeLocation,
            entity.Value, entity.AppId, entity.ValueDiagnosticName, owner.ValueDiagnosticName);

    private static Diagnostic DuplicateName(EntityDeclaration entity, EntityDeclaration owner) =>
        Diagnostic.Create(DiagnosticDescriptors.DuplicateEntityName, entity.NameLocation,
            entity.Symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
            owner.Symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat), entity.AppId);

    private static IOrderedEnumerable<EntityDeclaration> OrderByLocation(IEnumerable<EntityDeclaration> entities,
        Func<EntityDeclaration, Location> location) => entities
        .OrderBy(entity => location(entity).SourceTree?.FilePath, StringComparer.Ordinal)
        .ThenBy(entity => location(entity).SourceSpan.Start)
        .ThenBy(entity => location(entity).SourceSpan.Length)
        .ThenBy(entity => entity.Symbol.ToDisplayString(), StringComparer.Ordinal);

    [SuppressMessage("SonarQube", "S3267", Justification = "Iterating the list directly preserves per-entity cancellation without a projection iterator.")]
    private static void ReportMultiplePartitions(CompilationAnalysisContext context, List<EntityDeclaration> entities)
    {
        const byte testAppId = 127;
        if (IsMultiAppPermitted(context)) return;
        var partitions = new SortedSet<byte>();
        foreach (var entity in entities)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            if (entity.AppId != testAppId) partitions.Add(entity.AppId);
        }
        if (partitions.Count < 2) return;
        var firstLocal = OrderByLocation(entities.Where(entity => entity.IsLocal), static entity => entity.AttributeLocation).FirstOrDefault();
        context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.MultipleAppIdsNotPermitted, firstLocal?.AttributeLocation ?? Location.None,
            context.Compilation.AssemblyName ?? "Assembly", string.Join(", ", partitions)));
    }

    private static bool IsMultiAppPermitted(CompilationAnalysisContext context)
    {
        var options = context.Options.AnalyzerConfigOptionsProvider.GlobalOptions;
        bool Enabled(string key) => options.TryGetValue(key, out var value) && string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
        if (Enabled("build_property.AllowMultipleAppIds") || Enabled("build_property.IsTestProject") ||
            Enabled("build_property.UseMicrosoftTestingPlatformRunner")) return true;
        var name = context.Compilation.AssemblyName;
        return name != null && (name.Contains(".Test.", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("Test.", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".Tests", StringComparison.OrdinalIgnoreCase) ||
            name.EndsWith(".Test", StringComparison.OrdinalIgnoreCase));
    }
}
