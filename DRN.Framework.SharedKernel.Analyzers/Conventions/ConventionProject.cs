using Microsoft.CodeAnalysis.CSharp;

namespace DRN.Framework.SharedKernel.Analyzers.Conventions;

internal sealed record ConventionProject(bool Enabled, string Name, string Role, string RootNamespace,
    string AssemblyName, ImmutableArray<Diagnostic> Diagnostics)
{
    internal const string MetadataPrefix = "DRN.Conventions.";

    internal static bool IsRole(string role) => ConventionArchitecture.IsRole(role);

    internal static ConventionProject Read(Compilation compilation, AnalyzerConfigOptions options)
    {
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
        var projectName = Property(options, "MSBuildProjectName");
        if (projectName.Length == 0) projectName = compilation.AssemblyName ?? "";
        var (role, problem) = ConventionNames.Read(projectName);
        if (problem.Length > 0)
            return new(false, "", "", "", compilation.AssemblyName ?? "",
                [InputIssue("MSBuildProjectName", problem, "project name with one role token")]);
        if (role.Length == 0)
            return new(false, "", "", "", compilation.AssemblyName ?? "", []);

        var rootNamespace = Property(options, "RootNamespace");
        if (!IsNamespace(rootNamespace))
            diagnostics.Add(InputIssue("RootNamespace", "missing or malformed", "namespace independent of AssemblyName"));

        var assemblyName = compilation.AssemblyName ?? "";
        return new(true, projectName, role, rootNamespace, assemblyName, diagnostics.ToImmutable());
    }

    internal static string Property(AnalyzerConfigOptions options, string name) =>
        options.TryGetValue("build_property." + name, out var value) ? value : "";

    internal static bool IsNamespace(string value) => value.Length > 0 &&
        value.Split('.').All(segment => SyntaxFacts.IsValidIdentifier(segment));

    internal static Diagnostic InputIssue(string key, string problem, string remedy, Location? location = null) =>
        Diagnostic.Create(ConventionDiagnosticDescriptors.InvalidInput, location ?? Location.None, key, problem, "Declare one valid " + remedy);

    internal static string NormalizeSeparators(string value) => value.Replace('\\', '/');

    internal static bool IsLogicalPath(string value) => ConventionInputSyntax.IsLogicalPath(value);

    internal static bool IsNamespaceWithin(string value, string parent) =>
        value == parent || value.StartsWith(parent + ".", StringComparison.Ordinal);
}
