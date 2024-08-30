using System.Reflection;
using DRN.Framework.Utils.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace DRN.Framework.Utils.DependencyInjection.Attributes;

[AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = true)]
public abstract class ServiceRegistrationAttribute : Attribute
{
    public static bool HasServiceCollectionModule(Type type) =>
        type is { IsAbstract: false, IsClass: true, IsVisible: true } &&
        type.GetCustomAttributes().Any(a => a.GetType().IsAssignableTo(typeof(ServiceRegistrationAttribute)));

    public static ServiceRegistrationAttribute GetModuleAttribute(Type type) =>
        (ServiceRegistrationAttribute)type.GetCustomAttributes()
            .Single(a => a.GetType().IsAssignableTo(typeof(ServiceRegistrationAttribute)));


    public abstract void ServiceRegistration(IServiceCollection sc, Assembly? assembly);

    public virtual async Task PostStartupValidationAsync(object service, IServiceProvider serviceProvider, IScopedLog? scopedLog = null)
        => await Task.CompletedTask;
}