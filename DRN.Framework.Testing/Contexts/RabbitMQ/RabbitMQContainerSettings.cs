using DRN.Framework.SharedKernel.Attributes;

namespace DRN.Framework.Testing.Contexts.RabbitMQ;

public class RabbitMQContainerSettings
{
    private const string InitialDefaultImage = "rabbitmq";
    private const string InitialDefaultVersion = "4.4.1-management-alpine";
    private const string InitialDefaultDigest = "sha256:2b47b1aa05ec7e373950ad2e9200e9f95a9da7039c8d22c19dbf5a59101362c4";

    public static string DefaultImage { get; set; } = InitialDefaultImage;
    public static string DefaultVersion { get; set; } = InitialDefaultVersion;
    public static string DefaultDigest { get; set; } = InitialDefaultDigest;

    public string? Image { get; init; } = DefaultImage;
    public string? Version { get; init; } = DefaultVersion;
    public string? Digest { get; init; }

    [Personal]
    public string? Username { get; init; }
    [Sensitive(false, Justification = "Username configuration presence flag.")]
    public bool HasUsername => !string.IsNullOrWhiteSpace(Username);

    [Sensitive]
    public string? Password { get; init; }
    [Sensitive(false, Justification = "Password configuration presence flag.")]
    public bool HasPassword => !string.IsNullOrWhiteSpace(Password);

    public string GetImageTag()
    {
        var image = Image ?? DefaultImage;
        var version = Version ?? DefaultVersion;
        var imageTag = $"{image}:{version}";
        var digest = Digest;

        var matchesCurrentDefaults = string.Equals(image, DefaultImage, StringComparison.Ordinal) &&
                                     string.Equals(version, DefaultVersion, StringComparison.Ordinal);

        var matchesInitialDefaults = string.Equals(DefaultImage, InitialDefaultImage, StringComparison.Ordinal) &&
                                     string.Equals(DefaultVersion, InitialDefaultVersion, StringComparison.Ordinal);

        var hasCustomDefaultDigest = !string.Equals(DefaultDigest, InitialDefaultDigest, StringComparison.Ordinal);

        var shouldApplyDefaultDigest = string.IsNullOrWhiteSpace(digest) &&
                                       matchesCurrentDefaults &&
                                       (hasCustomDefaultDigest || matchesInitialDefaults);

        if (shouldApplyDefaultDigest)
            digest = DefaultDigest;

        return string.IsNullOrWhiteSpace(digest) ? imageTag : $"{imageTag}@{digest}";
    }
}
