using Testcontainers.RabbitMq;

namespace DRN.Framework.Testing.Contexts.RabbitMQ;

public class RabbitMQContext(TestContext testContext)
{
    private static bool _started;
    private static readonly object ContainerLock = new();
    private static readonly Lazy<RabbitMqContainer> Container = new(() => BuildContainer());

    public IsolatedRabbitMQContext Isolated { get; } = new(testContext);

    public RabbitMqContainer Start()
    {
        lock (ContainerLock)
        {
            if (_started) return Container.Value;
            Container.Value.StartAsync().GetAwaiter().GetResult();
            _started = true;
            return Container.Value;
        }
    }

    public static RabbitMqContainer BuildContainer(string? version = null, string? username = null, string? password = null)
    {
        version ??= "3.12.10-alpine";
        var builder = new RabbitMqBuilder().WithImage($"rabbitmq:{version}");
        if (username != null) builder.WithUsername(username);
        if (password != null) builder.WithPassword(password);

        var container = builder.Build();

        return container;
    }
}