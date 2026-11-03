using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using DRN.Framework.Utils.Extensions;

namespace DRN.Framework.Utils.Logging.Diagnostics;

// Getters run outside ScopedLog's storage lock. The owner retains storage and failure accounting.
internal readonly struct DiagnosticPropertyCapture(ScopedLog owner, string[] ignoredPropertyNames)
{
    private IDiagnosticSanitizer Sanitizer => owner.Sanitizer;
    private readonly DiagnosticSanitizer? _native = owner.Sanitizer as DiagnosticSanitizer;

    [SuppressMessage("ReSharper", "StaticMemberInGenericType")]
    private static class PropertyCache<TValue> where TValue : class
    {
        private static readonly PropertyInfo[] Properties = typeof(TValue)
            .GetProperties(BindingFlag.InstancePublic)
            .Where(property => property.GetMethod is { IsPublic: true } && property.GetIndexParameters().Length == 0)
            .ToArray();
        [SuppressMessage("Major Code Smell", "S2743:Static fields should not be used in generic types",
            Justification = "Each TValue requires its own runtime metadata cache because entries are projected from that declared type's properties.")]
        private static readonly ConcurrentDictionary<Type, DiagnosticTypeMetadata.Member[]> RuntimeProperties = new();

        internal static DiagnosticTypeMetadata.Member[] For(Type runtimeType) => RuntimeProperties.GetOrAdd(runtimeType, static type =>
            Properties.Select(property => DiagnosticTypeMetadata.ForProperty(property, type)).ToArray());
    }

    internal static DiagnosticTypeMetadata.Member[] PropertiesFor<TValue>(Type runtimeType) where TValue : class =>
        PropertyCache<TValue>.For(runtimeType);

    internal void Add(object classObject, string key, DiagnosticTypeMetadata.Member property)
    {
        var (propertyInfo, ignoredByAttribute, sensitive) = property;
        var ignored = ignoredByAttribute || ignoredPropertyNames.Contains(propertyInfo.Name);
        if (ignored) { owner.Store(Sanitizer.SanitizeKey(key), ScopedLogConventions.IgnoredLogValue); return; }
        if (_native == null && (sensitive || Sanitizer.IsSensitive(key)))
        {
            owner.Store(Sanitizer.SanitizeKey(key), DiagnosticSanitizer.Redacted);
            return;
        }
        var originalKey = key;
        try
        {
            var value = propertyInfo.GetValue(classObject) ?? string.Empty;
            if (_native == null) { owner.Add(key, value); return; }

            if (value is TimeSpan time && key.Length <= Sanitizer.Options.MaxKeyBytes)
            {
                key = ScopedLogConventions.TimeSpanKey(key);
                value = time.TotalSeconds;
            }
            var node = new DiagnosticCaptureNode(key, sensitive);
            var capture = _native.Capture(node, value, deferRedaction: true);
            owner.StoreCapture(Sanitizer.SanitizeKey(key), capture);
        }
        catch (Exception)
        {
            _native?.RecordGetterFailure();
            const string unavailable = "[diagnostic getter unavailable]";
            if (_native == null) owner.Store(Sanitizer.SanitizeKey(originalKey), unavailable);
            else owner.StoreCapture(Sanitizer.SanitizeKey(originalKey), _native.Capture(
                new DiagnosticCaptureNode(originalKey, sensitive), unavailable, deferRedaction: true));
        }
    }
}
