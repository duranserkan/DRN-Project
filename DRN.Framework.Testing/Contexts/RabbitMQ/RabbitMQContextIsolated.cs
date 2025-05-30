using Testcontainers.RabbitMq;

namespace DRN.Framework.Testing.Contexts.RabbitMQ;

public class RabbitMQContextIsolated(TestContext testContext)
{
    public TestContext TestContext { get; } = testContext;
    public ContainerContext ContainerContext => TestContext.ContainerContext;

    public async Task<RabbitMqContainer> StartRabbitMqAsync(RabbitMQContainerSettings? settings = null)
    {
        var container = RabbitMQContext.BuildContainer(settings);
        ContainerContext.AddContainer(container);

        await container.StartAsync();

        return container;
    }
}