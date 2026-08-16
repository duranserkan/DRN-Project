using System.Collections.Concurrent;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DRN.Framework.SharedKernel.Analyzers;

internal static class EntityAnalyzerHelper
{
    internal const string SourceKnownEntityMetadataName = "DRN.Framework.SharedKernel.Domain.SourceKnownEntity";
    internal const string EntityTypeAttributeMetadataName = "DRN.Framework.SharedKernel.Domain.EntityTypeAttribute";
    private const string AppIdName = "AppId";

    internal static ConcurrentBag<INamedTypeSymbol> ScanReferencedAssemblies(
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
            if (module.ReferencedAssemblySymbols.Any(referencedAssemblySymbol => SymbolEqualityComparer.Default.Equals(referencedAssemblySymbol, targetAssembly)))
                return true;

            if (module.ReferencedAssemblies.Any(referencedIdentity =>
                    AssemblyIdentityComparer.Default.ReferenceMatchesDefinition(referencedIdentity, targetIdentity) ||
                    string.Equals(referencedIdentity.Name, targetIdentity.Name, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
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

        if (typeSymbol is { TypeKind: TypeKind.Class, IsAbstract: false } && DerivesFrom(typeSymbol, sourceKnownEntitySymbol))
        {
            collectedEntities.Add(typeSymbol);
        }

        foreach (var nestedType in typeSymbol.GetTypeMembers())
        {
            ScanType(nestedType, sourceKnownEntitySymbol, collectedEntities, cancellationToken);
        }
    }

    internal static bool DerivesFrom(INamedTypeSymbol typeSymbol, INamedTypeSymbol baseTargetSymbol)
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

    internal static AttributeData? FindAttribute(INamedTypeSymbol typeSymbol, INamedTypeSymbol attributeSymbol)
    {
        foreach (var attribute in typeSymbol.GetAttributes())
        {
            var attrClass = attribute.AttributeClass;
            if (attrClass == null)
                continue;

            if (SymbolEqualityComparer.Default.Equals(attrClass, attributeSymbol) ||
                attrClass.ToDisplayString() == EntityTypeAttributeMetadataName ||
                IsOrDerivesFromEntityTypeAttribute(attrClass, attributeSymbol))
                return attribute;
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

    internal static bool TryGetEntityType(AttributeData attributeData, out byte entityTypeValue, out byte appId)
    {
        entityTypeValue = 0;
        appId = 0;

        if (attributeData.ConstructorArguments.Length == 0 || !TryExtractByte(attributeData.ConstructorArguments[0].Value, out entityTypeValue))
            return false;

        if (TryExtractAppIdFromAttribute(attributeData, out var extractedAppId))
            appId = extractedAppId;

        return true;
    }

    private static bool TryExtractAppIdFromAttribute(AttributeData attributeData, out byte appId)
    {
        if (attributeData.ConstructorArguments.Length > 1 && TryExtractByte(attributeData.ConstructorArguments[1].Value, out appId))
            return true;

        if (TryExtractAppIdFromNamedArguments(attributeData.NamedArguments, out appId))
            return true;

        return TryExtractAppIdFromClassHierarchy(attributeData.AttributeClass, out appId);
    }

    private static bool TryExtractAppIdFromNamedArguments(ImmutableArray<KeyValuePair<string, TypedConstant>> namedArguments, out byte appId)
    {
        foreach (var namedArg in namedArguments)
        {
            if (namedArg.Key == AppIdName && TryExtractByte(namedArg.Value.Value, out appId))
                return true;
        }

        appId = 0;
        return false;
    }

    private static bool TryExtractAppIdFromClassHierarchy(INamedTypeSymbol? attributeClass, out byte appId)
    {
        var current = attributeClass;
        while (current != null)
        {
            if (current is { IsGenericType: true, TypeArguments.Length: > 0 } && TryExtractAppIdFromType(current.TypeArguments[0], out appId))
                return true;

            current = current.BaseType;
        }

        appId = 0;
        return false;
    }

    private static bool TryExtractAppIdFromType(ITypeSymbol appTypeSymbol, out byte appId)
    {
        if (TryExtractAppIdFromKnownTypeName(appTypeSymbol.ToDisplayString(), out appId))
            return true;

        if (TryExtractAppIdFromConstantMembers(appTypeSymbol, out appId))
            return true;

        if (TryExtractAppIdFromTypeAttributes(appTypeSymbol, out appId))
            return true;

        return TryExtractAppIdFromPropertySyntax(appTypeSymbol, out appId);
    }

    private static bool TryExtractAppIdFromKnownTypeName(string fullName, out byte appId)
    {
        switch (fullName)
        {
            case "DRN.Framework.SharedKernel.Domain.DefaultApp":
                appId = 0;
                return true;
            case "DRN.Framework.SharedKernel.Domain.NexusApp":
                appId = 126;
                return true;
            case "DRN.Framework.SharedKernel.Domain.TestApp":
                appId = 127;
                return true;
            default:
                appId = 0;
                return false;
        }
    }

    private static bool TryExtractAppIdFromConstantMembers(ITypeSymbol appTypeSymbol, out byte appId)
    {
        foreach (var member in appTypeSymbol.GetMembers())
        {
            if (member is IFieldSymbol { HasConstantValue: true, Name: AppIdName or "Value" } field && TryExtractByte(field.ConstantValue, out appId))
                return true;
        }

        appId = 0;
        return false;
    }

    private static bool TryExtractAppIdFromTypeAttributes(ITypeSymbol appTypeSymbol, out byte appId)
    {
        foreach (var attr in appTypeSymbol.GetAttributes())
        {
            if (attr.AttributeClass?.Name is "AppIdAttribute" or AppIdName && attr.ConstructorArguments.Length > 0 &&
                TryExtractByte(attr.ConstructorArguments[0].Value, out appId))
                return true;
        }

        appId = 0;
        return false;
    }

    private static bool TryExtractAppIdFromPropertySyntax(ITypeSymbol appTypeSymbol, out byte appId)
    {
        foreach (var member in appTypeSymbol.GetMembers(AppIdName))
        {
            if (member is not IPropertySymbol prop)
                continue;

            if (TryExtractAppIdFromPropertySymbol(prop, appTypeSymbol, out appId))
                return true;
        }

        appId = 0;
        return false;
    }

    private static bool TryExtractAppIdFromPropertySymbol(IPropertySymbol prop, ITypeSymbol appTypeSymbol, out byte appId)
    {
        foreach (var syntaxRef in prop.DeclaringSyntaxReferences)
        {
            if (syntaxRef.GetSyntax() is PropertyDeclarationSyntax propSyntax &&
                TryExtractExpressionFromProperty(propSyntax, out var expr) &&
                TryExtractByteFromSyntax(expr!, appTypeSymbol, out appId))
                return true;
        }

        appId = 0;
        return false;
    }

    private static bool TryExtractExpressionFromProperty(PropertyDeclarationSyntax propSyntax, out ExpressionSyntax? expression)
    {
        expression = propSyntax.ExpressionBody?.Expression ?? propSyntax.Initializer?.Value;
        if (expression != null)
            return true;

        if (propSyntax.AccessorList != null)
            return TryExtractExpressionFromAccessors(propSyntax.AccessorList.Accessors, out expression);

        return false;
    }

    private static bool TryExtractExpressionFromAccessors(SyntaxList<AccessorDeclarationSyntax> accessors, out ExpressionSyntax? expression)
    {
        foreach (var accessor in accessors)
        {
            expression = accessor.ExpressionBody?.Expression;
            if (expression != null)
                return true;

            if (accessor.Body != null && TryExtractReturnExpression(accessor.Body.Statements, out expression))
                return true;
        }

        expression = null;
        return false;
    }

    private static bool TryExtractReturnExpression(SyntaxList<StatementSyntax> statements, out ExpressionSyntax? expression)
    {
        foreach (var stmt in statements)
        {
            if (stmt is not ReturnStatementSyntax { Expression: not null } returnStmt)
                continue;
            expression = returnStmt.Expression;
            return true;
        }

        expression = null;
        return false;
    }

    private static bool TryExtractByteFromSyntax(ExpressionSyntax expr, ITypeSymbol appTypeSymbol, out byte byteValue)
    {
        switch (expr)
        {
            case LiteralExpressionSyntax { Token.Value: { } val }:
                return TryExtractByte(val, out byteValue);
            case CastExpressionSyntax cast:
                return TryExtractByteFromSyntax(cast.Expression, appTypeSymbol, out byteValue);
            case IdentifierNameSyntax idName:
            {
                var memberName = idName.Identifier.Text;
                if (TryExtractConstantFromTypeOrContainers(appTypeSymbol, memberName, out byteValue))
                    return true;

                break;
            }
            case MemberAccessExpressionSyntax memberAccess:
            {
                var memberName = memberAccess.Name.Identifier.Text;
                if (TryExtractConstantFromTypeOrContainers(appTypeSymbol, memberName, out byteValue))
                    return true;

                break;
            }
        }

        byteValue = 0;
        return false;
    }

    private static bool TryExtractConstantFromTypeOrContainers(ITypeSymbol appTypeSymbol, string memberName, out byte byteValue)
    {
        foreach (var member in appTypeSymbol.GetMembers(memberName))
        {
            if (member is IFieldSymbol { HasConstantValue: true } field && TryExtractByte(field.ConstantValue, out byteValue))
                return true;
        }

        var containingType = appTypeSymbol.ContainingType;
        while (containingType != null)
        {
            foreach (var member in containingType.GetMembers(memberName))
            {
                if (member is IFieldSymbol { HasConstantValue: true } field && TryExtractByte(field.ConstantValue, out byteValue))
                    return true;
            }

            containingType = containingType.ContainingType;
        }

        byteValue = 0;
        return false;
    }

    private static bool TryExtractByte(object? rawValue, out byte byteValue)
    {
        switch (rawValue)
        {
            case byte b:
                byteValue = b;
                return true;
            case sbyte or short or ushort or int or uint or long or ulong:
                try
                {
                    byteValue = Convert.ToByte(rawValue);
                    return true;
                }
                catch (Exception ex) when (ex is OverflowException or InvalidCastException)
                {
                    // Ignored - value outside byte range or unsupported cast
                }

                break;
        }

        byteValue = 0;
        return false;
    }

    internal static List<INamedTypeSymbol> OrderSymbols(IEnumerable<INamedTypeSymbol> symbols) => symbols
        .OrderBy(s => s.ContainingAssembly.Name, StringComparer.Ordinal)
        .ThenBy(s => s.ToDisplayString(), StringComparer.Ordinal).ToList();

    internal static List<T> OrderByLocation<T>(IEnumerable<T> items, Func<T, Location> locationSelector) => items
        .OrderBy(d => locationSelector(d).SourceTree?.FilePath, StringComparer.Ordinal)
        .ThenBy(d => locationSelector(d).SourceSpan.Start)
        .ThenBy(d => locationSelector(d).SourceSpan.Length)
        .ToList();
}
