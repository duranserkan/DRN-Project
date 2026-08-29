using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using DRN.Framework.Utils.Models;

namespace DRN.Framework.Utils.Extensions;

public static class MethodUtils
{
    private static readonly ConcurrentDictionary<GenericCacheKey, MethodInfo> GenericMethodCache = new();
    private static readonly ConcurrentDictionary<NonGenericCacheKey, MethodInfo> NonGenericMethodCache = new();
    private static readonly ConcurrentDictionary<MethodInfo, Func<object?, object?[], object?>> FastInvokerCache = new();

    public static object? InvokeMethod(this object instance, string methodName, params object[] parameters)
    {
        var type = instance.GetType();
        var methodInfo = type.FindNonGenericMethod(methodName, parameters.Length, BindingFlag.Instance);
        return methodInfo.Invoke(instance, parameters);
    }

    public static object? InvokeMethodFast(this object instance, string methodName, params object?[] parameters)
    {
        var type = instance.GetType();
        var methodInfo = type.FindNonGenericMethod(methodName, parameters.Length, BindingFlag.Instance);
        return methodInfo.InvokeFast(instance, parameters);
    }

    public static object? InvokeStaticMethod(this Type type, string methodName, params object[] parameters) =>
        type.FindNonGenericMethod(methodName, parameters.Length, BindingFlag.Static).Invoke(null, parameters);

    public static object? InvokeStaticMethodFast(this Type type, string methodName, params object?[] parameters) =>
        type.FindNonGenericMethod(methodName, parameters.Length, BindingFlag.Static).InvokeFast(null, parameters);

    public static object? InvokeGenericMethod(this object instance, string methodName, params Type[] typeArguments) =>
        InvokeGenericMethod(instance, methodName, typeArguments, []);

    public static object? InvokeGenericMethod(this object instance, string methodName, Type[] typeArguments, params object[] parameters) =>
        instance.GetType().FindGenericMethod(methodName, typeArguments, parameters.Length, BindingFlag.Instance).Invoke(instance, parameters);

    public static object? InvokeGenericMethodFast(this object instance, string methodName, params Type[] typeArguments) =>
        InvokeGenericMethodFast(instance, methodName, typeArguments, []);

    public static object? InvokeGenericMethodFast(this object instance, string methodName, Type[] typeArguments, params object?[] parameters) =>
        instance.GetType().FindGenericMethod(methodName, typeArguments, parameters.Length, BindingFlag.Instance).InvokeFast(instance, parameters);

    public static object? InvokeStaticGenericMethod(this Type type, string methodName, params Type[] typeArguments) =>
        InvokeStaticGenericMethod(type, methodName, typeArguments, []);

    public static object? InvokeStaticGenericMethod(this Type type, string methodName, Type[] typeArguments, params object[] parameters) =>
        type.FindGenericMethod(methodName, typeArguments, parameters.Length, BindingFlag.Static).Invoke(null, parameters);

    public static object? InvokeStaticGenericMethodFast(this Type type, string methodName, params Type[] typeArguments) =>
        InvokeStaticGenericMethodFast(type, methodName, typeArguments, []);

    public static object? InvokeStaticGenericMethodFast(this Type type, string methodName, Type[] typeArguments, params object?[] parameters) =>
        type.FindGenericMethod(methodName, typeArguments, parameters.Length, BindingFlag.Static).InvokeFast(null, parameters);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static object? InvokeFast(this MethodInfo method, object? instance, params object?[] parameters)
    {
        var invoker = FastInvokerCache.GetOrAdd(method, static m => CompileMethodInvoker(m));
        return invoker(instance, parameters);
    }

    private static Func<object?, object?[], object?> CompileMethodInvoker(MethodInfo method)
    {
        var instanceParam = Expression.Parameter(typeof(object), "instance");
        var argsParam = Expression.Parameter(typeof(object[]), "args");

        var methodParams = method.GetParameters();
        var callArgs = new Expression[methodParams.Length];
        for (var i = 0; i < methodParams.Length; i++)
        {
            var index = Expression.Constant(i);
            var arrayAccess = Expression.ArrayIndex(argsParam, index);
            callArgs[i] = Expression.Convert(arrayAccess, methodParams[i].ParameterType);
        }

        var instanceCast = method.IsStatic ? null : Expression.Convert(instanceParam, method.DeclaringType ?? typeof(object));
        var call = Expression.Call(instanceCast, method, callArgs);
        Expression body = method.ReturnType == typeof(void)
            ? Expression.Block(call, Expression.Constant(null, typeof(object)))
            : Expression.Convert(call, typeof(object));

        return Expression.Lambda<Func<object?, object?[], object?>>(body, instanceParam, argsParam).Compile();
    }

    public static MethodInfo FindGenericMethod(this Type type, string methodName, Type[] typeArguments, int parameterCount, BindingFlags bindingFlags)
    {
        var cacheKey = new GenericCacheKey(type, methodName, new EquatableSequence<Type>(typeArguments), parameterCount, bindingFlags);
        var methodInfo = GenericMethodCache.GetOrAdd(cacheKey, FindGenericMethodUncached);

        return methodInfo;
    }

    public static MethodInfo FindNonGenericMethod(this Type type, string methodName, int parameterCount, BindingFlags bindingFlags)
    {
        var cacheKey = new NonGenericCacheKey(type, methodName, parameterCount, bindingFlags);
        var methodInfo = NonGenericMethodCache.GetOrAdd(cacheKey, FindNonGenericMethodUncached);

        return methodInfo;
    }

    public static MethodInfo FindGenericMethodUncached(this Type type, string methodName, Type[] typeArguments, int parameterCount, BindingFlags bindingFlags)
    {
        var methods = type.GetMethods(bindingFlags)
            .Where(m => m.Name == methodName
                        && m.IsGenericMethodDefinition
                        && m.GetParameters().Length == parameterCount
                        && m.GetGenericArguments().Length == typeArguments.Length).ToArray();

        if (methods.Length == 1)
            return methods[0].MakeGenericMethod(typeArguments);

        if (methods.Length == 0)
            throw new ArgumentException($"Generic method '{methodName}' not found with specified criteria");

        throw new ArgumentException($"{methods.Length} generic method '{methodName}' found with specified criteria");
    }
    
    public static MethodInfo FindNonGenericMethodUncached(this Type type, string methodName, int parameterCount, BindingFlags bindingFlags)
    {
        var methods = type.GetMethods(bindingFlags)
            .Where(m => m.Name == methodName && !m.IsGenericMethod && m.GetParameters().Length == parameterCount).ToArray();

        if (methods.Length == 1)
            return methods[0];

        if (methods.Length == 0)
            throw new ArgumentException($"Non-generic method '{methodName}' not found with specified criteria");

        throw new ArgumentException($"{methods.Length} non-generic methods '{methodName}' found with specified criteria");
    }

    private static MethodInfo FindGenericMethodUncached(GenericCacheKey key) =>
        FindGenericMethodUncached(key.Type, key.MethodName, key.TypeArgs.Items, key.ParameterCount, key.BindingFlags);

    private static MethodInfo FindNonGenericMethodUncached(NonGenericCacheKey key) =>
        FindNonGenericMethodUncached(key.Type, key.MethodName, key.ParameterCount, key.BindingFlags);

    public static bool IsExtensionMethod(this MethodInfo method)
    {
        //The ExtensionAttribute is automatically applied by the compiler when you use this keyword
        //https://learn.microsoft.com/en-us/dotnet/api/system.runtime.compilerservices.extensionattribute
        return method.IsStatic &&
               (method.DeclaringType?.IsSealed ?? false) &&
               method.DeclaringType.IsAbstract && // static classes are abstract and sealed
               method.IsDefined(typeof(ExtensionAttribute), false);
    }
}

public readonly record struct GenericCacheKey(Type Type, string MethodName, EquatableSequence<Type> TypeArgs, int ParameterCount, BindingFlags BindingFlags);

public readonly record struct NonGenericCacheKey(Type Type, string MethodName, int ParameterCount, BindingFlags BindingFlags);