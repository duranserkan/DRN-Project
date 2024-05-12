using DRN.Framework.SharedKernel.Conventions;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using DRN.Framework.Utils.Settings;
using Flurl.Http;
using Flurl.Http.Configuration;

namespace DRN.Framework.Utils.Factories;

/// <summary>
/// Internal request is a simple factory for your kubernetes http request calls.
/// <br/>
/// Use http with linkerd service mesh to benefit from mtls
/// </summary>
public interface IInternalRequest
{
    /// <summary>
    /// <see cref="DrnAppFeatures"/>'s InternalRequestHttpVersion and InternalRequestProtocol will be used
    /// </summary>
    /// <param name="service">kubernetes service name</param>
    /// <returns></returns>
    public IFlurlRequest For(string service);

    /// <summary>
    /// <see cref="DrnAppFeatures"/>'s InternalRequestHttpVersion will be used
    /// </summary>
    /// <param name="service">kubernetes service name</param>
    /// <param name="secure">https or not</param>
    IFlurlRequest For(string service, bool secure);

    /// <summary>
    /// <see cref="DrnAppFeatures"/>'s InternalRequestProtocol will be used
    /// </summary>
    /// <param name="service">kubernetes service name</param>
    /// <param name="httpVersion">1.1 or 2.0</param>
    IFlurlRequest For(string service, Version httpVersion);

    /// <param name="service">kubernetes service name</param>
    /// <param name="secure">https or not</param>
    /// <param name="httpVersion">1.1 or 2.0</param>
    IFlurlRequest For(string service, bool secure, Version httpVersion);
}

[Singleton<IInternalRequest>]
public class InternalRequest(IAppSettings appSettings) : IInternalRequest
{
    private const string Http = "http://";
    private const string Https = "https://";
    private static readonly DefaultJsonSerializer JsonSerializer = new(JsonConventions.DefaultOptions);
    private readonly string HttpVersion = new Version(appSettings.Features.InternalRequestHttpVersion).ToString();
    private readonly bool Secure = appSettings.Features.InternalRequestHttpVersion.ToLower().StartsWith("https");

    public IFlurlRequest For(string service) => For(service, Secure, HttpVersion);
    public IFlurlRequest For(string service, bool secure) => For(service, secure, HttpVersion);
    public IFlurlRequest For(string service, Version httpVersion) => For(service, Secure, httpVersion.ToString());

    /// <param name="service">kubernetes service name</param>
    /// <param name="secure">https or not</param>
    /// <param name="httpVersion">1.1 or 2.0</param>
    public IFlurlRequest For(string service, bool secure, Version httpVersion) => For(service, secure, httpVersion.ToString());

    private static IFlurlRequest For(string service, bool secure, string httpVersion)
    {
        var protocol = secure ? Http : Https;

        return $"{protocol}{service}".WithSettings(x =>
        {
            x.HttpVersion = httpVersion;
            x.JsonSerializer = JsonSerializer;
        });
    }
}