using DRN.Framework.SharedKernel.Analyzers.Conventions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DRN.Framework.SharedKernel.CodeFixes;

internal static class ConventionGenerationPreflight
{
    internal static (string Role, string Namespace) Identity(Project project)
    {
        var (role, problem) = ConventionNames.Read(project.Name);
        if (problem.Length > 0 || role.Length == 0) return ("", "");
        project.AnalyzerOptions.AnalyzerConfigOptionsProvider.GlobalOptions.TryGetValue("build_property.RootNamespace", out var ns);
        return (role, string.IsNullOrEmpty(ns) ? project.Name : ns);
    }

    internal static async Task<CompilationUnitSyntax?> ReadInputs(Project project, bool required, CancellationToken token)
    {
        var files = project.AdditionalDocuments.Where(document => document.Name == "DrnConventionInputs.g.cs").Take(2).ToArray();
        if (files.Length == 0) return required ? null : SyntaxFactory.CompilationUnit();
        if (files.Length != 1) return null;
        var root = ConventionInputSyntax.Parse(await files[0].GetTextAsync(token), files[0].FilePath ?? files[0].Name, token, out _);
        return root is not null && ConventionInputSyntax.SupportsGeneration(root, token) ? root : null;
    }

    internal static bool SymbolCollision(Compilation compilation, string ns, string name, CancellationToken token)
    {
        INamespaceSymbol[] parents = [compilation.GlobalNamespace];
        foreach (var segment in string.IsNullOrEmpty(ns) ? [] : ns.Split('.'))
        {
            token.ThrowIfCancellationRequested();
            var matches = parents.SelectMany(parent => parent.GetMembers())
                .Where(member => string.Equals(member.Name, segment, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (matches.Any(member => member is INamedTypeSymbol)) return true;
            parents = matches.OfType<INamespaceSymbol>().ToArray();
            if (parents.Length == 0) return false;
        }
        return parents.SelectMany(parent => parent.GetMembers()).Any(member =>
            string.Equals(member.Name, name, StringComparison.OrdinalIgnoreCase));
    }
}
