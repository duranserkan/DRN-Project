using DRN.Framework.Hosting.DrnProgram;
using DRN.Framework.Utils.Logging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HostFiltering;
using Microsoft.AspNetCore.HttpOverrides;

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
    
    [Fact]
    public void ConfigureHostFilteringOptions_Should_Allow_Development_Fallback()
    {
        var appSettings = CreateAppSettings(isDevelopment: true);
        var options = new HostFilteringOptions();
        var configure = new TestProgram().ExposeConfigureHostFilteringOptions(appSettings);

        configure(options);

        options.AllowedHosts.Should().Equal("*");
    }
    
    [Fact]
    public void ConfigureHostFilteringOptions_Should_Use_Configured_Production_Hosts()
    {
        var appSettings = CreateAppSettings(isDevelopment: false, ("AllowedHosts", "example.com;api.example.com"));
        var options = new HostFilteringOptions();
        var configure = new TestProgram().ExposeConfigureHostFilteringOptions(appSettings);

        configure(options);

        options.AllowedHosts.Should().Equal("example.com", "api.example.com");
    }

    [Fact]
    public void ConfigureForwardedHeadersOptions_Should_Use_Default_Trusted_Networks_And_ForwardLimit_Of_Two()
    {
        var appSettings = CreateAppSettings(isDevelopment: true);
        var options = new ForwardedHeadersOptions();
        var configure = new TestProgram().ExposeConfigureForwardedHeadersOptions(appSettings);

        configure(options);

        options.ForwardedHeaders.Should().Be(ForwardedHeaders.All);
        options.ForwardLimit.Should().Be(2);
        options.KnownIPNetworks.Should().Contain(n => n.BaseAddress.ToString() == "127.0.0.0" && n.PrefixLength == 8);
        options.KnownIPNetworks.Should().Contain(n => n.BaseAddress.ToString() == "::1" && n.PrefixLength == 128);
        options.KnownIPNetworks.Should().Contain(n => n.BaseAddress.ToString() == "10.0.0.0" && n.PrefixLength == 8);
        options.KnownIPNetworks.Should().Contain(n => n.BaseAddress.ToString() == "172.16.0.0" && n.PrefixLength == 12);
        options.KnownIPNetworks.Should().Contain(n => n.BaseAddress.ToString() == "192.168.0.0" && n.PrefixLength == 16);
    }

    [Fact]
    public void ConfigureForwardedHeadersOptions_Should_Bind_Configuration_Section_When_Present()
    {
        var appSettings = CreateAppSettings(isDevelopment: false, ("ForwardedHeaders:ForwardLimit", "3"));
        var options = new ForwardedHeadersOptions();
        var configure = new TestProgram().ExposeConfigureForwardedHeadersOptions(appSettings);

        configure(options);

        options.ForwardedHeaders.Should().Be(ForwardedHeaders.All);
        options.ForwardLimit.Should().Be(3);
        options.KnownIPNetworks.Should().Contain(n => n.BaseAddress.ToString() == "10.0.0.0" && n.PrefixLength == 8);
    }

    private static IAppSettings CreateAppSettings(bool isDevelopment, params (string Key, string Value)[] settings)
    {
        var appSettings = Substitute.For<IAppSettings>();
        appSettings.IsDevelopmentEnvironment.Returns(isDevelopment);
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(settings.Select(pair => new KeyValuePair<string, string?>(pair.Key, pair.Value)))
            .Build();
        appSettings.Configuration.Returns(config);
        appSettings.TryGetSection(Arg.Any<string>(), out Arg.Any<IConfigurationSection>())
            .Returns(callInfo =>
            {
                var key = callInfo.Arg<string>();
                var section = config.GetSection(key);
                if (section.Exists())
                {
                    callInfo[1] = section;
                    return true;
                }

                callInfo[1] = null!;
                return false;
            });

        return appSettings;
    }

    private sealed class TestProgram : DrnProgramBase<TestProgram>, IDrnProgram
    {
        public static Task Main(string[] args) => Task.CompletedTask;

        public Action<HostFilteringOptions> ExposeConfigureHostFilteringOptions(IAppSettings appSettings)
            => ConfigureHostFilteringOptions(appSettings);

        public Action<ForwardedHeadersOptions> ExposeConfigureForwardedHeadersOptions(IAppSettings appSettings)
            => ConfigureForwardedHeadersOptions(appSettings);

        protected override Task AddServicesAsync(WebApplicationBuilder builder, IAppSettings appSettings, IScopedLog scopedLog)
            => Task.CompletedTask;
    }
}
