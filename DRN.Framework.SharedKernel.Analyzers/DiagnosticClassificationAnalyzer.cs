using System.Collections.Concurrent;
using System.Collections.Immutable;
using DRN.Framework.SharedKernel.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Text;

namespace DRN.Framework.SharedKernel.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DiagnosticClassificationAnalyzer : DiagnosticAnalyzer
{
    private const string AttributeNamespace = "DRN.Framework.SharedKernel.Attributes.";

    // Preserve the public descriptor fields while sharing their definitions with capture analyses.
    public static readonly DiagnosticDescriptor MissingClassification = ClassificationDiagnosticDescriptors.MissingClassification;
    public static readonly DiagnosticDescriptor InvalidExemption = ClassificationDiagnosticDescriptors.InvalidExemption;
    public static readonly DiagnosticDescriptor PathClassification = ClassificationDiagnosticDescriptors.PathClassification;
    public static readonly DiagnosticDescriptor UnknownClassification = ClassificationDiagnosticDescriptors.UnknownClassification;
    public static readonly DiagnosticDescriptor AnonymousClassification = ClassificationDiagnosticDescriptors.AnonymousClassification;
    public static readonly DiagnosticDescriptor DictionaryClassification = ClassificationDiagnosticDescriptors.DictionaryClassification;
    public static readonly DiagnosticDescriptor InvalidRuleFile = ClassificationDiagnosticDescriptors.InvalidRuleFile;
    public static readonly DiagnosticDescriptor UnsafeLoggingCapture = ClassificationDiagnosticDescriptors.UnsafeLoggingCapture;
    public static readonly DiagnosticDescriptor InvalidNameExemptionComment = ClassificationDiagnosticDescriptors.InvalidNameExemptionComment;
    public static readonly DiagnosticDescriptor UnusedNameExemptionComment = ClassificationDiagnosticDescriptors.UnusedNameExemptionComment;
    public static readonly DiagnosticDescriptor IncompleteModelAnalysis = ClassificationDiagnosticDescriptors.IncompleteModelAnalysis;
    public static readonly DiagnosticDescriptor IncompleteInterfaceAnalysis = ClassificationDiagnosticDescriptors.IncompleteInterfaceAnalysis;

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        [MissingClassification, InvalidExemption, PathClassification, UnknownClassification, AnonymousClassification, DictionaryClassification, InvalidRuleFile, UnsafeLoggingCapture, InvalidNameExemptionComment, UnusedNameExemptionComment, IncompleteModelAnalysis, IncompleteInterfaceAnalysis];

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze);
        context.RegisterCompilationStartAction(start =>
        {
            var sensitive = start.Compilation.GetTypeByMetadataName(AttributeNamespace + "SensitiveAttribute");
            var personal = start.Compilation.GetTypeByMetadataName(AttributeNamespace + "PersonalAttribute");
            var hasAttributes = sensitive != null && personal != null;
            var rules = LoadRules(start.Compilation, start.Options, hasAttributes, start.CancellationToken, out var ruleError);
            if (ruleError != null)
                start.RegisterCompilationEndAction(end => end.ReportDiagnostic(ruleError));
            if (!hasAttributes) return;

            var contract = new ClassificationContract(sensitive!, personal!, start.Compilation);
            var captureApis = new CaptureApis(start.Compilation);
            var exemptions = new DiagnosticNameExemption();
            var dictionaryTypes = new DictionaryCaptureAnalysis.DictionaryTypeFacts();
            start.RegisterSyntaxTreeAction(exemptions.Collect);
            start.RegisterCompilationEndAction(exemptions.ReportUnused);
            start.RegisterSymbolAction(symbol => AnalyzeProperty(symbol, contract, rules), SymbolKind.Property);
            start.RegisterOperationAction(operation => AnalyzeAnonymousObject(operation, rules), OperationKind.AnonymousObjectCreation);
            start.RegisterSyntaxNodeAction(node => AnalyzeAttribute(node, contract), Microsoft.CodeAnalysis.CSharp.SyntaxKind.Attribute);
            start.RegisterOperationBlockStartAction(block =>
            {
                var localWrites = new DictionaryCaptureAnalysis.LocalWriteIndex(dictionaryTypes);
                block.RegisterOperationAction(operation =>
                    AnalyzeInvocation(operation, contract, captureApis, rules, exemptions, localWrites), OperationKind.Invocation);
            });
        });
    }

    private static void AnalyzeInvocation(OperationAnalysisContext context, ClassificationContract contract, CaptureApis captureApis,
        DiagnosticRedactionRules rules, DiagnosticNameExemption exemptions, DictionaryCaptureAnalysis.LocalWriteIndex localWrites)
    {
        var call = (IInvocationOperation)context.Operation;
        if (call.TargetMethod.MethodKind == MethodKind.Constructor)
        {
            AnalyzeConstructor(context, contract);
            return;
        }
        if (call.TargetMethod.Name is not ("Add" or "AddToList" or "AddIfNotNullOrEmpty" or "AddProperties" or
            "AddWithPolicy" or "AddPropertiesWithPolicy" or "SanitizeValue" or "SanitizeWithPolicy" or
            "AddDictionary" or "SanitizeDictionary" or "Log" or "LogTrace" or "LogDebug" or "LogInformation" or
            "LogWarning" or "LogError" or "LogCritical" or "BeginScope")) return;

        var targetMethod = call.TargetMethod.ReducedFrom ?? call.TargetMethod;
        var api = captureApis.Find(targetMethod.ContainingType);
        if (api == CaptureApi.None || IsSubstituteVerification(call, api, captureApis)) return;

        if (api is CaptureApi.ScopedLog or CaptureApi.Sanitizer or CaptureApi.Extensions)
        {
            AnalyzeCapture(context, contract, rules, exemptions, call, api);
            AnalyzeDictionaryCapture(context, rules, exemptions, call, localWrites);
        }
        LoggingCaptureAnalysis.Analyze(context, contract, rules, call, targetMethod, api, exemptions, localWrites);
    }

    internal enum CaptureApi { None, ScopedLog, Sanitizer, Extensions, Logger, LoggerExtensions }

    internal sealed class CaptureApis(Compilation compilation)
    {
        private const string LoggingAssembly = "Microsoft.Extensions.Logging.Abstractions";
        private readonly ConcurrentDictionary<INamedTypeSymbol, CaptureApi> _otherTypes = new(SymbolEqualityComparer.Default);
        private readonly INamedTypeSymbol? _log = compilation.GetTypeByMetadataName("DRN.Framework.Utils.Logging.IScopedLog");
        private readonly INamedTypeSymbol? _concreteLog = compilation.GetTypeByMetadataName("DRN.Framework.Utils.Logging.ScopedLog");
        private readonly INamedTypeSymbol? _sanitizer = compilation.GetTypeByMetadataName("DRN.Framework.Utils.Logging.Diagnostics.IDiagnosticSanitizer");
        private readonly INamedTypeSymbol? _concreteSanitizer = compilation.GetTypeByMetadataName("DRN.Framework.Utils.Logging.Diagnostics.DiagnosticSanitizer");
        private readonly INamedTypeSymbol? _extensions = compilation.GetTypeByMetadataName("DRN.Framework.Utils.Logging.Diagnostics.DiagnosticCaptureExtensions");
        private readonly INamedTypeSymbol? _logger = compilation.GetTypeByMetadataName("Microsoft.Extensions.Logging.ILogger");
        private readonly INamedTypeSymbol? _loggerExtensions = compilation.GetTypeByMetadataName("Microsoft.Extensions.Logging.LoggerExtensions");
        private readonly INamedTypeSymbol? _substituteExtensions = compilation.GetTypeByMetadataName("NSubstitute.SubstituteExtensions");
        private readonly INamedTypeSymbol? _receivedExtensions = compilation.GetTypeByMetadataName("NSubstitute.ReceivedExtensions.ReceivedExtensions");

        public CaptureApi Find(INamedTypeSymbol type)
        {
            if (type.Name is not ("IScopedLog" or "ScopedLog" or "IDiagnosticSanitizer" or "DiagnosticSanitizer" or
                "DiagnosticCaptureExtensions" or "ILogger" or "LoggerExtensions"))
                return CaptureApi.None;
            if (type.Name is ("ILogger" or "LoggerExtensions") && type.ContainingAssembly.Name != LoggingAssembly)
                return CaptureApi.None;

            if (SymbolEqualityComparer.Default.Equals(type, _log) || SymbolEqualityComparer.Default.Equals(type, _concreteLog))
                return CaptureApi.ScopedLog;
            if (SymbolEqualityComparer.Default.Equals(type, _sanitizer) || SymbolEqualityComparer.Default.Equals(type, _concreteSanitizer))
                return CaptureApi.Sanitizer;
            if (SymbolEqualityComparer.Default.Equals(type, _extensions)) return CaptureApi.Extensions;
            if (SymbolEqualityComparer.Default.Equals(type, _logger)) return CaptureApi.Logger;
            if (SymbolEqualityComparer.Default.Equals(type, _loggerExtensions)) return CaptureApi.LoggerExtensions;
            // Preserve name-based coverage for ambiguous references and extern aliases.
            return _otherTypes.GetOrAdd(type, static candidate => candidate.ToDisplayString() switch
            {
                "DRN.Framework.Utils.Logging.IScopedLog" or "DRN.Framework.Utils.Logging.ScopedLog" => CaptureApi.ScopedLog,
                "DRN.Framework.Utils.Logging.Diagnostics.IDiagnosticSanitizer" or "DRN.Framework.Utils.Logging.Diagnostics.DiagnosticSanitizer" => CaptureApi.Sanitizer,
                "DRN.Framework.Utils.Logging.Diagnostics.DiagnosticCaptureExtensions" => CaptureApi.Extensions,
                "Microsoft.Extensions.Logging.ILogger" => CaptureApi.Logger,
                "Microsoft.Extensions.Logging.LoggerExtensions" => CaptureApi.LoggerExtensions,
                _ => CaptureApi.None
            });
        }

        public bool IsSubstituteVerificationType(INamedTypeSymbol type) =>
            type.ContainingAssembly.Name == "NSubstitute" &&
            (SymbolEqualityComparer.Default.Equals(type, _substituteExtensions) ||
             SymbolEqualityComparer.Default.Equals(type, _receivedExtensions) ||
             type.Name is ("SubstituteExtensions" or "ReceivedExtensions") &&
             type.ToDisplayString() is ("NSubstitute.SubstituteExtensions" or "NSubstitute.ReceivedExtensions.ReceivedExtensions"));
    }

    private static DiagnosticRedactionRules LoadRules(Compilation compilation, AnalyzerOptions options,
        bool includeInheritedGraph, CancellationToken cancellationToken, out Diagnostic? error)
    {
        error = null;
        var files = options.AdditionalFiles.Where(file =>
            Path.GetFileName(file.Path).Equals("data-classification-rules.json", StringComparison.OrdinalIgnoreCase) ||
            Path.GetFileName(file.Path).Equals("diagnostic-rules.json", StringComparison.OrdinalIgnoreCase)).ToArray();
        var errorLocation = Location.None;
        try
        {
            var rulesAttribute = includeInheritedGraph
                ? compilation.GetTypeByMetadataName(DiagnosticRedactionRules.MetadataName)
                : null;
            var inherited = includeInheritedGraph
                ? DiagnosticRedactionRules.FromGraph<IAssemblySymbol>(compilation.Assembly,
                    assembly => assembly.Name,
                    assembly => assembly.Modules.SelectMany(module => module.ReferencedAssemblySymbols),
                    assembly => SymbolEqualityComparer.Default.Equals(assembly, compilation.Assembly) && files.Length > 0 ? [] :
                        assembly.GetAttributes().Where(attribute =>
                            attribute.AttributeClass?.Name == "DataClassificationRulesAttribute" &&
                            (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, rulesAttribute) ||
                             attribute.AttributeClass.ToDisplayString() == DiagnosticRedactionRules.MetadataName) &&
                            attribute.ConstructorArguments.Length == 1)
                        .Select(attribute => attribute.ConstructorArguments[0].Value as string), SymbolEqualityComparer.Default, cancellationToken)
                : DiagnosticRedactionRules.Empty;
            if (files.Length == 0) return inherited;
            errorLocation = Location.Create(files[0].Path, new TextSpan(0, 0), new LinePositionSpan(new(0, 0), new(0, 0)));
            if (files.Length > 1)
                throw new FormatException("Include exactly one diagnostic redaction rule file per project.");
            var text = files[0].GetText(cancellationToken);
            if (text == null) throw new FormatException("The diagnostic redaction rule file could not be read.");
            if (text.Length > DiagnosticRedactionRules.MaxCharacters)
                throw new FormatException("A diagnostic redaction rule file must contain at most 65536 characters.");
            return inherited.Merge(DiagnosticRedactionRules.Parse(text.ToString()));
        }
        catch (Exception exception) when (exception is FormatException or IOException or UnauthorizedAccessException)
        {
            var message = exception is FormatException ? exception.Message : "The diagnostic redaction rule file could not be read.";
            error = Diagnostic.Create(InvalidRuleFile, errorLocation, message);
            return DiagnosticRedactionRules.Empty;
        }
    }

    private static void AnalyzeProperty(SymbolAnalysisContext context, ClassificationContract contract, DiagnosticRedactionRules rules)
    {
        var property = (IPropertySymbol)context.Symbol;
        if (property.ContainingType.IsAnonymousType) return;
        if (!ClassificationContract.Eligible(property) || !rules.MatchesField(property.Name) || contract.Accepted(property, context.CancellationToken)) return;
        var location = property.Locations.FirstOrDefault(item => item.IsInSource);
        if (location == null) return;
        context.ReportDiagnostic(Diagnostic.Create(contract.HasUnknown(property, context.CancellationToken) ? UnknownClassification : MissingClassification,
            location, property.Name));
    }

    private static void AnalyzeAnonymousObject(OperationAnalysisContext context, DiagnosticRedactionRules rules)
    {
        var creation = (IAnonymousObjectCreationOperation)context.Operation;
        if (IsMigrationSchemaExpression(creation)) return;
        // Each creation is analyzed independently, including nested objects and inferred member names.
        foreach (var initializer in creation.Initializers)
        {
            if (initializer is not ISimpleAssignmentOperation { Target: IPropertyReferenceOperation target } ||
                !rules.MatchesField(target.Property.Name)) continue;
            context.ReportDiagnostic(Diagnostic.Create(AnonymousClassification, target.Syntax.GetLocation(), target.Property.Name));
        }
    }

    private static bool IsMigrationSchemaExpression(IAnonymousObjectCreationOperation creation)
    {
        IOperation result = creation;
        while (result.Parent is IConversionOperation) result = result.Parent;
        if (result.Parent is not IReturnOperation { Parent: IBlockOperation { Parent: IAnonymousFunctionOperation lambda } } ||
            lambda.Symbol.Parameters.Length != 1) return false;
        IOperation argumentValue = lambda;
        while (argumentValue.Parent is IConversionOperation or IDelegateCreationOperation) argumentValue = argumentValue.Parent;
        if (argumentValue.Parent is not IArgumentOperation { Parent: IInvocationOperation call } argument) return false;
        var owner = call.TargetMethod.ContainingType;
        if (owner.ContainingAssembly.Name != "Microsoft.EntityFrameworkCore.Relational") return false;
        var columns = owner.ToDisplayString() == "Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder" &&
            call.TargetMethod.Name == "CreateTable" && argument.Parameter?.Name == "columns";
        var selector = owner.MetadataName == "CreateTableBuilder`1" &&
            owner.ContainingNamespace.ToDisplayString() == "Microsoft.EntityFrameworkCore.Migrations.Operations.Builders" &&
            call.TargetMethod.Name is "PrimaryKey" or "UniqueConstraint" or "ForeignKey" && argument.Parameter?.Name == "columns";
        if (!columns && !selector) return false;
        // Only column-operation metadata is exempt. Nested default values and other data are analyzed separately.
        return creation.Initializers.All(initializer => IsMigrationColumnInitializer(initializer, columns, lambda.Symbol.Parameters[0]));
    }

    private static bool IsMigrationColumnInitializer(IOperation initializer, bool columns, IParameterSymbol lambdaParameter)
    {
        if (initializer is not ISimpleAssignmentOperation assignment || !IsColumnOperationBuilder(assignment.Value.Type)) return false;
        return columns || assignment.Value is IPropertyReferenceOperation { Instance: IParameterReferenceOperation parameter } &&
            SymbolEqualityComparer.Default.Equals(parameter.Parameter, lambdaParameter);
    }

    private static bool IsColumnOperationBuilder(ITypeSymbol? type) =>
        type is INamedTypeSymbol { MetadataName: "OperationBuilder`1", TypeArguments.Length: 1 } named &&
        named.ContainingAssembly.Name == "Microsoft.EntityFrameworkCore.Relational" &&
        named.ContainingNamespace.ToDisplayString() == "Microsoft.EntityFrameworkCore.Migrations.Operations.Builders" &&
        named.TypeArguments[0].ToDisplayString() == "Microsoft.EntityFrameworkCore.Migrations.Operations.AddColumnOperation";

    private static IOperation? GetArgumentValue(IInvocationOperation call, int ordinal) =>
        call.Arguments.FirstOrDefault(argument => argument.Parameter?.Ordinal == ordinal)?.Value;

    private static void AnalyzeDictionaryCapture(OperationAnalysisContext context, DiagnosticRedactionRules rules,
        DiagnosticNameExemption exemptions, IInvocationOperation call, DictionaryCaptureAnalysis.LocalWriteIndex localWrites)
    {
        var method = call.TargetMethod;
        if (method.Name is not ("Add" or "AddToList" or "AddProperties" or "AddWithPolicy" or "AddPropertiesWithPolicy" or
            "SanitizeValue" or "SanitizeWithPolicy" or "AddDictionary" or "SanitizeDictionary")) return;
        var offset = method.IsExtensionMethod && method.ReducedFrom == null ? 1 : 0;
        var value = GetArgumentValue(call, 1 + offset);
        if (value == null) return;
        var dictionaryApi = method.Name is "AddDictionary" or "SanitizeDictionary";
        var prefix = GetArgumentValue(call, offset);
        var rootPath = prefix?.ConstantValue is { HasValue: true, Value: string path } ? path : null;
        new DictionaryCaptureAnalysis(context, rules, call, exemptions, localWrites).Analyze(value, dictionaryApi, rootPath);
    }

    private static void AnalyzeAttribute(SyntaxNodeAnalysisContext context, ClassificationContract contract)
    {
        var syntax = (AttributeSyntax)context.Node;
        var argument = syntax.ArgumentList?.Arguments.FirstOrDefault(item => item.NameEquals == null);
        if (argument == null) return;
        // A positional record parameter with property: targets the generated property.
        var target = syntax.Parent as AttributeListSyntax;
        if (target?.Parent is PropertyDeclarationSyntax || target?.Parent is ParameterSyntax && target.Target?.Identifier.ValueText == "property") return;
        if (context.SemanticModel.GetSymbolInfo(syntax, context.CancellationToken).Symbol is not IMethodSymbol constructor ||
            !SymbolEqualityComparer.Default.Equals(constructor.ContainingType, contract.Sensitive)) return;
        if (context.SemanticModel.GetConstantValue(argument.Expression, context.CancellationToken) is not { HasValue: true, Value: false }) return;
        context.ReportDiagnostic(Diagnostic.Create(InvalidExemption, syntax.GetLocation(), "Use [Sensitive(false)] only on properties"));
    }

    private static void AnalyzeConstructor(OperationAnalysisContext context, ClassificationContract contract)
    {
        var invocation = (IInvocationOperation)context.Operation;
        if (invocation.TargetMethod.MethodKind != MethodKind.Constructor ||
            !SymbolEqualityComparer.Default.Equals(invocation.TargetMethod.ContainingType, contract.Sensitive) ||
            context.ContainingSymbol.ContainingType is not { } owner || SymbolEqualityComparer.Default.Equals(owner, contract.Sensitive)) return;
        if (invocation.Arguments.Any(argument => argument.Parameter?.Type.SpecialType == SpecialType.System_Boolean &&
                argument.Value.ConstantValue is { HasValue: true, Value: false }))
            context.ReportDiagnostic(Diagnostic.Create(InvalidExemption, invocation.Syntax.GetLocation(), "Derived Sensitive attributes must pass true"));
    }

    private static void AnalyzeCapture(OperationAnalysisContext context, ClassificationContract contract, DiagnosticRedactionRules rules,
        DiagnosticNameExemption exemptions, IInvocationOperation call, CaptureApi api)
    {
        if (!IsModelCapture(call, api, out var flat, out var namesAcknowledged)) return;
        var offset = call.TargetMethod.IsExtensionMethod && call.TargetMethod.ReducedFrom == null ? 1 : 0;
        var prefix = GetArgumentValue(call, offset);
        if (prefix == null || !prefix.ConstantValue.HasValue || prefix.ConstantValue.Value is not string path) return;
        var type = GetCaptureType(call, flat, offset);
        if (type == null) return;
        new ModelCaptureAnalysis(context, contract, rules, call, flat, exemptions, namesAcknowledged).Analyze(type, path);
    }

    private static bool IsModelCapture(IInvocationOperation call, CaptureApi api, out bool flat, out bool namesAcknowledged)
    {
        var method = call.TargetMethod;
        flat = method.Name is "AddProperties" or "AddPropertiesWithPolicy" && api is CaptureApi.ScopedLog or CaptureApi.Extensions;
        var valueCapture = IsValueCapture(method.Name, api);
        var offset = method.IsExtensionMethod && method.ReducedFrom == null ? 1 : 0;
        namesAcknowledged = method.Name.EndsWith("WithPolicy", StringComparison.Ordinal) ||
                            !flat && call.Arguments.Length > 2 + offset;
        // Reasons and policies acknowledge names, but never configured capture paths.
        return (flat || valueCapture) && call.Arguments.Length >= 2 + offset;
    }

    private static bool IsValueCapture(string methodName, CaptureApi api) =>
        methodName is "Add" or "AddToList" or "AddIfNotNullOrEmpty" or "AddWithPolicy" && api is CaptureApi.ScopedLog or CaptureApi.Extensions ||
        methodName is "SanitizeValue" or "SanitizeWithPolicy" && api is CaptureApi.Sanitizer or CaptureApi.Extensions;

    private static ITypeSymbol? GetCaptureType(IInvocationOperation call, bool flat, int offset)
    {
        var value = GetArgumentValue(call, 1 + offset);
        while (value is IConversionOperation { OperatorMethod: null } conversion) value = conversion.Operand;
        // Conditional string capture never stores a constant null or empty value.
        if (call.TargetMethod.Name == "AddIfNotNullOrEmpty" &&
            value?.ConstantValue is { HasValue: true, Value: null or "" }) return null;
        // The exact constant marker contains no captured data. Runtime values remain checked.
        if (!flat && value?.ConstantValue is { HasValue: true, Value: "[redacted]" }) return null;
        return flat ? call.TargetMethod.TypeArguments.FirstOrDefault() : value?.Type;
    }

    private static bool IsSubstituteVerification(IInvocationOperation call, CaptureApi api, CaptureApis captureApis)
    {
        var receiver = call.Instance;
        if (receiver == null && api == CaptureApi.LoggerExtensions)
            receiver = call.Arguments.FirstOrDefault(argument => argument.Parameter?.Name == "logger")?.Value;
        while (receiver is IConversionOperation conversion) receiver = conversion.Operand;
        if (receiver is not IInvocationOperation verification) return false;
        var method = verification.TargetMethod.ReducedFrom ?? verification.TargetMethod;
        // Only the immediate verification receiver is exempt, never other calls on a substitute.
        return method.IsExtensionMethod &&
            method.Name is "Received" or "DidNotReceive" or "ReceivedWithAnyArgs" or "DidNotReceiveWithAnyArgs" &&
            captureApis.IsSubstituteVerificationType(method.ContainingType);
    }

}
