namespace DRN.Framework.Testing.Contexts.RabbitMQ;

public class RabbitMQContainerSettings
{
    public static string DefaultImage { get; set; } = "rabbitmq";
    public static string DefaultVersion { get; set; } = "4.3.5-management-alpine";
    public static string DefaultDigest { get; set; } = "sha256:e2f08f846de10bb09649a8b020f286ed362a8f72ee45e5a8d043851f1533fda8";

    public string? Image { get; init; } = DefaultImage;
    public string? Version { get; init; } = DefaultVersion;
    public string? Digest { get; init; }

    public string? Username { get; init; }
    public bool HasUsername => !string.IsNullOrWhiteSpace(Username);

    public string? Password { get; init; }
    public bool HasPassword => !string.IsNullOrWhiteSpace(Password);

    public string GetImageTag()
    {
        var image = Image ?? DefaultImage;
        var version = Version ?? DefaultVersion;
        var imageTag = $"{image}:{version}";
        var digest = Digest;

        if (string.IsNullOrWhiteSpace(digest) &&
            string.Equals(image, DefaultImage, StringComparison.Ordinal) &&
            string.Equals(version, DefaultVersion, StringComparison.Ordinal))
            digest = DefaultDigest;

        return string.IsNullOrWhiteSpace(digest) ? imageTag : $"{imageTag}@{digest}";
    }
}
