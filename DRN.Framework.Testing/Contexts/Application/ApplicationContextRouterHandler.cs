using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace DRN.Framework.Testing.Contexts.Application;

/// <summary>
/// Routes HTTP requests across multiple in-memory applications hosted in <see cref="ApplicationContext"/>.
/// Matches incoming request URIs against registered application test servers, hostnames, ports, and aliases.
/// </summary>
public sealed class ApplicationContextRouterHandler : HttpMessageHandler
{
    private static readonly string[] Suffixes = ["Program", "App", "Host", "Hosted", "Server", "Service"];
    private static readonly string[] GenericSegments = ["DRN", "Framework", "Hosted", "Host", "App", "Server", "Service", "Test", "Utils", "Integration", "Unit"];

    private readonly ConcurrentDictionary<string, Func<HttpMessageHandler>> _addressHandlers = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<Type, Func<HttpMessageHandler>> _typeHandlers = new();
    private readonly ConcurrentDictionary<Type, Func<HttpMessageHandler>> _typeResolvers = new();
    private readonly ConcurrentDictionary<HttpMessageHandler, HttpMessageInvoker> _invokers = new();
    private Func<HttpMessageHandler>? _singleAppHandler;

    /// <summary>
    /// Registers an application entry point and its lazy <see cref="HttpMessageHandler"/> factory with canonical and custom address mappings.
    /// </summary>
    public void Register(Type entryPointType, Func<HttpMessageHandler> handlerFactory, IEnumerable<string>? additionalAddresses = null)
    {
        ArgumentNullException.ThrowIfNull(entryPointType);
        ArgumentNullException.ThrowIfNull(handlerFactory);

        var lazyHandler = new Lazy<HttpMessageHandler>(handlerFactory);
        var resolver = () => lazyHandler.Value;

        _typeHandlers[entryPointType] = resolver;

        // Register canonical type name (e.g. "NexusProgram")
        RegisterAddress(entryPointType.Name, resolver);

        // Register short name without generic suffixes (e.g. "NexusProgram" -> "Nexus")
        var shortName = GetShortName(entryPointType.Name);
        if (!string.Equals(shortName, entryPointType.Name, StringComparison.OrdinalIgnoreCase))
            RegisterAddress(shortName, resolver);

        // Register assembly name and meaningful segments
        RegisterAssemblySegments(entryPointType.Assembly.GetName().Name, resolver);

        RegisterAdditionalAddresses(additionalAddresses, resolver);

        Interlocked.Exchange(ref _singleAppHandler, resolver);
    }

    /// <summary>
    /// Registers an application entry point and its in-memory <see cref="HttpMessageHandler"/> with canonical and custom address mappings.
    /// </summary>
    public void Register(Type entryPointType, HttpMessageHandler handler, IEnumerable<string>? additionalAddresses = null) =>
        Register(entryPointType, () => handler, additionalAddresses);

    /// <summary>
    /// Explicitly maps a hostname, port, or base URL to a registered application type's lazy handler.
    /// </summary>
    public void RegisterAddress(string addressOrHost, Type entryPointType)
    {
        ArgumentNullException.ThrowIfNull(entryPointType);
        var resolver = _typeResolvers.GetOrAdd(entryPointType, t => () => ResolveTypeHandler(t));
        RegisterAddress(addressOrHost, resolver);
    }

    /// <summary>
    /// Explicitly maps a hostname, port, or base URL to a lazy <see cref="HttpMessageHandler"/> factory.
    /// </summary>
    private void RegisterAddress(string addressOrHost, Func<HttpMessageHandler> handlerFactory)
    {
        ArgumentNullException.ThrowIfNull(addressOrHost);
        ArgumentNullException.ThrowIfNull(handlerFactory);

        var normalized = NormalizeAddress(addressOrHost);
        if (string.IsNullOrWhiteSpace(normalized))
            return;

        _addressHandlers[normalized] = handlerFactory;

        if (int.TryParse(normalized, out var purePort) && purePort > 0)
        {
            RegisterPortAliases(normalized, handlerFactory);
            return;
        }

        if (TryExtractHostAndPort(normalized, out var hostPart, out var portPart))
        {
            RegisterHostAndPort(hostPart!, portPart, handlerFactory);
            return;
        }

        RegisterBracketedHostAlias(normalized, handlerFactory);
    }

    /// <summary>
    /// Explicitly maps a hostname, port, or base URL to a specific <see cref="HttpMessageHandler"/>.
    /// </summary>
    public void RegisterAddress(string addressOrHost, HttpMessageHandler handler) => RegisterAddress(addressOrHost, () => handler);

    private void RegisterAssemblySegments(string? assemblyName, Func<HttpMessageHandler> resolver)
    {
        if (string.IsNullOrWhiteSpace(assemblyName))
            return;

        RegisterAddress(assemblyName, resolver);
        var parts = assemblyName.Split('.')
            .Where(part => !string.IsNullOrWhiteSpace(part) &&
                           !GenericSegments.Any(g => part.Equals(g, StringComparison.OrdinalIgnoreCase)));
        foreach (var part in parts)
            RegisterAddress(part, resolver);
    }

    private void RegisterAdditionalAddresses(IEnumerable<string>? additionalAddresses, Func<HttpMessageHandler> resolver)
    {
        if (additionalAddresses == null)
            return;

        foreach (var addr in additionalAddresses.Where(addr => !string.IsNullOrWhiteSpace(addr)))
            RegisterAddress(addr, resolver);
    }

    private void RegisterHostAndPort(string hostPart, string? portPart, Func<HttpMessageHandler> handlerFactory)
    {
        var isWildcard = IsWildcardHost(hostPart);
        var hasNamedHost = !isWildcard && !string.IsNullOrWhiteSpace(hostPart);

        if (hasNamedHost)
        {
            _addressHandlers[hostPart] = handlerFactory;
            RegisterBracketedHostAlias(hostPart, handlerFactory);
        }

        if (!int.TryParse(portPart, out var portNum) || portNum <= 0)
            return;

        RegisterPortAliases(portPart, handlerFactory);

        if (hasNamedHost)
            _addressHandlers[$"{hostPart}:{portPart}"] = handlerFactory;
    }

    private void RegisterPortAliases(string port, Func<HttpMessageHandler> handlerFactory)
    {
        _addressHandlers[port] = handlerFactory;
        _addressHandlers[$"localhost:{port}"] = handlerFactory;
        _addressHandlers[$"127.0.0.1:{port}"] = handlerFactory;
        _addressHandlers[$"[::1]:{port}"] = handlerFactory;
    }

    private void RegisterBracketedHostAlias(string host, Func<HttpMessageHandler> handlerFactory)
    {
        if (!host.StartsWith('[') || !host.EndsWith(']'))
            return;

        var unbracketed = host[1..^1];
        if (!string.IsNullOrWhiteSpace(unbracketed))
            _addressHandlers[unbracketed] = handlerFactory;
    }

    private static bool IsWildcardHost(string? host) =>
        string.IsNullOrWhiteSpace(host) ||
        host.Equals("*", StringComparison.OrdinalIgnoreCase) ||
        host.Equals("+", StringComparison.OrdinalIgnoreCase) ||
        host.Equals("0.0.0.0", StringComparison.OrdinalIgnoreCase);

    private static bool TryExtractHostAndPort(string normalized, out string? hostPart, out string? portPart)
    {
        hostPart = null;
        portPart = null;

        if (normalized.StartsWith('[') && normalized.IndexOf(']') is var closingIndex and > 0)
        {
            hostPart = normalized[..(closingIndex + 1)];
            if (closingIndex + 1 >= normalized.Length || normalized[closingIndex + 1] != ':')
                return false;

            portPart = normalized[(closingIndex + 2)..].TrimEnd('/');
            return true;
        }

        var firstColon = normalized.IndexOf(':');
        var lastColon = normalized.LastIndexOf(':');

        if (firstColon < 0 || firstColon != lastColon)
            return false;

        hostPart = normalized[..lastColon].Trim().TrimStart('/', '*');
        portPart = normalized[(lastColon + 1)..].Trim().TrimEnd('/');

        return true;
    }

    private HttpMessageHandler ResolveTypeHandler(Type entryPointType)
    {
        return _typeHandlers.TryGetValue(entryPointType, out var handler)
            ? handler()
            : throw new InvalidOperationException($"Application '{entryPointType.Name}' is mapped to an address but has not been created in ApplicationContext.");
    }

    /// <summary>
    /// Unregisters an application entry point and its associated handler.
    /// </summary>
    public void Unregister(Type entryPointType)
    {
        ArgumentNullException.ThrowIfNull(entryPointType);
        if (!_typeHandlers.TryRemove(entryPointType, out var handler))
            return;

        foreach (var kvp in _addressHandlers.Where(kvp => ReferenceEquals(kvp.Value, handler)))
            _addressHandlers.TryRemove(kvp.Key, out _);

        if (ReferenceEquals(_singleAppHandler, handler))
            _singleAppHandler = _typeHandlers.Values.FirstOrDefault();
    }

    /// <summary>
    /// Clears all registered applications and addresses.
    /// </summary>
    public void Clear()
    {
        _addressHandlers.Clear();
        _typeHandlers.Clear();
        _typeResolvers.Clear();
        _invokers.Clear();
        _singleAppHandler = null;
    }

    /// <summary>
    /// Creates a non-owning forwarding handler that delegates requests to this router without disposing or clearing router state when closed.
    /// </summary>
    public HttpMessageHandler CreateForwardingHandler() => new NonDisposingForwardingHandler(this);

    private Task<HttpResponseMessage> SendRoutedAsync(HttpRequestMessage request, CancellationToken cancellationToken) => SendAsync(request, cancellationToken);

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var handler = ResolveHandler(request);
        var invoker = _invokers.GetOrAdd(handler, static h => new HttpMessageInvoker(h, disposeHandler: false));

        return invoker.SendAsync(request, cancellationToken);
    }

    private HttpMessageHandler ResolveHandler(HttpRequestMessage request)
    {
        var uri = request.RequestUri;
        if (uri == null)
            throw new InvalidOperationException("HttpRequestMessage RequestUri cannot be null.");

        if (TryFindResolver(request, uri, out var resolver))
            return resolver();

        var registered = string.Join(", ", _addressHandlers.Keys);
        var registeredTypes = string.Join(", ", _typeHandlers.Keys.Select(t => t.Name));

        throw new InvalidOperationException(
            $"No application registered in ApplicationContext matches host '{uri.Host}' (Authority: '{uri.Authority}', Port: {uri.Port}). " +
            $"Registered addresses: [{registered}]. Registered applications: [{registeredTypes}].");
    }

    private bool TryFindResolver(HttpRequestMessage request, Uri uri, [NotNullWhen(true)] out Func<HttpMessageHandler>? resolver)
    {
        // 1. Match by authority (host:port, e.g. "localhost:5988" or "nexus:80")
        if (_addressHandlers.TryGetValue(uri.Authority, out resolver))
            return true;

        // 2. Match by host:port
        if (uri.Port > 0 && _addressHandlers.TryGetValue($"{uri.Host}:{uri.Port}", out resolver))
            return true;

        // 3. Match by host (e.g. "nexus", "sample")
        if (_addressHandlers.TryGetValue(uri.Host, out resolver))
            return true;

        // 4. Match by port alone for non-default ports (e.g. "5988")
        if (uri is { Port: > 0, IsDefaultPort: false } && _addressHandlers.TryGetValue(uri.Port.ToString(), out resolver))
            return true;

        // 5. Match by Host header if present
        if (request.Headers.Host != null && _addressHandlers.TryGetValue(request.Headers.Host, out resolver))
            return true;

        // 6. Fallback for single registered application targeting localhost
        if (IsSingleLocalhostFallback(uri.Host))
        {
            resolver = _singleAppHandler;
            return resolver != null;
        }

        resolver = null;
        return false;
    }

    private bool IsSingleLocalhostFallback(string host) =>
        _typeHandlers.Count == 1 &&
        _singleAppHandler != null &&
        (host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
         host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase));

    public static string GetShortName(string typeName)
    {
        foreach (var suffix in Suffixes)
            if (typeName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) && typeName.Length > suffix.Length)
                return typeName[..^suffix.Length];

        return typeName;
    }

    private static string NormalizeAddress(string address)
    {
        address = address.Trim().TrimEnd('/');
        if (address.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            address = address["http://".Length..];
        else if (address.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            address = address["https://".Length..];

        var slashIndex = address.IndexOf('/');
        if (slashIndex >= 0)
            address = address[..slashIndex];

        return address.TrimEnd('/');
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            Clear();

        base.Dispose(disposing);
    }

    private sealed class NonDisposingForwardingHandler(ApplicationContextRouterHandler routerHandler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            routerHandler.SendRoutedAsync(request, cancellationToken);
    }
}
