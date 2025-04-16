using System.Collections.Concurrent;
using System.Reflection;
using DRN.Framework.Utils.Extensions;

namespace DRN.Framework.Utils.Generic;

public static class MethodUtils
{
    private static readonly ConcurrentDictionary<CacheKey, MethodInfo> InstanceMethodCache = new();
    private static readonly ConcurrentDictionary<CacheKey, MethodInfo> GenericMethodCache = new();

    public static object? InvokeMethod(this object instance, string methodName, params object[] parameters)
    {
        var type = instance.GetType();
        var methodInfo = type.FindNonGenericMethod(methodName, parameters.Length, BindingFlags.Instance);
        return methodInfo.Invoke(instance, parameters);
    }

    public static object? InvokeStaticMethod(this Type type, string methodName, params object[] parameters) => 
        type.FindNonGenericMethod(methodName, parameters.Length, BindingFlags.Static).Invoke(null, parameters);

    public static object? InvokeGenericMethod(this object instance, string methodName, params Type[] typeArguments) =>
        InvokeGenericMethod(instance, methodName, typeArguments);

    public static object? InvokeGenericMethod(this object instance, string methodName, Type[] typeArguments, params object[] parameters) =>
        instance.GetType().FindGenericMethod(methodName, typeArguments, parameters, BindingFlag.Instance).MethodInfo.Invoke(instance, parameters);

    public static object? InvokeStaticGenericMethod(this Type type, string methodName, params Type[] typeArguments) =>
        InvokeStaticGenericMethod(type, methodName, typeArguments);

    public static object? InvokeStaticGenericMethod(this Type type, string methodName, Type[] typeArguments, params object[] parameters) =>
        type.FindGenericMethod(methodName, typeArguments, parameters, BindingFlag.Static).MethodInfo.Invoke(null, parameters);

    public static MethodResult FindGenericMethod(this Type type, string methodName, Type[] typeArguments, object[] parameters, BindingFlags bindingFlags)
    {
        var cacheKey = new CacheKey(type, methodName, typeArguments.Length, parameters.Length, bindingFlags);
        var methodInfo = GenericMethodCache.GetOrAdd(cacheKey, key =>
        {
            var methods = key.Type.GetMethods(key.BindingFlags)
                .Where(m => m.Name == key.MethodName &&
                            m.IsGenericMethodDefinition &&
                            m.GetGenericArguments().Length == key.TypeArgCount &&
                            m.GetParameters().Length == key.ParameterCount).ToArray();

            if (methods.Length == 1)
                return methods[0].MakeGenericMethod(typeArguments);

            if (methods.Length == 0)
                throw new ArgumentException($"Generic method '{key.MethodName}' not found with specified criteria");

            throw new ArgumentException($"{methods.Length} generic method '{key.MethodName}' found with specified criteria");
        });

        return new MethodResult(methodInfo.MakeGenericMethod(typeArguments));
    }

    public static MethodInfo FindNonGenericMethod(this Type type, string methodName, int parameterCount, BindingFlags bindingFlags)
    {
        var cacheKey = new CacheKey(type, methodName, 0, parameterCount, bindingFlags);
        var methodInfo = InstanceMethodCache.GetOrAdd(cacheKey, key =>
        {
            var methods = key.Type.GetMethods(key.BindingFlags)
                .Where(m => m.Name == key.MethodName &&
                            !m.IsGenericMethod &&
                            m.GetParameters().Length == key.ParameterCount)
                .ToArray();

            if (methods.Length == 1)
                return methods[0];

            if (methods.Length == 0)
                throw new ArgumentException($"Non-generic method '{key.MethodName}' not found with specified criteria");

            throw new ArgumentException($"{methods.Length} non-generic methods '{key.MethodName}' found with specified criteria");
        });

        return methodInfo;
    }
}

public readonly record struct CacheKey(Type Type, string MethodName, int TypeArgCount, int ParameterCount, BindingFlags BindingFlags);

public readonly record struct MethodResult(MethodInfo MethodInfo)
{
    public MethodInfo MethodInfo { get; } = MethodInfo;
    public bool IsVoid { get; } = MethodInfo.ReturnType == typeof(void);
}