using System.Collections.Concurrent;
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

    internal static bool TryGetEntityType(AttributeData attributeData, out byte entityTypeValue, out byte appId)
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
                if (namedArg.Key == AppIdName &&
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
                if (current is { IsGenericType: true, TypeArguments.Length: > 0 })
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
        }

        foreach (var member in appTypeSymbol.GetMembers())
        {
            if (member is IFieldSymbol { HasConstantValue: true, Name: AppIdName or "Value" } field &&
                TryExtractByte(field.ConstantValue, out appId))
                return true;
        }

        foreach (var attr in appTypeSymbol.GetAttributes())
        {
            if (attr.AttributeClass?.Name is "AppIdAttribute" or AppIdName &&
                attr.ConstructorArguments.Length > 0 &&
                TryExtractByte(attr.ConstructorArguments[0].Value, out appId))
                return true;
        }

        foreach (var member in appTypeSymbol.GetMembers(AppIdName))
        {
            if (member is not IPropertySymbol prop) continue;

            foreach (var syntax in prop.DeclaringSyntaxReferences.Select(syntaxRef => syntaxRef.GetSyntax()))
            {
                if (syntax is not PropertyDeclarationSyntax propSyntax) continue;

                var expr = propSyntax.ExpressionBody?.Expression ?? propSyntax.Initializer?.Value;

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
                                if (stmt is not ReturnStatementSyntax { Expression: not null } returnStmt)
                                    continue;
                                expr = returnStmt.Expression;
                                break;
                            }
                        }

                        if (expr != null)
                            break;
                    }
                }

                if (expr != null && TryExtractByteFromSyntax(expr, appTypeSymbol, out appId))
                    return true;
            }
        }

        return false;
    }

    private static bool TryExtractByteFromSyntax(
        ExpressionSyntax expr,
        ITypeSymbol appTypeSymbol,
        out byte byteValue)
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

    private static bool TryExtractConstantFromTypeOrContainers(
        ITypeSymbol appTypeSymbol,
        string memberName,
        out byte byteValue)
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

    internal static List<INamedTypeSymbol> OrderSymbols(IEnumerable<INamedTypeSymbol> symbols) =>
        symbols
            .OrderBy(s => s.ContainingAssembly.Name, StringComparer.Ordinal)
            .ThenBy(s => s.ToDisplayString(), StringComparer.Ordinal)
            .ToList();

    internal static List<T> OrderByLocation<T>(IEnumerable<T> items, Func<T, Location> locationSelector) =>
        items
            .OrderBy(d => locationSelector(d).SourceTree?.FilePath, StringComparer.Ordinal)
            .ThenBy(d => locationSelector(d).SourceSpan.Start)
            .ThenBy(d => locationSelector(d).SourceSpan.Length)
            .ToList();
}
