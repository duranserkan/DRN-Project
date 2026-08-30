using System.Reflection;
using DRN.Framework.EntityFramework.Context.Interceptors;
using DRN.Framework.EntityFramework.Extensions;
using DRN.Framework.SharedKernel.Domain;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using DRN.Framework.Utils.Entity;
using DRN.Framework.Utils.Ids;
using DRN.Framework.Utils.Logging;
using DRN.Framework.Utils.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DRN.Framework.EntityFramework.Context;

/// <summary>
/// Adds DRNContexts in derived DbContext's assembly by using <br/>
/// <see cref="Extensions.ServiceCollectionExtensions.AddDbContextsWithConventions"/>
/// <br/>
/// when
/// <br/>
/// <see cref="DRN.Framework.Utils.DependencyInjection.ServiceCollectionExtensions.AddServicesWithAttributes(IServiceCollection)"/>
/// <br/> is called from DbContext's assembly
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class DrnContextServiceRegistrationAttribute : ServiceRegistrationAttribute
{
    public override void ServiceRegistration(IServiceCollection sc, Assembly? assembly)
    {
        sc.AddDbContextsWithConventions(assembly);
        sc.TryAddSingleton<IDrnMaterializationInterceptor, DrnMaterializationInterceptor>();
        sc.TryAddSingleton<IDrnSaveChangesInterceptor, DrnSaveChangesInterceptor>();
        sc.TryAddSingleton<IPaginationUtils, PaginationUtils>();
    }

    public override async Task PostStartupValidationAsync(object service, IServiceProvider serviceProvider, IScopedLog? scopedLog = null)
    {
        if (service is not DbContext context) return;
        Validate(serviceProvider, scopedLog, context);

        var appSettings = serviceProvider.GetRequiredService<IAppSettings>();
        var changeModel = await DrnContextServiceRegistrationHelper.GetChangeModelAsync(serviceProvider, context);
        changeModel.LogChanges(scopedLog, appSettings.Environment.ToString());

        await DrnContextServiceRegistrationHelper.ProcessChangeModelAsync(context, serviceProvider, appSettings, changeModel, scopedLog);
    }

    private static void Validate(IServiceProvider serviceProvider, IScopedLog? scopedLog, DbContext context)
    {
        DrnContextServiceRegistrationHelper.WarmupScopedProviders(serviceProvider, context);

        var appSettings = serviceProvider.GetService<IAppSettings>();
        PreValidateAllDbContexts(serviceProvider, scopedLog, appSettings, context);
        ValidateEntityTypes(context, scopedLog, appSettings, serviceProvider);
        serviceProvider.GetRequiredService(context.GetType());
    }

    private static void PreValidateAllDbContexts(IServiceProvider serviceProvider, IScopedLog? scopedLog, IAppSettings? appSettings, DbContext? currentContext = null)
    {
        var allDbContexts = DrnContextServiceRegistrationHelper.CollectAllDbContexts(serviceProvider, currentContext);
        var allDomainTypes = DrnContextServiceRegistrationHelper.GetHostDomainEntityTypes(serviceProvider, allDbContexts);

        var hostValidation = DrnContextServiceRegistrationHelper.GetEntityTypeValidationResult(allDomainTypes);
        DrnContextServiceRegistrationHelper.ValidateHostEntityTypes(hostValidation, scopedLog);

        EntityTypeRegistry.Register(allDomainTypes);
        SourceKnownIdUtils.Warmup(allDomainTypes);

        foreach (var dbContext in allDbContexts)
            ValidateEntityTypes(dbContext, scopedLog, appSettings, serviceProvider);
    }

    internal static void ValidateEntityTypes(DbContext context, IScopedLog? scopedLog, IAppSettings? appSettings = null, IServiceProvider? serviceProvider = null)
    {
        var domainTypes = DrnContextServiceRegistrationHelper.GetModelDomainEntityTypes(context);
        var idValidation = DrnContextServiceRegistrationHelper.GetEntityTypeValidationResult(domainTypes);

        DrnContextServiceRegistrationHelper.ValidateModelEntityTypes(idValidation, scopedLog);

        // Validates and bulk registers domain entity types into the immutable EntityTypeRegistry.
        // This catches application-wide inconsistencies and freezes the lookup snapshot.
        EntityTypeRegistry.Register(domainTypes);

        DrnContextServiceRegistrationHelper.ValidateAppIdPartition(context, idValidation, appSettings, serviceProvider);
    }

    internal static Type[] GetModelDomainEntityTypes(DbContext context) =>
        DrnContextServiceRegistrationHelper.GetModelDomainEntityTypes(context);

    internal static Type[] GetHostDomainEntityTypes(IServiceProvider? serviceProvider, IReadOnlyCollection<DbContext>? dbContexts = null) =>
        DrnContextServiceRegistrationHelper.GetHostDomainEntityTypes(serviceProvider, dbContexts);

    internal static Type[] GetAssemblyDomainEntityTypes(Assembly assembly) =>
        DrnContextServiceRegistrationHelper.GetAssemblyDomainEntityTypes(assembly);

    internal static EntityTypeValidationResult GetEntityTypeValidationResult(IReadOnlyCollection<Type> domainTypes) =>
        DrnContextServiceRegistrationHelper.GetEntityTypeValidationResult(domainTypes);

    internal static Type[] GetAllDomainEntityTypes(DbContext context) =>
        DrnContextServiceRegistrationHelper.GetAllDomainEntityTypes(context);
}

public record DuplicateEntityTypeValue(string EntityName, ushort EntityType, byte AppId)
{
    public override string ToString() => $"AppId {AppId}, EntityType {EntityType}: {EntityName}";
}

public record EntityTypeValidationResult(
    string[] MissingEntityTypes,
    DuplicateEntityTypeValue[] DuplicateEntityTypes,
    byte[]? MultipleAppIds = null,
    byte[]? NonTestAppIds = null)
{
    public byte[] MultipleAppIds { get; init; } = MultipleAppIds ?? [];
    public byte[] NonTestAppIds { get; init; } = NonTestAppIds ?? [];
    public string GetMissingEntityTypes() => string.Join(',', MissingEntityTypes);
    public string GetDuplicateEntityTypes() => string.Join(',', DuplicateEntityTypes.Select(p => p.ToString()));
    public string GetMultipleAppIds() => string.Join(',', MultipleAppIds);
}
