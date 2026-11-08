using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace DRN.Framework.Utils.Extensions;

[SuppressMessage("SonarQube", "S3011", Justification = "These constants describe lookup scopes. Member selection and access occur at their call sites.")]
public static class BindingFlag
{
    public const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    public const BindingFlags StaticPublic = BindingFlags.Static | BindingFlags.Public;
    public const BindingFlags StaticNonPublic = BindingFlags.Static | BindingFlags.NonPublic;
    public const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    public const BindingFlags InstancePublic = BindingFlags.Instance | BindingFlags.Public;
    public const BindingFlags InstanceNonPublic = BindingFlags.Instance | BindingFlags.NonPublic;
}
