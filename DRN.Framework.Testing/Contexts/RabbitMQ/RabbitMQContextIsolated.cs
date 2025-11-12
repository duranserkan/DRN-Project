using Testcontainers.RabbitMq;

namespace DRN.Framework.Testing.Contexts.RabbitMQ;

public class RabbitMQContextIsolated(DrnTestContext drnTestContext)
{
    public DrnTestContext drnTestContext { get; } = drnTestContext;
    public ContainerContext ContainerContext => drnTestContext.ContainerContext;

    public async Task<RabbitMqContainer> StartRabbitMqAsync(RabbitMQContainerSettings? settings = null)
    {
        var container = RabbitMQContext.BuildContainer(settings);
        ContainerContext.AddContainer(container);

        await container.StartAsync();

        return container;
    }
}