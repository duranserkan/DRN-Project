using Testcontainers.RabbitMq;

namespace DRN.Framework.Testing.Contexts.RabbitMQ;

public class RabbitMQContext(TestContext testContext)
{
    private static bool _started;
    static readonly SemaphoreSlim ContainerLock = new(1, 1);
    private static readonly Lazy<RabbitMqContainer> Container = new(() => BuildContainer());

    public static string Image { get; set; } = "rabbitmq";
    public static string Version { get; set; } = "3.13.2-alpine";

    public TestContext TestContext { get; } = testContext;
    public RabbitMQContextIsolated RabbitMqContextIsolated { get; } = new(testContext);

    //todo refactor parameters
    public static RabbitMqContainer BuildContainer(string? version = null, string? username = null, string? password = null)
    {
        version ??= Version;
        var builder = new RabbitMqBuilder().WithImage($"{Image}:{version}");
        if (username != null) builder.WithUsername(username);
        if (password != null) builder.WithPassword(password);

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