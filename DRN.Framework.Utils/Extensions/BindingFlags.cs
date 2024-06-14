using System.Reflection;

namespace DRN.Framework.Utils.Extensions;

public static class BindingFlag
{
    public static readonly BindingFlags StaticPublic = BindingFlags.Static | BindingFlags.Public;
    public static readonly BindingFlags StaticNonPublic = BindingFlags.Static | BindingFlags.NonPublic;
    public static readonly BindingFlags InstanceNonPublic = BindingFlags.Instance | BindingFlags.NonPublic;
}