using System.Diagnostics.CodeAnalysis;
using System.Net;
using DRN.Framework.SharedKernel;
using DRN.Framework.Utils.Settings;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HostFiltering;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Configuration;
using IPNetwork = System.Net.IPNetwork;

namespace DRN.Framework.Hosting.DrnProgram.Configurators;

internal static class DrnRequestConfigurator
{
    internal static Action<ForwardedHeadersOptions> ConfigureForwardedHeadersOptions(IAppSettings appSettings)
    {
        return options =>
        {
            ApplyDefaultForwardedHeaders(options);

            if (!appSettings.TryGetSection("ForwardedHeaders", out var section))
                return;

            section.Bind(options);
            ApplyTrustPrivateNetworksSetting(options, section);
            ApplyCustomKnownIpNetworks(options, section);
            ApplyCustomKnownProxies(options, section);
        };
    }

    [SuppressMessage("SonarQube", "S1313", Justification = "Standard RFC 1918 private network ranges and loopback for default forwarded headers in cloud/k8s environments.")]
    private static void ApplyDefaultForwardedHeaders(ForwardedHeadersOptions options)
    {
        options.ForwardedHeaders = ForwardedHeaders.All;
        options.ForwardLimit = 2;

        options.KnownIPNetworks.Clear();
        options.KnownIPNetworks.Add(new IPNetwork(new IPAddress([127, 0, 0, 0]), 8));
        options.KnownIPNetworks.Add(new IPNetwork(IPAddress.IPv6Loopback, 128));
        options.KnownIPNetworks.Add(new IPNetwork(new IPAddress([10, 0, 0, 0]), 8));
        options.KnownIPNetworks.Add(new IPNetwork(new IPAddress([172, 16, 0, 0]), 12));
        options.KnownIPNetworks.Add(new IPNetwork(new IPAddress([192, 168, 0, 0]), 16));
    }

    private static void ApplyTrustPrivateNetworksSetting(ForwardedHeadersOptions options, IConfigurationSection section)
    {
        if (section.GetValue<bool?>("TrustPrivateNetworks") is false)
        {
            for (var i = options.KnownIPNetworks.Count - 1; i >= 0; i--)
            {
                var net = options.KnownIPNetworks[i];
                if (IsRfc1918PrivateNetwork(net))
                    options.KnownIPNetworks.RemoveAt(i);
            }
        }
    }

    private static bool IsRfc1918PrivateNetwork(IPNetwork net) =>
        (net.BaseAddress.Equals(new IPAddress([10, 0, 0, 0])) && net.PrefixLength == 8) ||
        (net.BaseAddress.Equals(new IPAddress([172, 16, 0, 0])) && net.PrefixLength == 12) ||
        (net.BaseAddress.Equals(new IPAddress([192, 168, 0, 0])) && net.PrefixLength == 16);

    private static void ApplyCustomKnownIpNetworks(ForwardedHeadersOptions options, IConfigurationSection section)
    {
        var customNetworks = section.GetSection(nameof(ForwardedHeadersOptions.KnownIPNetworks)).GetChildren().ToList();
        if (customNetworks.Count == 0)
            return;

        options.KnownIPNetworks.Clear();
        foreach (var net in customNetworks)
            options.KnownIPNetworks.Add(ParseIpNetwork(net));
    }

    private static IPNetwork ParseIpNetwork(IConfigurationSection net)
    {
        try
        {
            return net.Value is { } cidr
                ? IPNetwork.Parse(cidr)
                : new IPNetwork(IPAddress.Parse(net["BaseAddress"]!), int.Parse(net["PrefixLength"]!));
        }
        catch (Exception e) when (IsIpParsingException(e))
        {
            throw new ConfigurationException($"Invalid ForwardedHeaders:{nameof(ForwardedHeadersOptions.KnownIPNetworks)} configuration.", e);
        }
    }

    private static void ApplyCustomKnownProxies(ForwardedHeadersOptions options, IConfigurationSection section)
    {
        foreach (var proxy in section.GetSection(nameof(ForwardedHeadersOptions.KnownProxies)).GetChildren())
            if (proxy.Value is { } ip)
                options.KnownProxies.Add(ParseProxyIp(ip));
    }

    private static IPAddress ParseProxyIp(string ip)
    {
        try
        {
            return IPAddress.Parse(ip);
        }
        catch (Exception e) when (IsIpParsingException(e))
        {
            throw new ConfigurationException($"Invalid ForwardedHeaders:{nameof(ForwardedHeadersOptions.KnownProxies)} configuration.", e);
        }
    }

    private static bool IsIpParsingException(Exception e) => e is FormatException or ArgumentException or OverflowException;

    internal static Action<RequestLocalizationOptions> ConfigureRequestLocalizationOptions(IAppSettings appSettings)
    {
        var locOptions = appSettings.Localization;
        return options =>
        {
            var cookieRequestCultureProvider = new CookieRequestCultureProvider
            {
                CookieName = appSettings.GetAppSpecificName("Culture")
            };

            options.RequestCultureProviders.Clear();
            options.RequestCultureProviders.Add(cookieRequestCultureProvider);
            options.RequestCultureProviders.Add(new QueryStringRequestCultureProvider());
            options.RequestCultureProviders.Add(new AcceptLanguageHeaderRequestCultureProvider());
            options.SetDefaultCulture(locOptions.DefaultCulture)
                .AddSupportedCultures(locOptions.SupportedCultures)
                .AddSupportedUICultures(locOptions.SupportedCultures);
        };
    }

    internal static Action<HostFilteringOptions> ConfigureHostFilteringOptions(IAppSettings appSettings)
    {
        return options =>
        {
            if (options.AllowedHosts.Count != 0)
            {
                EnsureAllowedHostsSafe(options.AllowedHosts, appSettings);
                return;
            }

            // "AllowedHosts": "localhost;127.0.0.1;[::1]"
            var hosts = appSettings.Configuration["AllowedHosts"]?.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (hosts?.Length > 0)
            {
                EnsureAllowedHostsSafe(hosts, appSettings);
                options.AllowedHosts = hosts;
                return;
            }

            if (appSettings.IsDevelopmentEnvironment)
            {
                // Fall back to "*" only for local development convenience.
                options.AllowedHosts = ["*"];
                return;
            }

            throw new ConfigurationException("AllowedHosts must be configured outside Development.");
        };
    }

    private static void EnsureAllowedHostsSafe(IEnumerable<string> hosts, IAppSettings appSettings)
    {
        if (appSettings.IsDevelopmentEnvironment) return;

        if (hosts.Any(IsWildcardAllowedHost))
            throw new ConfigurationException("AllowedHosts cannot contain '*' outside Development.");
    }

    private static bool IsWildcardAllowedHost(string host) => host.Trim() == "*";
}
