using DRN.Framework.Utils.DependencyInjection.Attributes;
using DRN.Framework.Utils.Http;
using Flurl.Http;

namespace DRN.Framework.Hosting.Nexus;

public interface INexusClient
{
    Task<HttpResponse<string>> GetStatusAsync();
}

[Singleton<INexusClient>]
public class NexusClient(INexusRequest request) : INexusClient
{
    public async Task<HttpResponse<string>> GetStatusAsync() =>
        await request.For("status").GetAsync().ToStringAsync();
}