using DRN.Framework.Utils.Settings;

namespace DRN.Framework.Utils.Data.Encryption;

internal sealed class NexusKeyRing : IDisposable
{
    public NexusKeyRing(NexusAppSettings settings)
    {
        var defaultNexusKey = settings.GetDefaultKey();
        Default = new NexusKeyMaterial(defaultNexusKey);
        Fallback = settings.Keys
            .Where(key => !key.Default)
            .Select(key => new NexusKeyMaterial(key))
            .ToArray();

        AllWithDefaultAsFirstItem = [Default, ..Fallback];
    }

    public NexusKeyMaterial Default { get; }
    public IReadOnlyList<NexusKeyMaterial> Fallback { get; }
    public IReadOnlyList<NexusKeyMaterial> AllWithDefaultAsFirstItem { get; }

    public void Dispose()
    {
        foreach (var key in AllWithDefaultAsFirstItem)
            key.Dispose();
    }
}
