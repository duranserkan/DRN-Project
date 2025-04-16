using System.Reflection;

namespace DRN.Framework.Utils.Extensions;

public static class BindingFlag
{
    public const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    public const BindingFlags StaticPublic = BindingFlags.Static | BindingFlags.Public;
    public const BindingFlags StaticNonPublic = BindingFlags.Static | BindingFlags.NonPublic;
    public const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    public const BindingFlags InstancePublic = BindingFlags.Instance | BindingFlags.Public;
    public const BindingFlags InstanceNonPublic = BindingFlags.Instance | BindingFlags.NonPublic;
}