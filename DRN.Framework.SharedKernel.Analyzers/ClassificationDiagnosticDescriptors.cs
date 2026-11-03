using Microsoft.CodeAnalysis;

namespace DRN.Framework.SharedKernel.Analyzers;

internal static class ClassificationDiagnosticDescriptors
{
    private const string Category = "DRN.Classification";
    private const string Help = "https://github.com/duranserkan/DRN-Project/blob/master/DRN.Framework.Utils/README.md";

    internal static readonly DiagnosticDescriptor MissingClassification = new("DRN0015", "Classify property",
        "Add [Personal] or [Sensitive] to '{0}'. Use [Sensitive(false)] for a false positive.",
        Category, DiagnosticSeverity.Error, true, helpLinkUri: Help);
    internal static readonly DiagnosticDescriptor InvalidExemption = new("DRN0016", "Invalid name exemption",
        "{0}", Category, DiagnosticSeverity.Error, true, helpLinkUri: Help);
    internal static readonly DiagnosticDescriptor PathClassification = new("DRN0017", "Classify or exempt capture",
        "Path '{0}' blocks '{1}'. Use [Personal] or [Sensitive] on model properties. For false positives, use AddWithPolicy, AddPropertiesWithPolicy, or SanitizeWithPolicy.",
        Category, DiagnosticSeverity.Error, true, helpLinkUri: Help);
    internal static readonly DiagnosticDescriptor UnknownClassification = new("DRN0018", "Unsupported classification",
        "Use [Personal], [Sensitive], or a derived attribute on '{0}'",
        Category, DiagnosticSeverity.Error, true, helpLinkUri: Help);
    internal static readonly DiagnosticDescriptor AnonymousClassification = new("DRN0019", "Use a named model",
        "Anonymous property '{0}' needs a named class or record. Add [Personal] or [Sensitive], or [Sensitive(false)] for a false positive.",
        Category, DiagnosticSeverity.Error, true, helpLinkUri: Help);
    internal static readonly DiagnosticDescriptor DictionaryClassification = new("DRN0020", "Classify dictionary key",
        "Classify dictionary key '{0}' with AddDictionary or SanitizeDictionary",
        Category, DiagnosticSeverity.Error, true, helpLinkUri: Help);

    internal static readonly DiagnosticDescriptor InvalidRuleFile = new("DRN0021", "Invalid diagnostic redaction rules",
        "{0}", Category, DiagnosticSeverity.Error, true, helpLinkUri: Help,
        customTags: WellKnownDiagnosticTags.CompilationEnd);
}
