using System.Collections.Immutable;
using DRN.Framework.SharedKernel.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace DRN.Framework.SharedKernel.Analyzers;

// Check known logging boundaries without inferring arbitrary wrapper or alias data flow.
internal sealed class LoggingCaptureAnalysis(OperationAnalysisContext context, ClassificationContract contract,
    DiagnosticRedactionRules rules, IInvocationOperation call, DiagnosticNameExemption exemptions)
{
    private const string LoggingAssembly = "Microsoft.Extensions.Logging.Abstractions";
    private const int MaxTemplateLength = 65536;
    private const int MaxValues = 256;
    private static DiagnosticDescriptor Descriptor => ClassificationDiagnosticDescriptors.UnsafeLoggingCapture;

    internal static void Analyze(OperationAnalysisContext context, ClassificationContract contract,
        DiagnosticRedactionRules rules, IInvocationOperation call, DiagnosticNameExemption exemptions)
    {
        var method = call.TargetMethod.ReducedFrom ?? call.TargetMethod;
        switch (method.ContainingType.Name, method.Name)
        {
            case ("LoggerExtensions", "Log" or "LogTrace" or "LogDebug" or "LogInformation" or "LogWarning" or "LogError" or "LogCritical" or "BeginScope")
                when method.ContainingAssembly.Name == LoggingAssembly &&
                     method.ContainingType.ToDisplayString() == "Microsoft.Extensions.Logging.LoggerExtensions":
                new LoggingCaptureAnalysis(context, contract, rules, call, exemptions).AnalyzeTemplate();
                break;
            case ("ILogger", "BeginScope")
                when method.ContainingAssembly.Name == LoggingAssembly &&
                     method.ContainingType.ToDisplayString() == "Microsoft.Extensions.Logging.ILogger":
            {
                var analysis = new LoggingCaptureAnalysis(context, contract, rules, call, exemptions);
                var state = analysis.Argument("state");
                if (state != null)
                {
                    new DictionaryCaptureAnalysis(context, rules, call, exemptions, Descriptor, analysis.AnalyzeValue).Analyze(state, false, null);
                    analysis.AnalyzeValue(state);
                }
                break;
            }
            case ("IScopedLog" or "ScopedLog" or "DiagnosticCaptureExtensions", "Add" or "AddToList" or "AddIfNotNullOrEmpty" or "AddWithPolicy")
                when method.ContainingAssembly.Name != LoggingAssembly && method.ContainingType.ToDisplayString() is
                    "DRN.Framework.Utils.Logging.IScopedLog" or "DRN.Framework.Utils.Logging.ScopedLog" or
                    "DRN.Framework.Utils.Logging.Diagnostics.DiagnosticCaptureExtensions":
            {
                var analysis = new LoggingCaptureAnalysis(context, contract, rules, call, exemptions);
                var value = analysis.Argument("value");
                if (value != null) analysis.AnalyzeValue(value);
                break;
            }
        }
    }

    private IOperation? Argument(string name) =>
        call.Arguments.FirstOrDefault(argument => argument.Parameter?.Name == name)?.Value;

    private void AnalyzeTemplate()
    {
        var args = Unwrap(Argument("args"));
        if (args?.ConstantValue is { HasValue: true, Value: null }) return;
        var values = args is IArrayCreationOperation { Initializer: { } initializer }
            ? initializer.ElementValues : default;
        if (!values.IsDefault)
            foreach (var value in values.Take(MaxValues)) AnalyzeValue(value);

        var message = Argument("message") ?? Argument("messageFormat");
        if (message?.ConstantValue is not { HasValue: true, Value: string template } || template.Length > MaxTemplateLength ||
            !values.IsDefault && values.Length == 0) return;
        AnalyzeTemplateNames(template, values);
    }

    private void AnalyzeTemplateNames(string template, ImmutableArray<IOperation> values)
    {
        var position = 0;
        var valueIndex = 0;
        while (position < template.Length && valueIndex < MaxValues)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            var open = FindBrace(template, '{', position);
            if (open == template.Length) break;
            var close = FindBrace(template, '}', open + 1);
            if (close == template.Length) break;
            var nameEnd = open + 1;
            while (nameEnd < close && template[nameEnd] is not (',' or ':')) nameEnd++;
            var name = template.Substring(open + 1, nameEnd - open - 1);
            var knownSafe = !values.IsDefault && valueIndex < values.Length && IsSafeConstant(values[valueIndex]);
            AnalyzeTemplateName(name, knownSafe);
            valueIndex++;
            position = close + 1;
        }
    }

    private void AnalyzeTemplateName(string name, bool knownSafe)
    {
        var configuredPath = rules.MatchesPath(name);
        if (!knownSafe && (configuredPath || rules.MatchesField(name) && !exemptions.Allows(call.Syntax, name)))
            context.ReportDiagnostic(Diagnostic.Create(Descriptor, call.Syntax.GetLocation(),
                DiagnosticNameExemption.NameMessage(name, configuredPath)));
    }

    // Paired braces are escaped. Opening runs select their last brace, closing runs their first.
    private static int FindBrace(string text, char brace, int start)
    {
        for (var index = start; index < text.Length;)
        {
            if (text[index] != brace) { index++; continue; }
            var runStart = index;
            while (index < text.Length && text[index] == brace) index++;
            if ((index - runStart) % 2 != 0) return brace == '{' ? index - 1 : runStart;
        }
        return text.Length;
    }

    private static IOperation? Unwrap(IOperation? value)
    {
        while (value is IConversionOperation conversion) value = conversion.Operand;
        return value;
    }

    internal static bool IsSafeConstant(IOperation value)
    {
        // User-defined conversions can replace an otherwise harmless constant.
        while (value is IConversionOperation { OperatorMethod: null } conversion) value = conversion.Operand;
        return value.ConstantValue is { HasValue: true, Value: null or "" or "[redacted]" };
    }

    private void AnalyzeValue(IOperation value)
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        if (Unwrap(value) is not IPropertyReferenceOperation reference) return;
        var property = reference.Property;
        if (contract.Positive(property) || contract.Positive(property.ContainingType) || contract.Positive(property.Type))
        {
            Report(property.ToDisplayString());
            return;
        }
        // Concrete access can still carry an interface-only classification.
        // An inherited property can implement an interface introduced by the receiver's type.
        var receiverType = reference.Instance?.Type as INamedTypeSymbol ?? property.ContainingType;
        var inspected = 0;
        foreach (var @interface in receiverType.AllInterfaces)
            foreach (var member in @interface.GetMembers().OfType<IPropertySymbol>())
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                if (++inspected > MaxValues) return;
                if (SymbolEqualityComparer.Default.Equals(receiverType.FindImplementationForInterfaceMember(member), property) &&
                    (contract.Positive(member) || contract.Positive(member.ContainingType) || contract.Positive(member.Type)))
                {
                    Report(property.ToDisplayString());
                    return;
                }
            }
    }

    private void Report(string name) => context.ReportDiagnostic(Diagnostic.Create(Descriptor, call.Syntax.GetLocation(),
        $"Property '{name}' carries a sensitive or personal classification. Remove it or explicitly redact its value before logging. A harmless destination name or DiagnosticNameExemption comment cannot waive this classification."));
}
