using DotNet.Testcontainers.Containers;
using DRN.Framework.Testing.Contexts.Postgres;
using DRN.Framework.Testing.Contexts.RabbitMQ;

namespace DRN.Framework.Testing.Contexts;

public sealed class ContainerContext(DrnTestContext drnTestContext) : IDisposable
{
    private readonly List<DockerContainer> _isolatedContainers = [];

    /// <summary>
    /// Intentionally made public to allow extension methods to support more containers
    /// </summary>
    public DrnTestContext drnTestContext { get; } = drnTestContext;

    public PostgresContext Postgres { get; } = new(drnTestContext);
    public RabbitMQContext RabbitMQ { get; } = new(drnTestContext);

    /// <summary>
    /// Intentionally made public to allow extension methods to support more containers
    /// </summary>
    public void AddContainer(DockerContainer container) => _isolatedContainers.Add(container);

    public async Task BindExternalDependenciesAsync()
    {
        await drnTestContext.ContainerContext.Postgres.ApplyMigrationsAsync();
    }

    public void Dispose() => Task.WaitAll(_isolatedContainers.Select(c => c.DisposeAsync().AsTask()).ToArray());
}