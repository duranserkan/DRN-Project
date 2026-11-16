using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DRN.Framework.SharedKernel.Analyzers.Conventions;

internal static class ConventionContextAnalysis
{
    internal static void Analyze(ConventionFacts facts)
    {
        var registrations = ReadRegistrations(facts);
        var artifacts = ReadContextArtifacts(facts);
        foreach (var type in facts.Types)
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            if (type.TypeKind != TypeKind.Class || type.IsAbstract ||
                !facts.Symbols.IsContext(type, facts.Cancellation)) continue;
            var location = facts.Location(type);
            if (location == Location.None) continue;
            if (ConventionSymbols.Is(type, facts.Symbols.DbContext, facts.Cancellation) &&
                !ConventionSymbols.Is(type, facts.Symbols.Context, facts.Cancellation))
                facts.Diagnostics.Add(Diagnostic.Create(ConventionDiagnosticDescriptors.ContextContract, location,
                    System.Collections.Immutable.ImmutableDictionary<string, string?>.Empty
                        .Add("Target", type.GetDocumentationCommentId()), type.Name));
            if (ConventionSymbols.IsOpen(type, facts.Cancellation) || facts.Role(type).Role != "Infra") continue;
            if (!type.Name.EndsWith("Context", StringComparison.Ordinal) || type.Name.Length <= "Context".Length)
                facts.Diagnostics.Add(Diagnostic.Create(ConventionDiagnosticDescriptors.ContextName, location, type.Name));

            var input = facts.ContextInput(type);
            if (!facts.HasInvalidContext(type))
            {
                AnalyzeAlias(facts, type, input, location);
                AnalyzeDestinations(facts, type, input, location, artifacts);
                registrations.TryGetValue(type, out var registration);
                AnalyzeConstruction(facts, type, input, registration, location);
                // Options may be supplied by another composition path. An empty local delegate is not proof of absence.
                if (!string.IsNullOrWhiteSpace(input?.Get("Provider")) && string.IsNullOrWhiteSpace(input.Get("Reason")))
                    facts.Diagnostics.Add(ConventionProject.InputIssue(type.Name + ":Provider", "missing its review reason",
                        "reasoned provider declaration with model-validation coverage", input.Location));
            }
            AnalyzeModelOverrides(facts, type);
        }
    }

    private static void AnalyzeAlias(ConventionFacts facts, INamedTypeSymbol type, ConventionInput? input, Location location)
    {
        if (!type.Name.EndsWith("Context", StringComparison.Ordinal) || type.Name.Length <= "Context".Length ||
            facts.Owner(type) is not { } owner || facts.HasInvalidOwner(type) || type.Name[..^7] == owner.Subdomain) return;
        var mapped = input?.Get("Subdomain") == owner.Subdomain && !string.IsNullOrWhiteSpace(input.Get("Reason")) ||
            type.GetAttributes().Any(attribute => ConventionSymbols.Same(attribute.AttributeClass, facts.Symbols.OwnershipAttribute) &&
                attribute.ConstructorArguments is [{ Value: string name }] && name == owner.Subdomain &&
                !string.IsNullOrWhiteSpace(ConventionFacts.Reason(attribute)));
        if (!mapped)
            facts.Diagnostics.Add(Diagnostic.Create(ConventionDiagnosticDescriptors.InvalidInput, location,
                type.Name + ":Subdomain", $"inconsistent with subdomain '{owner.Subdomain}'",
                $"Use subdomain '{owner.Subdomain}' with context '{owner.Subdomain}Context', or subdomain '{type.Name[..^7]}' with context '{type.Name}'. Align the namespace and folder. Only for an intentional alias, set Subdomain=\"{owner.Subdomain}\" and Reason on its DrnConventionContext item"));
    }

    private static void AnalyzeConstruction(ConventionFacts facts, INamedTypeSymbol type, ConventionInput? input,
        Registration registration, Location location)
    {
        if (!string.IsNullOrWhiteSpace(input?.Get("Factory")))
        {
            if (string.IsNullOrWhiteSpace(input.Get("Reason")))
                facts.Diagnostics.Add(ConventionProject.InputIssue(type.Name + ":Factory", "missing its review reason",
                    "reasoned custom construction declaration", input.Location));
            return;
        }
        var drnBase = ConventionSymbols.Is(type, facts.Symbols.DrnContext, facts.Cancellation) ||
                      ConventionSymbols.Is(type, facts.Symbols.DrnIdentityContext, facts.Cancellation);
        if ((!drnBase && !registration.Selected) || facts.Symbols.ContextOptions is null) return;
        var options = facts.Symbols.ContextOptions.Construct(type);
        var constructors = type.InstanceConstructors.Where(constructor => constructor.DeclaredAccessibility == Accessibility.Public &&
            constructor.Parameters.Length == 1 && constructor.Parameters[0].RefKind == RefKind.None &&
            SymbolEqualityComparer.Default.Equals(constructor.Parameters[0].Type, options)).ToArray();
        if (constructors.Length != 0)
        {
            if (constructors.Length == 1 && DiscardsProviderOptions(facts, type, constructors[0], options) &&
                (registration.Selected || HasInheritedDrnFactory(facts, type)))
            {
                // The selected constructor replaces even convention-configured options with a fresh providerless instance.
                facts.Diagnostics.Add(Diagnostic.Create(ConventionDiagnosticDescriptors.Provider, location, type.Name));
            }
            return;
        }
        // EF DI can supply additional services. The DRN design-time factory passes only options.
        var nonGenericOptions = facts.Compilation.GetTypeByMetadataName("Microsoft.EntityFrameworkCore.DbContextOptions");
        var hasInjectableOptionsConstructor = type.InstanceConstructors.Any(constructor =>
            constructor.DeclaredAccessibility == Accessibility.Public &&
            constructor.Parameters.All(parameter => parameter.RefKind == RefKind.None) &&
            constructor.Parameters.Any(parameter => SymbolEqualityComparer.Default.Equals(parameter.Type, options) ||
                SymbolEqualityComparer.Default.Equals(parameter.Type, nonGenericOptions)));
        if (!drnBase && hasInjectableOptionsConstructor) return;
        // Generic constraint errors already explain unusable DRN construction.
        if (type.DeclaringSyntaxReferences.Any(reference => facts.Compilation.GetSemanticModel(reference.SyntaxTree)
                .GetDiagnostics(reference.Span, facts.Cancellation).Any(diagnostic => diagnostic.Id is "CS0310" or "CS0311"))) return;
        facts.Diagnostics.Add(Diagnostic.Create(ConventionDiagnosticDescriptors.ContextConstructor, location, type.Name));
    }

    private static bool HasInheritedDrnFactory(ConventionFacts facts, INamedTypeSymbol type)
    {
        var factory = facts.Compilation.GetTypeByMetadataName("Microsoft.EntityFrameworkCore.Design.IDesignTimeDbContextFactory`1");
        if (factory is null) return false;
        foreach (var binding in ConventionSymbols.Bindings(type, factory, facts.Cancellation))
        foreach (var member in binding.GetMembers("CreateDbContext"))
            if (type.FindImplementationForInterfaceMember(member) is IMethodSymbol implementation &&
                (ConventionSymbols.Same(implementation.ContainingType, facts.Symbols.DrnContext) ||
                 ConventionSymbols.Same(implementation.ContainingType, facts.Symbols.DrnIdentityContext))) return true;
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
        var model = facts.Compilation.GetSemanticModel(reference.SyntaxTree);
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
            !string.IsNullOrWhiteSpace(input?.Get("Repositories"))) CheckDestination("Repositories");
        if (artifacts.Migrations.ContainsKey(type) || !string.IsNullOrWhiteSpace(input?.Get("Migrations")) ||
            !string.IsNullOrWhiteSpace(input?.Get("MigrationsAssembly")))
            CheckDestination("Migrations");

        void CheckDestination(string kind)
        {
            var expected = area + "/" + kind;
            var actual = input?.Get(kind) ?? "";
            if (actual.Length == 0 && HasConventionalDestination(kind, expected)) return;
            var valid = ConventionProject.IsLogicalPath(actual);
            var conventional = ConventionProject.NormalizeSeparators(actual) == expected;
            var reasonedOverride = !string.IsNullOrWhiteSpace(input?.Get("Reason"));
            if (valid && (conventional || reasonedOverride)) return;
            facts.Diagnostics.Add(Diagnostic.Create(ConventionDiagnosticDescriptors.Destination,
                input?.Location ?? location,
                properties: System.Collections.Immutable.ImmutableDictionary<string, string?>.Empty
                    .Add("Target", type.GetDocumentationCommentId()).Add("Context", ConventionSymbols.MetadataName(type)),
                messageArgs: [type.Name, kind, expected]));
        }

        bool HasConventionalDestination(string kind, string expected)
        {
            if (kind == "Migrations" && input?.Get("MigrationsAssembly") is { Length: > 0 } assembly &&
                assembly != type.ContainingAssembly.Name) return false;
            if (!HasLogicalHome(type, area)) return false;
            var index = kind == "Repositories" ? artifacts.Repositories : artifacts.Migrations;
            if (!index.TryGetValue(type, out var associated)) return true;
            var expectedNamespace = ns + "." + kind;
            foreach (var artifact in associated)
            {
                facts.Cancellation.ThrowIfCancellationRequested();
                if (artifact.ContainingNamespace.ToDisplayString() != expectedNamespace ||
                    !HasLogicalHome(artifact, expected)) return false;
            }
            return true;
        }

        bool HasLogicalHome(INamedTypeSymbol symbol, string expected)
        {
            if (symbol.DeclaringSyntaxReferences.Length == 0) return false;
            foreach (var reference in symbol.DeclaringSyntaxReferences)
            {
                facts.Cancellation.ThrowIfCancellationRequested();
                if (facts.IsGenerated(reference.SyntaxTree)) continue;
                if (facts.HasInvalidPath(reference.SyntaxTree) ||
                    facts.LogicalDirectory(reference.SyntaxTree)?.Directory != expected) return false;
            }
            return true;
        }
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
                foreach (var binding in ConventionSymbols.Bindings(type, facts.Symbols.Repository, facts.Cancellation))
                    if (binding.TypeArguments is [INamedTypeSymbol context, _]) Add(result.Repositories, context, type);
            if (!facts.Symbols.IsKnown(type, facts.Symbols.Migration, facts.Cancellation) &&
                !facts.Symbols.IsKnown(type, facts.Symbols.Snapshot, facts.Cancellation)) continue;
            foreach (var attribute in type.GetAttributes())
            {
                facts.Cancellation.ThrowIfCancellationRequested();
                if (ConventionSymbols.Same(attribute.AttributeClass, facts.Symbols.ContextAttribute) &&
                    attribute.ConstructorArguments is [{ Value: INamedTypeSymbol context }])
                    Add(result.Migrations, context, type);
            }
        }
        facts.Cancellation.ThrowIfCancellationRequested();
        return result;

        void Add(Dictionary<INamedTypeSymbol, List<INamedTypeSymbol>> index, INamedTypeSymbol context, INamedTypeSymbol artifact)
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            if (!index.TryGetValue(context, out var list)) index.Add(context, list = []);
            if (list.Count == 0 || !SymbolEqualityComparer.Default.Equals(list[^1], artifact)) list.Add(artifact);
        }
    }

    private static bool HasAggregateDbSet(ConventionFacts facts, INamedTypeSymbol context)
    {
        if (facts.Symbols.DbSet is null || facts.Symbols.Aggregate is null) return false;
        foreach (var type in ConventionSymbols.Bases(context, facts.Cancellation))
        foreach (var member in type.GetMembers())
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            if (member is not IPropertySymbol { IsStatic: false, Type: INamedTypeSymbol propertyType }) continue;
            foreach (var binding in ConventionSymbols.Bindings(propertyType, facts.Symbols.DbSet, facts.Cancellation))
                if (binding.TypeArguments is [INamedTypeSymbol entity] &&
                    facts.Symbols.IsKnown(entity, facts.Symbols.Aggregate, facts.Cancellation)) return true;
        }
        return false;
    }

    private readonly record struct Registration(bool Selected);

    private static Dictionary<INamedTypeSymbol, Registration> ReadRegistrations(ConventionFacts facts)
    {
        var result = new Dictionary<INamedTypeSymbol, Registration>(SymbolEqualityComparer.Default);
        var efExtensions = facts.Compilation.GetTypeByMetadataName("Microsoft.Extensions.DependencyInjection.EntityFrameworkServiceCollectionExtensions");
        var drnExtensions = facts.Compilation.GetTypeByMetadataName("DRN.Framework.EntityFramework.Extensions.ServiceCollectionExtensions");
        foreach (var tree in facts.Compilation.SyntaxTrees)
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            SemanticModel? model = null;
            foreach (var invocation in tree.GetRoot(facts.Cancellation).DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                facts.Cancellation.ThrowIfCancellationRequested();
                var name = invocation.Expression switch
                {
                    SimpleNameSyntax simple => simple.Identifier.ValueText,
                    MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText,
                    MemberBindingExpressionSyntax binding => binding.Name.Identifier.ValueText,
                    _ => null
                };
                if (name is not null and not ("AddDbContext" or "AddDbContextWithConventions")) continue;
                model ??= facts.Compilation.GetSemanticModel(tree);
                if (model.GetSymbolInfo(invocation, facts.Cancellation).Symbol is not IMethodSymbol method ||
                    method.TypeArguments is not [{ } argument] || argument is not INamedTypeSymbol context) continue;
                var drn = method.Name == "AddDbContextWithConventions" && ConventionSymbols.Same(method.ContainingType, drnExtensions);
                var ef = method.Name == "AddDbContext" && ConventionSymbols.Same(method.ContainingType, efExtensions);
                if (!drn && !ef) continue;
                result[context] = new(true);
            }
        }
        return result;
    }

    private static void AnalyzeModelOverrides(ConventionFacts facts, INamedTypeSymbol type)
    {
        foreach (var method in type.GetMembers("OnModelCreating").OfType<IMethodSymbol>())
        {
            if (!method.IsOverride || method.Parameters is not [{ Type: INamedTypeSymbol parameter }] ||
                !ConventionSymbols.Same(parameter, facts.Symbols.ModelBuilder)) continue;
            var overridden = method.OverriddenMethod;
            var requiresBase = false;
            while (overridden is not null)
            {
                if (ConventionSymbols.Same(overridden.ContainingType, facts.Symbols.DrnContext) ||
                    ConventionSymbols.Same(overridden.ContainingType, facts.Symbols.DrnIdentityContext)) requiresBase = true;
                overridden = overridden.OverriddenMethod;
            }
            if (!requiresBase || facts.Location(method) == Location.None) continue;
            foreach (var reference in method.DeclaringSyntaxReferences)
            {
                if (reference.GetSyntax(facts.Cancellation) is not MethodDeclarationSyntax { Body: { } body }) continue;
                var model = facts.Compilation.GetSemanticModel(reference.SyntaxTree);
                if (model.GetDiagnostics(body.Span, facts.Cancellation).Any(item => item.Severity == DiagnosticSeverity.Error)) continue;
                var missing = MissingBasePath(facts, model, body, method, method.OverriddenMethod!,
                    new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default));
                if (missing is true)
                    facts.Diagnostics.Add(Diagnostic.Create(ConventionDiagnosticDescriptors.BaseModel, facts.Location(method), method.Name));
            }
        }
    }

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
                if (statement is ReturnStatementSyntax { Expression: null }) return true;
                if (statement is EmptyStatementSyntax) continue;
                if (statement is not ExpressionStatementSyntax { Expression: InvocationExpressionSyntax invocation }) return null;
                if (model.GetSymbolInfo(invocation, facts.Cancellation).Symbol is not IMethodSymbol called) return null;
                if (invocation.Expression is MemberAccessExpressionSyntax { Expression: BaseExpressionSyntax } &&
                    called.Name == "OnModelCreating" && called.Parameters.Length == 1 &&
                    SymbolEqualityComparer.Default.Equals(called, requiredBase) && ForwardsModel(model, invocation, method, facts)) return false;
                if (called.IsVirtual || called.IsAbstract || called.IsOverride ||
                    !SymbolEqualityComparer.Default.Equals(called.ContainingType, method.ContainingType) ||
                    called.DeclaringSyntaxReferences.Length != 1 || called.Parameters.Length != 1 ||
                    !ForwardsModel(model, invocation, method, facts)) return null;
                if (called.DeclaringSyntaxReferences[0].GetSyntax(facts.Cancellation) is not MethodDeclarationSyntax { Body: { } helper }) return null;
                var helperResult = MissingBasePath(facts, facts.Compilation.GetSemanticModel(helper.SyntaxTree), helper, called, requiredBase, visiting);
                if (helperResult is null) return null;
                if (helperResult is false) return false;
            }
            return true;
        }
        finally
        {
            visiting.Remove(method);
        }
    }

    private static bool ForwardsModel(SemanticModel model, InvocationExpressionSyntax invocation, IMethodSymbol method,
        ConventionFacts facts) => method.Parameters.Length == 1 && invocation.ArgumentList.Arguments.Count == 1 &&
        SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(invocation.ArgumentList.Arguments[0].Expression,
            facts.Cancellation).Symbol, method.Parameters[0]);
}
