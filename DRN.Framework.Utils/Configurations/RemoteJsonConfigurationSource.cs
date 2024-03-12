using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;

namespace DRN.Framework.Utils.Configurations;

public class RemoteJsonConfigurationSource(string url) : IConfigurationSource
{
    public string Url { get; } = url;

    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        return new RemoteJsonConfigurationProvider(this);
    }
}

public class RemoteJsonConfigurationProvider(RemoteJsonConfigurationSource source) : JsonConfigurationProvider(new JsonConfigurationSource())
{
    public override void Load()
    {
        using var httpClient = new HttpClient();
        var response = httpClient.GetAsync(source.Url).Result;
        response.EnsureSuccessStatusCode();

        using var stream = response.Content.ReadAsStreamAsync().Result;
        Load(stream);
    }
}

public static partial class ConfigurationExtensions
{
    public static IConfigurationBuilder RemoteJsonConfiguration(this IConfigurationBuilder builder, string url)
    {
        return builder.Add(new ObjectToJsonConfigurationSource(url));
    }
}