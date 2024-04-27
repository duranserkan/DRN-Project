using DotNet.Testcontainers.Containers;
using DRN.Framework.Testing.Contexts.Postgres;
using DRN.Framework.Testing.Contexts.RabbitMQ;

namespace DRN.Framework.Testing.Contexts;

public sealed class ContainerContext(TestContext testContext) : IDisposable
{
    private readonly List<DockerContainer> _containers = [];

    /// <summary>
    /// Intentionally made public to allow extension methods to support more containers
    /// </summary>
    public TestContext TestContext { get; } = testContext;
    public PostgresContext Postgres { get; } = new(testContext);
    public RabbitMQContext RabbitMQ { get; } = new(testContext);

    /// <summary>
    /// Intentionally made public to allow extension methods to support more containers
    /// </summary>
    public void AddContainer(DockerContainer container)
    {
        _containers.Add(container);
    }

    public void Dispose() => Task.WaitAll(_containers.Select(c => c.DisposeAsync().AsTask()).ToArray());
}