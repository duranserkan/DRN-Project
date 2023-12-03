using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;

namespace DRN.Framework.Utils.Settings;

public class DRNConfigurationSource(string url) : IConfigurationSource
{
    public string Url { get; } = url;

    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        return new DRNConfigurationProvider(this);
    }
}

public class DRNConfigurationProvider(DRNConfigurationSource source) : JsonConfigurationProvider(new JsonConfigurationSource())
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

public static class DRNConfigurationExtensions
{
    public static IConfigurationBuilder AddDRNConfiguration(this IConfigurationBuilder builder, string url)
    {
        return builder.Add(new DRNConfigurationSource(url));
    }
}