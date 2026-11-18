using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DRN.Framework.SharedKernel.Analyzers.Conventions;

// Persistence and context checks request EF evidence first. An eligible DI check reuses the same walk.
internal sealed class ConventionCallSites(ConventionFacts facts, bool includeCompositionWithEf = false)
{
    internal delegate void ConfigurationCall(SemanticModel model, InvocationExpressionSyntax call,
        IMethodSymbol method, IMethodSymbol owner, TypeInfo? boundReceiver);

    private ConventionCompositionReader.Composition? _composition;

    internal ConventionCompositionReader.Composition ReadComposition()
    {
        facts.Cancellation.ThrowIfCancellationRequested();
        if (_composition is null) Scan(null, null, includeComposition: true);
        return _composition ??= new();
    }

    internal IReadOnlySet<INamedTypeSymbol> ReadEfRegistrations(ConfigurationCall? visitConfigurationCall = null)
    {
        var registeredContexts = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        Scan(registeredContexts, visitConfigurationCall, includeCompositionWithEf);
        return registeredContexts;
    }

    private void Scan(HashSet<INamedTypeSymbol>? registeredContexts, ConfigurationCall? visitConfigurationCall, bool includeComposition)
    {
        facts.Cancellation.ThrowIfCancellationRequested();
        var canRegister = registeredContexts is not null && (facts.Symbols.EfServiceCollectionExtensions is not null ||
                          facts.Symbols.DrnEfServiceCollectionExtensions is not null);
        var readComposition = includeComposition && _composition is null && (facts.Symbols.ServiceCollection is not null ||
            facts.Symbols.DiServiceCollectionExtensions is not null || facts.Symbols.DrnDiServiceCollectionExtensions is not null);
        if (!readComposition && !canRegister && visitConfigurationCall is null) return;
        var composition = readComposition ? new ConventionCompositionReader(facts) : null;

        foreach (var tree in facts.Compilation.SyntaxTrees)
            ScanTree(tree, composition, canRegister ? registeredContexts : null, visitConfigurationCall);
        var evidence = composition?.Complete();
        facts.Cancellation.ThrowIfCancellationRequested();
        if (evidence is not null) _composition = evidence;
    }

    private void ScanTree(SyntaxTree tree, ConventionCompositionReader? composition, HashSet<INamedTypeSymbol>? registeredContexts,
        ConfigurationCall? visitConfigurationCall)
    {
        facts.Cancellation.ThrowIfCancellationRequested();
        SemanticModel? model = null;
        foreach (var node in tree.GetRoot(facts.Cancellation).DescendantNodes())
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            if (composition is not null && node is BaseObjectCreationExpressionSyntax { ArgumentList.Arguments.Count: > 0 } creation)
                composition.InspectCreation(model ??= facts.SemanticModel(tree), creation);
            if (node is not InvocationExpressionSyntax call) continue;
            var callRegistrations = RegistrationTarget(call, registeredContexts);
            var callConfiguration = ConfigurationTarget(call, visitConfigurationCall);
            if (composition is null && callRegistrations is null && callConfiguration is null) continue;

            model ??= facts.SemanticModel(tree);
            InspectInvocation(model, call, composition, callRegistrations, callConfiguration);
        }
    }

    private static HashSet<INamedTypeSymbol>? RegistrationTarget(InvocationExpressionSyntax call, HashSet<INamedTypeSymbol>? registeredContexts) =>
        registeredContexts is not null && InvocationName(call) is ("AddDbContext" or "AddDbContextWithConventions") ? registeredContexts : null;

    private static ConfigurationCall? ConfigurationTarget(InvocationExpressionSyntax call, ConfigurationCall? visitConfigurationCall) =>
        visitConfigurationCall is not null && call.ArgumentList.Arguments.Count == 1 ? visitConfigurationCall : null;

    private void InspectInvocation(SemanticModel model, InvocationExpressionSyntax call, ConventionCompositionReader? composition,
        HashSet<INamedTypeSymbol>? registeredContexts, ConfigurationCall? visitConfigurationCall)
    {
        var method = model.GetSymbolInfo(call, facts.Cancellation).Symbol as IMethodSymbol;
        var scope = method is not null && (composition is not null || visitConfigurationCall is not null)
            ? model.GetEnclosingSymbol(call.SpanStart, facts.Cancellation) : null;
        var boundReceiver = composition?.InspectInvocation(model, call, method, scope);
        if (method is null) return;
        if (registeredContexts is not null && IsRegistrationMethod(method, facts.Symbols))
        {
            if (method.TypeArguments is [INamedTypeSymbol context]) registeredContexts.Add(context);
            else if (method.Name == "AddDbContext" && method.TypeArguments is [_, INamedTypeSymbol implementation])
                registeredContexts.Add(implementation);
        }
        if (scope is IMethodSymbol owner) visitConfigurationCall?.Invoke(model, call, method, owner, boundReceiver);
    }

    private static bool IsRegistrationMethod(IMethodSymbol method, ConventionSymbols symbols) => method.Name switch
    {
        "AddDbContext" => ConventionSymbols.Same(method.ContainingType, symbols.EfServiceCollectionExtensions),
        "AddDbContextWithConventions" => ConventionSymbols.Same(method.ContainingType, symbols.DrnEfServiceCollectionExtensions),
        _ => false
    };

    private static string? InvocationName(InvocationExpressionSyntax invocation) => invocation.Expression switch
    {
        SimpleNameSyntax simple => simple.Identifier.ValueText,
        MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText,
        MemberBindingExpressionSyntax binding => binding.Name.Identifier.ValueText,
        _ => null
    };
}
