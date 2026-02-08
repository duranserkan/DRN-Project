using DRN.Framework.Utils.DependencyInjection.Attributes;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;

namespace DRN.Framework.Hosting.Utils;

/// <summary>
/// Interface for resolving server addresses from <see cref="IServer"/>.
/// </summary>
public interface IServerAddressResolver
{
    /// <summary>
    /// Resolves a loopback address from the server's bound addresses.
    /// Converts wildcard hosts (0.0.0.0, [::], +, *) to localhost for self-requests.
    /// Prefers HTTP over HTTPS to avoid TLS overhead for internal requests.
    /// </summary>
    /// <returns>
    /// A normalized loopback address (e.g., "http://localhost:5000"), or <c>null</c>
    /// if no server addresses are available.
    /// </returns>
    string? GetLoopbackAddress();

    /// <summary>
    /// Gets all normalized addresses from the server's bound addresses.
    /// </summary>
    /// <returns>A list of normalized addresses with wildcards converted to localhost.</returns>
    IReadOnlyList<string> GetAllAddresses();
}

/// <summary>
/// Utility class for resolving server addresses from <see cref="IServer"/>.
/// Provides methods to extract and normalize bound addresses for internal self-requests.
/// </summary>
[Singleton<IServerAddressResolver>]
public sealed class ServerAddressResolver : IServerAddressResolver
{
    private readonly IServer _server;

    /// <summary>
    /// Creates a new instance of <see cref="ServerAddressResolver"/>.
    /// </summary>
    /// <param name="server">The server instance to resolve addresses from.</param>
    public ServerAddressResolver(IServer server)
    {
        _server = server;
    }

    /// <inheritdoc />
    public string? GetLoopbackAddress()
    {
        var addressFeature = _server.Features.Get<IServerAddressesFeature>();
        if (addressFeature == null) return null;

        string? httpsAddress = null;
        foreach (var address in addressFeature.Addresses)
        {
            if (!Uri.TryCreate(address, UriKind.Absolute, out var uri))
                continue;

            var host = NormalizeHost(uri.Host);
            var normalized = $"{uri.Scheme}://{host}:{uri.Port}";

            // Prefer HTTP to avoid TLS handshake overhead for self-requests
            if (uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase))
                return normalized;

            httpsAddress ??= normalized;
        }

        return httpsAddress;
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GetAllAddresses()
    {
        var addressFeature = _server.Features.Get<IServerAddressesFeature>();
        if (addressFeature == null) return [];

        var addresses = new List<string>();
        foreach (var address in addressFeature.Addresses)
        {
            if (!Uri.TryCreate(address, UriKind.Absolute, out var uri))
                continue;

            var host = NormalizeHost(uri.Host);
            addresses.Add($"{uri.Scheme}://{host}:{uri.Port}");
        }

        return addresses;
    }

    /// <summary>
    /// Normalizes wildcard host bindings to localhost for self-request scenarios.
    /// </summary>
    /// <param name="host">The original host value from the server binding.</param>
    /// <returns>The normalized host, with wildcards replaced by "localhost".</returns>
    public static string NormalizeHost(string host) => host switch
    {
        "0.0.0.0" or "[::]" or "+" or "*" => "localhost",
        _ => host
    };
}
