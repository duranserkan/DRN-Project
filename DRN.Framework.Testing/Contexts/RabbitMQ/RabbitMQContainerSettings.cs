namespace DRN.Framework.Testing.Contexts.RabbitMQ;

public class RabbitMQContainerSettings
{
    public static string DefaultImage { get; set; } = "rabbitmq";
    public static string DefaultVersion { get; set; } = "4.2.0-management-alpine";

    public string? Image { get; init; } = DefaultImage;
    public string? Version { get; init; } = DefaultVersion;

    public string? Username { get; init; }
    public bool HasUsername => !string.IsNullOrWhiteSpace(Username);

    public string? Password { get; init; }
    public bool HasPassword => !string.IsNullOrWhiteSpace(Password);

    public string GetImageTag()
    {
        var image = Image ?? DefaultImage;
        var version = Version ?? DefaultVersion;

        return $"{image}:{version}";
    }
}