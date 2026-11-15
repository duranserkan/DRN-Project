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

    private readonly record struct ReferencedEntityInfo(INamedTypeSymbol Symbol, bool HasValue, byte Value, byte AppId,
        bool Unresolvable, bool Unsupported);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
    [
        DiagnosticDescriptors.MissingEntityTypeAttribute,
        DiagnosticDescriptors.DuplicateEntityTypeValue,
        DiagnosticDescriptors.InvalidEntityTypeAttributeUsage,
        DiagnosticDescriptors.DuplicateEntityName,
        DiagnosticDescriptors.MultipleAppIdsNotPermitted,
        DiagnosticDescriptors.UnresolvableAppId,
        DiagnosticDescriptors.AppIdOutOfRange,
        DiagnosticDescriptors.UnsupportedEntityAttribute,
        DiagnosticDescriptors.InvalidAppIdArgument,
        DiagnosticDescriptors.InvalidEntityIdFormatArgument,
        DiagnosticDescriptors.HiddenEntityIdentityMember,
        DiagnosticDescriptors.GenericConcreteEntity,
        DiagnosticDescriptors.AbstractEntityMetadataArgument,
        DiagnosticDescriptors.UninitializedEntityOperations
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

            var entityFacts = new EntityAnalyzerHelper.EntityTypeFacts(sourceKnownEntitySymbol);
            SourceKnownEntityUsageAnalysis.Register(compilationContext, sourceKnownEntitySymbol, entityFacts);
            UninitializedEntityOperationsAnalysis.Register(compilationContext, sourceKnownEntitySymbol, entityFacts);

            var collectedEntityTypeDeclarations = new ConcurrentBag<EntityTypeDeclaration>();
            var collectedEntityNameDeclarations = new ConcurrentBag<EntityNameDeclaration>();
            var metadata = new EntityAnalyzerHelper.CompilationMetadata(compilationContext.Compilation);

            var referencedEntitySymbols = EntityAnalyzerHelper.ScanReferencedAssemblies(
                compilationContext.Compilation,
                sourceKnownEntitySymbol,
                compilationContext.CancellationToken);

            compilationContext.RegisterSymbolAction(
                symbolContext => AnalyzeNamedType(
                    symbolContext,
                    entityFacts,
                    entityTypeAttributeSymbol,
                    collectedEntityTypeDeclarations,
                    collectedEntityNameDeclarations,
                    metadata),
                SymbolKind.NamedType);

            compilationContext.RegisterCompilationEndAction(
                endContext => AnalyzeCompilationEnd(
                    endContext,
                    referencedEntitySymbols,
                    collectedEntityTypeDeclarations,
                    collectedEntityNameDeclarations,
                    entityTypeAttributeSymbol,
                    metadata));
        });
    }

    private static void AnalyzeNamedType(
        SymbolAnalysisContext symbolContext,
        EntityAnalyzerHelper.EntityTypeFacts entityFacts,
        INamedTypeSymbol entityTypeAttributeSymbol,
        ConcurrentBag<EntityTypeDeclaration> collectedEntityTypeDeclarations,
        ConcurrentBag<EntityNameDeclaration> collectedEntityNameDeclarations,
        EntityAnalyzerHelper.CompilationMetadata metadata)
    {
        var namedType = (INamedTypeSymbol)symbolContext.Symbol;
        symbolContext.CancellationToken.ThrowIfCancellationRequested();
        if (namedType.TypeKind != TypeKind.Class)
            return;

        // Validate producers even if no entity in their assembly uses the attribute yet.
        if (!SymbolEqualityComparer.Default.Equals(namedType, entityTypeAttributeSymbol) &&
            EntityAnalyzerHelper.IsOrDerivesFromEntityTypeAttribute(namedType, entityTypeAttributeSymbol) &&
            !metadata.IsSupported(namedType, symbolContext.CancellationToken))
        {
            symbolContext.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.UnsupportedEntityAttribute,
                namedType.Locations.FirstOrDefault() ?? Location.None,
                namedType.Name));
        }

        var inheritsSourceKnownEntity = entityFacts.IsDerived(namedType, symbolContext.CancellationToken);
        var entityTypeAttribute = EntityAnalyzerHelper.FindAttribute(namedType, entityTypeAttributeSymbol);
        var isPrivate = EntityAnalyzerHelper.IsEffectivelyPrivate(namedType);

        if (inheritsSourceKnownEntity && !namedType.IsAbstract && !isPrivate)
        {
            ProcessValidCandidateEntity(
                symbolContext,
                namedType,
                entityTypeAttribute,
                collectedEntityTypeDeclarations,
                collectedEntityNameDeclarations,
                metadata);
        }
        else if (entityTypeAttribute != null)
        {
            // EntityTypeAttribute applied to abstract class, private class, or non-SourceKnownEntity (DRN0003)
            var location = entityTypeAttribute.ApplicationSyntaxReference?.GetSyntax(symbolContext.CancellationToken).GetLocation()
                           ?? (namedType.Locations.Length > 0 ? namedType.Locations[0] : Location.None);

            symbolContext.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.InvalidEntityTypeAttributeUsage,
                location,
                namedType.Name));
        }
    }

    private static void ProcessValidCandidateEntity(
        SymbolAnalysisContext symbolContext,
        INamedTypeSymbol namedType,
        AttributeData? entityTypeAttribute,
        ConcurrentBag<EntityTypeDeclaration> collectedEntityTypeDeclarations,
        ConcurrentBag<EntityNameDeclaration> collectedEntityNameDeclarations,
        EntityAnalyzerHelper.CompilationMetadata metadata)
    {
        var location = namedType.Locations.Length > 0 ? namedType.Locations[0] : Location.None;

        if (entityTypeAttribute == null)
        {
            symbolContext.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.MissingEntityTypeAttribute,
                location,
                namedType.Name));
            return;
        }

        if (!metadata.IsSupported(entityTypeAttribute.AttributeClass, symbolContext.CancellationToken))
        {
            symbolContext.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.UnsupportedEntityAttribute,
                entityTypeAttribute.ApplicationSyntaxReference?.GetSyntax(symbolContext.CancellationToken).GetLocation() ?? location,
                entityTypeAttribute.AttributeClass?.Name ?? namedType.Name));
            return;
        }

        if (!EntityAnalyzerHelper.TryGetEntityType(entityTypeAttribute, symbolContext.Compilation, symbolContext.CancellationToken,
                out var entityTypeValue, out var declaredAppId, metadata))
        {
            var attrLocation = entityTypeAttribute.ApplicationSyntaxReference?.GetSyntax(symbolContext.CancellationToken).GetLocation() ?? location;
            symbolContext.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.UnresolvableAppId,
                attrLocation,
                namedType.Name));
            return;
        }

        if (declaredAppId > EntityAnalyzerHelper.MaxAppId)
        {
            var attrLocation = entityTypeAttribute.ApplicationSyntaxReference?.GetSyntax(symbolContext.CancellationToken).GetLocation() ?? location;
            symbolContext.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.AppIdOutOfRange,
                attrLocation,
                namedType.Name,
                declaredAppId));
            return;
        }

        // 1. Buffer non-private entity class names for compilation end analysis (DRN0004 - Warning)
        collectedEntityNameDeclarations.Add(new EntityNameDeclaration(namedType.Name, declaredAppId, namedType, location));

        // 2. Collect for compilation end (DRN0002 & DRN0005)
        var validAttrLocation = entityTypeAttribute.ApplicationSyntaxReference?.GetSyntax(symbolContext.CancellationToken).GetLocation() ?? location;
        collectedEntityTypeDeclarations.Add(new EntityTypeDeclaration(entityTypeValue, declaredAppId, namedType, validAttrLocation));
    }

    private static void AnalyzeCompilationEnd(
        CompilationAnalysisContext endContext,
        ConcurrentBag<INamedTypeSymbol> referencedEntitySymbols,
        ConcurrentBag<EntityTypeDeclaration> collectedEntityTypeDeclarations,
        ConcurrentBag<EntityNameDeclaration> collectedEntityNameDeclarations,
        INamedTypeSymbol entityTypeAttributeSymbol,
        EntityAnalyzerHelper.CompilationMetadata metadata)
    {
        var distinctReferencedEntities = referencedEntitySymbols
            .Distinct(SymbolEqualityComparer.Default)
            .OfType<INamedTypeSymbol>()
            .Select(symbol => ExtractEntityTypeInfo(symbol, entityTypeAttributeSymbol, endContext.Compilation, endContext.CancellationToken, metadata))
            .ToArray();

        AnalyzeDuplicateEntityTypeValues(endContext, distinctReferencedEntities, collectedEntityTypeDeclarations, entityTypeAttributeSymbol);
        AnalyzeDuplicateEntityNames(endContext, distinctReferencedEntities, collectedEntityNameDeclarations);
        AnalyzeMultipleAppIds(endContext, distinctReferencedEntities, collectedEntityTypeDeclarations);
    }

    private static void AnalyzeDuplicateEntityTypeValues(
        CompilationAnalysisContext endContext,
        IReadOnlyList<ReferencedEntityInfo> distinctReferencedEntities,
        ConcurrentBag<EntityTypeDeclaration> collectedEntityTypeDeclarations,
        INamedTypeSymbol entityTypeAttributeSymbol)
    {
        var referencedEntityTypeMap = BuildAndReportReferencedEntityTypeCollisions(
            endContext,
            distinctReferencedEntities,
            entityTypeAttributeSymbol);

        CheckLocalEntityTypeCollisions(
            endContext,
            collectedEntityTypeDeclarations,
            referencedEntityTypeMap);
    }

    private static Dictionary<(byte AppId, byte EntityTypeValue), List<INamedTypeSymbol>> BuildAndReportReferencedEntityTypeCollisions(
        CompilationAnalysisContext endContext,
        IReadOnlyList<ReferencedEntityInfo> extractedInfo,
        INamedTypeSymbol entityTypeAttributeSymbol)
    {
        foreach (var info in extractedInfo)
        {
            endContext.CancellationToken.ThrowIfCancellationRequested();
            if (info.Unsupported)
            {
                var attributeName = EntityAnalyzerHelper.FindAttribute(info.Symbol, entityTypeAttributeSymbol)?.AttributeClass?.ToDisplayString();
                endContext.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.UnsupportedEntityAttribute,
                    Location.None,
                    attributeName ?? info.Symbol.Name));
            }
            else if (info.Unresolvable)
            {
                endContext.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.UnresolvableAppId,
                    Location.None,
                    $"{info.Symbol.ContainingAssembly.Name}::{info.Symbol.Name}"));
            }
            else if (info.HasValue && info.AppId > EntityAnalyzerHelper.MaxAppId)
            {
                endContext.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.AppIdOutOfRange,
                    Location.None,
                    $"{info.Symbol.ContainingAssembly.Name}::{info.Symbol.Name}",
                    info.AppId));
            }
        }

        var referencedByValue = extractedInfo
            .Where(x => x.HasValue && x.AppId <= EntityAnalyzerHelper.MaxAppId)
            .GroupBy(x => (x.AppId, x.Value));

        var referencedEntityTypeMap = new Dictionary<(byte AppId, byte EntityTypeValue), List<INamedTypeSymbol>>();
        foreach (var refGroup in referencedByValue)
        {
            var groupKey = refGroup.Key; // (AppId, Value)
            var orderedRefGroup = EntityAnalyzerHelper.OrderSymbols(refGroup.Select(x => x.Symbol));
            referencedEntityTypeMap[groupKey] = orderedRefGroup;

            ReportReferencedEntityTypeCollisions(endContext, groupKey, orderedRefGroup);
        }

        return referencedEntityTypeMap;
    }

    private static ReferencedEntityInfo ExtractEntityTypeInfo(
        INamedTypeSymbol symbol,
        INamedTypeSymbol entityTypeAttributeSymbol,
        Compilation compilation,
        CancellationToken cancellationToken,
        EntityAnalyzerHelper.CompilationMetadata metadata)
    {
        var attr = EntityAnalyzerHelper.FindAttribute(symbol, entityTypeAttributeSymbol);
        if (attr == null)
            return new(symbol, HasValue: false, Value: 0, AppId: 0, Unresolvable: false, Unsupported: false);

        if (!metadata.IsSupported(attr.AttributeClass, cancellationToken))
            return new(symbol, HasValue: false, Value: 0, AppId: 0, Unresolvable: false, Unsupported: true);

        if (EntityAnalyzerHelper.TryGetEntityType(attr, compilation, cancellationToken, out var val, out var refAppId, metadata))
            return new(symbol, HasValue: true, Value: val, AppId: refAppId, Unresolvable: false, Unsupported: false);

        return new(symbol, HasValue: false, Value: 0, AppId: 0, Unresolvable: true, Unsupported: false);
    }

    private static void ReportReferencedEntityTypeCollisions(
        CompilationAnalysisContext endContext,
        (byte AppId, byte EntityTypeValue) groupKey,
        List<INamedTypeSymbol> orderedRefGroup)
    {
        if (orderedRefGroup.Count <= 1)
            return;

        var firstRefSymbol = orderedRefGroup[0];
        var firstTargetName = $"{firstRefSymbol.ContainingAssembly.Name}::{firstRefSymbol.Name}";

        for (var i = 1; i < orderedRefGroup.Count; i++)
        {
            var duplicateRefSymbol = orderedRefGroup[i];
            var duplicateTargetName = $"{duplicateRefSymbol.ContainingAssembly.Name}::{duplicateRefSymbol.Name}";

            endContext.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.DuplicateEntityTypeValue,
                Location.None,
                groupKey.EntityTypeValue,
                groupKey.AppId,
                duplicateTargetName,
                firstTargetName));
        }
    }

    private static void CheckLocalEntityTypeCollisions(
        CompilationAnalysisContext endContext,
        ConcurrentBag<EntityTypeDeclaration> collectedEntityTypeDeclarations,
        Dictionary<(byte AppId, byte EntityTypeValue), List<INamedTypeSymbol>> referencedEntityTypeMap)
    {
        var localGroupedByValue = collectedEntityTypeDeclarations.GroupBy(d => (d.AppId, d.EntityTypeValue));

        foreach (var group in localGroupedByValue)
        {
            var groupKey = group.Key; // (AppId, EntityTypeValue)
            var orderedDeclarations = EntityAnalyzerHelper.OrderByLocation(group, d => d.Location);

            if (referencedEntityTypeMap.TryGetValue(groupKey, out var refSymbols) && refSymbols.Count > 0)
                ReportLocalAgainstReferencedEntityTypeCollisions(endContext, groupKey, orderedDeclarations, refSymbols[0]);
            else if (orderedDeclarations.Count > 1)
                ReportLocalDuplicateEntityTypeCollisions(endContext, groupKey, orderedDeclarations);
        }
    }

    private static void ReportLocalAgainstReferencedEntityTypeCollisions(
        CompilationAnalysisContext endContext,
        (byte AppId, byte EntityTypeValue) groupKey,
        List<EntityTypeDeclaration> orderedDeclarations,
        INamedTypeSymbol firstRefSymbol)
    {
        var referencedTargetName = $"{firstRefSymbol.ContainingAssembly.Name}::{firstRefSymbol.Name}";

        foreach (var decl in orderedDeclarations.Where(decl => !SymbolEqualityComparer.Default.Equals(firstRefSymbol, decl.Symbol)))
        {
            endContext.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.DuplicateEntityTypeValue,
                decl.Location,
                groupKey.EntityTypeValue,
                groupKey.AppId,
                decl.Symbol.Name,
                referencedTargetName));
        }
    }

    private static void ReportLocalDuplicateEntityTypeCollisions(
        CompilationAnalysisContext endContext,
        (byte AppId, byte EntityTypeValue) groupKey,
        List<EntityTypeDeclaration> orderedDeclarations)
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
                    groupKey.EntityTypeValue,
                    groupKey.AppId,
                    duplicateDecl.Symbol.Name,
                    firstDecl.Symbol.Name));
            }
        }
    }

    private static void AnalyzeDuplicateEntityNames(
        CompilationAnalysisContext endContext,
        IReadOnlyList<ReferencedEntityInfo> distinctReferencedEntities,
        ConcurrentBag<EntityNameDeclaration> collectedEntityNameDeclarations)
    {
        var referencedEntityNameMap = BuildAndReportReferencedEntityNameCollisions(
            endContext,
            distinctReferencedEntities);

        CheckLocalEntityNameCollisions(
            endContext,
            collectedEntityNameDeclarations,
            referencedEntityNameMap);
    }

    private static Dictionary<(byte AppId, string Name), List<INamedTypeSymbol>> BuildAndReportReferencedEntityNameCollisions(
        CompilationAnalysisContext endContext,
        IReadOnlyList<ReferencedEntityInfo> distinctReferencedEntities)
    {
        var referencedByName = distinctReferencedEntities
            .Where(x => x.HasValue && x.AppId <= EntityAnalyzerHelper.MaxAppId)
            .GroupBy(x => (x.AppId, x.Symbol.Name), x => x.Symbol);

        var referencedEntityNameMap = new Dictionary<(byte AppId, string Name), List<INamedTypeSymbol>>();
        foreach (var refNameGroup in referencedByName)
        {
            var groupKey = refNameGroup.Key; // (AppId, Name)
            var orderedRefGroup = EntityAnalyzerHelper.OrderSymbols(refNameGroup);
            referencedEntityNameMap[groupKey] = orderedRefGroup;

            ReportReferencedEntityNameCollisions(endContext, groupKey.AppId, orderedRefGroup);
        }

        return referencedEntityNameMap;
    }

    private static void ReportReferencedEntityNameCollisions(
        CompilationAnalysisContext endContext,
        byte appId,
        List<INamedTypeSymbol> orderedRefGroup)
    {
        if (orderedRefGroup.Count <= 1)
            return;

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
                appId));
        }
    }

    private static void CheckLocalEntityNameCollisions(
        CompilationAnalysisContext endContext,
        ConcurrentBag<EntityNameDeclaration> collectedEntityNameDeclarations,
        Dictionary<(byte AppId, string Name), List<INamedTypeSymbol>> referencedEntityNameMap)
    {
        var localGroupedByName = collectedEntityNameDeclarations.GroupBy(d => (d.AppId, d.Name));

        foreach (var group in localGroupedByName)
        {
            var groupKey = group.Key; // (AppId, Name)
            var orderedDeclarations = EntityAnalyzerHelper.OrderByLocation(group, d => d.Location);

            if (orderedDeclarations.Count == 0)
                continue;

            var firstDecl = orderedDeclarations[0];

            ReportLocalAgainstReferencedEntityNameCollision(endContext, groupKey, firstDecl, referencedEntityNameMap);
            ReportLocalDuplicateEntityNameCollisions(endContext, groupKey.AppId, firstDecl, orderedDeclarations);
        }
    }

    private static void ReportLocalAgainstReferencedEntityNameCollision(
        CompilationAnalysisContext endContext,
        (byte AppId, string Name) groupKey,
        EntityNameDeclaration firstDecl,
        Dictionary<(byte AppId, string Name), List<INamedTypeSymbol>> referencedEntityNameMap)
    {
        if (referencedEntityNameMap.TryGetValue(groupKey, out var refSymbols)
            && refSymbols.Count > 0
            && !SymbolEqualityComparer.Default.Equals(refSymbols[0], firstDecl.Symbol))
        {
            endContext.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.DuplicateEntityName,
                firstDecl.Location,
                firstDecl.Symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
                refSymbols[0].ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
                groupKey.AppId));
        }
    }

    private static void ReportLocalDuplicateEntityNameCollisions(
        CompilationAnalysisContext endContext,
        byte appId,
        EntityNameDeclaration firstDecl,
        List<EntityNameDeclaration> orderedDeclarations)
    {
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
                    appId));
            }
        }
    }

    private const byte TestAppId = 127;

    private static void AnalyzeMultipleAppIds(
        CompilationAnalysisContext endContext,
        IReadOnlyList<ReferencedEntityInfo> distinctReferencedEntities,
        ConcurrentBag<EntityTypeDeclaration> collectedEntityTypeDeclarations)
    {
        if (IsMultiAppPermitted(endContext))
            return;

        var localAppIds = collectedEntityTypeDeclarations
            .Select(d => d.AppId);

        var referencedAppIds = distinctReferencedEntities
            .Where(x => x.HasValue && x.AppId <= EntityAnalyzerHelper.MaxAppId)
            .Select(x => x.AppId);

        var distinctNonTestAppIds = localAppIds
            .Concat(referencedAppIds)
            .Where(appId => appId != TestAppId)
            .Distinct()
            .OrderBy(appId => appId)
            .ToList();

        if (distinctNonTestAppIds.Count <= 1)
            return;

        var assemblyName = endContext.Compilation.AssemblyName ?? "Assembly";
        var appIdsSummary = string.Join(", ", distinctNonTestAppIds);

        var location = EntityAnalyzerHelper.OrderByLocation(collectedEntityTypeDeclarations, d => d.Location)
            .FirstOrDefault()?.Location ?? Location.None;

        endContext.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.MultipleAppIdsNotPermitted,
            location,
            assemblyName,
            appIdsSummary));
    }

    private static bool IsMultiAppPermitted(CompilationAnalysisContext context)
    {
        var options = context.Options.AnalyzerConfigOptionsProvider.GlobalOptions;

        if (options.TryGetValue("build_property.AllowMultipleAppIds", out var allowMulti) &&
            string.Equals(allowMulti, "true", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (options.TryGetValue("build_property.IsTestProject", out var isTest) &&
            string.Equals(isTest, "true", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (options.TryGetValue("build_property.UseMicrosoftTestingPlatformRunner", out var isMtp) &&
            string.Equals(isMtp, "true", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var assemblyName = context.Compilation.AssemblyName;
        if (assemblyName != null &&
            (assemblyName.Contains(".Test.", StringComparison.OrdinalIgnoreCase) ||
             assemblyName.StartsWith("Test.", StringComparison.OrdinalIgnoreCase) ||
             assemblyName.EndsWith(".Tests", StringComparison.OrdinalIgnoreCase) ||
             assemblyName.EndsWith(".Test", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return false;
    }
}
