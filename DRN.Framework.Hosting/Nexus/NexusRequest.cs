using DRN.Framework.Utils.DependencyInjection.Attributes;
using DRN.Framework.Utils.Http;
using Flurl.Http;

namespace DRN.Framework.Hosting.Nexus;

public interface INexusRequest
{
    IFlurlRequest For(string path);
}

[Singleton<INexusRequest>]
public class NexusRequest(IInternalRequest request) : INexusRequest
{
    public IFlurlRequest For(string path) => request.For("nexus").AppendPathSegment(path);
}