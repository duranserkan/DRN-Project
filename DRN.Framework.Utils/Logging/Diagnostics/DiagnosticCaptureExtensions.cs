namespace DRN.Framework.Utils.Logging.Diagnostics;

/// <summary>Additive capture APIs. Foreign implementations retain their existing conservative policy.</summary>
public static class DiagnosticCaptureExtensions
{
    /// <summary>Foreign implementations fail closed because they cannot enforce key classifications.</summary>
    public static IScopedLog AddDictionary<TValue>(this IScopedLog log, string key, IReadOnlyDictionary<string, TValue> values,
        params DiagnosticKeyClassification[] classifications) => log is ScopedLog native
        ? native.AddDictionary(key, values, classifications) : log.Add(key, DiagnosticSanitizer.Invalid);

    public static object? SanitizeDictionary<TValue>(this IDiagnosticSanitizer sanitizer, string path,
        IReadOnlyDictionary<string, TValue> values, params DiagnosticKeyClassification[] classifications) =>
        sanitizer is DiagnosticSanitizer native ? native.SanitizeDictionary(path, values, classifications) : DiagnosticSanitizer.Invalid;

    /// <summary>Exempts one supported scalar. Foreign implementations keep their existing protection.</summary>
    public static IScopedLog Add(this IScopedLog log, string key, object? value, string exemptionReason) =>
        log is ScopedLog native ? native.Add(key, value, exemptionReason) :
            log.Add(key, DiagnosticSanitizer.IsSimpleValue(value) ? value ?? string.Empty : DiagnosticSanitizer.Invalid);

    /// <summary>Exempts one supported scalar. Foreign implementations keep their existing protection.</summary>
    public static object? SanitizeValue(this IDiagnosticSanitizer sanitizer, string path, object? value, string exemptionReason) =>
        sanitizer is DiagnosticSanitizer native ? native.SanitizeValue(path, value, exemptionReason) :
            sanitizer.SanitizeValue(path, DiagnosticSanitizer.IsSimpleValue(value) ? value : DiagnosticSanitizer.Invalid);

    public static IScopedLog AddWithPolicy(this IScopedLog log, string key, object value, DiagnosticCapturePolicy policy) =>
        log is ScopedLog native ? native.AddWithPolicy(key, value, policy) : log.Add(key, value);

    public static IScopedLog AddPropertiesWithPolicy<TValue>(this IScopedLog log, string prefix, TValue value,
        DiagnosticCapturePolicy policy, params string[] ignoredPropertyNames) where TValue : class =>
        log is ScopedLog native ? native.AddPropertiesWithPolicy(prefix, value, policy, ignoredPropertyNames) :
            log.AddProperties(prefix, value, ignoredPropertyNames);

    public static object? SanitizeWithPolicy(this IDiagnosticSanitizer sanitizer, string path, object? value,
        DiagnosticCapturePolicy policy) => sanitizer is DiagnosticSanitizer native
        ? native.SanitizeValue(path, value, policy) : sanitizer.SanitizeValue(path, value);
}
