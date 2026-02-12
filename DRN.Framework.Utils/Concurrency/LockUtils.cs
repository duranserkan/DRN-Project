namespace DRN.Framework.Utils.Concurrency;

//todo benchmark
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
    /// Attempts to claim a lock and returns a disposable scope that automatically releases it.
    /// The lock is released on <see cref="LockScope.Dispose"/> only if it was successfully acquired.
    /// </summary>
    /// <param name="lockValue">The integer variable used as a lock (0 = free, 1 = claimed).</param>
    /// <returns>A <see cref="LockScope"/> whose <see cref="LockScope.Acquired"/> property indicates success.</returns>
    public static LockScope TryClaimScope(ref int lockValue) => new(ref lockValue, TryClaimLock(ref lockValue));

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
    /// Returns false if the current value equals the comparand or if max retries are exhausted.
    /// </summary>
    /// <param name="location">The reference-type variable to update.</param>
    /// <param name="value">The value to set.</param>
    /// <param name="comparand">The value that must NOT be current for the operation to proceed.</param>
    /// <param name="maxRetries">Maximum number of CAS retry attempts before giving up. Default is 100.</param>
    public static bool TrySetIfNotEqual<T>(ref T? location, T value, T? comparand, int maxRetries = 100) where T : class
    {
        for (var i = 0; i < maxRetries; i++)
        {
            var snapshot = Volatile.Read(ref location);
            if (snapshot == comparand) return false;
            if (TrySetIfEqual(ref location, value, snapshot)) return true;
        }

        return false;
    }

    /// <summary>
    /// Atomically sets a reference-type value if the current value is NOT null.
    /// Returns false if the current value is null or if max retries are exhausted.
    /// </summary>
    /// <param name="location">The reference-type variable to update.</param>
    /// <param name="value">The value to set.</param>
    /// <param name="maxRetries">Maximum number of CAS retry attempts before giving up. Default is 100.</param>
    public static bool TrySetIfNotNull<T>(ref T? location, T value, int maxRetries = 100) where T : class
        => TrySetIfNotEqual(ref location, value, null, maxRetries);

    /// <summary>
    /// A disposable scope that automatically releases a lock when disposed.
    /// Use with <c>using</c> to guarantee lock release without explicit try/finally.
    /// </summary>
    public readonly ref struct LockScope
    {
        private readonly ref int _lockValue;

        /// <summary>
        /// Indicates whether the lock was successfully acquired.
        /// </summary>
        public readonly bool Acquired;

        internal LockScope(ref int lockValue, bool acquired)
        {
            _lockValue = ref lockValue;
            Acquired = acquired;
        }

        /// <summary>
        /// Releases the lock if it was acquired.
        /// </summary>
        public void Dispose()
        {
            if (Acquired)
                ReleaseLock(ref _lockValue);
        }
    }
}