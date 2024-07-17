namespace DRN.Framework.Testing.Contexts.Postgres;

public class NpgsqlConnectionStringParameters
{
    public static bool DefaultMultiplexing { get; set; } = true;
    public static int DefaultMaxAutoPrepare { get; set; } = 200;
    public static int DefaultMaxPoolSize { get; set; } = 15;

    public bool Multiplexing { get; init; } = DefaultMultiplexing;
    public int MaxAutoPrepare { get; init; } = DefaultMaxAutoPrepare;
    public int MaxPoolSize { get; init; } = DefaultMaxPoolSize;
}