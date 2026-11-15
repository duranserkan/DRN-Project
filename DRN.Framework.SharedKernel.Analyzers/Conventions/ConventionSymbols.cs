using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace DRN.Framework.SharedKernel.Analyzers.Conventions;

internal sealed class ConventionSymbols(Compilation compilation)
{
    internal INamedTypeSymbol? Entity { get; } = compilation.GetTypeByMetadataName("DRN.Framework.SharedKernel.Domain.SourceKnownEntity");
    internal INamedTypeSymbol? Aggregate { get; } = compilation.GetTypeByMetadataName("DRN.Framework.SharedKernel.Domain.AggregateRoot");
    internal INamedTypeSymbol? IdentityUser { get; } = compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Identity.IdentityUser`1");
    internal INamedTypeSymbol? Context { get; } = compilation.GetTypeByMetadataName("DRN.Framework.EntityFramework.Context.IDrnContext");
    internal INamedTypeSymbol? DrnContext { get; } = compilation.GetTypeByMetadataName("DRN.Framework.EntityFramework.Context.DrnContext`1");
    internal INamedTypeSymbol? DrnIdentityContext { get; } = compilation.GetTypeByMetadataName("DRN.Framework.EntityFramework.Context.Identity.DrnContextIdentity`2");
    internal INamedTypeSymbol? DbContext { get; } = compilation.GetTypeByMetadataName("Microsoft.EntityFrameworkCore.DbContext");
    internal INamedTypeSymbol? ContextOptions { get; } = compilation.GetTypeByMetadataName("Microsoft.EntityFrameworkCore.DbContextOptions`1");
    internal INamedTypeSymbol? DbSet { get; } = compilation.GetTypeByMetadataName("Microsoft.EntityFrameworkCore.DbSet`1");
    internal INamedTypeSymbol? Configuration { get; } = compilation.GetTypeByMetadataName("Microsoft.EntityFrameworkCore.IEntityTypeConfiguration`1");
    internal INamedTypeSymbol? ConfigurationAttribute { get; } = compilation.GetTypeByMetadataName("Microsoft.EntityFrameworkCore.EntityTypeConfigurationAttribute");
    internal INamedTypeSymbol? ModelBuilder { get; } = compilation.GetTypeByMetadataName("Microsoft.EntityFrameworkCore.ModelBuilder");
    internal INamedTypeSymbol? EntityBuilder { get; } = compilation.GetTypeByMetadataName("Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder");
    internal INamedTypeSymbol? GenericEntityBuilder { get; } = compilation.GetTypeByMetadataName("Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder`1");
    internal INamedTypeSymbol? Repository { get; } = compilation.GetTypeByMetadataName("DRN.Framework.EntityFramework.Domain.SourceKnownRepository`2");
    internal INamedTypeSymbol? RepositoryContract { get; } = compilation.GetTypeByMetadataName("DRN.Framework.SharedKernel.Domain.Repository.ISourceKnownRepository`1");
    internal INamedTypeSymbol? ServiceContract { get; } = compilation.GetTypeByMetadataName("DRN.Framework.SharedKernel.Domain.ISourceKnownService`1");
    internal INamedTypeSymbol? Dto { get; } = compilation.GetTypeByMetadataName("DRN.Framework.SharedKernel.Domain.Dto");
    internal INamedTypeSymbol? Controller { get; } = compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Mvc.ControllerBase");
    internal INamedTypeSymbol? PageModel { get; } = compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Mvc.RazorPages.PageModel");
    internal INamedTypeSymbol? OwnershipAttribute { get; } = compilation.GetTypeByMetadataName("DRN.Framework.SharedKernel.Attributes.SourceKnownSubdomainAttribute");
    internal INamedTypeSymbol? PersistenceAttribute { get; } = compilation.GetTypeByMetadataName("DRN.Framework.EntityFramework.Attributes.SourceKnownPersistenceAttribute");
    internal INamedTypeSymbol? LifetimeAttribute { get; } = compilation.GetTypeByMetadataName("DRN.Framework.Utils.DependencyInjection.Attributes.LifetimeAttribute");
    internal INamedTypeSymbol? GenericLifetimeAttribute { get; } = compilation.GetTypeByMetadataName("DRN.Framework.Utils.DependencyInjection.Attributes.LifetimeAttribute`1");
    internal INamedTypeSymbol? KeyedLifetimeAttribute { get; } = compilation.GetTypeByMetadataName("DRN.Framework.Utils.DependencyInjection.Attributes.LifetimeWithKeyAttribute`1");
    internal INamedTypeSymbol? RegistrationAttribute { get; } = compilation.GetTypeByMetadataName("DRN.Framework.Utils.DependencyInjection.Attributes.ServiceRegistrationAttribute");
    internal INamedTypeSymbol? ContextRegistrationAttribute { get; } = compilation.GetTypeByMetadataName("DRN.Framework.EntityFramework.Context.DrnContextServiceRegistrationAttribute");
    internal INamedTypeSymbol? DefaultsAttribute { get; } = compilation.GetTypeByMetadataName("DRN.Framework.EntityFramework.Attributes.DrnContextDefaultsAttribute");
    internal INamedTypeSymbol? NpgsqlOptions { get; } = compilation.GetTypeByMetadataName("Microsoft.EntityFrameworkCore.NpgsqlDbContextOptionsBuilderExtensions");
    internal INamedTypeSymbol? Migration { get; } = compilation.GetTypeByMetadataName("Microsoft.EntityFrameworkCore.Migrations.Migration");
    internal INamedTypeSymbol? Snapshot { get; } = compilation.GetTypeByMetadataName("Microsoft.EntityFrameworkCore.Infrastructure.ModelSnapshot");
    internal INamedTypeSymbol? ContextAttribute { get; } = compilation.GetTypeByMetadataName("Microsoft.EntityFrameworkCore.Infrastructure.DbContextAttribute");
    internal INamedTypeSymbol? AssemblyMetadataAttribute { get; } = compilation.GetTypeByMetadataName("System.Reflection.AssemblyMetadataAttribute");
    internal INamedTypeSymbol? SuppressionAttribute { get; } = compilation.GetTypeByMetadataName("System.Diagnostics.CodeAnalysis.SuppressMessageAttribute");
    internal INamedTypeSymbol? AttributeUsage { get; } = compilation.GetTypeByMetadataName("System.AttributeUsageAttribute");

    internal bool IsEntity(INamedTypeSymbol type, CancellationToken cancellation) =>
        Is(type, Entity, cancellation) || Is(type, IdentityUser, cancellation);

    internal bool IsContext(INamedTypeSymbol type, CancellationToken cancellation) =>
        Is(type, Context, cancellation) || Is(type, DbContext, cancellation);

    internal bool IsTransport(INamedTypeSymbol type, CancellationToken cancellation) =>
        Is(type, Controller, cancellation) || Is(type, PageModel, cancellation);

    internal bool IsPersistenceType(INamedTypeSymbol type, CancellationToken cancellation) =>
        IsEntity(type, cancellation) || IsContext(type, cancellation) ||
        Is(type, RepositoryContract, cancellation) || Is(type, Configuration, cancellation) ||
        Is(type, ModelBuilder, cancellation) || Is(type, EntityBuilder, cancellation) || Is(type, GenericEntityBuilder, cancellation);

    internal static bool Same(ITypeSymbol? left, ITypeSymbol? right) => left is not null && right is not null &&
        SymbolEqualityComparer.Default.Equals(left.OriginalDefinition, right.OriginalDefinition);

    internal static bool Is(INamedTypeSymbol? type, INamedTypeSymbol? target, CancellationToken cancellation)
    {
        if (type is null || target is null) return false;
        foreach (var current in Bases(type, cancellation))
            if (Same(current, target)) return true;
        if (target.TypeKind != TypeKind.Interface) return false;
        foreach (var current in type.AllInterfaces)
        {
            cancellation.ThrowIfCancellationRequested();
            if (Same(current, target)) return true;
        }
        return false;
    }

    internal static IEnumerable<INamedTypeSymbol> Bases(INamedTypeSymbol type, CancellationToken cancellation)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            cancellation.ThrowIfCancellationRequested();
            yield return current;
        }
    }

    internal static IEnumerable<INamedTypeSymbol> Bindings(INamedTypeSymbol type, INamedTypeSymbol? target,
        CancellationToken cancellation)
    {
        if (target is null) yield break;
        foreach (var current in Bases(type, cancellation))
            if (Same(current, target)) yield return current;
        foreach (var current in type.AllInterfaces)
        {
            cancellation.ThrowIfCancellationRequested();
            if (Same(current, target)) yield return current;
        }
    }

    internal IEnumerable<AttributeData> EffectiveAttributes(INamedTypeSymbol type, INamedTypeSymbol? target,
        CancellationToken cancellation)
    {
        if (target is null) yield break;
        var first = true;
        var singleAttributes = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        foreach (var current in Bases(type, cancellation))
        {
            foreach (var attribute in current.GetAttributes())
            {
                if (!Is(attribute.AttributeClass, target, cancellation)) continue;
                var usage = Bases(attribute.AttributeClass!, cancellation).SelectMany(owner => owner.GetAttributes())
                    .FirstOrDefault(item => Same(item.AttributeClass, AttributeUsage));
                var inherited = usage?.NamedArguments.FirstOrDefault(pair => pair.Key == "Inherited").Value.Value is not false;
                var multiple = usage?.NamedArguments.FirstOrDefault(pair => pair.Key == "AllowMultiple").Value.Value is true;
                if (!first && !inherited) continue;
                if (!multiple && !singleAttributes.Add(attribute.AttributeClass!)) continue;
                yield return attribute;
            }
            first = false;
        }
    }

    internal static bool IsOpen(INamedTypeSymbol type, CancellationToken cancellation = default)
    {
        var pending = new Stack<ITypeSymbol>();
        var visited = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        pending.Push(type);
        while (pending.TryPop(out var current))
        {
            cancellation.ThrowIfCancellationRequested();
            if (!visited.Add(current)) continue;
            if (current is ITypeParameterSymbol) return true;
            if (current is IArrayTypeSymbol array) pending.Push(array.ElementType);
            if (current is not INamedTypeSymbol named) continue;
            if (named.IsUnboundGenericType) return true;
            if (named.ContainingType is { } parent) pending.Push(parent);
            foreach (var argument in named.TypeArguments) pending.Push(argument);
        }
        return false;
    }

    internal static string MetadataName(INamedTypeSymbol type)
    {
        var names = new Stack<string>();
        for (var current = type; current is not null; current = current.ContainingType) names.Push(current.MetadataName);
        var name = string.Join("+", names);
        var namespaces = new Stack<string>();
        for (var current = type.ContainingNamespace; current is { IsGlobalNamespace: false }; current = current.ContainingNamespace)
            namespaces.Push(current.MetadataName);
        return namespaces.Count == 0 ? name : string.Join(".", namespaces) + "." + name;
    }

    internal static ImmutableArray<INamedTypeSymbol> SourceTypes(Compilation compilation, CancellationToken cancellation)
    {
        var pending = new Stack<INamespaceOrTypeSymbol>();
        var types = ImmutableArray.CreateBuilder<INamedTypeSymbol>();
        pending.Push(compilation.Assembly.GlobalNamespace);
        while (pending.TryPop(out var container))
        {
            cancellation.ThrowIfCancellationRequested();
            foreach (var member in container.GetMembers())
            {
                if (member is INamespaceSymbol ns) pending.Push(ns);
                if (member is not INamedTypeSymbol type) continue;
                pending.Push(type);
                if (type.DeclaringSyntaxReferences.Length != 0 && !type.IsImplicitlyDeclared) types.Add(type);
            }
        }
        return types.ToImmutable();
    }
}
