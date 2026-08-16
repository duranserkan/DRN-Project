using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DRN.Framework.SharedKernel.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SourceKnownEntityTypeAnalyzer : DiagnosticAnalyzer
{
    private const string SourceKnownEntityMetadataName = "DRN.Framework.SharedKernel.Domain.SourceKnownEntity";
    private const string EntityTypeAttributeMetadataName = "DRN.Framework.SharedKernel.Domain.EntityTypeAttribute";

    private sealed record EntityTypeDeclaration(
        byte EntityTypeValue,
        byte AppId,
        INamedTypeSymbol Symbol,
        Location Location);

    private sealed record EntityNameDeclaration(
        string Name,
        byte AppId,
        INamedTypeSymbol Symbol,
        Location Location);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(
            DiagnosticDescriptors.MissingEntityTypeAttribute,
            DiagnosticDescriptors.DuplicateEntityTypeValue,
            DiagnosticDescriptors.InvalidEntityTypeAttributeUsage,
            DiagnosticDescriptors.DuplicateEntityName);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(compilationContext =>
        {
            var sourceKnownEntitySymbol = compilationContext.Compilation.GetTypeByMetadataName(SourceKnownEntityMetadataName);
            var entityTypeAttributeSymbol = compilationContext.Compilation.GetTypeByMetadataName(EntityTypeAttributeMetadataName);

            if (sourceKnownEntitySymbol == null || entityTypeAttributeSymbol == null)
                return;

            var collectedEntityTypeDeclarations = new ConcurrentBag<EntityTypeDeclaration>();
            var collectedEntityNameDeclarations = new ConcurrentBag<EntityNameDeclaration>();

            var referencedEntitySymbols = ScanReferencedAssemblies(
                compilationContext.Compilation,
                sourceKnownEntitySymbol,
                compilationContext.CancellationToken);

            compilationContext.RegisterSymbolAction(symbolContext =>
            {
                var namedType = (INamedTypeSymbol)symbolContext.Symbol;
                if (namedType.TypeKind != TypeKind.Class)
                    return;

                var inheritsSourceKnownEntity = DerivesFrom(namedType, sourceKnownEntitySymbol);
                var entityTypeAttribute = FindAttribute(namedType, entityTypeAttributeSymbol);
                var isPrivate = namedType.DeclaredAccessibility == Accessibility.Private;

                if (inheritsSourceKnownEntity && !namedType.IsAbstract && !isPrivate)
                {
                    var entityTypeValue = (byte)0;
                    var declaredAppId = (byte)0;
                    var hasValidAttribute = entityTypeAttribute != null && TryGetEntityType(entityTypeAttribute, out entityTypeValue, out declaredAppId);

                    // 1. Buffer non-private entity class names for compilation end analysis (DRN0004 - Warning)
                    var location = namedType.Locations.Length > 0 ? namedType.Locations[0] : Location.None;
                    collectedEntityNameDeclarations.Add(new EntityNameDeclaration(namedType.Name, declaredAppId, namedType, location));

                    // 2. Check EntityType attribute presence (DRN0001) & collect for compilation end (DRN0002)
                    if (entityTypeAttribute == null)
                    {
                        symbolContext.ReportDiagnostic(Diagnostic.Create(
                            DiagnosticDescriptors.MissingEntityTypeAttribute,
                            location,
                            namedType.Name));
                    }
                    else if (hasValidAttribute)
                    {
                        var attrLocation = entityTypeAttribute.ApplicationSyntaxReference?.GetSyntax().GetLocation()
                                           ?? location;

                        collectedEntityTypeDeclarations.Add(new EntityTypeDeclaration(entityTypeValue, declaredAppId, namedType, attrLocation));
                    }
                }
                else if (entityTypeAttribute != null)
                {
                    // EntityTypeAttribute applied to abstract class, private class, or non-SourceKnownEntity (DRN0003)
                    var location = entityTypeAttribute.ApplicationSyntaxReference?.GetSyntax().GetLocation()
                                   ?? (namedType.Locations.Length > 0 ? namedType.Locations[0] : Location.None);

                    symbolContext.ReportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.InvalidEntityTypeAttributeUsage,
                        location,
                        namedType.Name));
                }
            }, SymbolKind.NamedType);

            compilationContext.RegisterCompilationEndAction(endContext =>
            {
                // 1. Process distinct referenced entities (deduplicated by SymbolEqualityComparer for diamond dependencies)
                var distinctReferencedEntities = referencedEntitySymbols
                    .Distinct(SymbolEqualityComparer.Default)
                    .OfType<INamedTypeSymbol>()
                    .ToList();

                var referencedByValue = distinctReferencedEntities
                    .Select(s =>
                    {
                        var attr = FindAttribute(s, entityTypeAttributeSymbol);
                        if (attr != null && TryGetEntityType(attr, out var val, out var refAppId))
                        {
                            return (Symbol: s, HasValue: true, Value: val, AppId: refAppId);
                        }

                        return (Symbol: s, HasValue: false, Value: (byte)0, AppId: (byte)0);
                    })
                    .Where(x => x.HasValue)
                    .GroupBy(x => (x.AppId, x.Value));

                var referencedEntityTypeMap = new System.Collections.Generic.Dictionary<(byte AppId, byte EntityTypeValue), System.Collections.Generic.List<INamedTypeSymbol>>();
                foreach (var refGroup in referencedByValue)
                {
                    var groupKey = refGroup.Key; // (AppId, Value)
                    var orderedRefGroup = refGroup
                        .Select(x => x.Symbol)
                        .OrderBy(s => s.ContainingAssembly.Name, StringComparer.Ordinal)
                        .ThenBy(s => s.ToDisplayString(), StringComparer.Ordinal)
                        .ToList();

                    referencedEntityTypeMap[groupKey] = orderedRefGroup;

                    // Report collisions between distinct referenced assemblies (DRN0002)
                    if (orderedRefGroup.Count > 1)
                    {
                        var firstRefSymbol = orderedRefGroup[0];
                        var firstTargetName = $"{firstRefSymbol.ContainingAssembly.Name}::{firstRefSymbol.Name}";

                        for (var i = 1; i < orderedRefGroup.Count; i++)
                        {
                            var duplicateRefSymbol = orderedRefGroup[i];
                            var duplicateTargetName = $"{duplicateRefSymbol.ContainingAssembly.Name}::{duplicateRefSymbol.Name}";

                            endContext.ReportDiagnostic(Diagnostic.Create(
                                DiagnosticDescriptors.DuplicateEntityTypeValue,
                                Location.None,
                                groupKey.Value,
                                groupKey.AppId,
                                duplicateTargetName,
                                firstTargetName));
                        }
                    }
                }

                // Check local declarations against each other and against referenced assemblies (DRN0002)
                var localGroupedByValue = collectedEntityTypeDeclarations.GroupBy(d => (d.AppId, d.EntityTypeValue));

                foreach (var group in localGroupedByValue)
                {
                    var groupKey = group.Key; // (AppId, EntityTypeValue)
                    var entityTypeValue = groupKey.EntityTypeValue;

                    var orderedDeclarations = group
                        .OrderBy(d => d.Location.SourceTree?.FilePath, StringComparer.Ordinal)
                        .ThenBy(d => d.Location.SourceSpan.Start)
                        .ThenBy(d => d.Location.SourceSpan.Length)
                        .ToList();

                    if (referencedEntityTypeMap.TryGetValue(groupKey, out var refSymbols) && refSymbols.Count > 0)
                    {
                        var firstRefSymbol = refSymbols[0];
                        var referencedTargetName = $"{firstRefSymbol.ContainingAssembly.Name}::{firstRefSymbol.Name}";

                        foreach (var decl in orderedDeclarations)
                        {
                            if (!SymbolEqualityComparer.Default.Equals(firstRefSymbol, decl.Symbol))
                            {
                                endContext.ReportDiagnostic(Diagnostic.Create(
                                    DiagnosticDescriptors.DuplicateEntityTypeValue,
                                    decl.Location,
                                    entityTypeValue,
                                    groupKey.AppId,
                                    decl.Symbol.Name,
                                    referencedTargetName));
                            }
                        }
                    }
                    else if (orderedDeclarations.Count > 1)
                    {
                        var firstDecl = orderedDeclarations[0];
                        for (var i = 1; i < orderedDeclarations.Count; i++)
                        {
                            var duplicateDecl = orderedDeclarations[i];
                            if (!SymbolEqualityComparer.Default.Equals(firstDecl.Symbol, duplicateDecl.Symbol))
                            {
                                endContext.ReportDiagnostic(Diagnostic.Create(
                                    DiagnosticDescriptors.DuplicateEntityTypeValue,
                                    duplicateDecl.Location,
                                    entityTypeValue,
                                    groupKey.AppId,
                                    duplicateDecl.Symbol.Name,
                                    firstDecl.Symbol.Name));
                            }
                        }
                    }
                }

                // 2. Process duplicate entity class names (DRN0004)
                var referencedByName = distinctReferencedEntities
                    .Select(s =>
                    {
                        var attr = FindAttribute(s, entityTypeAttributeSymbol);
                        var refAppId = attr != null && TryGetEntityType(attr, out _, out var app) ? app : (byte)0;
                        return (Symbol: s, AppId: refAppId);
                    })
                    .GroupBy(x => (x.AppId, Name: x.Symbol.Name), x => x.Symbol);

                var referencedEntityNameMap = new System.Collections.Generic.Dictionary<(byte AppId, string Name), System.Collections.Generic.List<INamedTypeSymbol>>();
                foreach (var refNameGroup in referencedByName)
                {
                    var groupKey = refNameGroup.Key; // (AppId, Name)
                    var orderedRefGroup = refNameGroup
                        .OrderBy(s => s.ContainingAssembly.Name, StringComparer.Ordinal)
                        .ThenBy(s => s.ToDisplayString(), StringComparer.Ordinal)
                        .ToList();

                    referencedEntityNameMap[groupKey] = orderedRefGroup;

                    // Report collisions between distinct referenced assemblies (DRN0004)
                    if (orderedRefGroup.Count > 1)
                    {
                        var firstRefSymbol = orderedRefGroup[0];
                        var firstName = firstRefSymbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);

                        for (var i = 1; i < orderedRefGroup.Count; i++)
                        {
                            var duplicateRefSymbol = orderedRefGroup[i];
                            var duplicateName = duplicateRefSymbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);

                            endContext.ReportDiagnostic(Diagnostic.Create(
                                DiagnosticDescriptors.DuplicateEntityName,
                                Location.None,
                                duplicateName,
                                firstName,
                                groupKey.AppId));
                        }
                    }
                }

                // Check local declarations against each other and against referenced assemblies (DRN0004)
                var localGroupedByName = collectedEntityNameDeclarations.GroupBy(d => (d.AppId, d.Name));

                foreach (var group in localGroupedByName)
                {
                    var groupKey = group.Key; // (AppId, Name)

                    var orderedDeclarations = group
                        .OrderBy(d => d.Location.SourceTree?.FilePath, StringComparer.Ordinal)
                        .ThenBy(d => d.Location.SourceSpan.Start)
                        .ThenBy(d => d.Location.SourceSpan.Length)
                        .ToList();

                    if (orderedDeclarations.Count == 0)
                        continue;

                    var firstDecl = orderedDeclarations[0];

                    if (referencedEntityNameMap.TryGetValue(groupKey, out var refSymbols) && refSymbols.Count > 0 &&
                        !SymbolEqualityComparer.Default.Equals(refSymbols[0], firstDecl.Symbol))
                    {
                        endContext.ReportDiagnostic(Diagnostic.Create(
                            DiagnosticDescriptors.DuplicateEntityName,
                            firstDecl.Location,
                            firstDecl.Symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
                            refSymbols[0].ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
                            groupKey.AppId));
                    }

                    for (var i = 1; i < orderedDeclarations.Count; i++)
                    {
                        var duplicateDecl = orderedDeclarations[i];
                        if (!SymbolEqualityComparer.Default.Equals(firstDecl.Symbol, duplicateDecl.Symbol))
                        {
                            endContext.ReportDiagnostic(Diagnostic.Create(
                                DiagnosticDescriptors.DuplicateEntityName,
                                duplicateDecl.Location,
                                duplicateDecl.Symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
                                firstDecl.Symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
                                groupKey.AppId));
                        }
                    }
                }
            });
        });
    }

    private static ConcurrentBag<INamedTypeSymbol> ScanReferencedAssemblies(
        Compilation compilation,
        INamedTypeSymbol sourceKnownEntitySymbol,
        CancellationToken cancellationToken)
    {
        var collectedEntities = new ConcurrentBag<INamedTypeSymbol>();
        var targetAssembly = sourceKnownEntitySymbol.ContainingAssembly;

        foreach (var referencedAssembly in compilation.SourceModule.ReferencedAssemblySymbols)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var name = referencedAssembly.Name;
            if (name.StartsWith("System.", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("System", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("mscorlib", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("netstandard", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!ReferencesAssembly(referencedAssembly, targetAssembly))
            {
                continue;
            }

            ScanNamespace(referencedAssembly.GlobalNamespace, sourceKnownEntitySymbol, collectedEntities, cancellationToken);
        }

        return collectedEntities;
    }

    private static bool ReferencesAssembly(IAssemblySymbol assembly, IAssemblySymbol? targetAssembly)
    {
        if (targetAssembly == null)
            return false;

        if (SymbolEqualityComparer.Default.Equals(assembly, targetAssembly))
            return true;

        var targetIdentity = targetAssembly.Identity;
        foreach (var module in assembly.Modules)
        {
            foreach (var referencedAssemblySymbol in module.ReferencedAssemblySymbols)
            {
                if (SymbolEqualityComparer.Default.Equals(referencedAssemblySymbol, targetAssembly))
                    return true;
            }

            foreach (var referencedIdentity in module.ReferencedAssemblies)
            {
                if (AssemblyIdentityComparer.Default.ReferenceMatchesDefinition(referencedIdentity, targetIdentity) ||
                    string.Equals(referencedIdentity.Name, targetIdentity.Name, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static void ScanNamespace(
        INamespaceSymbol namespaceSymbol,
        INamedTypeSymbol sourceKnownEntitySymbol,
        ConcurrentBag<INamedTypeSymbol> collectedEntities,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        foreach (var type in namespaceSymbol.GetTypeMembers())
        {
            ScanType(type, sourceKnownEntitySymbol, collectedEntities, cancellationToken);
        }

        foreach (var nestedNamespace in namespaceSymbol.GetNamespaceMembers())
        {
            ScanNamespace(nestedNamespace, sourceKnownEntitySymbol, collectedEntities, cancellationToken);
        }
    }

    private static void ScanType(
        INamedTypeSymbol typeSymbol,
        INamedTypeSymbol sourceKnownEntitySymbol,
        ConcurrentBag<INamedTypeSymbol> collectedEntities,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (typeSymbol.DeclaredAccessibility == Accessibility.Private)
            return;

        if (typeSymbol.TypeKind == TypeKind.Class && !typeSymbol.IsAbstract && DerivesFrom(typeSymbol, sourceKnownEntitySymbol))
        {
            collectedEntities.Add(typeSymbol);
        }

        foreach (var nestedType in typeSymbol.GetTypeMembers())
        {
            ScanType(nestedType, sourceKnownEntitySymbol, collectedEntities, cancellationToken);
        }
    }

    private static bool TryGetEntityType(AttributeData attributeData, out byte entityTypeValue, out byte appId)
    {
        entityTypeValue = 0;
        appId = 0;

        if (attributeData.ConstructorArguments.Length > 0 &&
            TryExtractByte(attributeData.ConstructorArguments[0].Value, out entityTypeValue))
        {
            if (attributeData.ConstructorArguments.Length > 1 &&
                TryExtractByte(attributeData.ConstructorArguments[1].Value, out var parsedAppId))
            {
                appId = parsedAppId;
                return true;
            }

            foreach (var namedArg in attributeData.NamedArguments)
            {
                if (namedArg.Key == "AppId" &&
                    TryExtractByte(namedArg.Value.Value, out var namedAppId))
                {
                    appId = namedAppId;
                    return true;
                }
            }

            // Check generic type argument or base class generic type argument (e.g. EntityTypeAttribute<TApp>)
            var current = attributeData.AttributeClass;
            while (current != null)
            {
                if (current.IsGenericType && current.TypeArguments.Length > 0)
                {
                    var appType = current.TypeArguments[0];
                    if (TryExtractAppIdFromType(appType, out var extractedAppId))
                    {
                        appId = extractedAppId;
                        return true;
                    }
                }

                current = current.BaseType;
            }

            return true;
        }

        return false;
    }

    private static bool TryExtractAppIdFromType(ITypeSymbol appTypeSymbol, out byte appId)
    {
        appId = 0;

        var fullName = appTypeSymbol.ToDisplayString();
        if (fullName == "DRN.Framework.SharedKernel.Domain.DefaultApp")
        {
            appId = 0;
            return true;
        }

        if (fullName == "DRN.Framework.SharedKernel.Domain.NexusApp")
        {
            appId = 126;
            return true;
        }

        if (fullName == "DRN.Framework.SharedKernel.Domain.TestApp")
        {
            appId = 127;
            return true;
        }

        foreach (var member in appTypeSymbol.GetMembers())
        {
            if (member is IFieldSymbol field && field.HasConstantValue &&
                (field.Name == "AppId" || field.Name == "Value") &&
                TryExtractByte(field.ConstantValue, out appId))
            {
                return true;
            }
        }

        foreach (var attr in appTypeSymbol.GetAttributes())
        {
            if ((attr.AttributeClass?.Name is "AppIdAttribute" or "AppId") &&
                attr.ConstructorArguments.Length > 0 &&
                TryExtractByte(attr.ConstructorArguments[0].Value, out appId))
            {
                return true;
            }
        }

        foreach (var member in appTypeSymbol.GetMembers("AppId"))
        {
            if (member is IPropertySymbol prop)
            {
                foreach (var syntaxRef in prop.DeclaringSyntaxReferences)
                {
                    var syntax = syntaxRef.GetSyntax();
                    if (syntax is Microsoft.CodeAnalysis.CSharp.Syntax.PropertyDeclarationSyntax propSyntax)
                    {
                        var expr = propSyntax.ExpressionBody?.Expression
                                   ?? propSyntax.Initializer?.Value;

                        if (expr == null && propSyntax.AccessorList != null)
                        {
                            foreach (var accessor in propSyntax.AccessorList.Accessors)
                            {
                                expr = accessor.ExpressionBody?.Expression;
                                if (expr != null)
                                    break;

                                if (accessor.Body != null)
                                {
                                    foreach (var stmt in accessor.Body.Statements)
                                    {
                                        if (stmt is Microsoft.CodeAnalysis.CSharp.Syntax.ReturnStatementSyntax returnStmt &&
                                            returnStmt.Expression != null)
                                        {
                                            expr = returnStmt.Expression;
                                            break;
                                        }
                                    }
                                }

                                if (expr != null)
                                    break;
                            }
                        }

                        if (expr != null && TryExtractByteFromSyntax(expr, appTypeSymbol, out appId))
                        {
                            return true;
                        }
                    }
                }
            }
        }

        return false;
    }

    private static bool TryExtractByteFromSyntax(
        Microsoft.CodeAnalysis.CSharp.Syntax.ExpressionSyntax expr,
        ITypeSymbol appTypeSymbol,
        out byte byteValue)
    {
        if (expr is Microsoft.CodeAnalysis.CSharp.Syntax.LiteralExpressionSyntax literal &&
            literal.Token.Value is object val)
        {
            return TryExtractByte(val, out byteValue);
        }

        if (expr is Microsoft.CodeAnalysis.CSharp.Syntax.CastExpressionSyntax cast)
        {
            return TryExtractByteFromSyntax(cast.Expression, appTypeSymbol, out byteValue);
        }

        if (expr is Microsoft.CodeAnalysis.CSharp.Syntax.IdentifierNameSyntax idName)
        {
            var memberName = idName.Identifier.Text;
            if (TryExtractConstantFromTypeOrContainers(appTypeSymbol, memberName, out byteValue))
            {
                return true;
            }
        }

        if (expr is Microsoft.CodeAnalysis.CSharp.Syntax.MemberAccessExpressionSyntax memberAccess)
        {
            var memberName = memberAccess.Name.Identifier.Text;
            if (TryExtractConstantFromTypeOrContainers(appTypeSymbol, memberName, out byteValue))
            {
                return true;
            }
        }

        byteValue = 0;
        return false;
    }

    private static bool TryExtractConstantFromTypeOrContainers(
        ITypeSymbol appTypeSymbol,
        string memberName,
        out byte byteValue)
    {
        foreach (var member in appTypeSymbol.GetMembers(memberName))
        {
            if (member is IFieldSymbol field && field.HasConstantValue && TryExtractByte(field.ConstantValue, out byteValue))
            {
                return true;
            }
        }

        var containingType = appTypeSymbol.ContainingType;
        while (containingType != null)
        {
            foreach (var member in containingType.GetMembers(memberName))
            {
                if (member is IFieldSymbol field && field.HasConstantValue && TryExtractByte(field.ConstantValue, out byteValue))
                {
                    return true;
                }
            }

            containingType = containingType.ContainingType;
        }

        byteValue = 0;
        return false;
    }

    private static bool TryExtractByte(object? rawValue, out byte byteValue)
    {
        if (rawValue is byte b)
        {
            byteValue = b;
            return true;
        }

        if (rawValue is sbyte or short or ushort or int or uint or long or ulong)
        {
            try
            {
                byteValue = Convert.ToByte(rawValue);
                return true;
            }
            catch (Exception ex) when (ex is OverflowException or InvalidCastException)
            {
                // Ignored - value outside byte range or unsupported cast
            }
        }

        byteValue = 0;
        return false;
    }

    private static bool DerivesFrom(INamedTypeSymbol typeSymbol, INamedTypeSymbol baseTargetSymbol)
    {
        var current = typeSymbol.BaseType;
        while (current != null)
        {
            if (SymbolEqualityComparer.Default.Equals(current, baseTargetSymbol) ||
                current.ToDisplayString() == SourceKnownEntityMetadataName)
                return true;

            current = current.BaseType;
        }

        return false;
    }

    private static AttributeData? FindAttribute(INamedTypeSymbol typeSymbol, INamedTypeSymbol attributeSymbol)
    {
        foreach (var attribute in typeSymbol.GetAttributes())
        {
            var attrClass = attribute.AttributeClass;
            if (attrClass == null)
                continue;

            if (SymbolEqualityComparer.Default.Equals(attrClass, attributeSymbol) ||
                attrClass.ToDisplayString() == EntityTypeAttributeMetadataName ||
                IsOrDerivesFromEntityTypeAttribute(attrClass, attributeSymbol))
            {
                return attribute;
            }
        }

        return null;
    }

    private static bool IsOrDerivesFromEntityTypeAttribute(INamedTypeSymbol attrClass, INamedTypeSymbol baseAttributeSymbol)
    {
        var current = attrClass;
        while (current != null)
        {
            if (SymbolEqualityComparer.Default.Equals(current, baseAttributeSymbol) ||
                current.ToDisplayString() == EntityTypeAttributeMetadataName ||
                (current.IsGenericType && current.ConstructedFrom.ToDisplayString().StartsWith("DRN.Framework.SharedKernel.Domain.EntityTypeAttribute<", StringComparison.Ordinal)))
            {
                return true;
            }

            current = current.BaseType;
        }

        return false;
    }
}
