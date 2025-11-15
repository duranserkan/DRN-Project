using Testcontainers.RabbitMq;

namespace DRN.Framework.Testing.Contexts.RabbitMQ;

public class RabbitMQContextIsolated(DrnTestContext testContext)
{
    public DrnTestContext DrnTestContext { get; } = testContext;
    public ContainerContext ContainerContext => DrnTestContext.ContainerContext;

    public async Task<RabbitMqContainer> StartRabbitMqAsync(RabbitMQContainerSettings? settings = null)
    {
        var container = RabbitMQContext.BuildContainer(settings);
        ContainerContext.AddContainer(container);

        await container.StartAsync();

        return container;
    }
}