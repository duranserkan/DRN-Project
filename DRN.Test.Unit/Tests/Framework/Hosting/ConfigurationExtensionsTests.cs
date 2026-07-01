using DRN.Framework.Hosting.Extensions;
using DRN.Framework.SharedKernel.Enums;
using DRN.Framework.Utils.Data.Serialization;
using DRN.Framework.Utils.Settings.Conventions;
using Microsoft.Extensions.FileProviders;

namespace DRN.Test.Unit.Tests.Framework.Hosting;

[Collection(EnvironmentVariableIsolationCollectionNames.Configuration)]
public class ConfigurationExtensionsTests
{
    private static readonly string[] EnvironmentVariablePrefixes =
    [
        "ASPNETCORE_",
        "DOTNET_"
    ];

    [Theory]
    [DataInlineUnit("Staging")]
    public void AddDrnSettings_Should_Discover_Environment_Without_Requiring_Full_AppSettings(DrnTestContextUnit context, string environment)
    {
        var settingsDirectory = context.GetTempPath();

        File.WriteAllText(Path.Combine(settingsDirectory, "appsettings.json"), $$"""
            {
              "Environment": "{{environment}}"
            }
            """);
        File.WriteAllText(Path.Combine(settingsDirectory, "appsettings.Staging.json"), """
            {
              "EnvironmentSpecificValue": "loaded"
            }
            """);

        WithoutEnvironmentOverrides(() =>
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(settingsDirectory)
                .AddDrnSettings("")
                .Build();

            configuration["EnvironmentSpecificValue"].Should().Be("loaded");
        });
    }

    [Theory]
    [DataInlineUnit("Staging")]
    public void AddDrnSettings_Should_Preserve_Custom_FileProvider_During_Environment_Discovery(DrnTestContextUnit context, string environment)
    {
        var settingsDirectory = context.GetTempPath();

        File.WriteAllText(Path.Combine(settingsDirectory, "appsettings.json"), $$"""
            {
              "Environment": "{{environment}}"
            }
            """);
        File.WriteAllText(Path.Combine(settingsDirectory, "appsettings.Staging.json"), """
            {
              "EnvironmentSpecificValue": "loaded"
            }
            """);

        WithoutEnvironmentOverrides(() =>
        {
            using var physicalProvider = new PhysicalFileProvider(settingsDirectory);
            var compositeProvider = new CompositeFileProvider(physicalProvider);

            var configuration = new ConfigurationBuilder()
                .SetFileProvider(compositeProvider)
                .AddDrnSettings("")
                .Build();

            configuration["EnvironmentSpecificValue"].Should().Be("loaded");
        });
    }

    [Theory]
    [DataInlineUnit]
    public void AddDrnSettings_Should_Throw_When_Environment_Is_Missing(DrnTestContextUnit context)
    {
        var settingsDirectory = context.GetTempPath();

        File.WriteAllText(Path.Combine(settingsDirectory, "appsettings.json"), "{}");

        var action = () => WithoutEnvironmentOverrides(() => new ConfigurationBuilder()
            .SetBasePath(settingsDirectory)
            .AddDrnSettings("")
            .Build());

        action.Should().ThrowExactly<ConfigurationException>()
            .WithMessage("Environment setting is missing.*");
    }

    [Theory]
    [DataInlineUnit("Unknown")]
    [DataInlineUnit("NotDefined")]
    [DataInlineUnit("999")]
    [DataInlineUnit("1")]
    [DataInlineUnit(" Staging ")]
    public void AddDrnSettings_Should_Throw_When_Environment_Is_Invalid(DrnTestContextUnit context, string environment)
    {
        var settingsDirectory = context.GetTempPath();

        File.WriteAllText(Path.Combine(settingsDirectory, "appsettings.json"), $$"""
            {
              "Environment": "{{environment}}"
            }
            """);

        var action = () => WithoutEnvironmentOverrides(() => new ConfigurationBuilder()
            .SetBasePath(settingsDirectory)
            .AddDrnSettings("")
            .Build());

        action.Should().ThrowExactly<ConfigurationException>()
            .WithMessage($"Invalid Environment value: '{environment}'. Valid values are: Development, Staging, Production.");
    }

    [Theory]
    [DataInlineUnit]
    public void MountDirectorySettings_Should_Be_Added(DrnTestContextUnit context, IMountedSettingsConventionsOverride conventionsOverride)
    {
        var testFolder = context.MethodContext.GetTestFolderLocation();
        conventionsOverride.MountedSettingsDirectory.Returns(Path.Combine(testFolder, "MountDir"));

        var appsettings = context.GetRequiredService<IAppSettings>();
        appsettings.Environment.Should().Be(AppEnvironment.Staging);

        var password = appsettings.GetValue<string>("postgres-password");
        password.Should().Be("Be Always Progressive: Follow the Mustafa Kemal Atatürk's Enlightenment Ideals");

        appsettings.DevelopmentSettings.AutoMigrateDevelopment.Should().BeFalse();
        appsettings.DevelopmentSettings.AutoMigrateStaging.Should().BeTrue();

        var summaryJson = appsettings.GetDebugView().ToSummary().Serialize();
        summaryJson.Should().NotBeEmpty();
    }

    private static void WithoutEnvironmentOverrides(Action action)
    {
        var previousValues = Environment.GetEnvironmentVariables()
            .Cast<DictionaryEntry>()
            .Select(entry => entry.Key.ToString()!)
            .Where(IsEnvironmentOverrideName)
            .ToDictionary(
                name => name,
                Environment.GetEnvironmentVariable);

        try
        {
            foreach (var name in previousValues.Keys)
                Environment.SetEnvironmentVariable(name, null);

            action();
        }
        finally
        {
            foreach (var (name, value) in previousValues)
                Environment.SetEnvironmentVariable(name, value);
        }
    }

    private static bool IsEnvironmentOverrideName(string name)
    {
        if (name.Equals(nameof(AppSettings.Environment), StringComparison.OrdinalIgnoreCase))
            return true;

        return EnvironmentVariablePrefixes.Any(prefix =>
            name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
            name[prefix.Length..].Equals(nameof(AppSettings.Environment), StringComparison.OrdinalIgnoreCase));
    }
}

[CollectionDefinition(EnvironmentVariableIsolationCollectionNames.Configuration, DisableParallelization = true)]
public sealed class EnvironmentVariableIsolationCollection
{
}

internal static class EnvironmentVariableIsolationCollectionNames
{
    public const string Configuration = "Environment variable isolation";
}
