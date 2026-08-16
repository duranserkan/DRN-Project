namespace DRN.Framework.SharedKernel.Domain;

/// <summary>
/// Defines application identifier metadata for entity types.
/// </summary>
public interface IAppId
{
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
    public static byte AppId => 0;
}

/// <summary>
/// Built-in Nexus service partition (AppId = 126).
/// </summary>
public readonly struct NexusApp : IAppId
{
    public static byte AppId => 126;
}

/// <summary>
/// Built-in test application partition (AppId = 127) for test entities.
/// </summary>
public readonly struct TestApp : IAppId
{
    public static byte AppId => 127;
}

/// <summary>
/// Convenience EntityType attribute bound to <see cref="TestApp"/> (AppId = 127).
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public class TestEntityTypeAttribute(byte entityType) : EntityTypeAttribute<TestApp>(entityType);
