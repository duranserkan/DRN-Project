namespace DRN.Framework.Utils.Concurrency;

public static class LockUtils
{
    /// <summary>
    /// Atomically claims a lock by changing the value from 0 to 1.
    /// Returns true if the claim was successful (value was 0).
    /// </summary>
    /// <param name="lockValue">The integer variable used as a lock (0 = free, 1 = claimed).</param>
    /// <returns><c>true</c> if the lock was successfully claimed; otherwise, <c>false</c>.</returns>
    public static bool TryClaimLock(ref int lockValue) => Interlocked.CompareExchange(ref lockValue, 1, 0) == 0;

    /// <summary>
    /// Unconditionally releases a lock by setting the value to 0.
    /// </summary>
    /// <param name="lockValue">The integer variable used as a lock (0 = free, 1 = claimed).</param>
    public static void ReleaseLock(ref int lockValue) => Interlocked.Exchange(ref lockValue, 0);

    /// <summary>
    /// Atomically sets a reference-type value if it equals the comparand.
    /// </summary>
    public static bool TrySetIfEqual<T>(ref T? location, T value, T? comparand) where T : class
        => Interlocked.CompareExchange(ref location, value, comparand) == comparand;

    /// <summary>
    /// Atomically sets a reference-type value if the current value is null.
    /// </summary>
    public static bool TrySetIfNull<T>(ref T? location, T value) where T : class
        => TrySetIfEqual(ref location, value, null);

    /// <summary>
    /// Atomically sets a reference-type value if it does NOT equal the comparand.
    /// </summary>
    public static bool TrySetIfNotEqual<T>(ref T? location, T value, T? comparand) where T : class
    {
        while (true)
        {
            var snapshot = Volatile.Read(ref location);
            if (snapshot == comparand) return false;
            if (TrySetIfEqual(ref location, value, snapshot)) return true;
        }
    }

    /// <summary>
    /// Atomically sets a reference-type value if the current value is NOT null.
    /// </summary>
    public static bool TrySetIfNotNull<T>(ref T? location, T value) where T : class
        => TrySetIfNotEqual(ref location, value, null);
}