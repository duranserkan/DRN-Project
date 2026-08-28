using DRN.Framework.Testing.Contexts.Postgres;
using DRN.Framework.Testing.Contexts.RabbitMQ;

namespace DRN.Test.Unit.Tests.Framework.Testing.Contexts;

public class ContainerSettingsTests
{
    [Fact]
    public void PostgresContainerSettings_GetImageTag_Should_Pin_Default_Image_By_Digest()
    {
        var image = new PostgresContainerSettings().GetImageTag();

        image.Should().Be($"{PostgresContainerSettings.DefaultImage}:{PostgresContainerSettings.DefaultVersion}@{PostgresContainerSettings.DefaultDigest}");
        PostgresContainerSettings.DefaultDigest.Should().StartWith("sha256:");
    }

    [Fact]
    public void RabbitMQContainerSettings_GetImageTag_Should_Pin_Default_Image_By_Digest()
    {
        var image = new RabbitMQContainerSettings().GetImageTag();

        image.Should().Be($"{RabbitMQContainerSettings.DefaultImage}:{RabbitMQContainerSettings.DefaultVersion}@{RabbitMQContainerSettings.DefaultDigest}");
        RabbitMQContainerSettings.DefaultDigest.Should().StartWith("sha256:");
    }

    [Fact]
    public void PostgresContainerSettings_GetImageTag_Should_Not_Reuse_Default_Digest_For_Custom_Tag()
    {
        var settings = new PostgresContainerSettings
        {
            Image = "registry.example.com/postgres",
            Version = "custom"
        };

        settings.GetImageTag().Should().Be("registry.example.com/postgres:custom");
    }

    [Fact]
    public void RabbitMQContainerSettings_GetImageTag_Should_Use_Explicit_Custom_Digest()
    {
        var settings = new RabbitMQContainerSettings
        {
            Image = "registry.example.com/rabbitmq",
            Version = "custom",
            Digest = "sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"
        };

        settings.GetImageTag().Should().Be(
            "registry.example.com/rabbitmq:custom@sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef");
    }
}
