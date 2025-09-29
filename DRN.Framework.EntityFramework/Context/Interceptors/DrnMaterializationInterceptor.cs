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
        if (entity is not SourceKnownEntity e)
            return entity;

        e.EntityIdSource = idUtils.Generate(e);
        e.IdFactory = idUtils.Generate;
        e.Parser = idUtils.Parse;
        e.Validator = idUtils.Validate;

        return entity;
    }
}