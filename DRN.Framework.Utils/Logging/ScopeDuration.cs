namespace DRN.Framework.Utils.Logging;

public struct ScopeDuration(string key, IScopedLog? log = null) : IDisposable
{
    public string Key { get; } = key;
    public DateTimeOffset Start { get; } = DateTimeOffset.Now;
    public DateTimeOffset? End { get; private set; }
    public TimeSpan Duration => (End ?? DateTimeOffset.Now) - Start;
    public bool IsCompleted => End is not null;
    
    public void Complete()
    {
        if (IsCompleted)
            return;

        End = DateTimeOffset.Now;
        log?.IncreaseTimeSpentOn(Key, Duration);
    }

    //convenience method to complete measurements with one using statement which calls Complete() during scope close
    public void Dispose() => Complete();
}