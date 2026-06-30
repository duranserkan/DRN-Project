using DRN.Framework.Utils.Settings;

namespace DRN.Framework.Utils.Data.Encryption;

internal sealed class NexusKeyRing : IDisposable
{
    public NexusKeyRing(NexusAppSettings settings)
    {
        var defaultNexusKey = settings.GetDefaultKey();
        var createdSecrets = new List<NexusSecret>();
        try
        {
            var defaultSecret = new NexusSecret(defaultNexusKey);
            createdSecrets.Add(defaultSecret);
            Default = defaultSecret;

            var fallbackList = new List<NexusSecret>();
            foreach (var key in settings.Keys.Where(key => !key.Default))
            {
                var secret = new NexusSecret(key);
                createdSecrets.Add(secret);
                fallbackList.Add(secret);
            }

            Fallback = fallbackList.ToArray();
            AllWithDefaultAsFirstItem = [Default, ..Fallback];
        }
        catch
        {
            foreach (var secret in createdSecrets)
                secret.Dispose();
            throw;
        }
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
