using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using DRN.Framework.SharedKernel.Diagnostics;

namespace DRN.Framework.Utils.Logging.Diagnostics;

internal static class EmbeddedClassificationRules
{
    private static readonly ConditionalWeakTable<Assembly, Lazy<DiagnosticRedactionRules>> Cache = new();

    internal static DiagnosticRedactionRules Load(string? assemblyName)
    {
        return assemblyName == null ? DiagnosticRedactionRules.Empty : Load(Assembly.Load(new AssemblyName(assemblyName)));
    }

    internal static DiagnosticRedactionRules Load(Assembly root) => Cache.GetValue(root,
        static assembly => new Lazy<DiagnosticRedactionRules>(() => DiagnosticRedactionRules.FromGraph(assembly,
            item => item.GetName().Name ?? "",
            item => LoadReferences(item.GetReferencedAssemblies(),
                (AssemblyLoadContext.GetLoadContext(item) ?? AssemblyLoadContext.Default).LoadFromAssemblyName),
            item => item.GetCustomAttributesData()
                .Where(attribute => attribute.AttributeType.FullName == DiagnosticRedactionRules.MetadataName && attribute.ConstructorArguments.Count == 1)
                .Select(attribute => attribute.ConstructorArguments[0].Value as string),
            EqualityComparer<Assembly>.Default))).Value;

    internal static IEnumerable<Assembly> LoadReferences(IEnumerable<AssemblyName> references, Func<AssemblyName, Assembly> load)
    {
        foreach (var reference in references)
        {
            Assembly assembly;
            try { assembly = load(reference); }
            // A reference may belong to an optional dependency that is not deployed.
            // Configured roots and catalogs are loaded separately and still fail on errors.
            catch (FileNotFoundException) { continue; }
            yield return assembly;
        }
    }
}
