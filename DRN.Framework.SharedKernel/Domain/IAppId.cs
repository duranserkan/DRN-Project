namespace DRN.Framework.SharedKernel.Domain;

/// <summary>
/// Defines application identifier metadata for entity types.
/// </summary>
public interface IAppId
{
    public const byte DefaultAppId = 0;
    public const byte NexusAppId = 126;
    public const byte TestAppId = 127;
    public const byte MaxAppId = 127;

    /// <summary>
    /// Application Identifier (0..127) for domain/application partitioning.
    /// </summary>
    static abstract byte AppId { get; }
}

/// <summary>
/// Built-in default application partition (AppId = 0) for standalone applications or general tests.
/// </summary>
public readonly struct DefaultApp : IAppId
{
    public const byte Value = IAppId.DefaultAppId;
    public static byte AppId => Value;
}

/// <summary>
/// Built-in Nexus service partition (AppId = 126).
/// </summary>
public readonly struct NexusApp : IAppId
{
    public const byte Value = IAppId.NexusAppId;
    public static byte AppId => Value;
}

/// <summary>
/// Built-in test application partition (AppId = 127) for test entities.
/// </summary>
public readonly struct TestApp : IAppId
{
    public const byte Value = IAppId.TestAppId;
    public static byte AppId => Value;
}

/// <summary>
/// Convenience EntityType attribute bound to <see cref="TestApp"/> (AppId = 127).
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public class TestEntityTypeAttribute(byte entityType) : EntityTypeAttribute<TestApp>(entityType);
