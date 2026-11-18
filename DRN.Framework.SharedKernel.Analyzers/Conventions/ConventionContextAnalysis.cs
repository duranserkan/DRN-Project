using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DRN.Framework.SharedKernel.Analyzers.Conventions;

internal static class ConventionContextAnalysis
{
    private const string ContextSuffix = "Context";

    internal static void Analyze(ConventionFacts facts) => Analyze(facts, new ConventionCallSites(facts), null);

    internal static void Analyze(ConventionFacts facts, ConventionCallSites calls, IReadOnlySet<INamedTypeSymbol>? registeredContexts)
    {
        ContextArtifacts? artifacts = null;
        foreach (var type in facts.Types)
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            if (type.TypeKind != TypeKind.Class || type.IsAbstract ||
                !facts.Symbols.IsContext(type, facts.Cancellation)) continue;
            var location = facts.Location(type);
            if (location == Location.None) continue;
            AnalyzeInfraContext(facts, type, location, calls, ref registeredContexts, ref artifacts);
        }
    }

    private static void AnalyzeInfraContext(ConventionFacts facts, INamedTypeSymbol type, Location location, ConventionCallSites calls,
        ref IReadOnlySet<INamedTypeSymbol>? registeredContexts, ref ContextArtifacts? artifacts)
    {
        if (ConventionSymbols.Is(type, facts.Symbols.DbContext, facts.Cancellation) &&
            !ConventionSymbols.Is(type, facts.Symbols.Context, facts.Cancellation))
            facts.Diagnostics.Add(Diagnostic.Create(ConventionDiagnosticDescriptors.ContextContract, location,
                ImmutableDictionary<string, string?>.Empty.Add(ConventionInputSyntax.TargetField, type.GetDocumentationCommentId()),
                type.Name));
        if (facts.Symbols.IsOpen(type, facts.Cancellation) || facts.Role(type).Role != ConventionArchitecture.Infra) return;
        if (!type.Name.EndsWith(ContextSuffix, StringComparison.Ordinal) || type.Name.Length <= ContextSuffix.Length)
            facts.Diagnostics.Add(Diagnostic.Create(ConventionDiagnosticDescriptors.ContextName, location, type.Name));

        var input = facts.ContextInput(type);
        if (!facts.HasInvalidContext(type))
        {
            AnalyzeAlias(facts, type, input, location);
            AnalyzeDestinations(facts, type, input, location, artifacts ??= ReadContextArtifacts(facts));
            registeredContexts ??= calls.ReadEfRegistrations();
            AnalyzeConstruction(facts, type, input, registeredContexts.Contains(type), location);
            // Options may be supplied by another composition path. An empty local delegate is not proof of absence.
            if (!string.IsNullOrWhiteSpace(input?.Get(ConventionInputSyntax.ProviderField)) &&
                string.IsNullOrWhiteSpace(input.Get(ConventionInputSyntax.ReasonField)))
                facts.Diagnostics.Add(ConventionProject.InputIssue(type.Name + ":Provider", "missing its review reason",
                    "reasoned provider declaration with model-validation coverage", input.Location));
        }
        AnalyzeModelOverrides(facts, type);
    }

    private static void AnalyzeAlias(ConventionFacts facts, INamedTypeSymbol type, ConventionInput? input, Location location)
    {
        if (!type.Name.EndsWith(ContextSuffix, StringComparison.Ordinal) || type.Name.Length <= ContextSuffix.Length ||
            facts.Owner(type) is not { } owner || facts.HasInvalidOwner(type) || type.Name[..^7] == owner.Subdomain) return;
        var mapped = input?.Get(ConventionInputSyntax.SubdomainField) == owner.Subdomain &&
            !string.IsNullOrWhiteSpace(input.Get(ConventionInputSyntax.ReasonField)) ||
            type.GetAttributes().Any(attribute => ConventionSymbols.Same(attribute.AttributeClass, facts.Symbols.OwnershipAttribute) &&
                attribute.ConstructorArguments is [{ Value: string name }] && name == owner.Subdomain &&
                !string.IsNullOrWhiteSpace(ConventionFacts.Reason(attribute)));
        if (!mapped)
            facts.Diagnostics.Add(Diagnostic.Create(ConventionDiagnosticDescriptors.InvalidInput, location,
                type.Name + ":" + ConventionInputSyntax.SubdomainField, $"inconsistent with subdomain '{owner.Subdomain}'",
                $"Use subdomain '{owner.Subdomain}' with context '{owner.Subdomain}{ContextSuffix}', or subdomain '{type.Name[..^7]}' with context '{type.Name}'. Align the namespace and folder. Only for an intentional alias, set Subdomain=\"{owner.Subdomain}\" and Reason on its DrnConventionContext item"));
    }

    private static void AnalyzeConstruction(ConventionFacts facts, INamedTypeSymbol type, ConventionInput? input,
        bool registered, Location location)
    {
        if (ValidateCustomFactoryReason(facts, type, input)) return;
        var drnBase = ConventionSymbols.Is(type, facts.Symbols.DrnContext, facts.Cancellation) ||
                      ConventionSymbols.Is(type, facts.Symbols.DrnIdentityContext, facts.Cancellation);
        if ((!drnBase && !registered) || facts.Symbols.ContextOptions is null) return;
        var options = facts.Symbols.ContextOptions.Construct(type);
        var constructors = type.InstanceConstructors.Where(constructor => constructor.DeclaredAccessibility == Accessibility.Public &&
            constructor.Parameters.Length == 1 && constructor.Parameters[0].RefKind == RefKind.None &&
            SymbolEqualityComparer.Default.Equals(constructor.Parameters[0].Type, options)).ToArray();
        if (constructors.Length != 0)
        {
            if (constructors.Length == 1 && DiscardsProviderOptions(facts, type, constructors[0], options) &&
                (registered || HasInheritedDrnFactory(facts, type)))
            {
                // The selected constructor replaces even convention-configured options with a fresh providerless instance.
                facts.Diagnostics.Add(Diagnostic.Create(ConventionDiagnosticDescriptors.Provider, location, type.Name));
            }
            return;
        }
        // EF DI can supply additional services. The DRN design-time factory passes only options.
        if (!drnBase && HasInjectableOptionsConstructor(facts, type, options)) return;
        // Generic constraint errors already explain unusable DRN construction.
        if (HasGenericConstraintDiagnostics(facts, type)) return;
        facts.Diagnostics.Add(Diagnostic.Create(ConventionDiagnosticDescriptors.ContextConstructor, location, type.Name));
    }

    private static bool ValidateCustomFactoryReason(ConventionFacts facts, INamedTypeSymbol type, ConventionInput? input)
    {
        if (string.IsNullOrWhiteSpace(input?.Get(ConventionInputSyntax.FactoryField))) return false;
        if (string.IsNullOrWhiteSpace(input.Get(ConventionInputSyntax.ReasonField)))
            facts.Diagnostics.Add(ConventionProject.InputIssue(type.Name + ":" + ConventionInputSyntax.FactoryField,
                "missing its review reason", "reasoned custom construction declaration", input.Location));
        return true;
    }

    private static bool HasInjectableOptionsConstructor(ConventionFacts facts, INamedTypeSymbol type, INamedTypeSymbol options)
    {
        var nonGenericOptions = facts.Symbols.NonGenericContextOptions;
        return type.InstanceConstructors.Any(constructor =>
            constructor.DeclaredAccessibility == Accessibility.Public &&
            constructor.Parameters.All(parameter => parameter.RefKind == RefKind.None) &&
            constructor.Parameters.Any(parameter => SymbolEqualityComparer.Default.Equals(parameter.Type, options) ||
                SymbolEqualityComparer.Default.Equals(parameter.Type, nonGenericOptions)));
    }

    private static bool HasGenericConstraintDiagnostics(ConventionFacts facts, INamedTypeSymbol type) =>
        type.DeclaringSyntaxReferences.Any(reference => facts.SemanticModel(reference.SyntaxTree)
            .GetDiagnostics(reference.Span, facts.Cancellation).Any(diagnostic => diagnostic.Id is "CS0310" or "CS0311"));

    private static bool HasInheritedDrnFactory(ConventionFacts facts, INamedTypeSymbol type)
    {
        var factory = facts.Symbols.DesignTimeContextFactory;
        if (factory is null) return false;
        foreach (var binding in facts.Symbols.Bindings(type, factory, facts.Cancellation))
        {
            foreach (var member in binding.GetMembers("CreateDbContext"))
                if (type.FindImplementationForInterfaceMember(member) is IMethodSymbol implementation &&
                    (ConventionSymbols.Same(implementation.ContainingType, facts.Symbols.DrnContext) ||
                     ConventionSymbols.Same(implementation.ContainingType, facts.Symbols.DrnIdentityContext))) return true;
        }
        return false;
    }

    private static bool DiscardsProviderOptions(ConventionFacts facts, INamedTypeSymbol type,
        IMethodSymbol constructor, INamedTypeSymbol options)
    {
        if (!ConventionSymbols.Same(type.BaseType, facts.Symbols.DrnContext) &&
            !ConventionSymbols.Same(type.BaseType, facts.Symbols.DrnIdentityContext)) return false;
        if (ConventionSymbols.Bases(type, facts.Cancellation).TakeWhile(current => !ConventionSymbols.Same(current, facts.Symbols.DbContext))
            .Any(current => current.GetMembers("OnConfiguring").OfType<IMethodSymbol>().Any(method => method.IsOverride))) return false;
        if (constructor.DeclaringSyntaxReferences is not [var reference] ||
            reference.GetSyntax(facts.Cancellation) is not ConstructorDeclarationSyntax
            {
                Body.Statements.Count: 0, ExpressionBody: null,
                Initializer: { RawKind: (int)Microsoft.CodeAnalysis.CSharp.SyntaxKind.BaseConstructorInitializer,
                    ArgumentList.Arguments: [var argument] } initializer
            } declaration) return false;
        var model = facts.SemanticModel(reference.SyntaxTree);
        if (model.GetDiagnostics(declaration.Span, facts.Cancellation).Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error) ||
            model.GetSymbolInfo(initializer, facts.Cancellation).Symbol is not IMethodSymbol called ||
            !SymbolEqualityComparer.Default.Equals(called.ContainingType, type.BaseType)) return false;
        if (argument.Expression is LiteralExpressionSyntax literal && literal.Token.Value is null) return true;
        if (argument.Expression is not ObjectCreationExpressionSyntax { Initializer: null, ArgumentList.Arguments.Count: 0 } creation ||
            model.GetSymbolInfo(creation, facts.Cancellation).Symbol is not IMethodSymbol created) return false;
        return SymbolEqualityComparer.Default.Equals(created.ContainingType, options) && created.Parameters.Length == 0;
    }

    private static void AnalyzeDestinations(ConventionFacts facts, INamedTypeSymbol type, ConventionInput? input, Location location,
        ContextArtifacts artifacts)
    {
        var owner = facts.Owner(type);
        if (owner is null || facts.HasInvalidOwner(type)) return;
        var role = facts.Role(type);
        var ns = type.ContainingNamespace.ToDisplayString();
        if (!ns.StartsWith(role.Namespace + ".", StringComparison.Ordinal)) return;
        var area = ns[(role.Namespace.Length + 1)..].Replace('.', '/');
        if (role.PathPrefix.Length > 0) area = role.PathPrefix + "/" + area;
        if (HasAggregateDbSet(facts, type) || artifacts.Repositories.ContainsKey(type) ||
            !string.IsNullOrWhiteSpace(input?.Get(ConventionInputSyntax.RepositoriesField)))
            CheckDestination(facts, type, input, location, artifacts, (ns, area), ConventionInputSyntax.RepositoriesField);
        if (artifacts.Migrations.ContainsKey(type) ||
            !string.IsNullOrWhiteSpace(input?.Get(ConventionInputSyntax.MigrationsField)) ||
            !string.IsNullOrWhiteSpace(input?.Get(ConventionInputSyntax.MigrationsAssemblyField)))
            CheckDestination(facts, type, input, location, artifacts, (ns, area), ConventionInputSyntax.MigrationsField);
    }

    private static void CheckDestination(ConventionFacts facts, INamedTypeSymbol type, ConventionInput? input,
        Location location, ContextArtifacts artifacts, (string Namespace, string Directory) home, string kind)
    {
        var expected = home.Directory + "/" + kind;
        var actual = input?.Get(kind) ?? "";
        if (actual.Length == 0 && HasConventionalDestination(facts, type, input, artifacts, home, kind, expected)) return;
        var valid = ConventionProject.IsLogicalPath(actual);
        var conventional = ConventionProject.NormalizeSeparators(actual) == expected;
        var reasonedOverride = !string.IsNullOrWhiteSpace(input?.Get(ConventionInputSyntax.ReasonField));
        if (valid && (conventional || reasonedOverride)) return;
        facts.Diagnostics.Add(Diagnostic.Create(ConventionDiagnosticDescriptors.Destination,
            input?.Location ?? location,
            properties: ImmutableDictionary<string, string?>.Empty
                .Add(ConventionInputSyntax.TargetField, type.GetDocumentationCommentId())
                .Add(ConventionInputSyntax.ContextKind, ConventionSymbols.MetadataName(type)),
            messageArgs: [type.Name, kind, expected]));
    }

    private static bool HasConventionalDestination(ConventionFacts facts, INamedTypeSymbol type, ConventionInput? input,
        ContextArtifacts artifacts, (string Namespace, string Directory) home, string kind, string expected)
    {
        if (kind == ConventionInputSyntax.MigrationsField &&
            input?.Get(ConventionInputSyntax.MigrationsAssemblyField) is { Length: > 0 } assembly &&
            assembly != type.ContainingAssembly.Name) return false;
        if (!HasLogicalHome(facts, type, home.Directory, ignoreGenerated: true)) return false;
        var index = kind == ConventionInputSyntax.RepositoriesField ? artifacts.Repositories : artifacts.Migrations;
        if (!index.TryGetValue(type, out var associated)) return true;
        var expectedNamespace = home.Namespace + "." + kind;
        foreach (var artifact in associated)
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            if (artifact.ContainingNamespace.ToDisplayString() != expectedNamespace ||
                !HasLogicalHome(facts, artifact, expected)) return false;
        }
        return true;
    }

    [SuppressMessage("SonarQube", "S3267", Justification = "Iterating DeclaringSyntaxReferences directly avoids boxing ImmutableArray and preserves per-reference cancellation.")]
    private static bool HasLogicalHome(ConventionFacts facts, INamedTypeSymbol symbol, string expected, bool ignoreGenerated = false)
    {
        if (symbol.DeclaringSyntaxReferences.Length == 0) return false;
        foreach (var reference in symbol.DeclaringSyntaxReferences)
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            if (ignoreGenerated && facts.IsGenerated(reference.SyntaxTree)) continue;
            if (facts.HasInvalidPath(reference.SyntaxTree) ||
                facts.LogicalDirectory(reference.SyntaxTree)?.Directory != expected) return false;
        }
        return true;
    }

    private sealed class ContextArtifacts
    {
        internal Dictionary<INamedTypeSymbol, List<INamedTypeSymbol>> Repositories { get; } = new(SymbolEqualityComparer.Default);
        internal Dictionary<INamedTypeSymbol, List<INamedTypeSymbol>> Migrations { get; } = new(SymbolEqualityComparer.Default);
    }

    private static ContextArtifacts ReadContextArtifacts(ConventionFacts facts)
    {
        var result = new ContextArtifacts();
        foreach (var type in facts.Types)
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            if (facts.Symbols.IsKnown(type, facts.Symbols.Repository, facts.Cancellation))
                foreach (var binding in facts.Symbols.Bindings(type, facts.Symbols.Repository, facts.Cancellation))
                    if (binding.TypeArguments is [INamedTypeSymbol context, _])
                        AddContextArtifact(facts, result.Repositories, context, type);
            IndexMigrationArtifacts(facts, result.Migrations, type);
        }
        facts.Cancellation.ThrowIfCancellationRequested();
        return result;
    }

    private static void IndexMigrationArtifacts(ConventionFacts facts, Dictionary<INamedTypeSymbol, List<INamedTypeSymbol>> migrations,
        INamedTypeSymbol type)
    {
        if (!facts.Symbols.IsKnown(type, facts.Symbols.Migration, facts.Cancellation) &&
            !facts.Symbols.IsKnown(type, facts.Symbols.Snapshot, facts.Cancellation)) return;
        foreach (var attribute in type.GetAttributes())
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            if (ConventionSymbols.Same(attribute.AttributeClass, facts.Symbols.ContextAttribute) &&
                attribute.ConstructorArguments is [{ Value: INamedTypeSymbol context }])
                AddContextArtifact(facts, migrations, context, type);
        }
    }

    private static void AddContextArtifact(ConventionFacts facts, Dictionary<INamedTypeSymbol, List<INamedTypeSymbol>> index,
        INamedTypeSymbol context, INamedTypeSymbol artifact)
    {
        facts.Cancellation.ThrowIfCancellationRequested();
        if (!index.TryGetValue(context, out var list))
        {
            list = [];
            index.Add(context, list);
        }
        if (list.Count == 0 || !SymbolEqualityComparer.Default.Equals(list[^1], artifact)) list.Add(artifact);
    }

    private static bool HasAggregateDbSet(ConventionFacts facts, INamedTypeSymbol context)
    {
        if (facts.Symbols.DbSet is null || facts.Symbols.Aggregate is null) return false;
        foreach (var type in ConventionSymbols.Bases(context, facts.Cancellation))
        {
            foreach (var member in type.GetMembers())
            {
                facts.Cancellation.ThrowIfCancellationRequested();
                if (member is IPropertySymbol { IsStatic: false, Type: INamedTypeSymbol propertyType } &&
                    ExposesAggregateDbSet(facts, propertyType)) return true;
            }
        }
        return false;
    }

    private static bool ExposesAggregateDbSet(ConventionFacts facts, INamedTypeSymbol propertyType)
    {
        foreach (var binding in facts.Symbols.Bindings(propertyType, facts.Symbols.DbSet, facts.Cancellation))
            if (binding.TypeArguments is [INamedTypeSymbol entity] &&
                facts.Symbols.IsKnown(entity, facts.Symbols.Aggregate, facts.Cancellation)) return true;
        return false;
    }

    private static void AnalyzeModelOverrides(ConventionFacts facts, INamedTypeSymbol type)
    {
        foreach (var method in type.GetMembers("OnModelCreating").OfType<IMethodSymbol>())
        {
            if (RequiresBaseOnModelCreating(facts, method) && facts.Location(method) != Location.None)
                CheckModelOverrideDeclaration(facts, method);
        }
    }

    private static bool RequiresBaseOnModelCreating(ConventionFacts facts, IMethodSymbol method)
    {
        if (!method.IsOverride || method.Parameters is not [{ Type: INamedTypeSymbol parameter }] ||
            !ConventionSymbols.Same(parameter, facts.Symbols.ModelBuilder)) return false;
        for (var overridden = method.OverriddenMethod; overridden is not null; overridden = overridden.OverriddenMethod)
        {
            if (ConventionSymbols.Same(overridden.ContainingType, facts.Symbols.DrnContext) ||
                ConventionSymbols.Same(overridden.ContainingType, facts.Symbols.DrnIdentityContext)) return true;
        }
        return false;
    }

    private static void CheckModelOverrideDeclaration(ConventionFacts facts, IMethodSymbol method)
    {
        foreach (var reference in method.DeclaringSyntaxReferences)
        {
            if (reference.GetSyntax(facts.Cancellation) is not MethodDeclarationSyntax { Body: { } body }) continue;
            var model = facts.SemanticModel(reference.SyntaxTree);
            if (model.GetDiagnostics(body.Span, facts.Cancellation).Any(item => item.Severity == DiagnosticSeverity.Error)) continue;
            var missing = MissingBasePath(facts, model, body, method, method.OverriddenMethod!,
                new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default));
            if (missing is true)
                facts.Diagnostics.Add(Diagnostic.Create(ConventionDiagnosticDescriptors.BaseModel, facts.Location(method), method.Name));
        }
    }

    private enum PathOutcome { Continue, FoundBase, MissingBase, Unknown }

    private static bool? MissingBasePath(ConventionFacts facts, SemanticModel model, BlockSyntax body, IMethodSymbol method, IMethodSymbol requiredBase,
        HashSet<IMethodSymbol> visiting)
    {
        if (!visiting.Add(method) || visiting.Count > 16) return null;
        try
        {
            // Restrict the proof to straight-line bodies. Unsupported flow and calls remain unknown.
            foreach (var statement in body.Statements)
            {
                facts.Cancellation.ThrowIfCancellationRequested();
                switch (EvaluateModelStatement(facts, model, statement, method, requiredBase, visiting))
                {
                    case PathOutcome.FoundBase: return false;
                    case PathOutcome.MissingBase: return true;
                    case PathOutcome.Unknown: return null;
                }
            }
            return true;
        }
        finally
        {
            visiting.Remove(method);
        }
    }

    private static PathOutcome EvaluateModelStatement(ConventionFacts facts, SemanticModel model, StatementSyntax statement,
        IMethodSymbol method, IMethodSymbol requiredBase, HashSet<IMethodSymbol> visiting)
    {
        if (statement is ReturnStatementSyntax { Expression: null }) return PathOutcome.MissingBase;
        if (statement is EmptyStatementSyntax) return PathOutcome.Continue;
        if (statement is not ExpressionStatementSyntax { Expression: InvocationExpressionSyntax invocation } ||
            model.GetSymbolInfo(invocation, facts.Cancellation).Symbol is not IMethodSymbol called)
            return PathOutcome.Unknown;
        if (IsDirectBaseOnModelCreatingCall(facts, model, invocation, called, method, requiredBase))
            return PathOutcome.FoundBase;
        if (!IsInspectableLocalHelper(called, method) || !ForwardsModel(model, invocation, method, facts) ||
            called.DeclaringSyntaxReferences[0].GetSyntax(facts.Cancellation) is not MethodDeclarationSyntax { Body: { } helper })
            return PathOutcome.Unknown;
        var helperResult = MissingBasePath(facts, facts.SemanticModel(helper.SyntaxTree),
            helper, called, requiredBase, visiting);
        return helperResult switch
        {
            null => PathOutcome.Unknown,
            false => PathOutcome.FoundBase,
            true => PathOutcome.Continue
        };
    }

    private static bool IsDirectBaseOnModelCreatingCall(ConventionFacts facts, SemanticModel model,
        InvocationExpressionSyntax invocation, IMethodSymbol called, IMethodSymbol method, IMethodSymbol requiredBase) =>
        invocation.Expression is MemberAccessExpressionSyntax { Expression: BaseExpressionSyntax } &&
        called.Name == "OnModelCreating" && called.Parameters.Length == 1 &&
        SymbolEqualityComparer.Default.Equals(called, requiredBase) && ForwardsModel(model, invocation, method, facts);

    private static bool IsInspectableLocalHelper(IMethodSymbol called, IMethodSymbol caller) =>
        !called.IsVirtual && !called.IsAbstract && !called.IsOverride &&
        SymbolEqualityComparer.Default.Equals(called.ContainingType, caller.ContainingType) &&
        called.DeclaringSyntaxReferences.Length == 1 && called.Parameters.Length == 1;

    private static bool ForwardsModel(SemanticModel model, InvocationExpressionSyntax invocation, IMethodSymbol method,
        ConventionFacts facts) => method.Parameters.Length == 1 && invocation.ArgumentList.Arguments.Count == 1 &&
        SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(invocation.ArgumentList.Arguments[0].Expression,
            facts.Cancellation).Symbol, method.Parameters[0]);
}
