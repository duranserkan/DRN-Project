using DRN.Framework.SharedKernel.Domain;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using DRN.Framework.Utils.Ids;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace DRN.Framework.EntityFramework.Context.Interceptors;

public interface IDrnMaterializationInterceptor : IMaterializationInterceptor;

[Singleton<IDrnMaterializationInterceptor>]
public class DrnMaterializationInterceptor(ISourceKnownEntityIdUtils idUtils) : IDrnMaterializationInterceptor
{
    public object InitializedInstance(MaterializationInterceptionData materializationData, object entity)
    {
        if (entity is Entity e)
            e.EntityIdSource = idUtils.Generate(e);

        return entity;
    }
}