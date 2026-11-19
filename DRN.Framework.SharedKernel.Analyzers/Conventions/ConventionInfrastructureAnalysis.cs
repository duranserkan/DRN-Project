namespace DRN.Framework.SharedKernel.Analyzers.Conventions;

internal static class ConventionInfrastructureAnalysis
{
    internal static void Analyze(ConventionFacts facts)
    {
        foreach (var type in facts.Types)
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            if (type.TypeKind != TypeKind.Class || type.IsAbstract || facts.Symbols.IsOpen(type, facts.Cancellation)) continue;
            var location = facts.Location(type);
            AnalyzeInfraType(facts, type, location);
            AnalyzeMigrationAttributes(facts, type, location);
        }
    }

    private static void AnalyzeInfraType(ConventionFacts facts, INamedTypeSymbol type, Location location)
    {
        if (location == Location.None) return;
        if (facts.Role(type).Role == ConventionArchitecture.Infra)
        {
            if (facts.Symbols.IsKnown(type, facts.Symbols.RepositoryContract, facts.Cancellation))
                CheckFolder(facts, type, "Repositories", location);
            if (facts.Symbols.IsKnown(type, facts.Symbols.Configuration, facts.Cancellation))
                CheckFolder(facts, type, "Configurations", location);
        }
        if (IsDrnContext(facts, type)) CheckOptions(facts, type, location);
    }

    private static void AnalyzeMigrationAttributes(ConventionFacts facts, INamedTypeSymbol type, Location location)
    {
        if (!facts.Symbols.IsKnown(type, facts.Symbols.Migration, facts.Cancellation) &&
            !facts.Symbols.IsKnown(type, facts.Symbols.Snapshot, facts.Cancellation)) return;
        foreach (var attribute in type.GetAttributes())
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            if (!ConventionSymbols.Same(attribute.AttributeClass, facts.Symbols.ContextAttribute) ||
                attribute.ConstructorArguments is not [{ Value: INamedTypeSymbol context }] || !IsDrnContext(facts, context)) continue;
            var anchor = location != Location.None ? location : facts.Location(context);
            if (anchor == Location.None) anchor = facts.ContextInput(context)?.Location ?? Location.None;
            CheckFolder(facts, type, "Migrations", anchor, includeGenerated: true);
        }
    }

    private static bool IsDrnContext(ConventionFacts facts, INamedTypeSymbol type) =>
        facts.Symbols.IsKnown(type, facts.Symbols.DrnContext, facts.Cancellation) ||
        facts.Symbols.IsKnown(type, facts.Symbols.DrnIdentityContext, facts.Cancellation);

    private static void CheckFolder(ConventionFacts facts, INamedTypeSymbol type, string folder, Location location,
        bool includeGenerated = false)
    {
        var conflict = type.ContainingNamespace.Name != folder;
        var missing = false;
        List<Location>? evidence = null;
        foreach (var reference in type.DeclaringSyntaxReferences)
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            var tree = reference.SyntaxTree;
            if (!includeGenerated && facts.IsGenerated(tree) || facts.HasInvalidPath(tree)) continue;
            var directory = facts.LogicalDirectory(tree);
            if (directory is null) { missing = true; continue; }
            if (directory.Value.Name == folder) continue;
            conflict = true;
            (evidence ??= []).Add(reference.GetSyntax(facts.Cancellation).GetLocation());
        }
        // Ordinary ownership analysis already reports missing paths for repositories/configurations.
        if (missing && includeGenerated)
            facts.Diagnostics.Add(ConventionProject.InputIssue(ConventionSymbols.MetadataName(type),
                "missing logical source placement", "Compile or Link path", location));
        if (conflict)
            facts.Diagnostics.Add(Diagnostic.Create(ConventionDiagnosticDescriptors.InfrastructureFolder, location,
                additionalLocations: evidence, properties: Properties(type), messageArgs: [type.Name, folder]));
    }

    private static void CheckOptions(ConventionFacts facts, INamedTypeSymbol context, Location location)
    {
        if (facts.Symbols.ContextOptionsAttribute is null) return;
        var hasDeclaredOptions = context.GetAttributes().Any(attribute =>
            ConventionSymbols.Is(attribute.AttributeClass, facts.Symbols.ContextOptionsAttribute, facts.Cancellation) &&
            !SymbolEqualityComparer.Default.Equals(attribute.AttributeClass?.ContainingAssembly,
                facts.Symbols.ContextOptionsAttribute.ContainingAssembly));
        if (!hasDeclaredOptions)
            facts.Diagnostics.Add(Diagnostic.Create(ConventionDiagnosticDescriptors.ContextOptionsRequired, location,
                Properties(context), context.Name));
        foreach (var attribute in facts.Symbols.EffectiveAttributes(context, facts.Symbols.ContextOptionsAttribute, facts.Cancellation))
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            if (attribute.AttributeClass is not { } options ||
                SymbolEqualityComparer.Default.Equals(options.ContainingAssembly, facts.Symbols.ContextOptionsAttribute.ContainingAssembly)) continue;
            if (IsValidOptionsName(options.Name, context.Name)) continue;
            var attributeLocation = IsDirectContextAttribute(facts, context, attribute)
                ? attribute.ApplicationSyntaxReference!.GetSyntax(facts.Cancellation).GetLocation()
                : location;
            facts.Diagnostics.Add(Diagnostic.Create(ConventionDiagnosticDescriptors.ContextOptionsName, attributeLocation,
                Properties(context), options.Name, context.Name));
        }
    }

    private static bool IsValidOptionsName(string optionsName, string contextName)
    {
        var name = optionsName.EndsWith("Attribute", StringComparison.Ordinal) ? optionsName[..^"Attribute".Length] : optionsName;
        return name.StartsWith(contextName, StringComparison.Ordinal) && name.EndsWith("Options", StringComparison.Ordinal) &&
               name.Length >= contextName.Length + "Options".Length;
    }

    private static bool IsDirectContextAttribute(ConventionFacts facts, INamedTypeSymbol context, AttributeData attribute) =>
        attribute.ApplicationSyntaxReference is { } reference &&
        facts.Compilation.ContainsSyntaxTree(reference.SyntaxTree) && !facts.IsGenerated(reference.SyntaxTree) &&
        context.GetAttributes().Any(declared => declared.ApplicationSyntaxReference is { } declaredRef &&
            ReferenceEquals(declaredRef.SyntaxTree, reference.SyntaxTree) && declaredRef.Span == reference.Span);

    private static ImmutableDictionary<string, string?> Properties(ISymbol symbol) =>
        ImmutableDictionary<string, string?>.Empty.Add("Target", symbol.GetDocumentationCommentId());
}
