using DRN.Framework.Utils.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DRN.Framework.EntityFramework;

public class DbContextCollection
{
    public ConnectionStringsCollection ConnectionStrings { get; init; } = new();
    public Dictionary<string, ServiceDescriptor> ServiceDescriptors { get; init; } = new(5);

    public bool Any => ServiceDescriptors.Count != 0;

    public DbContext[] GetDbContexts(IServiceProvider serviceProvider) =>
        ServiceDescriptors.Select(pair => (DbContext)serviceProvider.GetRequiredService(pair.Value.ServiceType)).ToArray();
}