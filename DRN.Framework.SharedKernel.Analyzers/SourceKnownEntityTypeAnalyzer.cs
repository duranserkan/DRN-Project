using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DRN.Framework.SharedKernel.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SourceKnownEntityTypeAnalyzer : DiagnosticAnalyzer
{
    private const string SourceKnownEntityMetadataName = "DRN.Framework.SharedKernel.Domain.SourceKnownEntity";
    private const string EntityTypeAttributeMetadataName = "DRN.Framework.SharedKernel.Domain.EntityTypeAttribute";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(
            DiagnosticDescriptors.MissingEntityTypeAttribute,
            DiagnosticDescriptors.DuplicateEntityTypeValue,
            DiagnosticDescriptors.InvalidEntityTypeAttributeUsage);

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

            var entityTypeMap = new ConcurrentDictionary<byte, INamedTypeSymbol>();

            compilationContext.RegisterSymbolAction(symbolContext =>
            {
                var namedType = (INamedTypeSymbol)symbolContext.Symbol;
                if (namedType.TypeKind != TypeKind.Class)
                    return;

                var inheritsSourceKnownEntity = DerivesFrom(namedType, sourceKnownEntitySymbol);
                var entityTypeAttribute = FindAttribute(namedType, entityTypeAttributeSymbol);

                if (inheritsSourceKnownEntity && !namedType.IsAbstract)
                {
                    if (entityTypeAttribute == null)
                    {
                        var location = namedType.Locations.Length > 0 ? namedType.Locations[0] : Location.None;
                        symbolContext.ReportDiagnostic(Diagnostic.Create(
                            DiagnosticDescriptors.MissingEntityTypeAttribute,
                            location,
                            namedType.Name));
                    }
                    else if (entityTypeAttribute.ConstructorArguments.Length > 0 &&
                             entityTypeAttribute.ConstructorArguments[0].Value is IConvertible convertible)
                    {
                        try
                        {
                            var entityTypeValue = Convert.ToByte(convertible);
                            if (!entityTypeMap.TryAdd(entityTypeValue, namedType))
                            {
                                var existingType = entityTypeMap[entityTypeValue];
                                if (!SymbolEqualityComparer.Default.Equals(existingType, namedType))
                                {
                                    var location = entityTypeAttribute.ApplicationSyntaxReference?.GetSyntax().GetLocation()
                                                   ?? (namedType.Locations.Length > 0 ? namedType.Locations[0] : Location.None);

                                    symbolContext.ReportDiagnostic(Diagnostic.Create(
                                        DiagnosticDescriptors.DuplicateEntityTypeValue,
                                        location,
                                        entityTypeValue,
                                        namedType.Name,
                                        existingType.Name));
                                }
                            }
                        }
                        catch (OverflowException)
                        {
                            // Out of byte range, compiler reports type error
                        }
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
        });
    }

    private static bool DerivesFrom(INamedTypeSymbol typeSymbol, INamedTypeSymbol baseTargetSymbol)
    {
        var current = typeSymbol.BaseType;
        while (current != null)
        {
            if (SymbolEqualityComparer.Default.Equals(current, baseTargetSymbol))
                return true;

            current = current.BaseType;
        }

        return false;
    }

    private static AttributeData? FindAttribute(INamedTypeSymbol typeSymbol, INamedTypeSymbol attributeSymbol)
    {
        foreach (var attribute in typeSymbol.GetAttributes())
        {
            if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, attributeSymbol))
                return attribute;
        }

        return null;
    }
}
