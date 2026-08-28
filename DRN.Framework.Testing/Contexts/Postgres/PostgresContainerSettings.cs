using DRN.Framework.EntityFramework.Context;

namespace DRN.Framework.Testing.Contexts.Postgres;

public class PostgresContainerSettings
{
    public static string DefaultImage { get; set; } = "postgres";
    public static string DefaultVersion { get; set; } = "18.6-alpine3.24";
    public static string DefaultDigest { get; set; } = "sha256:d3e1620b530c944afa6e887d22eb899824da68e19c52024bf98f5220c88a65b2";
    public static string DefaultPassword { get; set; } = "drn";

    public string? Image { get; init; } = DefaultImage;
    public string? Version { get; init; } = DefaultVersion;
    public string? Digest { get; init; }

    public string? Database { get; init; } = DbContextConventions.DefaultDatabase;
    public bool HasDatabase => !string.IsNullOrWhiteSpace(Database);

    public string? Username { get; init; } = DbContextConventions.DefaultUsername;
    public bool HasUsername => !string.IsNullOrWhiteSpace(Username);

    public string? Password { get; init; } = DefaultPassword;
    public bool HasPassword => !string.IsNullOrWhiteSpace(Password);

    public int HostPort { get; init; }
    public bool HasValidHostPort => HostPort is >= 0 and < 65535;

    public bool Reuse { get; init; }

    public string ContainerName { get; set; } = string.Empty;

    public bool HasContainerName => !string.IsNullOrWhiteSpace(ContainerName);

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

    public PostgresContainerSettings Clone(int? hostPort = null) =>
        new()
        {
            Image = Image,
            Version = Version,
            Digest = Digest,
            Database = Database,
            Username = Username,
            Password = Password,
            HostPort = hostPort ?? HostPort,
            Reuse = Reuse,
            ContainerName = ContainerName
        };
}
