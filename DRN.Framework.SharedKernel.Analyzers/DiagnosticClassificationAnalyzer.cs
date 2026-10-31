using System.Collections.Immutable;
using DRN.Framework.SharedKernel.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace DRN.Framework.SharedKernel.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DiagnosticClassificationAnalyzer : DiagnosticAnalyzer
{
    private const string AttributeNamespace = "DRN.Framework.SharedKernel.Attributes.";
    private const string Category = "DRN.Classification";
    private const string Help = "https://github.com/duranserkan/DRN-Project/blob/develop/DRN.Framework.Utils/Logging/Diagnostics/README.md";

    public static readonly DiagnosticDescriptor MissingClassification = new("DRN0015", "Classify property",
        "Add [Personal] or [Sensitive] to '{0}'. Use [Sensitive(false)] for a false positive.",
        Category, DiagnosticSeverity.Error, true, helpLinkUri: Help);
    public static readonly DiagnosticDescriptor InvalidExemption = new("DRN0016", "Invalid name exemption",
        "{0}", Category, DiagnosticSeverity.Error, true, helpLinkUri: Help);
    public static readonly DiagnosticDescriptor PathClassification = new("DRN0017", "Classify or exempt capture",
        "Path '{0}' blocks '{1}'. Use [Personal] or [Sensitive] on model properties. For false positives, use AddWithPolicy, AddPropertiesWithPolicy, or SanitizeWithPolicy.",
        Category, DiagnosticSeverity.Error, true, helpLinkUri: Help);
    public static readonly DiagnosticDescriptor UnknownClassification = new("DRN0018", "Unsupported classification",
        "Use [Personal], [Sensitive], or a derived attribute on '{0}'",
        Category, DiagnosticSeverity.Error, true, helpLinkUri: Help);
    public static readonly DiagnosticDescriptor AnonymousClassification = new("DRN0019", "Use a named model",
        "Anonymous property '{0}' needs a named class or record. Add [Personal] or [Sensitive], or [Sensitive(false)] for a false positive.",
        Category, DiagnosticSeverity.Error, true, helpLinkUri: Help);
    public static readonly DiagnosticDescriptor DictionaryClassification = new("DRN0020", "Classify dictionary key",
        "Classify dictionary key '{0}' with AddDictionary or SanitizeDictionary",
        Category, DiagnosticSeverity.Error, true, helpLinkUri: Help);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        [MissingClassification, InvalidExemption, PathClassification, UnknownClassification, AnonymousClassification, DictionaryClassification];

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.RegisterCompilationStartAction(start =>
        {
            var sensitive = start.Compilation.GetTypeByMetadataName(AttributeNamespace + "SensitiveAttribute");
            var personal = start.Compilation.GetTypeByMetadataName(AttributeNamespace + "PersonalAttribute");
            if (sensitive == null || personal == null) return;
            var contract = new ClassificationContract(sensitive, personal);
            start.RegisterSymbolAction(symbol => AnalyzeProperty(symbol, contract), SymbolKind.Property);
            start.RegisterOperationAction(AnalyzeAnonymousObject, OperationKind.AnonymousObjectCreation);
            start.RegisterSyntaxNodeAction(node => AnalyzeAttribute(node, contract), Microsoft.CodeAnalysis.CSharp.SyntaxKind.Attribute);
            start.RegisterOperationAction(operation => AnalyzeConstructor(operation, contract), OperationKind.Invocation);
            start.RegisterOperationAction(operation => AnalyzeCapture(operation, contract), OperationKind.Invocation);
            start.RegisterOperationAction(AnalyzeDictionaryCapture, OperationKind.Invocation);
        });
    }

    private static void AnalyzeProperty(SymbolAnalysisContext context, ClassificationContract contract)
    {
        var property = (IPropertySymbol)context.Symbol;
        if (property.ContainingType.IsAnonymousType) return;
        if (!Eligible(property) || !DiagnosticRuleCatalog.MatchesField(property.Name) || contract.Accepted(property)) return;
        var location = property.Locations.FirstOrDefault(item => item.IsInSource);
        if (location == null) return;
        context.ReportDiagnostic(Diagnostic.Create(contract.HasUnknown(property) ? UnknownClassification : MissingClassification,
            location, property.Name));
    }

    private static void AnalyzeAnonymousObject(OperationAnalysisContext context)
    {
        var creation = (IAnonymousObjectCreationOperation)context.Operation;
        if (IsMigrationSchemaExpression(creation)) return;
        // Each creation is analyzed independently, including nested objects and inferred member names.
        foreach (var initializer in creation.Initializers)
        {
            if (initializer is not ISimpleAssignmentOperation { Target: IPropertyReferenceOperation target } ||
                !DiagnosticRuleCatalog.MatchesField(target.Property.Name)) continue;
            context.ReportDiagnostic(Diagnostic.Create(AnonymousClassification, target.Syntax.GetLocation(), target.Property.Name));
        }
    }

    private static bool Eligible(IPropertySymbol property) => !property.IsStatic && !property.IsIndexer &&
        property.DeclaredAccessibility == Accessibility.Public && property.GetMethod?.DeclaredAccessibility == Accessibility.Public;

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
        return creation.Initializers.All(initializer =>
        {
            if (initializer is not ISimpleAssignmentOperation assignment || !IsColumnOperationBuilder(assignment.Value.Type)) return false;
            return columns || assignment.Value is IPropertyReferenceOperation { Instance: IParameterReferenceOperation parameter } &&
                SymbolEqualityComparer.Default.Equals(parameter.Parameter, lambda.Symbol.Parameters[0]);
        });
    }

    private static bool IsColumnOperationBuilder(ITypeSymbol? type) =>
        type is INamedTypeSymbol { MetadataName: "OperationBuilder`1", TypeArguments.Length: 1 } named &&
        named.ContainingAssembly.Name == "Microsoft.EntityFrameworkCore.Relational" &&
        named.ContainingNamespace.ToDisplayString() == "Microsoft.EntityFrameworkCore.Migrations.Operations.Builders" &&
        named.TypeArguments[0].ToDisplayString() == "Microsoft.EntityFrameworkCore.Migrations.Operations.AddColumnOperation";

    private static bool IsDictionary(ITypeSymbol? type) => type is INamedTypeSymbol named &&
        new[] { named }.Concat(named.AllInterfaces).Any(item => item.ToDisplayString() == "System.Collections.IDictionary" ||
            item.OriginalDefinition.ToDisplayString() is "System.Collections.Generic.IDictionary<TKey, TValue>" or
                "System.Collections.Generic.IReadOnlyDictionary<TKey, TValue>");

    private static void AnalyzeDictionaryCapture(OperationAnalysisContext context)
    {
        var call = (IInvocationOperation)context.Operation;
        if (IsSubstituteVerification(call)) return;
        var method = call.TargetMethod;
        var owner = method.ContainingType.ToDisplayString();
        if (owner is not ("DRN.Framework.Utils.Logging.IScopedLog" or "DRN.Framework.Utils.Logging.ScopedLog" or
            "DRN.Framework.Utils.Logging.Diagnostics.IDiagnosticSanitizer" or "DRN.Framework.Utils.Logging.Diagnostics.DiagnosticSanitizer" or
            "DRN.Framework.Utils.Logging.Diagnostics.DiagnosticCaptureExtensions")) return;
        if (method.Name is not ("Add" or "AddToList" or "AddProperties" or "AddWithPolicy" or "AddPropertiesWithPolicy" or
            "SanitizeValue" or "SanitizeWithPolicy" or "AddDictionary" or "SanitizeDictionary")) return;
        var offset = method.IsExtensionMethod && method.ReducedFrom == null ? 1 : 0;
        var value = call.Arguments.FirstOrDefault(argument => argument.Parameter?.Ordinal == 1 + offset)?.Value;
        if (value == null) return;
        var dictionaryApi = method.Name is "AddDictionary" or "SanitizeDictionary";
        var visited = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        var reported = new HashSet<string>(StringComparer.Ordinal);
        Inspect(value, dictionaryApi, 0);

        void Report(IOperation? key, bool allowed)
        {
            if (!allowed && key?.ConstantValue is { HasValue: true, Value: string name } &&
                DiagnosticRuleCatalog.MatchesField(name) && reported.Add(name))
                context.ReportDiagnostic(Diagnostic.Create(DictionaryClassification, call.Syntax.GetLocation(), name));
        }

        void Inspect(IOperation operation, bool allowKeys, int depth)
        {
            if (depth > 32) return;
            if (operation is IConversionOperation conversion) { Inspect(conversion.Operand, allowKeys, depth + 1); return; }
            var type = operation.Type;
            if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable)
                type = nullable.TypeArguments[0];
            // Scalar projections do not capture their dictionary receiver or arguments.
            if (type?.SpecialType is SpecialType.System_Boolean or SpecialType.System_Char or SpecialType.System_String or
                SpecialType.System_Byte or SpecialType.System_SByte or SpecialType.System_Int16 or SpecialType.System_UInt16 or
                SpecialType.System_Int32 or SpecialType.System_UInt32 or SpecialType.System_Int64 or SpecialType.System_UInt64 or
                SpecialType.System_Single or SpecialType.System_Double or SpecialType.System_Decimal or SpecialType.System_DateTime ||
                type?.TypeKind == TypeKind.Enum) return;
            if (operation is ILocalReferenceOperation local && visited.Add(local.Local))
            {
                // Reuse the analyzed operation tree for declarations and direct writes before capture.
                var root = call.Parent;
                while (root?.Parent != null) root = root.Parent;
                if (root != null)
                    foreach (var prior in root.Descendants())
                    {
                        if (prior is IVariableDeclaratorOperation { Initializer: { } initializer } declaration &&
                            SymbolEqualityComparer.Default.Equals(declaration.Symbol, local.Local))
                            Inspect(initializer.Value, allowKeys, depth + 1);
                        if (prior.Syntax.SpanStart >= call.Syntax.SpanStart) continue;
                        if (prior is ISimpleAssignmentOperation { Target: ILocalReferenceOperation assignedLocal } localAssignment &&
                            SymbolEqualityComparer.Default.Equals(assignedLocal.Local, local.Local))
                            Inspect(localAssignment.Value, allowKeys, depth + 1);
                        if (prior is IInvocationOperation { TargetMethod.Name: "Add", Instance: ILocalReferenceOperation receiver } add &&
                            SymbolEqualityComparer.Default.Equals(receiver.Local, local.Local) && IsDictionary(receiver.Type))
                        {
                            Report(add.Arguments.FirstOrDefault()?.Value, allowKeys);
                            foreach (var argument in add.Arguments.Skip(1)) Inspect(argument.Value, false, depth + 1);
                        }
                        if (prior is ISimpleAssignmentOperation { Target: IPropertyReferenceOperation { Property.IsIndexer: true, Instance: ILocalReferenceOperation receiver2 } index } assignment &&
                            SymbolEqualityComparer.Default.Equals(receiver2.Local, local.Local) && IsDictionary(receiver2.Type))
                        {
                            Report(index.Arguments.FirstOrDefault()?.Value, allowKeys);
                            Inspect(assignment.Value, false, depth + 1);
                        }
                    }
                return;
            }
            if (operation is IObjectCreationOperation creation && IsDictionary(creation.Type))
            {
                if (creation.Initializer != null)
                    foreach (var entry in creation.Initializer.Initializers)
                    {
                        if (entry is IInvocationOperation add)
                        {
                            Report(add.Arguments.FirstOrDefault()?.Value, allowKeys);
                            foreach (var argument in add.Arguments.Skip(1)) Inspect(argument.Value, false, depth + 1);
                        }
                        else if (entry is ISimpleAssignmentOperation { Target: IPropertyReferenceOperation index } assignment)
                        {
                            Report(index.Arguments.FirstOrDefault()?.Value, allowKeys);
                            Inspect(assignment.Value, false, depth + 1);
                        }
                    }
                foreach (var argument in creation.Arguments) Inspect(argument.Value, false, depth + 1);
                return;
            }
            foreach (var child in operation.ChildOperations) Inspect(child, false, depth + 1);
        }
    }

    private static void AnalyzeAttribute(SyntaxNodeAnalysisContext context, ClassificationContract contract)
    {
        var syntax = (AttributeSyntax)context.Node;
        if (context.SemanticModel.GetSymbolInfo(syntax, context.CancellationToken).Symbol is not IMethodSymbol constructor ||
            !SymbolEqualityComparer.Default.Equals(constructor.ContainingType, contract.Sensitive)) return;
        var argument = syntax.ArgumentList?.Arguments.FirstOrDefault(item => item.NameEquals == null);
        if (argument == null || context.SemanticModel.GetConstantValue(argument.Expression, context.CancellationToken) is not { HasValue: true, Value: false }) return;
        // A positional record parameter with property: targets the generated property.
        var target = syntax.Parent as AttributeListSyntax;
        if (target?.Parent is PropertyDeclarationSyntax || target?.Parent is ParameterSyntax && target.Target?.Identifier.ValueText == "property") return;
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

    private static void AnalyzeCapture(OperationAnalysisContext context, ClassificationContract contract)
    {
        var call = (IInvocationOperation)context.Operation;
        if (IsSubstituteVerification(call)) return;
        var method = call.TargetMethod;
        var owner = method.ContainingType.ToDisplayString();
        var flat = method.Name == "AddProperties" && owner is "DRN.Framework.Utils.Logging.IScopedLog" or "DRN.Framework.Utils.Logging.ScopedLog";
        var valueCapture = method.Name is "Add" or "AddToList" && owner is "DRN.Framework.Utils.Logging.IScopedLog" or "DRN.Framework.Utils.Logging.ScopedLog" ||
                           method.Name == "SanitizeValue" && owner is "DRN.Framework.Utils.Logging.Diagnostics.IDiagnosticSanitizer" or "DRN.Framework.Utils.Logging.Diagnostics.DiagnosticSanitizer";
        // Reason and policy overloads own runtime validation. Declarations are still checked independently.
        if ((!flat && !valueCapture) || call.Arguments.Length != 2 && !flat || call.Arguments.Length < 2) return;
        var prefix = call.Arguments.FirstOrDefault(argument => argument.Parameter?.Ordinal == 0)?.Value;
        if (prefix == null || !prefix.ConstantValue.HasValue || prefix.ConstantValue.Value is not string path) return;
        var value = call.Arguments.FirstOrDefault(argument => argument.Parameter?.Ordinal == 1)?.Value;
        while (value is IConversionOperation conversion) value = conversion.Operand;
        var type = flat ? method.TypeArguments.FirstOrDefault() : value?.Type;
        if (type == null || contract.Positive(type)) return;
        if (method.Name == "Add" && type.ToDisplayString() is "System.TimeSpan" or "System.TimeSpan?")
            path += "_Seconds";
        if (!IsModelType(type) && DiagnosticRuleCatalog.MatchesField(path))
        {
            // Untyped roots still require an explicit capture decision.
            context.ReportDiagnostic(Diagnostic.Create(PathClassification, call.Syntax.GetLocation(), path, type.Name));
            return;
        }
        Visit(type, path, IsModelType(type) ? "" : path, new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default), 0);

        void Visit(ITypeSymbol current, string currentPath, string namePath, HashSet<ITypeSymbol> ancestors, int depth)
        {
            if (current is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable)
                current = nullable.TypeArguments[0];
            if (depth > 16 || current.SpecialType != SpecialType.None || current.TypeKind is TypeKind.Enum or TypeKind.TypeParameter ||
                current.ToDisplayString() is "System.TimeSpan" or "System.DateTimeOffset" or "System.DateOnly" or "System.TimeOnly" or
                    "System.Uri" or "System.Guid" or "DRN.Framework.Utils.Logging.ScopeEvent" ||
                contract.Positive(current) || !ancestors.Add(current)) return;
            try
            {
                if (current is IArrayTypeSymbol array) { Visit(array.ElementType, currentPath, namePath, ancestors, depth + 1); return; }
                if (current is not INamedTypeSymbol named) return;
                if (IsUri(named)) return;
                if (named.AllInterfaces.Any(item => item.ToDisplayString() == "System.Collections.IDictionary" ||
                        item.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.IReadOnlyDictionary<TKey, TValue>")) return;
                if (named.ToDisplayString() == "System.Text.Json.JsonElement") return;
                var list = named.AllInterfaces.FirstOrDefault(item => item.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.IList<T>");
                if (list != null) { Visit(list.TypeArguments[0], currentPath, namePath, ancestors, depth + 1); return; }
                if (named.AllInterfaces.Any(item => item.ToDisplayString() == "System.Collections.IEnumerable")) return;
                var names = new HashSet<string>(StringComparer.Ordinal);
                for (var level = named; level != null; level = level.BaseType)
                    foreach (var property in level.GetMembers().OfType<IPropertySymbol>().Where(Eligible))
                    {
                        if (!names.Add(property.Name) || contract.Positive(property) || contract.Positive(property.Type) ||
                            contract.Positive(property.ContainingType)) continue;
                        var full = currentPath + "." + property.Name;
                        var propertyNamePath = IsModelType(current) ? property.Name : namePath + "." + property.Name;
                        // Flat property capture converts TimeSpan to seconds and checks the resulting key again.
                        if (flat && depth == 0 && property.Type.ToDisplayString() is "System.TimeSpan" or "System.TimeSpan?")
                        {
                            full += "_Seconds";
                            propertyNamePath += "_Seconds";
                        }
                        // Source own-name diagnostics are emitted once at declaration, independent of logging calls.
                        if (DiagnosticRuleCatalog.MatchesField(propertyNamePath) && !contract.Exempt(property) &&
                            (!DiagnosticRuleCatalog.MatchesField(property.Name) || !property.Locations.Any(location => location.IsInSource)))
                            context.ReportDiagnostic(Diagnostic.Create(PathClassification, call.Syntax.GetLocation(), full, property.Name));
                        else if (!DiagnosticRuleCatalog.MatchesField(propertyNamePath) || contract.Exempt(property))
                            Visit(property.Type, full, propertyNamePath, ancestors, depth + 1);
                    }
            }
            finally { ancestors.Remove(current); }
        }
    }

    private static bool IsSubstituteVerification(IInvocationOperation call)
    {
        var receiver = call.Instance;
        while (receiver is IConversionOperation conversion) receiver = conversion.Operand;
        if (receiver is not IInvocationOperation verification) return false;
        var method = verification.TargetMethod.ReducedFrom ?? verification.TargetMethod;
        // Only the immediate verification receiver is exempt, never other calls on a substitute.
        return method.IsExtensionMethod && method.ContainingAssembly.Name == "NSubstitute" &&
            method.ContainingType.ToDisplayString() is "NSubstitute.SubstituteExtensions" or "NSubstitute.ReceivedExtensions.ReceivedExtensions" &&
            method.Name is "Received" or "DidNotReceive" or "ReceivedWithAnyArgs" or "DidNotReceiveWithAnyArgs";
    }

    private static bool IsModelType(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable)
            type = nullable.TypeArguments[0];
        return type is INamedTypeSymbol { IsAnonymousType: false } named && type.SpecialType == SpecialType.None &&
            type.TypeKind != TypeKind.Enum &&
            type.ToDisplayString() is not ("System.TimeSpan" or "System.DateTimeOffset" or "System.DateOnly" or "System.TimeOnly" or "System.Guid" or
                "System.Text.Json.JsonElement" or "DRN.Framework.Utils.Logging.ScopeEvent") &&
            !IsUri(named) &&
            type.ToDisplayString() != "System.Collections.IEnumerable" &&
            !named.AllInterfaces.Any(item => item.ToDisplayString() == "System.Collections.IEnumerable");
    }

    private static bool IsUri(INamedTypeSymbol type)
    {
        for (var current = type; current != null; current = current.BaseType)
            if (current.ToDisplayString() == "System.Uri") return true;
        return false;
    }

    internal sealed class ClassificationContract(INamedTypeSymbol sensitive, INamedTypeSymbol personal)
    {
        internal INamedTypeSymbol Sensitive { get; } = sensitive;
        internal bool Accepted(IPropertySymbol property) => Positive(property) || Positive(property.ContainingType) ||
            Positive(property.Type) || Exempt(property);

        internal bool Exempt(IPropertySymbol property) => property.GetAttributes().Any(attribute =>
                SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, Sensitive) && IsFalse(attribute));

        internal bool Positive(ISymbol symbol)
        {
            if (symbol is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable)
                symbol = nullable.TypeArguments[0];
            for (ISymbol? current = symbol; current != null; current = current switch
                 { IPropertySymbol property => property.OverriddenProperty, INamedTypeSymbol type => type.BaseType, _ => null })
                foreach (var attribute in current.GetAttributes())
                    if ((SymbolEqualityComparer.Default.Equals(current, symbol) || Inherited(attribute)) &&
                        (Derives(attribute.AttributeClass, personal) || Derives(attribute.AttributeClass, Sensitive) &&
                        (!SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, Sensitive) || !IsFalse(attribute)))) return true;
            return false;
        }

        internal bool HasUnknown(IPropertySymbol property) => property.GetAttributes().Concat(property.Type.GetAttributes())
            .Any(attribute => Ancestors(attribute.AttributeClass).Any(type =>
                type.ToDisplayString() == "Microsoft.Extensions.Compliance.Classification.DataClassificationAttribute"));

        internal static bool IsFalse(AttributeData attribute) => attribute.ConstructorArguments is [{ Value: false }];
        private static bool Inherited(AttributeData attribute)
        {
            foreach (var type in Ancestors(attribute.AttributeClass))
                foreach (var usage in type.GetAttributes())
                    if (usage.AttributeClass?.ToDisplayString() == "System.AttributeUsageAttribute")
                        return !usage.NamedArguments.Any(pair => pair.Key == "Inherited" && pair.Value.Value is false);
            return true;
        }
        internal static bool Derives(INamedTypeSymbol? type, INamedTypeSymbol parent) =>
            Ancestors(type).Any(candidate => SymbolEqualityComparer.Default.Equals(candidate, parent));
        private static IEnumerable<INamedTypeSymbol> Ancestors(INamedTypeSymbol? type)
        {
            for (; type != null; type = type.BaseType) yield return type;
        }
    }
}
