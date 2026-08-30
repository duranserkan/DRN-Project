using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using DRN.Framework.Utils.Models;

namespace DRN.Framework.Utils.Extensions;

public static class MethodUtils
{
    private static readonly ConcurrentDictionary<MethodCacheKey, MethodInfo> MethodCache = new();
    private static readonly ConcurrentDictionary<MethodInfo, MethodInvoker> InvokerCache = new();

    // ==========================================
    // 1. High-Level Standardized Invocation APIs
    // ==========================================

    // --- Instance Invocations ---

    public static object? InvokeMethod(this object instance, string methodName) =>
        instance.GetType().FindMethod(methodName, 0, BindingFlag.Instance).InvokeFast(instance);

    public static object? InvokeMethod(this object instance, string methodName, object? arg1) =>
        instance.GetType().FindMethod(methodName, 1, BindingFlag.Instance).InvokeFast(instance, arg1);

    public static object? InvokeMethod(this object instance, string methodName, object? arg1, object? arg2) =>
        instance.GetType().FindMethod(methodName, 2, BindingFlag.Instance).InvokeFast(instance, arg1, arg2);

    public static object? InvokeMethod(this object instance, string methodName, object? arg1, object? arg2, object? arg3) =>
        instance.GetType().FindMethod(methodName, 3, BindingFlag.Instance).InvokeFast(instance, arg1, arg2, arg3);

    public static object? InvokeMethod(this object instance, string methodName, Span<object?> arguments) =>
        instance.GetType().FindMethod(methodName, arguments.Length, BindingFlag.Instance).InvokeFast(instance, arguments);

    public static object? InvokeMethod(this object instance, string methodName, params object?[] parameters) =>
        instance.GetType().FindMethod(methodName, parameters.Length, BindingFlag.Instance).InvokeFast(instance, parameters.AsSpan());

    // --- Instance Generic Invocations ---

    public static object? InvokeMethod(this object instance, string methodName, params Type[] typeArguments) =>
        instance.GetType().FindMethod(methodName, typeArguments, 0, BindingFlag.Instance).InvokeFast(instance);

    public static object? InvokeMethod(this object instance, string methodName, Type[] typeArguments, object? arg1) =>
        instance.GetType().FindMethod(methodName, typeArguments, 1, BindingFlag.Instance).InvokeFast(instance, arg1);

    public static object? InvokeMethod(this object instance, string methodName, Type[] typeArguments, object? arg1, object? arg2) =>
        instance.GetType().FindMethod(methodName, typeArguments, 2, BindingFlag.Instance).InvokeFast(instance, arg1, arg2);

    public static object? InvokeMethod(this object instance, string methodName, Type[] typeArguments, object? arg1, object? arg2, object? arg3) =>
        instance.GetType().FindMethod(methodName, typeArguments, 3, BindingFlag.Instance).InvokeFast(instance, arg1, arg2, arg3);

    public static object? InvokeMethod(this object instance, string methodName, Type[] typeArguments, Span<object?> arguments) =>
        instance.GetType().FindMethod(methodName, typeArguments, arguments.Length, BindingFlag.Instance).InvokeFast(instance, arguments);

    public static object? InvokeMethod(this object instance, string methodName, Type[] typeArguments, params object?[] parameters) =>
        instance.GetType().FindMethod(methodName, typeArguments, parameters.Length, BindingFlag.Instance).InvokeFast(instance, parameters.AsSpan());

    // --- Static Invocations ---

    public static object? InvokeStaticMethod(this Type type, string methodName) =>
        type.FindMethod(methodName, 0, BindingFlag.Static).InvokeFast(null);

    public static object? InvokeStaticMethod(this Type type, string methodName, object? arg1) =>
        type.FindMethod(methodName, 1, BindingFlag.Static).InvokeFast(null, arg1);

    public static object? InvokeStaticMethod(this Type type, string methodName, object? arg1, object? arg2) =>
        type.FindMethod(methodName, 2, BindingFlag.Static).InvokeFast(null, arg1, arg2);

    public static object? InvokeStaticMethod(this Type type, string methodName, object? arg1, object? arg2, object? arg3) =>
        type.FindMethod(methodName, 3, BindingFlag.Static).InvokeFast(null, arg1, arg2, arg3);

    public static object? InvokeStaticMethod(this Type type, string methodName, Span<object?> arguments) =>
        type.FindMethod(methodName, arguments.Length, BindingFlag.Static).InvokeFast(null, arguments);

    public static object? InvokeStaticMethod(this Type type, string methodName, params object?[] parameters) =>
        type.FindMethod(methodName, parameters.Length, BindingFlag.Static).InvokeFast(null, parameters.AsSpan());

    // --- Static Generic Invocations ---

    public static object? InvokeStaticMethod(this Type type, string methodName, params Type[] typeArguments) =>
        type.FindMethod(methodName, typeArguments, 0, BindingFlag.Static).InvokeFast(null);

    public static object? InvokeStaticMethod(this Type type, string methodName, Type[] typeArguments, object? arg1) =>
        type.FindMethod(methodName, typeArguments, 1, BindingFlag.Static).InvokeFast(null, arg1);

    public static object? InvokeStaticMethod(this Type type, string methodName, Type[] typeArguments, object? arg1, object? arg2) =>
        type.FindMethod(methodName, typeArguments, 2, BindingFlag.Static).InvokeFast(null, arg1, arg2);

    public static object? InvokeStaticMethod(this Type type, string methodName, Type[] typeArguments, object? arg1, object? arg2, object? arg3) =>
        type.FindMethod(methodName, typeArguments, 3, BindingFlag.Static).InvokeFast(null, arg1, arg2, arg3);

    public static object? InvokeStaticMethod(this Type type, string methodName, Type[] typeArguments, Span<object?> arguments) =>
        type.FindMethod(methodName, typeArguments, arguments.Length, BindingFlag.Static).InvokeFast(null, arguments);

    public static object? InvokeStaticMethod(this Type type, string methodName, Type[] typeArguments, params object?[] parameters) =>
        type.FindMethod(methodName, typeArguments, parameters.Length, BindingFlag.Static).InvokeFast(null, parameters.AsSpan());

    // ==========================================
    // 2. High-Performance Zero-Allocation Invokers
    // ==========================================

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static object? InvokeFast(this MethodInfo method, object? instance = null) =>
        InvokerCache.GetOrAdd(method, static m => MethodInvoker.Create(m)).Invoke(instance);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static object? InvokeFast(this MethodInfo method, object? instance, object? arg1) =>
        InvokerCache.GetOrAdd(method, static m => MethodInvoker.Create(m)).Invoke(instance, arg1);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static object? InvokeFast(this MethodInfo method, object? instance, object? arg1, object? arg2) =>
        InvokerCache.GetOrAdd(method, static m => MethodInvoker.Create(m)).Invoke(instance, arg1, arg2);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static object? InvokeFast(this MethodInfo method, object? instance, object? arg1, object? arg2, object? arg3) =>
        InvokerCache.GetOrAdd(method, static m => MethodInvoker.Create(m)).Invoke(instance, arg1, arg2, arg3);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static object? InvokeFast(this MethodInfo method, object? instance, Span<object?> arguments) =>
        InvokerCache.GetOrAdd(method, static m => MethodInvoker.Create(m)).Invoke(instance, arguments);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static object? InvokeFast(this MethodInfo method, object? instance, params object?[] parameters) =>
        InvokerCache.GetOrAdd(method, static m => MethodInvoker.Create(m)).Invoke(instance, parameters.AsSpan());

    // ==========================================
    // 3. Public Discovery APIs (Cached & Uncached)
    // ==========================================

    public static MethodInfo FindMethod(this Type type, string methodName, int parameterCount, BindingFlags bindingFlags) =>
        MethodCache.GetOrAdd(new MethodCacheKey(type, methodName, parameterCount, bindingFlags), static key => FindMethodUncached(key));

    public static MethodInfo FindMethod(this Type type, string methodName, Type[] typeArguments, int parameterCount, BindingFlags bindingFlags) =>
        MethodCache.GetOrAdd(new MethodCacheKey(type, methodName, parameterCount, bindingFlags, new EquatableSequence<Type>(typeArguments)), static key => FindMethodUncached(key));

    public static MethodInfo FindMethodUncached(this Type type, string methodName, int parameterCount, BindingFlags bindingFlags) =>
        FindMethodUncached(new MethodCacheKey(type, methodName, parameterCount, bindingFlags));

    public static MethodInfo FindMethodUncached(this Type type, string methodName, Type[] typeArguments, int parameterCount, BindingFlags bindingFlags) =>
        FindMethodUncached(new MethodCacheKey(type, methodName, parameterCount, bindingFlags, new EquatableSequence<Type>(typeArguments)));

    internal static MethodInfo FindMethodUncached(MethodCacheKey key)
    {
        MethodInfo? match = null;
        var count = 0;
        var hasTypeArgs = key.TypeArgs.Items is { Length: > 0 };

        foreach (var m in key.Type.GetMethods(key.BindingFlags))
        {
            if (m.Name != key.MethodName || m.GetParameters().Length != key.ParameterCount)
                continue;

            if (hasTypeArgs)
            {
                if (m.IsGenericMethodDefinition && m.GetGenericArguments().Length == key.TypeArgs.Items.Length)
                {
                    match = m;
                    count++;
                }
            }
            else if (!m.IsGenericMethod)
            {
                match = m;
                count++;
            }
        }

        var kind = hasTypeArgs ? "Generic" : "Non-generic";
        return count switch
        {
            1 => hasTypeArgs ? match!.MakeGenericMethod(key.TypeArgs.Items) : match!,
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

internal readonly record struct MethodCacheKey(
    Type Type,
    string MethodName,
    int ParameterCount,
    BindingFlags BindingFlags,
    EquatableSequence<Type> TypeArgs = default);