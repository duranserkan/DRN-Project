using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DRN.Framework.SharedKernel.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SourceKnownEntityTypeAnalyzer : DiagnosticAnalyzer
{
    private const string SourceKnownEntityMetadataName = "DRN.Framework.SharedKernel.Domain.SourceKnownEntity";
    private const string EntityTypeAttributeMetadataName = "DRN.Framework.SharedKernel.Domain.EntityTypeAttribute";

    private sealed record EntityTypeDeclaration(
        byte EntityTypeValue,
        INamedTypeSymbol Symbol,
        Location Location);

    private sealed record EntityNameDeclaration(
        string Name,
        INamedTypeSymbol Symbol,
        Location Location);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(
            DiagnosticDescriptors.MissingEntityTypeAttribute,
            DiagnosticDescriptors.DuplicateEntityTypeValue,
            DiagnosticDescriptors.InvalidEntityTypeAttributeUsage,
            DiagnosticDescriptors.DuplicateEntityName);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(compilationContext =>
        {
            var sourceKnownEntitySymbol = compilationContext.Compilation.GetTypeByMetadataName(SourceKnownEntityMetadataName);
            var entityTypeAttributeSymbol = compilationContext.Compilation.GetTypeByMetadataName(EntityTypeAttributeMetadataName);

            if (sourceKnownEntitySymbol == null || entityTypeAttributeSymbol == null)
                return;

            var collectedEntityTypeDeclarations = new ConcurrentBag<EntityTypeDeclaration>();
            var collectedEntityNameDeclarations = new ConcurrentBag<EntityNameDeclaration>();

            var referencedEntitySymbols = ScanReferencedAssemblies(
                compilationContext.Compilation,
                sourceKnownEntitySymbol,
                compilationContext.CancellationToken);

            compilationContext.RegisterSymbolAction(symbolContext =>
            {
                var namedType = (INamedTypeSymbol)symbolContext.Symbol;
                if (namedType.TypeKind != TypeKind.Class)
                    return;

                var inheritsSourceKnownEntity = DerivesFrom(namedType, sourceKnownEntitySymbol);
                var entityTypeAttribute = FindAttribute(namedType, entityTypeAttributeSymbol);
                var isPrivate = namedType.DeclaredAccessibility == Accessibility.Private;

                if (inheritsSourceKnownEntity && !namedType.IsAbstract && !isPrivate)
                {
                    // 1. Buffer non-private entity class names for compilation end analysis (DRN0004 - Warning)
                    var location = namedType.Locations.Length > 0 ? namedType.Locations[0] : Location.None;
                    collectedEntityNameDeclarations.Add(new EntityNameDeclaration(namedType.Name, namedType, location));

                    // 2. Check EntityType attribute presence (DRN0001) & collect for compilation end (DRN0002)
                    if (entityTypeAttribute == null)
                    {
                        symbolContext.ReportDiagnostic(Diagnostic.Create(
                            DiagnosticDescriptors.MissingEntityTypeAttribute,
                            location,
                            namedType.Name));
                    }
                    else if (TryGetEntityTypeValue(entityTypeAttribute, out var entityTypeValue))
                    {
                        var attrLocation = entityTypeAttribute.ApplicationSyntaxReference?.GetSyntax().GetLocation()
                                           ?? location;

                        collectedEntityTypeDeclarations.Add(new EntityTypeDeclaration(entityTypeValue, namedType, attrLocation));
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
                // 1. Process distinct referenced entities (deduplicated by SymbolEqualityComparer for diamond dependencies)
                var distinctReferencedEntities = referencedEntitySymbols
                    .Distinct(SymbolEqualityComparer.Default)
                    .OfType<INamedTypeSymbol>()
                    .ToList();

                var referencedByValue = distinctReferencedEntities
                    .Select(s =>
                    {
                        var attr = FindAttribute(s, entityTypeAttributeSymbol);
                        if (attr != null && TryGetEntityTypeValue(attr, out var val))
                        {
                            return (Symbol: s, HasValue: true, Value: val);
                        }

                        return (Symbol: s, HasValue: false, Value: (byte)0);
                    })
                    .Where(x => x.HasValue)
                    .GroupBy(x => x.Value);

                var referencedEntityTypeMap = new System.Collections.Generic.Dictionary<byte, System.Collections.Generic.List<INamedTypeSymbol>>();
                foreach (var refGroup in referencedByValue)
                {
                    var entityTypeValue = refGroup.Key;
                    var orderedRefGroup = refGroup
                        .Select(x => x.Symbol)
                        .OrderBy(s => s.ContainingAssembly.Name, StringComparer.Ordinal)
                        .ThenBy(s => s.ToDisplayString(), StringComparer.Ordinal)
                        .ToList();

                    referencedEntityTypeMap[entityTypeValue] = orderedRefGroup;

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
                                entityTypeValue,
                                duplicateTargetName,
                                firstTargetName));
                        }
                    }
                }

                // Check local declarations against each other and against referenced assemblies (DRN0002)
                var localGroupedByValue = collectedEntityTypeDeclarations.GroupBy(d => d.EntityTypeValue);

                foreach (var group in localGroupedByValue)
                {
                    var entityTypeValue = group.Key;

                    var orderedDeclarations = group
                        .OrderBy(d => d.Location.SourceTree?.FilePath, StringComparer.Ordinal)
                        .ThenBy(d => d.Location.SourceSpan.Start)
                        .ThenBy(d => d.Location.SourceSpan.Length)
                        .ToList();

                    if (referencedEntityTypeMap.TryGetValue(entityTypeValue, out var refSymbols) && refSymbols.Count > 0)
                    {
                        var firstRefSymbol = refSymbols[0];
                        var referencedTargetName = $"{firstRefSymbol.ContainingAssembly.Name}::{firstRefSymbol.Name}";

                        foreach (var decl in orderedDeclarations)
                        {
                            if (!SymbolEqualityComparer.Default.Equals(firstRefSymbol, decl.Symbol))
                            {
                                endContext.ReportDiagnostic(Diagnostic.Create(
                                    DiagnosticDescriptors.DuplicateEntityTypeValue,
                                    decl.Location,
                                    entityTypeValue,
                                    decl.Symbol.Name,
                                    referencedTargetName));
                            }
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
                                    duplicateDecl.Symbol.Name,
                                    firstDecl.Symbol.Name));
                            }
                        }
                    }
                }

                // 2. Process duplicate entity class names (DRN0004)
                var referencedByName = distinctReferencedEntities
                    .GroupBy(s => s.Name, StringComparer.OrdinalIgnoreCase);

                var referencedEntityNameMap = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<INamedTypeSymbol>>(StringComparer.OrdinalIgnoreCase);
                foreach (var refNameGroup in referencedByName)
                {
                    var entityName = refNameGroup.Key;
                    var orderedRefGroup = refNameGroup
                        .OrderBy(s => s.ContainingAssembly.Name, StringComparer.Ordinal)
                        .ThenBy(s => s.ToDisplayString(), StringComparer.Ordinal)
                        .ToList();

                    referencedEntityNameMap[entityName] = orderedRefGroup;

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
                                firstName));
                        }
                    }
                }

                // Check local declarations against each other and against referenced assemblies (DRN0004)
                var localGroupedByName = collectedEntityNameDeclarations.GroupBy(d => d.Name, StringComparer.OrdinalIgnoreCase);

                foreach (var group in localGroupedByName)
                {
                    var entityName = group.Key;

                    var orderedDeclarations = group
                        .OrderBy(d => d.Location.SourceTree?.FilePath, StringComparer.Ordinal)
                        .ThenBy(d => d.Location.SourceSpan.Start)
                        .ThenBy(d => d.Location.SourceSpan.Length)
                        .ToList();

                    if (orderedDeclarations.Count == 0)
                        continue;

                    var firstDecl = orderedDeclarations[0];

                    if (referencedEntityNameMap.TryGetValue(entityName, out var refSymbols) && refSymbols.Count > 0 &&
                        !SymbolEqualityComparer.Default.Equals(refSymbols[0], firstDecl.Symbol))
                    {
                        endContext.ReportDiagnostic(Diagnostic.Create(
                            DiagnosticDescriptors.DuplicateEntityName,
                            firstDecl.Location,
                            firstDecl.Symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
                            refSymbols[0].ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat)));
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
                                firstDecl.Symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat)));
                        }
                    }
                }
            });
        });
    }

    private static ConcurrentBag<INamedTypeSymbol> ScanReferencedAssemblies(
        Compilation compilation,
        INamedTypeSymbol sourceKnownEntitySymbol,
        CancellationToken cancellationToken)
    {
        var collectedEntities = new ConcurrentBag<INamedTypeSymbol>();
        var targetAssembly = sourceKnownEntitySymbol.ContainingAssembly;

        foreach (var referencedAssembly in compilation.SourceModule.ReferencedAssemblySymbols)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var name = referencedAssembly.Name;
            if (name.StartsWith("System.", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("System", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("mscorlib", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("netstandard", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!ReferencesAssembly(referencedAssembly, targetAssembly))
            {
                continue;
            }

            ScanNamespace(referencedAssembly.GlobalNamespace, sourceKnownEntitySymbol, collectedEntities, cancellationToken);
        }

        return collectedEntities;
    }

    private static bool ReferencesAssembly(IAssemblySymbol assembly, IAssemblySymbol? targetAssembly)
    {
        if (targetAssembly == null)
            return false;

        if (SymbolEqualityComparer.Default.Equals(assembly, targetAssembly))
            return true;

        var targetIdentity = targetAssembly.Identity;
        foreach (var module in assembly.Modules)
        {
            foreach (var referencedAssemblySymbol in module.ReferencedAssemblySymbols)
            {
                if (SymbolEqualityComparer.Default.Equals(referencedAssemblySymbol, targetAssembly))
                    return true;
            }

            foreach (var referencedIdentity in module.ReferencedAssemblies)
            {
                if (AssemblyIdentityComparer.Default.ReferenceMatchesDefinition(referencedIdentity, targetIdentity) ||
                    string.Equals(referencedIdentity.Name, targetIdentity.Name, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static void ScanNamespace(
        INamespaceSymbol namespaceSymbol,
        INamedTypeSymbol sourceKnownEntitySymbol,
        ConcurrentBag<INamedTypeSymbol> collectedEntities,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        foreach (var type in namespaceSymbol.GetTypeMembers())
        {
            ScanType(type, sourceKnownEntitySymbol, collectedEntities, cancellationToken);
        }

        foreach (var nestedNamespace in namespaceSymbol.GetNamespaceMembers())
        {
            ScanNamespace(nestedNamespace, sourceKnownEntitySymbol, collectedEntities, cancellationToken);
        }
    }

    private static void ScanType(
        INamedTypeSymbol typeSymbol,
        INamedTypeSymbol sourceKnownEntitySymbol,
        ConcurrentBag<INamedTypeSymbol> collectedEntities,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (typeSymbol.DeclaredAccessibility == Accessibility.Private)
            return;

        if (typeSymbol.TypeKind == TypeKind.Class && !typeSymbol.IsAbstract && DerivesFrom(typeSymbol, sourceKnownEntitySymbol))
        {
            collectedEntities.Add(typeSymbol);
        }

        foreach (var nestedType in typeSymbol.GetTypeMembers())
        {
            ScanType(nestedType, sourceKnownEntitySymbol, collectedEntities, cancellationToken);
        }
    }

    private static bool TryGetEntityTypeValue(AttributeData attributeData, out byte entityTypeValue)
    {
        if (attributeData.ConstructorArguments.Length > 0)
        {
            var rawValue = attributeData.ConstructorArguments[0].Value;
            if (rawValue is byte b)
            {
                entityTypeValue = b;
                return true;
            }

            if (rawValue is sbyte or short or ushort or int or uint or long or ulong)
            {
                try
                {
                    entityTypeValue = Convert.ToByte(rawValue);
                    return true;
                }
                catch (Exception ex) when (ex is OverflowException or InvalidCastException)
                {
                    // Ignored - value outside byte range or unsupported cast
                }
            }
        }

        entityTypeValue = 0;
        return false;
    }

    private static bool DerivesFrom(INamedTypeSymbol typeSymbol, INamedTypeSymbol baseTargetSymbol)
    {
        var current = typeSymbol.BaseType;
        while (current != null)
        {
            if (SymbolEqualityComparer.Default.Equals(current, baseTargetSymbol) ||
                current.ToDisplayString() == SourceKnownEntityMetadataName)
                return true;

            current = current.BaseType;
        }

        return false;
    }

    private static AttributeData? FindAttribute(INamedTypeSymbol typeSymbol, INamedTypeSymbol attributeSymbol)
    {
        foreach (var attribute in typeSymbol.GetAttributes())
        {
            if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, attributeSymbol) ||
                attribute.AttributeClass?.ToDisplayString() == EntityTypeAttributeMetadataName)
                return attribute;
        }

        return null;
    }
}
