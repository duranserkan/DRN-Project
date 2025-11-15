using Testcontainers.RabbitMq;

namespace DRN.Framework.Testing.Contexts.RabbitMQ;

public class RabbitMQContext(DrnTestContext testContext)
{
    private static bool _started;
    private static readonly SemaphoreSlim ContainerLock = new(1, 1);

    public DrnTestContext DrnTestContext { get; } = testContext;
    public RabbitMQContextIsolated RabbitMqContextIsolated { get; } = new(testContext);

    public static readonly Lazy<RabbitMqContainer> Container = new(() => BuildContainer(RabbitMqContainerSettings));

    /// <summary>
    /// Update before container creation. StartAsync initializes the container.
    /// Updated settings after the container initialized will not be reflected on container.
    /// </summary>
    public static RabbitMQContainerSettings RabbitMqContainerSettings { get; set; } = new();

    public static RabbitMqContainer BuildContainer(RabbitMQContainerSettings? settings = null)
    {
        settings ??= new RabbitMQContainerSettings();

        var builder = new RabbitMqBuilder().WithImage(settings.GetImageTag());
        if (settings.HasUsername) builder = builder.WithUsername(settings.Username);
        if (settings.HasPassword) builder = builder.WithPassword(settings.Password);

        var container = builder.Build();

        return container;
    }

    public static async Task<RabbitMqContainer> StartAsync()
    {
        await ContainerLock.WaitAsync();
        try
        {
            if (_started) return Container.Value;
            await Container.Value.StartAsync();
            _started = true;
            return Container.Value;
        }
        finally
        {
            ContainerLock.Release();
        }
    }
}