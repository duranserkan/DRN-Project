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
        options.KnownIPNetworks.Should().HaveCount(5);
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

    [Fact]
    public void ConfigureForwardedHeadersOptions_Should_Exclude_Private_Networks_When_TrustPrivateNetworks_Is_False()
    {
        var appSettings = CreateAppSettings(isDevelopment: false, ("ForwardedHeaders:TrustPrivateNetworks", "false"));
        var options = new ForwardedHeadersOptions();
        var configure = new TestProgram().ExposeConfigureForwardedHeadersOptions(appSettings);

        configure(options);

        options.ForwardedHeaders.Should().Be(ForwardedHeaders.All);
        options.ForwardLimit.Should().Be(2);
        options.KnownIPNetworks.Should().HaveCount(2);
        options.KnownIPNetworks.Should().Contain(n => n.BaseAddress.ToString() == "127.0.0.0" && n.PrefixLength == 8);
        options.KnownIPNetworks.Should().Contain(n => n.BaseAddress.ToString() == "::1" && n.PrefixLength == 128);
        options.KnownIPNetworks.Should().NotContain(n => n.BaseAddress.ToString() == "10.0.0.0");
        options.KnownIPNetworks.Should().NotContain(n => n.BaseAddress.ToString() == "172.16.0.0");
        options.KnownIPNetworks.Should().NotContain(n => n.BaseAddress.ToString() == "192.168.0.0");
    }

    [Fact]
    public void ConfigureForwardedHeadersOptions_Should_Override_Default_Networks_When_KnownIPNetworks_Configured()
    {
        var appSettings = CreateAppSettings(
            isDevelopment: false,
            ("ForwardedHeaders:KnownIPNetworks:0:BaseAddress", "198.51.100.0"),
            ("ForwardedHeaders:KnownIPNetworks:0:PrefixLength", "24"));
        var options = new ForwardedHeadersOptions();
        var configure = new TestProgram().ExposeConfigureForwardedHeadersOptions(appSettings);

        configure(options);

        options.KnownIPNetworks.Should().HaveCount(1);
        options.KnownIPNetworks.Should().ContainSingle(n => n.BaseAddress.ToString() == "198.51.100.0" && n.PrefixLength == 24);
    }

    [Fact]
    public void ConfigureForwardedHeadersOptions_Should_Support_CIDR_Notation_In_KnownIPNetworks()
    {
        var appSettings = CreateAppSettings(
            isDevelopment: false,
            ("ForwardedHeaders:KnownIPNetworks:0", "203.0.113.0/24"));
        var options = new ForwardedHeadersOptions();
        var configure = new TestProgram().ExposeConfigureForwardedHeadersOptions(appSettings);

        configure(options);

        options.KnownIPNetworks.Should().HaveCount(1);
        options.KnownIPNetworks.Should().ContainSingle(n => n.BaseAddress.ToString() == "203.0.113.0" && n.PrefixLength == 24);
    }

    [Fact]
    public void ConfigureForwardedHeadersOptions_Should_Allow_Explicit_Private_Network_When_TrustPrivateNetworks_Is_False()
    {
        var appSettings = CreateAppSettings(
            isDevelopment: false,
            ("ForwardedHeaders:TrustPrivateNetworks", "false"),
            ("ForwardedHeaders:KnownIPNetworks:0", "10.0.0.0/8"));
        var options = new ForwardedHeadersOptions();
        var configure = new TestProgram().ExposeConfigureForwardedHeadersOptions(appSettings);

        configure(options);

        options.KnownIPNetworks.Should().HaveCount(1);
        options.KnownIPNetworks.Should().ContainSingle(n => n.BaseAddress.ToString() == "10.0.0.0" && n.PrefixLength == 8);
    }

    [Fact]
    public void ConfigureForwardedHeadersOptions_Should_Support_KnownProxies_Configuration()
    {
        var appSettings = CreateAppSettings(
            isDevelopment: false,
            ("ForwardedHeaders:KnownProxies:0", "198.51.100.50"));
        var options = new ForwardedHeadersOptions();
        var configure = new TestProgram().ExposeConfigureForwardedHeadersOptions(appSettings);

        configure(options);

        options.KnownProxies.Should().ContainSingle(ip => ip.ToString() == "198.51.100.50");
    }

    [Theory]
    [DataInlineUnit("invalid-cidr")]
    [DataInlineUnit("192.168.1.1/33")]
    public void ConfigureForwardedHeadersOptions_Should_Throw_ConfigurationException_When_KnownIPNetworks_CIDR_Is_Invalid(string cidr)
    {
        var appSettings = CreateAppSettings(
            isDevelopment: false,
            ("ForwardedHeaders:KnownIPNetworks:0", cidr));
        var options = new ForwardedHeadersOptions();
        var configure = new TestProgram().ExposeConfigureForwardedHeadersOptions(appSettings);

        var act = () => configure(options);

        act.Should().Throw<ConfigurationException>()
            .WithMessage("Invalid ForwardedHeaders:KnownIPNetworks configuration.");
    }

    [Fact]
    public void ConfigureForwardedHeadersOptions_Should_Throw_ConfigurationException_When_KnownIPNetworks_Object_Is_Invalid()
    {
        var appSettings = CreateAppSettings(
            isDevelopment: false,
            ("ForwardedHeaders:KnownIPNetworks:0:BaseAddress", "invalid-ip"),
            ("ForwardedHeaders:KnownIPNetworks:0:PrefixLength", "24"));
        var options = new ForwardedHeadersOptions();
        var configure = new TestProgram().ExposeConfigureForwardedHeadersOptions(appSettings);

        var act = () => configure(options);

        act.Should().Throw<ConfigurationException>()
            .WithMessage("Invalid ForwardedHeaders:KnownIPNetworks configuration.");
    }

    [Fact]
    public void ConfigureForwardedHeadersOptions_Should_Throw_ConfigurationException_When_KnownProxies_Is_Invalid()
    {
        var appSettings = CreateAppSettings(
            isDevelopment: false,
            ("ForwardedHeaders:KnownProxies:0", "invalid-proxy-ip"));
        var options = new ForwardedHeadersOptions();
        var configure = new TestProgram().ExposeConfigureForwardedHeadersOptions(appSettings);

        var act = () => configure(options);

        act.Should().Throw<ConfigurationException>()
            .WithMessage("Invalid ForwardedHeaders:KnownProxies configuration.");
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
