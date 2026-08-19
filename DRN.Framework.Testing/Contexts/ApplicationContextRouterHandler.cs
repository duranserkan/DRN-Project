using System.Collections.Concurrent;

namespace DRN.Framework.Testing.Contexts;

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
        Func<HttpMessageHandler> resolver = () => lazyHandler.Value;

        _typeHandlers[entryPointType] = resolver;

        // Register canonical type name (e.g. "NexusProgram")
        RegisterAddress(entryPointType.Name, resolver);

        // Register short name without generic suffixes (e.g. "NexusProgram" -> "Nexus")
        var shortName = GetShortName(entryPointType.Name);
        if (!string.Equals(shortName, entryPointType.Name, StringComparison.OrdinalIgnoreCase))
            RegisterAddress(shortName, resolver);

        // Register assembly name and meaningful segments
        var assemblyName = entryPointType.Assembly.GetName().Name;
        if (!string.IsNullOrWhiteSpace(assemblyName))
        {
            RegisterAddress(assemblyName, resolver);
            var parts = assemblyName.Split('.');
            foreach (var part in parts)
            {
                if (!string.IsNullOrWhiteSpace(part) &&
                    !GenericSegments.Any(g => part.Equals(g, StringComparison.OrdinalIgnoreCase)))
                {
                    RegisterAddress(part, resolver);
                }
            }
        }

        if (additionalAddresses != null)
        {
            foreach (var addr in additionalAddresses)
            {
                if (!string.IsNullOrWhiteSpace(addr))
                    RegisterAddress(addr, resolver);
            }
        }

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
    public void RegisterAddress(string addressOrHost, Func<HttpMessageHandler> handlerFactory)
    {
        ArgumentNullException.ThrowIfNull(addressOrHost);
        ArgumentNullException.ThrowIfNull(handlerFactory);

        var normalized = NormalizeAddress(addressOrHost);
        if (string.IsNullOrWhiteSpace(normalized))
            return;

        _addressHandlers[normalized] = handlerFactory;

        if (int.TryParse(normalized, out var purePort) && purePort > 0)
        {
            _addressHandlers[normalized] = handlerFactory;
            _addressHandlers[$"localhost:{normalized}"] = handlerFactory;
            _addressHandlers[$"127.0.0.1:{normalized}"] = handlerFactory;
            _addressHandlers[$"[::1]:{normalized}"] = handlerFactory;
            return;
        }

        if (TryExtractHostAndPort(normalized, out var hostPart, out var portPart))
        {
            var isWildcardHost = string.IsNullOrWhiteSpace(hostPart) ||
                                 hostPart.Equals("*", StringComparison.OrdinalIgnoreCase) ||
                                 hostPart.Equals("+", StringComparison.OrdinalIgnoreCase) ||
                                 hostPart.Equals("0.0.0.0", StringComparison.OrdinalIgnoreCase);

            if (!isWildcardHost && !string.IsNullOrWhiteSpace(hostPart))
            {
                _addressHandlers[hostPart] = handlerFactory;
                if (hostPart.StartsWith('[') && hostPart.EndsWith(']'))
                {
                    var unbracketed = hostPart[1..^1];
                    if (!string.IsNullOrWhiteSpace(unbracketed))
                        _addressHandlers[unbracketed] = handlerFactory;
                }
            }

            if (int.TryParse(portPart, out var portNum) && portNum > 0)
            {
                _addressHandlers[portPart!] = handlerFactory;
                _addressHandlers[$"localhost:{portPart}"] = handlerFactory;
                _addressHandlers[$"127.0.0.1:{portPart}"] = handlerFactory;
                _addressHandlers[$"[::1]:{portPart}"] = handlerFactory;
                if (!isWildcardHost && !string.IsNullOrWhiteSpace(hostPart))
                    _addressHandlers[$"{hostPart}:{portPart}"] = handlerFactory;
            }
        }
        else if (normalized.StartsWith('[') && normalized.EndsWith(']'))
        {
            var unbracketed = normalized[1..^1];
            if (!string.IsNullOrWhiteSpace(unbracketed))
                _addressHandlers[unbracketed] = handlerFactory;
        }
    }

    /// <summary>
    /// Explicitly maps a hostname, port, or base URL to a specific <see cref="HttpMessageHandler"/>.
    /// </summary>
    public void RegisterAddress(string addressOrHost, HttpMessageHandler handler) =>
        RegisterAddress(addressOrHost, () => handler);

    private static bool TryExtractHostAndPort(string normalized, out string? hostPart, out string? portPart)
    {
        hostPart = null;
        portPart = null;

        if (normalized.StartsWith('[') && normalized.IndexOf(']') is var closingIndex and > 0)
        {
            hostPart = normalized[..(closingIndex + 1)];
            if (closingIndex + 1 < normalized.Length && normalized[closingIndex + 1] == ':')
            {
                portPart = normalized[(closingIndex + 2)..].TrimEnd('/');
                return true;
            }

            return false;
        }

        var firstColon = normalized.IndexOf(':');
        var lastColon = normalized.LastIndexOf(':');

        if (firstColon >= 0 && firstColon == lastColon)
        {
            hostPart = normalized[..lastColon].Trim().TrimStart('/', '*');
            portPart = normalized[(lastColon + 1)..].Trim().TrimEnd('/');
            return true;
        }

        return false;
    }

    private HttpMessageHandler ResolveTypeHandler(Type entryPointType)
    {
        if (_typeHandlers.TryGetValue(entryPointType, out var handler))
            return handler();

        throw new InvalidOperationException(
            $"Application '{entryPointType.Name}' is mapped to an address but has not been created in ApplicationContext.");
    }

    /// <summary>
    /// Unregisters an application entry point and its associated handler.
    /// </summary>
    public void Unregister(Type entryPointType)
    {
        ArgumentNullException.ThrowIfNull(entryPointType);

        if (_typeHandlers.TryRemove(entryPointType, out var handler))
        {
            foreach (var kvp in _addressHandlers)
            {
                if (ReferenceEquals(kvp.Value, handler))
                    _addressHandlers.TryRemove(kvp.Key, out _);
            }

            if (ReferenceEquals(_singleAppHandler, handler))
                _singleAppHandler = _typeHandlers.Values.FirstOrDefault();
        }
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

    internal Task<HttpResponseMessage> SendRoutedAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        SendAsync(request, cancellationToken);

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

        Func<HttpMessageHandler>? resolver = null;

        // 1. Match by authority (host:port, e.g. "localhost:5988" or "nexus:80")
        if (_addressHandlers.TryGetValue(uri.Authority, out var authorityHandler))
            resolver = authorityHandler;
        // 2. Match by host:port
        else if (uri.Port > 0 && _addressHandlers.TryGetValue($"{uri.Host}:{uri.Port}", out var hostPortHandler))
            resolver = hostPortHandler;
        // 3. Match by host (e.g. "nexus", "sample")
        else if (_addressHandlers.TryGetValue(uri.Host, out var hostHandler))
            resolver = hostHandler;
        // 4. Match by port alone for non-default ports (e.g. "5988")
        else if (uri.Port > 0 && !uri.IsDefaultPort && _addressHandlers.TryGetValue(uri.Port.ToString(), out var portHandler))
            resolver = portHandler;
        // 5. Match by Host header if present
        else if (request.Headers.Host != null && _addressHandlers.TryGetValue(request.Headers.Host, out var headerHandler))
            resolver = headerHandler;
        // 6. Fallback for single registered application targeting localhost
        else if (_typeHandlers.Count == 1 && _singleAppHandler != null && (uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) || uri.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase)))
            resolver = _singleAppHandler;

        if (resolver != null)
            return resolver();

        var registered = string.Join(", ", _addressHandlers.Keys);
        var registeredTypes = string.Join(", ", _typeHandlers.Keys.Select(t => t.Name));

        throw new InvalidOperationException(
            $"No application registered in ApplicationContext matches host '{uri.Host}' (Authority: '{uri.Authority}', Port: {uri.Port}). " +
            $"Registered addresses: [{registered}]. Registered applications: [{registeredTypes}].");
    }

    public static string GetShortName(string typeName)
    {
        foreach (var suffix in Suffixes)
        {
            if (typeName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) && typeName.Length > suffix.Length)
                return typeName[..^suffix.Length];
        }

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

        protected override void Dispose(bool disposing)
        {
            // Non-owning wrapper; do not dispose or clear the shared routerHandler.
            base.Dispose(disposing);
        }
    }
}
