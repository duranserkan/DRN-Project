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
        "Path '{0}' blocks '{1}'. Use [Personal] or [Sensitive] on model properties. For a reviewed name exemption, place '// DiagnosticNameExemption: {0} | <nonempty justification>' immediately above this statement. Comments cannot override configured sensitive paths or declaration requirements. Runtime capture does not redact unclassified names.",
        Category, DiagnosticSeverity.Error, true, helpLinkUri: Help);
    internal static readonly DiagnosticDescriptor UnknownClassification = new("DRN0018", "Unsupported classification",
        "Use [Personal], [Sensitive], or a derived attribute on '{0}'",
        Category, DiagnosticSeverity.Error, true, helpLinkUri: Help);
    internal static readonly DiagnosticDescriptor AnonymousClassification = new("DRN0019", "Use a named model",
        "Anonymous property '{0}' needs a named class or record. Add [Personal] or [Sensitive], or [Sensitive(false)] for a false positive.",
        Category, DiagnosticSeverity.Error, true, helpLinkUri: Help);
    internal static readonly DiagnosticDescriptor DictionaryClassification = new("DRN0020", "Classify dictionary key",
        "Classify dictionary key '{0}' with AddDictionary or SanitizeDictionary. For a reviewed name exemption, place '// DiagnosticNameExemption: {0} | <nonempty justification>' immediately above this statement. Comments cannot override configured sensitive paths or exempt other keys. Runtime capture does not redact unclassified keys.",
        Category, DiagnosticSeverity.Error, true, helpLinkUri: Help);

    internal static readonly DiagnosticDescriptor InvalidRuleFile = new("DRN0021", "Invalid diagnostic redaction rules",
        "{0}", Category, DiagnosticSeverity.Error, true, helpLinkUri: Help,
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    internal static readonly DiagnosticDescriptor UnsafeLoggingCapture = new("DRN0022", "Redact sensitive logging values",
        "{0}",
        Category, DiagnosticSeverity.Error, true, helpLinkUri: Help);

    internal static readonly DiagnosticDescriptor InvalidNameExemptionComment = new("DRN0023", "Invalid diagnostic name exemption comment",
        "Use '// DiagnosticNameExemption: ExactName | Nonempty justification' immediately above the logging statement, with no blank line. Wildcards are not supported. The name must be 1-512 characters and the comment at most 2048 characters. Classified values and configured sensitive paths cannot be exempted.",
        Category, DiagnosticSeverity.Error, true, helpLinkUri: Help);

    internal static readonly DiagnosticDescriptor UnusedNameExemptionComment = new("DRN0024", "Unused diagnostic name exemption comment",
        "This DiagnosticNameExemption comment suppresses no name diagnostic. Remove it or place it above the intended capture with the exact matching name.",
        Category, DiagnosticSeverity.Warning, true, helpLinkUri: Help,
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    internal static readonly DiagnosticDescriptor IncompleteModelAnalysis = new("DRN0025", "Incomplete model classification analysis",
        "Model capture exceeds the analysis work limit. Configured paths may remain unchecked. Capture a smaller model or classify sensitive subtrees explicitly.",
        Category, DiagnosticSeverity.Error, true, helpLinkUri: Help);
}
