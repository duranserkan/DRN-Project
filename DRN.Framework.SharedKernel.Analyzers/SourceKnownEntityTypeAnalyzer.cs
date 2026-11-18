using System.Collections.Concurrent;
using System.Collections.Immutable;
using DRN.Framework.CompilerTools;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DRN.Framework.SharedKernel.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SourceKnownEntityTypeAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
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
        context.RegisterCompilationStartAction(start =>
        {
            var entity = start.Compilation.GetTypeByMetadataName(EntityAnalyzerHelper.SourceKnownEntityMetadataName);
            var attribute = start.Compilation.GetTypeByMetadataName(EntityAnalyzerHelper.EntityTypeAttributeMetadataName);
            if (entity == null || attribute == null) return;

            var facts = EntityAnalyzerHelper.EntityTypeFacts.For(start.Compilation, entity);
            var usage = SourceKnownEntityUsageAnalysis.Register(start, entity, facts);
            UninitializedEntityOperationsAnalysis.Register(start, entity, facts);
            var metadata = new EntityAnalyzerHelper.CompilationMetadata(start.Compilation);
            var declarations = new ConcurrentBag<EntityDeclaration>();
            var invalidPartitions = new ConcurrentBag<(INamedTypeSymbol App, Diagnostic Problem)>();
            var reportedPartitions = new ConcurrentDictionary<INamedTypeSymbol, byte>(SymbolEqualityComparer.Default);
            start.RegisterSymbolAction(symbol =>
            {
                CollectInvalidPartition(symbol, metadata, invalidPartitions);
                AnalyzeNamedType(symbol, facts, usage, attribute, metadata, declarations, reportedPartitions);
            }, SymbolKind.NamedType);
            start.RegisterCompilationEndAction(end =>
            {
                foreach (var (app, problem) in invalidPartitions)
                {
                    end.CancellationToken.ThrowIfCancellationRequested();
                    if (!reportedPartitions.ContainsKey(app))
                        end.ReportDiagnostic(problem);
                }
                AnalyzeCompilationEnd(end, entity, facts, attribute, metadata, declarations);
            });
        });
    }

    private static void CollectInvalidPartition(SymbolAnalysisContext symbol, EntityAnalyzerHelper.CompilationMetadata metadata,
        ConcurrentBag<(INamedTypeSymbol App, Diagnostic Problem)> invalidPartitions)
    {
        var type = (INamedTypeSymbol)symbol.Symbol;
        if (type.TypeKind is not (TypeKind.Class or TypeKind.Struct) || type.IsAbstract || !metadata.Partitions.ImplementsAppId(type)) return;
        var appId = metadata.Partitions.ReadPartition(type, symbol.CancellationToken);
        if (appId is <= EntityAnalyzerHelper.MaxAppId || GeneratedSource.FirstUserLocation(type, symbol) is not { } location) return;
        var problem = appId.HasValue
            ? Diagnostic.Create(DiagnosticDescriptors.AppIdOutOfRange, location, type.Name, appId.Value)
            : Diagnostic.Create(DiagnosticDescriptors.UnresolvableAppId, location, type.Name);
        invalidPartitions.Add((type, problem));
    }

    private static void AnalyzeNamedType(SymbolAnalysisContext context, EntityAnalyzerHelper.EntityTypeFacts facts,
        SourceKnownEntityUsageAnalysis usage, INamedTypeSymbol attributeType,
        EntityAnalyzerHelper.CompilationMetadata metadata, ConcurrentBag<EntityDeclaration> declarations,
        ConcurrentDictionary<INamedTypeSymbol, byte> reportedPartitions)
    {
        var type = (INamedTypeSymbol)context.Symbol;
        context.CancellationToken.ThrowIfCancellationRequested();
        if (type.TypeKind != TypeKind.Class) return;

        // Validate attribute producers even when no entity uses their attribute yet.
        var unsupportedProducer = EntityAnalyzerHelper.IsOrDerivesFrom(type.BaseType, attributeType, context.CancellationToken) &&
            !metadata.IsSupported(type, context.CancellationToken);
        var derived = facts.IsDerived(type, context.CancellationToken);
        if (derived) usage.AnalyzeHiddenIdentityMembers(context, type);
        var eligible = derived && !type.IsAbstract && !EntityAnalyzerHelper.IsEffectivelyPrivate(type);
        var attribute = EntityAnalyzerHelper.FindAttribute(type, attributeType, context.CancellationToken);
        if (!unsupportedProducer && !eligible && attribute == null) return;
        if (GeneratedSource.FirstUserLocation(type, context) is not { } location) return;
        if (unsupportedProducer)
            context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.UnsupportedEntityAttribute, location, type.Name));
        if (eligible && type.IsGenericType)
            context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.GenericConcreteEntity, location, type.ToDisplayString()));
        if (attribute == null)
        {
            if (eligible) context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.MissingEntityTypeAttribute, location, type.Name));
            return;
        }
        AnalyzeEntityAttribute(context, attribute, location, eligible, metadata, declarations, reportedPartitions);
    }

    private static void AnalyzeEntityAttribute(SymbolAnalysisContext context, AttributeData attribute, Location location, bool eligible,
        EntityAnalyzerHelper.CompilationMetadata metadata, ConcurrentBag<EntityDeclaration> declarations,
        ConcurrentDictionary<INamedTypeSymbol, byte> reportedPartitions)
    {
        var type = (INamedTypeSymbol)context.Symbol;
        var attributeLocation = AttributeLocation(context, attribute, location);
        if (!eligible)
        {
            context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.InvalidEntityTypeAttributeUsage, attributeLocation, type.Name));
            return;
        }

        var declaration = EntityDeclaration.Read(type, attribute, location, attributeLocation, metadata, context.CancellationToken, out var problem);
        if (problem != null)
        {
            context.ReportDiagnostic(problem);
            if ((problem.Descriptor == DiagnosticDescriptors.UnresolvableAppId || problem.Descriptor == DiagnosticDescriptors.AppIdOutOfRange) &&
                attribute.AttributeClass is { } attributeClass &&
                metadata.Partitions.PartitionType(attributeClass, context.CancellationToken) is { } app)
                reportedPartitions.TryAdd(app.OriginalDefinition, 0);
        }
        if (declaration != null) declarations.Add(declaration);
    }

    private static Location AttributeLocation(SymbolAnalysisContext context, AttributeData attribute, Location fallback)
    {
        if (attribute.ApplicationSyntaxReference is { } reference && (reference.SyntaxTree == fallback.SourceTree ||
            !GeneratedSource.IsGenerated(reference.SyntaxTree, context.Compilation, context.Options.AnalyzerConfigOptionsProvider, context.CancellationToken)))
            return reference.GetSyntax(context.CancellationToken).GetLocation();
        return fallback;
    }

    private static void AnalyzeCompilationEnd(CompilationAnalysisContext context, INamedTypeSymbol entity,
        EntityAnalyzerHelper.EntityTypeFacts facts, INamedTypeSymbol attributeType,
        EntityAnalyzerHelper.CompilationMetadata metadata, ConcurrentBag<EntityDeclaration> localDeclarations)
    {
        var declarations = localDeclarations.ToList();
        Dictionary<Compilation, (INamedTypeSymbol? Attribute, EntityAnalyzerHelper.CompilationMetadata Metadata)>? producerContracts = null;
        foreach (var (type, producer) in ReferencedEntityDiscovery.Find(context.Compilation, entity, facts, context.CancellationToken))
        {
            var contract = (Attribute: (INamedTypeSymbol?)attributeType, Metadata: metadata);
            if (producer is not null)
            {
                producerContracts ??= new();
                if (!producerContracts.TryGetValue(producer, out contract))
                {
                    contract = (producer.GetTypeByMetadataName(EntityAnalyzerHelper.EntityTypeAttributeMetadataName), new(producer));
                    producerContracts.Add(producer, contract);
                }
            }
            if (contract.Attribute is null ||
                EntityAnalyzerHelper.FindAttribute(type, contract.Attribute, context.CancellationToken) is not { } attribute) continue;
            var declaration = EntityDeclaration.Read(type, attribute, Location.None, Location.None, contract.Metadata, context.CancellationToken, out var problem);
            if (problem != null) context.ReportDiagnostic(problem);
            if (declaration != null) declarations.Add(declaration);
        }
        EntityCollisionAnalysis.Report(context, declarations);
    }
}
