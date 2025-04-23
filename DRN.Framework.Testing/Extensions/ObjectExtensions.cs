using Castle.DynamicProxy;
using NSubstitute.Core;
using NSubstitute.Proxies.CastleDynamicProxy;

namespace DRN.Framework.Testing.Extensions;

public static class ObjectExtensions
{
    public static bool IsSubstitute(this object obj) => obj is ICallRouterProvider;

    public static IReadOnlyList<SubstitutePair> GetSubstitutePairs(this object[] obj)
    {
        var pairs = obj.Where(o => o.IsSubstitute()).Select(o =>
        {
            var interceptors = o.GetType().GetField("__interceptors", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(o) as IInterceptor[];
            if (interceptors == null) return null;

            var proxyIdInterceptor = interceptors.SingleOrDefault(i => i is ProxyIdInterceptor) as ProxyIdInterceptor;
            if (proxyIdInterceptor == null) return null;

            var proxyIdInterceptorFields = proxyIdInterceptor.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic);
            var interceptedRuntimeType = Array.Find(proxyIdInterceptorFields, fieldInfo =>
                fieldInfo.Name.Contains("primaryProxyType", StringComparison.OrdinalIgnoreCase))?.GetValue(proxyIdInterceptor) as Type;

            if (interceptedRuntimeType == null)
                return null;

            var interceptedType = interceptedRuntimeType.Assembly.GetType(interceptedRuntimeType.FullName!)!;

            return new SubstitutePair(interceptedType, o);
        }).Where(p => p != null).ToArray();

        return pairs!;
    }
}

public record SubstitutePair(Type InterfaceType, object Implementation);