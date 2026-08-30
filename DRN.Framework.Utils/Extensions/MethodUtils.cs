using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using DRN.Framework.Utils.Models;

namespace DRN.Framework.Utils.Extensions;

/// <summary>
/// High-performance reflection discovery and invocation utilities backed by unified <see cref="FrozenDictionary{TKey, TValue}"/> caching
/// and runtime <see cref="MethodInvoker"/> dispatch. Designed for runtime-provided types and methods.
/// </summary>
[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
public static class MethodUtils
{
    private static readonly Lock SyncLock = new();
    private static volatile FrozenDictionary<MethodCacheKey, MethodCacheEntry> _methodCache = FrozenDictionary<MethodCacheKey, MethodCacheEntry>.Empty;

    private readonly record struct MethodCacheEntry(MethodInfo Method, MethodInvoker Invoker);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static MethodCacheEntry GetOrAddEntry(MethodCacheKey key) => _methodCache.TryGetValue(key, out var entry) ? entry : AddSlow(key);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static MethodCacheEntry AddSlow(MethodCacheKey key)
    {
        lock (SyncLock)
        {
            if (_methodCache.TryGetValue(key, out var entry))
                return entry;

            var method = FindMethodUncached(key);
            var invoker = MethodInvoker.Create(method);
            entry = new MethodCacheEntry(method, invoker);

            var map = new Dictionary<MethodCacheKey, MethodCacheEntry>(_methodCache)
            {
                [key] = entry
            };
            _methodCache = map.ToFrozenDictionary();
            return entry;
        }
    }

    // ==========================================
    // 1. High-Level Standardized Invocation APIs
    // ==========================================

    // --- Instance Invocations ---

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static object? InvokeMethod(this object instance, string methodName) =>
        GetOrAddEntry(new(instance.GetType(), methodName, 0, BindingFlag.Instance)).Invoker.Invoke(instance);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static object? InvokeMethod(this object instance, string methodName, object? arg1) =>
        GetOrAddEntry(new(instance.GetType(), methodName, 1, BindingFlag.Instance)).Invoker.Invoke(instance, arg1);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static object? InvokeMethod(this object instance, string methodName, object? arg1, object? arg2) =>
        GetOrAddEntry(new(instance.GetType(), methodName, 2, BindingFlag.Instance)).Invoker.Invoke(instance, arg1, arg2);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static object? InvokeMethod(this object instance, string methodName, object? arg1, object? arg2, object? arg3) =>
        GetOrAddEntry(new(instance.GetType(), methodName, 3, BindingFlag.Instance)).Invoker.Invoke(instance, arg1, arg2, arg3);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static object? InvokeMethod(this object instance, string methodName, Span<object?> arguments) =>
        GetOrAddEntry(new(instance.GetType(), methodName, arguments.Length, BindingFlag.Instance)).Invoker.Invoke(instance, arguments);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static object? InvokeMethod(this object instance, string methodName, params object?[] parameters) =>
        GetOrAddEntry(new(instance.GetType(), methodName, parameters.Length, BindingFlag.Instance)).Invoker.Invoke(instance, parameters.AsSpan());

    // --- Instance Generic Invocations ---

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static object? InvokeMethod(this object instance, string methodName, Type[] typeArguments) =>
        GetOrAddEntry(new(instance.GetType(), methodName, 0, BindingFlag.Instance, new EquatableSequence<Type>(typeArguments))).Invoker.Invoke(instance);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static object? InvokeMethod(this object instance, string methodName, Type[] typeArguments, object? arg1) =>
        GetOrAddEntry(new(instance.GetType(), methodName, 1, BindingFlag.Instance, new EquatableSequence<Type>(typeArguments))).Invoker.Invoke(instance, arg1);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static object? InvokeMethod(this object instance, string methodName, Type[] typeArguments, object? arg1, object? arg2) =>
        GetOrAddEntry(new(instance.GetType(), methodName, 2, BindingFlag.Instance, new EquatableSequence<Type>(typeArguments))).Invoker.Invoke(instance, arg1, arg2);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static object? InvokeMethod(this object instance, string methodName, Type[] typeArguments, object? arg1, object? arg2, object? arg3) =>
        GetOrAddEntry(new(instance.GetType(), methodName, 3, BindingFlag.Instance, new EquatableSequence<Type>(typeArguments))).Invoker.Invoke(instance, arg1, arg2, arg3);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static object? InvokeMethod(this object instance, string methodName, Type[] typeArguments, Span<object?> arguments) =>
        GetOrAddEntry(new(instance.GetType(), methodName, arguments.Length, BindingFlag.Instance, new EquatableSequence<Type>(typeArguments))).Invoker.Invoke(instance, arguments);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static object? InvokeMethod(this object instance, string methodName, Type[] typeArguments, params object?[] parameters) =>
        GetOrAddEntry(new(instance.GetType(), methodName, parameters.Length, BindingFlag.Instance, new EquatableSequence<Type>(typeArguments))).Invoker.Invoke(instance, parameters.AsSpan());

    // --- Static Invocations ---

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static object? InvokeStaticMethod(this Type type, string methodName) =>
        GetOrAddEntry(new(type, methodName, 0, BindingFlag.Static)).Invoker.Invoke(null);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static object? InvokeStaticMethod(this Type type, string methodName, object? arg1) =>
        GetOrAddEntry(new(type, methodName, 1, BindingFlag.Static)).Invoker.Invoke(null, arg1);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static object? InvokeStaticMethod(this Type type, string methodName, object? arg1, object? arg2) =>
        GetOrAddEntry(new(type, methodName, 2, BindingFlag.Static)).Invoker.Invoke(null, arg1, arg2);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static object? InvokeStaticMethod(this Type type, string methodName, object? arg1, object? arg2, object? arg3) =>
        GetOrAddEntry(new(type, methodName, 3, BindingFlag.Static)).Invoker.Invoke(null, arg1, arg2, arg3);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static object? InvokeStaticMethod(this Type type, string methodName, Span<object?> arguments) =>
        GetOrAddEntry(new(type, methodName, arguments.Length, BindingFlag.Static)).Invoker.Invoke(null, arguments);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static object? InvokeStaticMethod(this Type type, string methodName, params object?[] parameters) =>
        GetOrAddEntry(new(type, methodName, parameters.Length, BindingFlag.Static)).Invoker.Invoke(null, parameters.AsSpan());

    // --- Static Generic Invocations ---

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static object? InvokeStaticMethod(this Type type, string methodName, Type[] typeArguments) =>
        GetOrAddEntry(new(type, methodName, 0, BindingFlag.Static, new EquatableSequence<Type>(typeArguments))).Invoker.Invoke(null);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static object? InvokeStaticMethod(this Type type, string methodName, Type[] typeArguments, object? arg1) =>
        GetOrAddEntry(new(type, methodName, 1, BindingFlag.Static, new EquatableSequence<Type>(typeArguments))).Invoker.Invoke(null, arg1);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static object? InvokeStaticMethod(this Type type, string methodName, Type[] typeArguments, object? arg1, object? arg2) =>
        GetOrAddEntry(new(type, methodName, 2, BindingFlag.Static, new EquatableSequence<Type>(typeArguments))).Invoker.Invoke(null, arg1, arg2);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static object? InvokeStaticMethod(this Type type, string methodName, Type[] typeArguments, object? arg1, object? arg2, object? arg3) =>
        GetOrAddEntry(new(type, methodName, 3, BindingFlag.Static, new EquatableSequence<Type>(typeArguments))).Invoker.Invoke(null, arg1, arg2, arg3);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static object? InvokeStaticMethod(this Type type, string methodName, Type[] typeArguments, Span<object?> arguments) =>
        GetOrAddEntry(new(type, methodName, arguments.Length, BindingFlag.Static, new EquatableSequence<Type>(typeArguments))).Invoker.Invoke(null, arguments);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static object? InvokeStaticMethod(this Type type, string methodName, Type[] typeArguments, params object?[] parameters) =>
        GetOrAddEntry(new(type, methodName, parameters.Length, BindingFlag.Static, new EquatableSequence<Type>(typeArguments))).Invoker.Invoke(null, parameters.AsSpan());

    // ==========================================
    // 2. Public Discovery APIs (Cached & Uncached)
    // ==========================================

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MethodInfo FindMethod(this Type type, string methodName, int parameterCount, BindingFlags bindingFlags) =>
        GetOrAddEntry(new MethodCacheKey(type, methodName, parameterCount, bindingFlags)).Method;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MethodInfo FindMethod(this Type type, string methodName, Type[] typeArguments, int parameterCount, BindingFlags bindingFlags) =>
        GetOrAddEntry(new MethodCacheKey(type, methodName, parameterCount, bindingFlags, new EquatableSequence<Type>(typeArguments))).Method;

    public static MethodInfo FindMethodUncached(this Type type, string methodName, int parameterCount, BindingFlags bindingFlags) =>
        FindMethodUncached(new MethodCacheKey(type, methodName, parameterCount, bindingFlags));

    public static MethodInfo FindMethodUncached(this Type type, string methodName, Type[] typeArguments, int parameterCount, BindingFlags bindingFlags) =>
        FindMethodUncached(new MethodCacheKey(type, methodName, parameterCount, bindingFlags, new EquatableSequence<Type>(typeArguments)));

    public static MethodInfo FindMethodUncached(MethodCacheKey key)
    {
        MethodInfo? match = null;
        var count = 0;
        var hasTypeArgs = key.TypeArgs.Count > 0;
        var typeArgsLength = key.TypeArgs.Count;

        foreach (var m in key.Type.GetMethods(key.BindingFlags))
        {
            if (!string.Equals(m.Name, key.MethodName, StringComparison.Ordinal) || m.GetParameters().Length != key.ParameterCount)
                continue;

            var matchesGenerics = hasTypeArgs
                ? m.IsGenericMethodDefinition && m.GetGenericArguments().Length == typeArgsLength
                : !m.IsGenericMethod;

            if (!matchesGenerics)
                continue;

            match = m;
            count++;
        }

        var kind = hasTypeArgs ? "Generic" : "Non-generic";
        return count switch
        {
            1 => hasTypeArgs ? match!.MakeGenericMethod(key.TypeArgs.Items!) : match!,
            0 => throw new ArgumentException($"{kind} method '{key.MethodName}' not found with specified criteria"),
            _ => throw new ArgumentException($"{count} {kind.ToLowerInvariant()} methods '{key.MethodName}' found with specified criteria")
        };
    }

    public static bool IsExtensionMethod(this MethodInfo method) =>
        method.IsStatic &&
        (method.DeclaringType?.IsSealed ?? false) &&
        method.DeclaringType.IsAbstract &&
        method.IsDefined(typeof(ExtensionAttribute), false);
}

public readonly record struct MethodCacheKey(
    Type Type,
    string MethodName,
    int ParameterCount,
    BindingFlags BindingFlags,
    EquatableSequence<Type> TypeArgs = default)
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(MethodCacheKey other) =>
        ReferenceEquals(Type, other.Type) &&
        ParameterCount == other.ParameterCount &&
        BindingFlags == other.BindingFlags &&
        string.Equals(MethodName, other.MethodName, StringComparison.Ordinal) &&
        TypeArgs.Equals(other.TypeArgs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode() =>
        HashCode.Combine(Type, ParameterCount, (int)BindingFlags, MethodName, TypeArgs);
}
