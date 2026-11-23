namespace DRN.Framework.SharedKernel.Analyzers.Conventions;

internal sealed class ConventionSymbols(Compilation compilation)
{
    private readonly Dictionary<INamedTypeSymbol, Dictionary<INamedTypeSymbol, bool>> _classifications = new(SymbolEqualityComparer.Default);
    private readonly AttributeUsageFacts.Cache _attributeUsage = AttributeUsageFacts.Cache.For(compilation);
    private EntityAnalyzerHelper.EntityTypeFacts? _entityTypes;
    private readonly Dictionary<INamedTypeSymbol, bool> _openTypes = new(SymbolEqualityComparer.Default);
    private readonly Dictionary<INamedTypeSymbol, Dictionary<INamedTypeSymbol, ImmutableArray<INamedTypeSymbol>>> _bindings = new(SymbolEqualityComparer.Default);
    internal INamedTypeSymbol? Entity { get; } = compilation.GetTypeByMetadataName(EntityAnalyzerHelper.SourceKnownEntityMetadataName);
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
    internal INamedTypeSymbol? DomainOnlyAttribute { get; } = compilation.GetTypeByMetadataName("DRN.Framework.SharedKernel.Attributes.DomainOnlyAttribute");
    internal INamedTypeSymbol? AllowZeroAttribute { get; } = compilation.GetTypeByMetadataName("DRN.Framework.SharedKernel.Attributes.AllowZeroAttribute");
    internal INamedTypeSymbol? EntityTypeAttribute { get; } = compilation.GetTypeByMetadataName("DRN.Framework.SharedKernel.Domain.EntityTypeAttribute");
    internal INamedTypeSymbol? AppId { get; } = compilation.GetTypeByMetadataName("DRN.Framework.SharedKernel.Domain.IAppId");
    internal INamedTypeSymbol? Controller { get; } = compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Mvc.ControllerBase");
    internal INamedTypeSymbol? SourceKnownController { get; } = compilation.GetTypeByMetadataName("DRN.Framework.Hosting.Controllers.SourceKnownControllerBase`2");
    internal INamedTypeSymbol? SourceKnownReadOnlyController { get; } = compilation.GetTypeByMetadataName("DRN.Framework.Hosting.Controllers.SourceKnownReadOnlyController`2");
    internal INamedTypeSymbol? SourceKnownCombinedController { get; } = compilation.GetTypeByMetadataName("DRN.Framework.Hosting.Controllers.SourceKnownController`3");
    internal INamedTypeSymbol? NonActionAttribute { get; } = compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Mvc.NonActionAttribute");
    internal INamedTypeSymbol? NonControllerAttribute { get; } = compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Mvc.NonControllerAttribute");
    internal INamedTypeSymbol? ProducesResponseTypeAttribute { get; } = compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Mvc.ProducesResponseTypeAttribute");
    internal INamedTypeSymbol? ProducesResponseTypeGenericAttribute { get; } = compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Mvc.ProducesResponseTypeAttribute`1");
    internal INamedTypeSymbol? FromRouteAttribute { get; } = compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Mvc.FromRouteAttribute");
    internal INamedTypeSymbol? FromHeaderAttribute { get; } = compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Mvc.FromHeaderAttribute");
    internal INamedTypeSymbol? FromServicesAttribute { get; } = compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Mvc.FromServicesAttribute");
    internal IMethodSymbol? DisposeMethod { get; } =
        compilation.GetSpecialType(SpecialType.System_IDisposable) is { TypeKind: not TypeKind.Error } disposable
            ? disposable.GetMembers("Dispose").OfType<IMethodSymbol>().FirstOrDefault(method => method.Parameters.IsEmpty)
            : null;
    internal INamedTypeSymbol? ActionResult { get; } = compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Mvc.ActionResult");
    internal INamedTypeSymbol? ActionResultGeneric { get; } = compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Mvc.ActionResult`1");
    internal INamedTypeSymbol? IActionResult { get; } = compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Mvc.IActionResult");
    internal INamedTypeSymbol? StatusCodeResult { get; } = compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Mvc.StatusCodeResult");
    internal INamedTypeSymbol? HttpResult { get; } = compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Http.IResult");
    internal INamedTypeSymbol? FileResult { get; } = compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Mvc.FileResult");
    internal INamedTypeSymbol? NoContentResult { get; } = compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Mvc.NoContentResult");
    internal INamedTypeSymbol? ProblemDetails { get; } = compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Mvc.ProblemDetails");
    internal INamedTypeSymbol? PaginationRequest { get; } = compilation.GetTypeByMetadataName("DRN.Framework.SharedKernel.Domain.Pagination.PaginationRequest");
    internal INamedTypeSymbol? PaginationOptions { get; } = compilation.GetTypeByMetadataName("DRN.Framework.SharedKernel.Domain.Pagination.PaginationOptions");
    internal INamedTypeSymbol? PaginationResultInfo { get; } = compilation.GetTypeByMetadataName("DRN.Framework.SharedKernel.Domain.Pagination.PaginationResultInfo");
    internal INamedTypeSymbol? PaginationResultModel { get; } = compilation.GetTypeByMetadataName("DRN.Framework.SharedKernel.Domain.Pagination.PaginationResultModel`1");
    internal INamedTypeSymbol? EntityCreatedFilter { get; } = compilation.GetTypeByMetadataName("DRN.Framework.SharedKernel.Domain.Repository.EntityCreatedFilter");
    internal INamedTypeSymbol? FormFile { get; } = compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Http.IFormFile");
    internal INamedTypeSymbol? FormFileCollection { get; } = compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Http.IFormFileCollection");
    internal INamedTypeSymbol? TaskGeneric { get; } = compilation.GetTypeByMetadataName("System.Threading.Tasks.Task`1");
    internal INamedTypeSymbol? Task { get; } = compilation.GetTypeByMetadataName("System.Threading.Tasks.Task");
    internal INamedTypeSymbol? ValueTaskGeneric { get; } = compilation.GetTypeByMetadataName("System.Threading.Tasks.ValueTask`1");
    internal INamedTypeSymbol? ValueTask { get; } = compilation.GetTypeByMetadataName("System.Threading.Tasks.ValueTask");
    internal INamedTypeSymbol? CancellationToken { get; } = compilation.GetTypeByMetadataName("System.Threading.CancellationToken");
    internal INamedTypeSymbol? ListGeneric { get; } = compilation.GetTypeByMetadataName("System.Collections.Generic.List`1");
    internal INamedTypeSymbol? AsyncEnumerableGeneric { get; } = compilation.GetTypeByMetadataName("System.Collections.Generic.IAsyncEnumerable`1");
    internal INamedTypeSymbol? ImmutableArrayGeneric { get; } = compilation.GetTypeByMetadataName("System.Collections.Immutable.ImmutableArray`1");
    internal INamedTypeSymbol? ImmutableListGeneric { get; } = compilation.GetTypeByMetadataName("System.Collections.Immutable.ImmutableList`1");
    internal INamedTypeSymbol? SetGeneric { get; } = compilation.GetTypeByMetadataName("System.Collections.Generic.ISet`1");
    internal INamedTypeSymbol? ReadOnlySetGeneric { get; } = compilation.GetTypeByMetadataName("System.Collections.Generic.IReadOnlySet`1");
    internal INamedTypeSymbol? HashSetGeneric { get; } = compilation.GetTypeByMetadataName("System.Collections.Generic.HashSet`1");
    internal INamedTypeSymbol? CollectionGeneric { get; } = compilation.GetTypeByMetadataName("System.Collections.ObjectModel.Collection`1");
    internal INamedTypeSymbol? ReadOnlyCollectionGeneric { get; } = compilation.GetTypeByMetadataName("System.Collections.ObjectModel.ReadOnlyCollection`1");
    internal INamedTypeSymbol? NonGenericList { get; } = compilation.GetTypeByMetadataName("System.Collections.IList");
    internal INamedTypeSymbol? Guid { get; } = compilation.GetTypeByMetadataName("System.Guid");
    internal INamedTypeSymbol? DateTimeOffset { get; } = compilation.GetTypeByMetadataName("System.DateTimeOffset");
    internal INamedTypeSymbol? DateOnly { get; } = compilation.GetTypeByMetadataName("System.DateOnly");
    internal INamedTypeSymbol? TimeOnly { get; } = compilation.GetTypeByMetadataName("System.TimeOnly");
    internal INamedTypeSymbol? TimeSpan { get; } = compilation.GetTypeByMetadataName("System.TimeSpan");
    internal INamedTypeSymbol? Uri { get; } = compilation.GetTypeByMetadataName("System.Uri");
    internal INamedTypeSymbol? PageModel { get; } = compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Mvc.RazorPages.PageModel");
    internal INamedTypeSymbol? OwnershipAttribute { get; } = compilation.GetTypeByMetadataName("DRN.Framework.SharedKernel.Attributes.SourceKnownSubdomainAttribute");
    internal INamedTypeSymbol? PersistenceAttribute { get; } = compilation.GetTypeByMetadataName("DRN.Framework.EntityFramework.Attributes.SourceKnownPersistenceAttribute");
    internal INamedTypeSymbol? LifetimeAttribute { get; } = compilation.GetTypeByMetadataName("DRN.Framework.Utils.DependencyInjection.Attributes.LifetimeAttribute");
    internal INamedTypeSymbol? HostedServiceAttribute { get; } = compilation.GetTypeByMetadataName("DRN.Framework.Utils.DependencyInjection.Attributes.HostedServiceAttribute");
    internal INamedTypeSymbol? HostedService { get; } = compilation.GetTypeByMetadataName("Microsoft.Extensions.Hosting.IHostedService");
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
    internal INamedTypeSymbol? ConfigAttribute { get; } = compilation.GetTypeByMetadataName("DRN.Framework.Utils.DependencyInjection.Attributes.ConfigAttribute");
    internal INamedTypeSymbol? ContextOptionsAttribute { get; } = compilation.GetTypeByMetadataName("DRN.Framework.EntityFramework.Attributes.NpgsqlDbContextOptionsAttribute");
    internal INamedTypeSymbol? NonGenericContextOptions { get; } = compilation.GetTypeByMetadataName("Microsoft.EntityFrameworkCore.DbContextOptions");
    internal INamedTypeSymbol? DesignTimeContextFactory { get; } = compilation.GetTypeByMetadataName("Microsoft.EntityFrameworkCore.Design.IDesignTimeDbContextFactory`1");
    internal INamedTypeSymbol? EfServiceCollectionExtensions { get; } = compilation.GetTypeByMetadataName("Microsoft.Extensions.DependencyInjection.EntityFrameworkServiceCollectionExtensions");
    internal INamedTypeSymbol? DrnEfServiceCollectionExtensions { get; } = compilation.GetTypeByMetadataName("DRN.Framework.EntityFramework.Extensions.ServiceCollectionExtensions");
    internal INamedTypeSymbol? ServiceCollection { get; } = compilation.GetTypeByMetadataName("Microsoft.Extensions.DependencyInjection.IServiceCollection");
    internal INamedTypeSymbol? DiServiceCollectionExtensions { get; } = compilation.GetTypeByMetadataName("Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions");
    internal INamedTypeSymbol? DrnDiServiceCollectionExtensions { get; } = compilation.GetTypeByMetadataName("DRN.Framework.Utils.DependencyInjection.ServiceCollectionExtensions");
    internal INamedTypeSymbol? FromKeyedServicesAttribute { get; } = compilation.GetTypeByMetadataName("Microsoft.Extensions.DependencyInjection.FromKeyedServicesAttribute");

    internal bool IsEntity(INamedTypeSymbol type, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        var sourceKnown = Entity is { TypeKind: TypeKind.Class } entity
            ? (_entityTypes ??= EntityAnalyzerHelper.EntityTypeFacts.For(compilation, entity)).IsEntity(type, cancellation)
            : IsKnown(type, Entity, cancellation);
        return sourceKnown || IsKnown(type, IdentityUser, cancellation);
    }

    internal bool IsContext(INamedTypeSymbol type, CancellationToken cancellation) =>
        IsKnown(type, Context, cancellation) || IsKnown(type, DbContext, cancellation);

    internal bool IsTransport(INamedTypeSymbol type, CancellationToken cancellation) =>
        IsKnown(type, Controller, cancellation) || IsKnown(type, PageModel, cancellation);

    internal bool IsPersistenceType(INamedTypeSymbol type, CancellationToken cancellation) =>
        IsEntity(type, cancellation) || IsContext(type, cancellation) || IsKnown(type, DbSet, cancellation) ||
        IsKnown(type, RepositoryContract, cancellation) || IsKnown(type, Configuration, cancellation) ||
        IsKnown(type, ModelBuilder, cancellation) || IsKnown(type, EntityBuilder, cancellation) || IsKnown(type, GenericEntityBuilder, cancellation);

    internal bool IsKnown(INamedTypeSymbol type, INamedTypeSymbol? target, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        if (target is null) return false;
        if (!_classifications.TryGetValue(target, out var types))
        {
            types = new(SymbolEqualityComparer.Default);
            _classifications.Add(target, types);
        }
        if (types.TryGetValue(type, out var cached)) return cached;
        var result = Is(type, target, cancellation);
        cancellation.ThrowIfCancellationRequested();
        types.Add(type, result);
        return result;
    }

    internal static bool Same(ITypeSymbol? left, ITypeSymbol? right) => left is not null && right is not null &&
        SymbolEqualityComparer.Default.Equals(left.OriginalDefinition, right.OriginalDefinition);

    internal static bool Is(INamedTypeSymbol? type, INamedTypeSymbol? target, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        if (type is null || target is null) return false;
        for (var current = type; current is not null; current = current.BaseType)
        {
            cancellation.ThrowIfCancellationRequested();
            if (Same(current, target)) return true;
        }
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

    internal ImmutableArray<INamedTypeSymbol> Bindings(INamedTypeSymbol type, INamedTypeSymbol? target,
        CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        if (target is null) return [];
        if (_bindings.TryGetValue(target, out var types) && types.TryGetValue(type, out var cached)) return cached;
        var bindings = ImmutableArray.CreateBuilder<INamedTypeSymbol>();
        for (var current = type; current is not null; current = current.BaseType)
        {
            cancellation.ThrowIfCancellationRequested();
            if (Same(current, target)) bindings.Add(current);
        }
        foreach (var current in type.AllInterfaces)
        {
            cancellation.ThrowIfCancellationRequested();
            if (Same(current, target)) bindings.Add(current);
        }
        var result = bindings.ToImmutable();
        cancellation.ThrowIfCancellationRequested();
        if (types is null)
        {
            types = new(SymbolEqualityComparer.Default);
            _bindings.Add(target, types);
        }
        types.Add(type, result);
        return result;
    }

    [SuppressMessage("SonarQube", "S3267", Justification = "A direct attribute scan avoids a capturing predicate and a filtered sequence per base type.")]
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
                if (TryAcceptEffectiveAttribute(attribute, target, first, singleAttributes, cancellation))
                    yield return attribute;
            }
            first = false;
        }
    }

    private bool TryAcceptEffectiveAttribute(AttributeData attribute, INamedTypeSymbol target, bool direct,
        HashSet<ISymbol> singleAttributes, CancellationToken cancellation)
    {
        if (attribute.AttributeClass is not { } attributeType || !IsKnown(attributeType, target, cancellation)) return false;
        var (inherited, multiple) = _attributeUsage.Get(attributeType, cancellation);
        if (!direct && !inherited) return false;
        return multiple || singleAttributes.Add(attributeType);
    }

    internal bool IsOpen(INamedTypeSymbol type, CancellationToken cancellation = default)
    {
        cancellation.ThrowIfCancellationRequested();
        if (_openTypes.TryGetValue(type, out var cached)) return cached;
        var result = ReadIsOpen(type, cancellation);
        cancellation.ThrowIfCancellationRequested();
        _openTypes.Add(type, result);
        return result;
    }

    private static bool ReadIsOpen(INamedTypeSymbol type, CancellationToken cancellation)
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

    internal static string MetadataName(INamedTypeSymbol type) => ConventionNames.MetadataName(type);

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
