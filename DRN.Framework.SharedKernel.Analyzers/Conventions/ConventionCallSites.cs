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
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            SemanticModel? model = null;
            foreach (var node in tree.GetRoot(facts.Cancellation).DescendantNodes())
            {
                facts.Cancellation.ThrowIfCancellationRequested();
                if (composition is not null && node is BaseObjectCreationExpressionSyntax { ArgumentList.Arguments.Count: > 0 } creation)
                    composition.InspectCreation(model ??= facts.SemanticModel(tree), creation);
                if (node is not InvocationExpressionSyntax call) continue;
                var registrationCandidate = canRegister && InvocationName(call) is ("AddDbContext" or "AddDbContextWithConventions");
                var configurationCandidate = visitConfigurationCall is not null && call.ArgumentList.Arguments.Count == 1;
                if (composition is null && !registrationCandidate && !configurationCandidate) continue;

                model ??= facts.SemanticModel(tree);
                InspectInvocation(model, call, composition, registrationCandidate ? registeredContexts : null,
                    configurationCandidate ? visitConfigurationCall : null);
            }
        }
        var evidence = composition?.Complete();
        facts.Cancellation.ThrowIfCancellationRequested();
        if (evidence is not null) _composition = evidence;
    }

    private void InspectInvocation(SemanticModel model, InvocationExpressionSyntax call, ConventionCompositionReader? composition,
        HashSet<INamedTypeSymbol>? registeredContexts, ConfigurationCall? visitConfigurationCall)
    {
        var method = model.GetSymbolInfo(call, facts.Cancellation).Symbol as IMethodSymbol;
        var scope = method is not null && (composition is not null || visitConfigurationCall is not null)
            ? model.GetEnclosingSymbol(call.SpanStart, facts.Cancellation) : null;
        var boundReceiver = composition?.InspectInvocation(model, call, method, scope);
        if (method is null) return;
        if (registeredContexts is not null && method.TypeArguments is [INamedTypeSymbol context] &&
            IsRegistrationMethod(method, facts.Symbols))
            registeredContexts.Add(context);
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
