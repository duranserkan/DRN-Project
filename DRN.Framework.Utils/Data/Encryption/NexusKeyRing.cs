using DRN.Framework.Utils.Settings;

namespace DRN.Framework.Utils.Data.Encryption;

internal sealed class NexusKeyRing : IDisposable
{
    public NexusKeyRing(NexusAppSettings settings)
    {
        var defaultNexusKey = settings.GetDefaultKey();
        Default = new NexusSecret(defaultNexusKey);
        Fallback = settings.Keys
            .Where(key => !key.Default)
            .Select(key => new NexusSecret(key))
            .ToArray();

        AllWithDefaultAsFirstItem = [Default, ..Fallback];
    }

    public NexusSecret Default { get; }
    public IReadOnlyList<NexusSecret> Fallback { get; }
    public IReadOnlyList<NexusSecret> AllWithDefaultAsFirstItem { get; }

    public void Dispose()
    {
        foreach (var key in AllWithDefaultAsFirstItem)
            key.Dispose();
    }
}
