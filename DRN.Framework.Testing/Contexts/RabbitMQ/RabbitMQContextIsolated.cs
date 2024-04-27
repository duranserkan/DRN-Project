using Testcontainers.RabbitMq;

namespace DRN.Framework.Testing.Contexts.RabbitMQ;

public class RabbitMQContextIsolated(TestContext testContext)
{
    public TestContext TestContext { get; } = testContext;
    public ContainerContext ContainerContext => TestContext.ContainerContext;

    public async Task<RabbitMqContainer> StartRabbitMqAsync(string? version = null, string? username = null, string? password = null)
    {
        var container = RabbitMQContext.BuildContainer(version, username, password);
        ContainerContext.AddContainer(container);

        await container.StartAsync();

        return container;
    }
}