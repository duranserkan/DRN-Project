using DRN.Framework.Hosting.DrnProgram;
using DRN.Framework.Utils.Logging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HostFiltering;

namespace DRN.Test.Unit.Tests.Framework.Hosting.DrnProgram;

public class DrnProgramBaseSecurityOptionsTests
{
    [Theory]
    [DataInlineUnit(false)]
    public void ConfigureHostFilteringOptions_Should_Require_AllowedHosts_Outside_Development(bool isDevelopment)
    {
        var appSettings = CreateAppSettings(isDevelopment);
        var options = new HostFilteringOptions();
        var configure = new TestProgram().ExposeConfigureHostFilteringOptions(appSettings);

        var act = () => configure(options);

        act.Should().Throw<ConfigurationException>()
            .WithMessage("AllowedHosts must be configured outside Development.");
    }

    [Theory]
    [DataInlineUnit(false)]
    public void ConfigureHostFilteringOptions_Should_Reject_Wildcard_AllowedHosts_Outside_Development(bool isDevelopment)
    {
        var appSettings = CreateAppSettings(isDevelopment, ("AllowedHosts", "*"));
        var options = new HostFilteringOptions();
        var configure = new TestProgram().ExposeConfigureHostFilteringOptions(appSettings);

        var act = () => configure(options);

        act.Should().Throw<ConfigurationException>()
            .WithMessage("AllowedHosts cannot contain '*' outside Development.");
    }

    [Theory]
    [DataInlineUnit]
    public void ConfigureHostFilteringOptions_Should_Allow_Development_Fallback()
    {
        var appSettings = CreateAppSettings(isDevelopment: true);
        var options = new HostFilteringOptions();
        var configure = new TestProgram().ExposeConfigureHostFilteringOptions(appSettings);

        configure(options);

        options.AllowedHosts.Should().Equal("*");
    }

    [Theory]
    [DataInlineUnit]
    public void ConfigureHostFilteringOptions_Should_Use_Configured_Production_Hosts()
    {
        var appSettings = CreateAppSettings(isDevelopment: false, ("AllowedHosts", "example.com;api.example.com"));
        var options = new HostFilteringOptions();
        var configure = new TestProgram().ExposeConfigureHostFilteringOptions(appSettings);

        configure(options);

        options.AllowedHosts.Should().Equal("example.com", "api.example.com");
    }

    private static IAppSettings CreateAppSettings(bool isDevelopment, params (string Key, string Value)[] settings)
    {
        var appSettings = Substitute.For<IAppSettings>();
        appSettings.IsDevelopmentEnvironment.Returns(isDevelopment);
        appSettings.Configuration.Returns(new ConfigurationBuilder()
            .AddInMemoryCollection(settings.Select(pair => new KeyValuePair<string, string?>(pair.Key, pair.Value)))
            .Build());

        return appSettings;
    }

    private sealed class TestProgram : DrnProgramBase<TestProgram>, IDrnProgram
    {
        public static Task Main(string[] args) => Task.CompletedTask;

        public Action<HostFilteringOptions> ExposeConfigureHostFilteringOptions(IAppSettings appSettings)
            => ConfigureHostFilteringOptions(appSettings);

        protected override Task AddServicesAsync(WebApplicationBuilder builder, IAppSettings appSettings, IScopedLog scopedLog)
            => Task.CompletedTask;
    }
}
