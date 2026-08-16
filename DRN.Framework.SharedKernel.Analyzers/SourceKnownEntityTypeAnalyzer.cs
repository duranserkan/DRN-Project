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
        Location Location,
        bool IsPrivate);

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

            var (referencedEntityTypeMap, referencedEntityNameMap) = ScanReferencedAssemblies(
                compilationContext.Compilation,
                sourceKnownEntitySymbol,
                entityTypeAttributeSymbol,
                compilationContext.CancellationToken);

            compilationContext.RegisterSymbolAction(symbolContext =>
            {
                var namedType = (INamedTypeSymbol)symbolContext.Symbol;
                if (namedType.TypeKind != TypeKind.Class)
                    return;

                var inheritsSourceKnownEntity = DerivesFrom(namedType, sourceKnownEntitySymbol);
                var entityTypeAttribute = FindAttribute(namedType, entityTypeAttributeSymbol);

                if (inheritsSourceKnownEntity && !namedType.IsAbstract)
                {
                    var isPrivate = namedType.DeclaredAccessibility == Accessibility.Private;

                    // 1. Buffer non-private entity class names for compilation end analysis (DRN0004 - Warning)
                    if (!isPrivate)
                    {
                        var location = namedType.Locations.Length > 0 ? namedType.Locations[0] : Location.None;
                        collectedEntityNameDeclarations.Add(new EntityNameDeclaration(namedType.Name, namedType, location));
                    }

                    // 2. Check EntityType attribute presence (DRN0001) & collect for compilation end (DRN0002)
                    if (entityTypeAttribute == null)
                    {
                        var location = namedType.Locations.Length > 0 ? namedType.Locations[0] : Location.None;
                        symbolContext.ReportDiagnostic(Diagnostic.Create(
                            DiagnosticDescriptors.MissingEntityTypeAttribute,
                            location,
                            namedType.Name));
                    }
                    else if (TryGetEntityTypeValue(entityTypeAttribute, out var entityTypeValue))
                    {
                        var location = entityTypeAttribute.ApplicationSyntaxReference?.GetSyntax().GetLocation()
                                       ?? (namedType.Locations.Length > 0 ? namedType.Locations[0] : Location.None);

                        collectedEntityTypeDeclarations.Add(new EntityTypeDeclaration(entityTypeValue, namedType, location, isPrivate));
                    }
                }
                else if (entityTypeAttribute != null)
                {
                    // EntityTypeAttribute applied to abstract class or non-SourceKnownEntity
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
                // Process duplicate EntityType values (DRN0002)
                var groupedByValue = collectedEntityTypeDeclarations.GroupBy(d => d.EntityTypeValue);

                foreach (var group in groupedByValue)
                {
                    var entityTypeValue = group.Key;

                    var orderedDeclarations = group
                        .OrderBy(d => d.Location.SourceTree?.FilePath, StringComparer.Ordinal)
                        .ThenBy(d => d.Location.SourceSpan.Start)
                        .ThenBy(d => d.Location.SourceSpan.Length)
                        .ToList();

                    if (referencedEntityTypeMap.TryGetValue(entityTypeValue, out var referencedType) && referencedType != null)
                    {
                        var referencedTargetName = $"{referencedType.ContainingAssembly.Name}::{referencedType.Name}";
                        foreach (var decl in orderedDeclarations)
                        {
                            if (!decl.IsPrivate && !SymbolEqualityComparer.Default.Equals(referencedType, decl.Symbol))
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

                // Process duplicate entity class names (DRN0004)
                var groupedByName = collectedEntityNameDeclarations.GroupBy(d => d.Name, StringComparer.OrdinalIgnoreCase);

                foreach (var group in groupedByName)
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

                    if (referencedEntityNameMap.TryGetValue(entityName, out var referencedType) &&
                        !SymbolEqualityComparer.Default.Equals(referencedType, firstDecl.Symbol))
                    {
                        endContext.ReportDiagnostic(Diagnostic.Create(
                            DiagnosticDescriptors.DuplicateEntityName,
                            firstDecl.Location,
                            firstDecl.Symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
                            referencedType.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat)));
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

    private static (ConcurrentDictionary<byte, INamedTypeSymbol> EntityTypes, ConcurrentDictionary<string, INamedTypeSymbol> EntityNames)
        ScanReferencedAssemblies(
            Compilation compilation,
            INamedTypeSymbol sourceKnownEntitySymbol,
            INamedTypeSymbol entityTypeAttributeSymbol,
            CancellationToken cancellationToken)
    {
        var typeMap = new ConcurrentDictionary<byte, INamedTypeSymbol>();
        var nameMap = new ConcurrentDictionary<string, INamedTypeSymbol>(StringComparer.OrdinalIgnoreCase);
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

            ScanNamespace(referencedAssembly.GlobalNamespace, sourceKnownEntitySymbol, entityTypeAttributeSymbol, typeMap, nameMap, cancellationToken);
        }

        return (typeMap, nameMap);
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
        INamedTypeSymbol entityTypeAttributeSymbol,
        ConcurrentDictionary<byte, INamedTypeSymbol> typeMap,
        ConcurrentDictionary<string, INamedTypeSymbol> nameMap,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        foreach (var type in namespaceSymbol.GetTypeMembers())
        {
            ScanType(type, sourceKnownEntitySymbol, entityTypeAttributeSymbol, typeMap, nameMap, cancellationToken);
        }

        foreach (var nestedNamespace in namespaceSymbol.GetNamespaceMembers())
        {
            ScanNamespace(nestedNamespace, sourceKnownEntitySymbol, entityTypeAttributeSymbol, typeMap, nameMap, cancellationToken);
        }
    }

    private static void ScanType(
        INamedTypeSymbol typeSymbol,
        INamedTypeSymbol sourceKnownEntitySymbol,
        INamedTypeSymbol entityTypeAttributeSymbol,
        ConcurrentDictionary<byte, INamedTypeSymbol> typeMap,
        ConcurrentDictionary<string, INamedTypeSymbol> nameMap,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (typeSymbol.TypeKind == TypeKind.Class && !typeSymbol.IsAbstract && DerivesFrom(typeSymbol, sourceKnownEntitySymbol))
        {
            nameMap.TryAdd(typeSymbol.Name, typeSymbol);

            var entityTypeAttribute = FindAttribute(typeSymbol, entityTypeAttributeSymbol);
            if (entityTypeAttribute != null && TryGetEntityTypeValue(entityTypeAttribute, out var entityTypeValue))
            {
                typeMap.TryAdd(entityTypeValue, typeSymbol);
            }
        }

        foreach (var nestedType in typeSymbol.GetTypeMembers())
        {
            ScanType(nestedType, sourceKnownEntitySymbol, entityTypeAttributeSymbol, typeMap, nameMap, cancellationToken);
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
