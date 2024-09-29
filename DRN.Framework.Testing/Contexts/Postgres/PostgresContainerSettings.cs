using DRN.Framework.EntityFramework.Context;

namespace DRN.Framework.Testing.Contexts.Postgres;

public class PostgresContainerSettings
{
    public static string DefaultImage { get; set; } = "postgres";
    public static string DefaultVersion { get; set; } = "17.0-alpine3.20";
    public static string DefaultPassword { get; set; } = "postgres";

    public string? Image { get; init; } = DefaultImage;
    public string? Version { get; init; } = DefaultVersion;

    public string? Database { get; init; } = DbContextConventions.DefaultDatabase;
    public bool HasDatabase => !string.IsNullOrWhiteSpace(Database);

    public string? Username { get; init; } = DbContextConventions.DefaultUsername;
    public bool HasUsername => !string.IsNullOrWhiteSpace(Username);

    public string? Password { get; init; } = DefaultPassword;
    public bool HasPassword => !string.IsNullOrWhiteSpace(Password);

    public int HostPort { get; init; }
    public bool HasValidHostPort => HostPort is >= 0 and < 65535;

    public bool Reuse { get; init; }

    public string GetImageTag()
    {
        var image = Image ?? DefaultImage;
        var version = Version ?? DefaultVersion;

        return $"{image}:{version}";
    }
}