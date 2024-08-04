using DRN.Framework.Utils.DependencyInjection.Attributes;
using DRN.Framework.Utils.Http;
using DRN.Framework.Utils.Settings;
using Flurl.Http;

namespace DRN.Framework.Hosting.Nexus;

public interface INexusRequest
{
    IFlurlRequest For(string path);
}

[Singleton<INexusRequest>]
public class NexusRequest(IInternalRequest request, IAppSettings settings) : INexusRequest
{
    private readonly string _nexusAddress = settings.Features.NexusAddress;
    public IFlurlRequest For(string path) => request.For(_nexusAddress).AppendPathSegment(path);
}