using System.Text.Json;
using DRN.Framework.Utils.Configurations;

namespace DRN.Test.Unit.Tests.Framework.Utils.Configurations;

public class ConfigurationDebugViewTests
{
    [Theory]
    [DataInlineUnit("testDb", "Server=127.0.0.1;Port=5432;Database=myDataBase;User Id=myUsername;Password=myPassword;")]
    public void ConfigurationDebugView_Should_List_Keys(DrnTestContextUnit context, string name, string connectionString)
    {
        var connectionStrings = new ConnectionStringsCollection();
        connectionStrings.ConnectionStrings.Add(name, connectionString);
        context.AddToConfiguration(connectionStrings);
        context.AddToConfiguration("SafeSection", "Visible", "safe-value");

        var debugView = context.GetConfigurationDebugView();
        var debugViewJson = JsonSerializer.Serialize(debugView);

        debugViewJson.Should().NotBeNullOrWhiteSpace();
        debugView.RawValuesIncluded.Should().BeFalse();

        var settings = debugView.SettingsByProvider.Values.SelectMany(value => value).ToArray();
        settings.Should().Contain($"ConnectionStrings:{name}=[redacted]");
        settings.Should().Contain("SafeSection:Visible=safe-value");
        debugViewJson.Should().NotContain(connectionString);
        debugViewJson.Should().NotContain("myPassword");
    }

    [Theory]
    [DataInlineUnit("testDb", "Server=127.0.0.1;Port=5432;Database=myDataBase;User Id=myUsername;Password=myPassword;")]
    public void ConfigurationDebugView_Should_Include_Raw_Values_Only_When_Development_Explicitly_Requests_Them(
        string name, string connectionString)
    {
        var connectionStrings = new ConnectionStringsCollection();
        connectionStrings.ConnectionStrings.Add(name, connectionString);
        var appSettings = AppSettings.Development(connectionStrings);

        var debugView = appSettings.GetDebugView(includeRawValues: true).ToSummary();

        debugView.RawValuesIncluded.Should().BeTrue();
        var settings = debugView.SettingsByProvider.Values.SelectMany(value => value).ToArray();
        settings.Should().Contain($"ConnectionStrings:{name}={connectionString}");
    }

    [Theory]
    [DataInlineUnit("testDb", "Server=127.0.0.1;Port=5432;Database=myDataBase;User Id=myUsername;Password=myPassword;")]
    public void ConfigurationDebugView_Should_Redact_Raw_Values_When_Not_Development(string name, string connectionString)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"ConnectionStrings:{name}"] = connectionString
            })
            .Build();
        var appSettings = Substitute.For<IAppSettings>();
        appSettings.Configuration.Returns(configuration);
        appSettings.Environment.Returns(AppEnvironment.Production);
        appSettings.ApplicationName.Returns("TestApp");
        appSettings.IsDevelopmentEnvironment.Returns(false);

        var debugView = new ConfigurationDebugView(appSettings, includeRawValues: true).ToSummary();

        debugView.RawValuesIncluded.Should().BeFalse();
        var settings = debugView.SettingsByProvider.Values.SelectMany(value => value).ToArray();
        settings.Should().Contain($"ConnectionStrings:{name}=[redacted]");
        settings.Should().NotContain(value => value.Contains(connectionString));
    }

    [Theory]
    [DataInlineUnit]
    public void ConfigurationDebugView_Should_Redact_Key_Containers_And_Key_Names(DrnTestContextUnit context)
    {
        context.AddToConfiguration("Keys:SomeConfig", "key-container-value");
        context.AddToConfiguration("ApiKeys:Primary", "api-key-value");
        context.AddToConfiguration("SafeSection:SomeKeys", "leaf-keys-value");
        context.AddToConfiguration("KeysSection:SomeConfig", "visible-value");
        context.AddToConfiguration("SafeSection:KeyboardLayout", "tr");

        var debugView = context.GetConfigurationDebugView();
        var settings = debugView.SettingsByProvider.Values.SelectMany(value => value).ToArray();

        settings.Should().Contain("Keys:SomeConfig=[redacted]");
        settings.Should().Contain("ApiKeys:Primary=[redacted]");
        settings.Should().Contain("SafeSection:SomeKeys=[redacted]");
        settings.Should().Contain("KeysSection:SomeConfig=visible-value");
        settings.Should().Contain("SafeSection:KeyboardLayout=tr");
    }
}
