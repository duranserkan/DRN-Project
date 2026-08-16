using System.Collections.Concurrent;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DRN.Framework.SharedKernel.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SourceKnownEntityTypeAnalyzer : DiagnosticAnalyzer
{
    private sealed record EntityTypeDeclaration(
        byte EntityTypeValue,
        byte AppId,
        INamedTypeSymbol Symbol,
        Location Location);

    private sealed record EntityNameDeclaration(
        string Name,
        byte AppId,
        INamedTypeSymbol Symbol,
        Location Location);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
    [
        DiagnosticDescriptors.MissingEntityTypeAttribute,
        DiagnosticDescriptors.DuplicateEntityTypeValue,
        DiagnosticDescriptors.InvalidEntityTypeAttributeUsage,
        DiagnosticDescriptors.DuplicateEntityName
    ];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(compilationContext =>
        {
            var sourceKnownEntitySymbol = compilationContext.Compilation.GetTypeByMetadataName(EntityAnalyzerHelper.SourceKnownEntityMetadataName);
            var entityTypeAttributeSymbol = compilationContext.Compilation.GetTypeByMetadataName(EntityAnalyzerHelper.EntityTypeAttributeMetadataName);

            if (sourceKnownEntitySymbol == null || entityTypeAttributeSymbol == null)
                return;

            var collectedEntityTypeDeclarations = new ConcurrentBag<EntityTypeDeclaration>();
            var collectedEntityNameDeclarations = new ConcurrentBag<EntityNameDeclaration>();

            var referencedEntitySymbols = EntityAnalyzerHelper.ScanReferencedAssemblies(
                compilationContext.Compilation,
                sourceKnownEntitySymbol,
                compilationContext.CancellationToken);

            compilationContext.RegisterSymbolAction(symbolContext =>
            {
                var namedType = (INamedTypeSymbol)symbolContext.Symbol;
                if (namedType.TypeKind != TypeKind.Class)
                    return;

                var inheritsSourceKnownEntity = EntityAnalyzerHelper.DerivesFrom(namedType, sourceKnownEntitySymbol);
                var entityTypeAttribute = EntityAnalyzerHelper.FindAttribute(namedType, entityTypeAttributeSymbol);
                var isPrivate = namedType.DeclaredAccessibility == Accessibility.Private;

                if (inheritsSourceKnownEntity && !namedType.IsAbstract && !isPrivate)
                {
                    var entityTypeValue = (byte)0;
                    var declaredAppId = (byte)0;
                    var hasValidAttribute = entityTypeAttribute != null && EntityAnalyzerHelper.TryGetEntityType(entityTypeAttribute, out entityTypeValue, out declaredAppId);

                    // 1. Buffer non-private entity class names for compilation end analysis (DRN0004 - Warning)
                    var location = namedType.Locations.Length > 0 ? namedType.Locations[0] : Location.None;
                    collectedEntityNameDeclarations.Add(new EntityNameDeclaration(namedType.Name, declaredAppId, namedType, location));

                    // 2. Check EntityType attribute presence (DRN0001) & collect for compilation end (DRN0002)
                    if (entityTypeAttribute == null)
                    {
                        symbolContext.ReportDiagnostic(Diagnostic.Create(
                            DiagnosticDescriptors.MissingEntityTypeAttribute,
                            location,
                            namedType.Name));
                    }
                    else if (hasValidAttribute)
                    {
                        var attrLocation = entityTypeAttribute.ApplicationSyntaxReference?.GetSyntax().GetLocation()
                                           ?? location;

                        collectedEntityTypeDeclarations.Add(new EntityTypeDeclaration(entityTypeValue, declaredAppId, namedType, attrLocation));
                    }
                }
                else if (entityTypeAttribute != null)
                {
                    // EntityTypeAttribute applied to abstract class, private class, or non-SourceKnownEntity (DRN0003)
                    var location = entityTypeAttribute.ApplicationSyntaxReference?.GetSyntax().GetLocation()
                                   ?? (namedType.Locations.Length > 0 ? namedType.Locations[0] : Location.None);

                    symbolContext.ReportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.InvalidEntityTypeAttributeUsage,
                        location,
                        namedType.Name));
                }
            }, SymbolKind.NamedType);

            compilationContext.RegisterCompilationEndAction(endContext =>
            {
                var distinctReferencedEntities = referencedEntitySymbols
                    .Distinct(SymbolEqualityComparer.Default)
                    .OfType<INamedTypeSymbol>()
                    .ToList();

                AnalyzeDuplicateEntityTypeValues(endContext, distinctReferencedEntities, collectedEntityTypeDeclarations, entityTypeAttributeSymbol);
                AnalyzeDuplicateEntityNames(endContext, distinctReferencedEntities, collectedEntityNameDeclarations, entityTypeAttributeSymbol);
            });
        });
    }

    private static void AnalyzeDuplicateEntityTypeValues(
        CompilationAnalysisContext endContext,
        IReadOnlyList<INamedTypeSymbol> distinctReferencedEntities,
        ConcurrentBag<EntityTypeDeclaration> collectedEntityTypeDeclarations,
        INamedTypeSymbol entityTypeAttributeSymbol)
    {
        var referencedByValue = distinctReferencedEntities
            .Select(s =>
            {
                var attr = EntityAnalyzerHelper.FindAttribute(s, entityTypeAttributeSymbol);
                if (attr != null && EntityAnalyzerHelper.TryGetEntityType(attr, out var val, out var refAppId))
                {
                    return (Symbol: s, HasValue: true, Value: val, AppId: refAppId);
                }

                return (Symbol: s, HasValue: false, Value: (byte)0, AppId: (byte)0);
            })
            .Where(x => x.HasValue)
            .GroupBy(x => (x.AppId, x.Value));

        var referencedEntityTypeMap = new Dictionary<(byte AppId, byte EntityTypeValue), List<INamedTypeSymbol>>();
        foreach (var refGroup in referencedByValue)
        {
            var groupKey = refGroup.Key; // (AppId, Value)
            var orderedRefGroup = EntityAnalyzerHelper.OrderSymbols(refGroup.Select(x => x.Symbol));
            referencedEntityTypeMap[groupKey] = orderedRefGroup;

            // Report collisions between distinct referenced assemblies (DRN0002)
            if (orderedRefGroup.Count > 1)
            {
                var firstRefSymbol = orderedRefGroup[0];
                var firstTargetName = $"{firstRefSymbol.ContainingAssembly.Name}::{firstRefSymbol.Name}";

                for (var i = 1; i < orderedRefGroup.Count; i++)
                {
                    var duplicateRefSymbol = orderedRefGroup[i];
                    var duplicateTargetName = $"{duplicateRefSymbol.ContainingAssembly.Name}::{duplicateRefSymbol.Name}";

                    endContext.ReportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.DuplicateEntityTypeValue,
                        Location.None,
                        groupKey.Value,
                        groupKey.AppId,
                        duplicateTargetName,
                        firstTargetName));
                }
            }
        }

        // Check local declarations against each other and against referenced assemblies (DRN0002)
        var localGroupedByValue = collectedEntityTypeDeclarations.GroupBy(d => (d.AppId, d.EntityTypeValue));

        foreach (var group in localGroupedByValue)
        {
            var groupKey = group.Key; // (AppId, EntityTypeValue)
            var entityTypeValue = groupKey.EntityTypeValue;
            var orderedDeclarations = EntityAnalyzerHelper.OrderByLocation(group, d => d.Location);

            if (referencedEntityTypeMap.TryGetValue(groupKey, out var refSymbols) && refSymbols.Count > 0)
            {
                var firstRefSymbol = refSymbols[0];
                var referencedTargetName = $"{firstRefSymbol.ContainingAssembly.Name}::{firstRefSymbol.Name}";

                foreach (var decl in orderedDeclarations.Where(decl => !SymbolEqualityComparer.Default.Equals(firstRefSymbol, decl.Symbol)))
                {
                    endContext.ReportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.DuplicateEntityTypeValue,
                        decl.Location,
                        entityTypeValue,
                        groupKey.AppId,
                        decl.Symbol.Name,
                        referencedTargetName));
                }
            }
            else if (orderedDeclarations.Count > 1)
            {
                var firstDecl = orderedDeclarations[0];
                for (var i = 1; i < orderedDeclarations.Count; i++)
                {
                    var duplicateDecl = orderedDeclarations[i];
                    if (!SymbolEqualityComparer.Default.Equals(firstDecl.Symbol, duplicateDecl.Symbol))
                    {
                        endContext.ReportDiagnostic(Diagnostic.Create(
                            DiagnosticDescriptors.DuplicateEntityTypeValue,
                            duplicateDecl.Location,
                            entityTypeValue,
                            groupKey.AppId,
                            duplicateDecl.Symbol.Name,
                            firstDecl.Symbol.Name));
                    }
                }
            }
        }
    }

    private static void AnalyzeDuplicateEntityNames(
        CompilationAnalysisContext endContext,
        IReadOnlyList<INamedTypeSymbol> distinctReferencedEntities,
        ConcurrentBag<EntityNameDeclaration> collectedEntityNameDeclarations,
        INamedTypeSymbol entityTypeAttributeSymbol)
    {
        var referencedByName = distinctReferencedEntities
            .Select(s =>
            {
                var attr = EntityAnalyzerHelper.FindAttribute(s, entityTypeAttributeSymbol);
                var refAppId = attr != null && EntityAnalyzerHelper.TryGetEntityType(attr, out _, out var app) ? app : (byte)0;
                return (Symbol: s, AppId: refAppId);
            }).GroupBy(x => (x.AppId, x.Symbol.Name), x => x.Symbol);

        var referencedEntityNameMap = new Dictionary<(byte AppId, string Name), List<INamedTypeSymbol>>();
        foreach (var refNameGroup in referencedByName)
        {
            var groupKey = refNameGroup.Key; // (AppId, Name)
            var orderedRefGroup = EntityAnalyzerHelper.OrderSymbols(refNameGroup);
            referencedEntityNameMap[groupKey] = orderedRefGroup;

            // Report collisions between distinct referenced assemblies (DRN0004)
            if (orderedRefGroup.Count > 1)
            {
                var firstRefSymbol = orderedRefGroup[0];
                var firstName = firstRefSymbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);

                for (var i = 1; i < orderedRefGroup.Count; i++)
                {
                    var duplicateRefSymbol = orderedRefGroup[i];
                    var duplicateName = duplicateRefSymbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);

                    endContext.ReportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.DuplicateEntityName,
                        Location.None,
                        duplicateName,
                        firstName,
                        groupKey.AppId));
                }
            }
        }

        // Check local declarations against each other and against referenced assemblies (DRN0004)
        var localGroupedByName = collectedEntityNameDeclarations.GroupBy(d => (d.AppId, d.Name));

        foreach (var group in localGroupedByName)
        {
            var groupKey = group.Key; // (AppId, Name)
            var orderedDeclarations = EntityAnalyzerHelper.OrderByLocation(group, d => d.Location);

            if (orderedDeclarations.Count == 0)
                continue;

            var firstDecl = orderedDeclarations[0];

            if (referencedEntityNameMap.TryGetValue(groupKey, out var refSymbols) && refSymbols.Count > 0 &&
                !SymbolEqualityComparer.Default.Equals(refSymbols[0], firstDecl.Symbol))
            {
                endContext.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.DuplicateEntityName,
                    firstDecl.Location,
                    firstDecl.Symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
                    refSymbols[0].ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
                    groupKey.AppId));
            }

            for (var i = 1; i < orderedDeclarations.Count; i++)
            {
                var duplicateDecl = orderedDeclarations[i];
                if (!SymbolEqualityComparer.Default.Equals(firstDecl.Symbol, duplicateDecl.Symbol))
                {
                    endContext.ReportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.DuplicateEntityName,
                        duplicateDecl.Location,
                        duplicateDecl.Symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
                        firstDecl.Symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
                        groupKey.AppId));
                }
            }
        }
    }
}
