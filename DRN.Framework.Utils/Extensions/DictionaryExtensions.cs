namespace DRN.Framework.Utils.Extensions;

public static class DictionaryExtensions
{
    /// <summary>
    /// Tries to get the value associated with the specified key or returns a default value if the key does not exist.
    /// </summary>
    public static TValue GetAndCastValueOrDefault<TKey, TValue>(this IDictionary<TKey, object> dictionary, TKey key, TValue defaultValue = default!)
    {
        if (dictionary.TryGetValue(key, out var value))
            return value is TValue tValue ? tValue : defaultValue;

        return defaultValue;
    }

    /// <summary>
    /// Updates the value of an existing key based on a condition or adds the key with a new value.
    /// </summary>
    public static void UpdateIf<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TValue newValue, TKey key, Func<TValue, bool> condition)
    {
        if (dictionary.TryGetValue(key, out var existingValue) && condition(existingValue))
            dictionary[key] = newValue;
    }
}