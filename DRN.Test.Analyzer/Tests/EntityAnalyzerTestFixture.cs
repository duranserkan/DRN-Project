namespace DRN.Test.Analyzer.Tests;

internal static class EntityAnalyzerTestFixture
{
    internal const string SharedKernelDomainStubs = """
                                                   namespace DRN.Framework.SharedKernel.Domain
                                                   {
                                                       using System;

                                                       public interface IAppId
                                                       {
                                                           public const byte DefaultAppId = 0;
                                                           public const byte NexusAppId = 126;
                                                           public const byte TestAppId = 127;
                                                           public const byte MaxAppId = 127;
                                                           static abstract byte AppId { get; }
                                                       }

                                                       public readonly struct DefaultApp : IAppId
                                                       {
                                                           public const byte Value = IAppId.DefaultAppId;
                                                           public static byte AppId => Value;
                                                       }

                                                       public readonly struct NexusApp : IAppId
                                                       {
                                                           public const byte Value = IAppId.NexusAppId;
                                                           public static byte AppId => Value;
                                                       }

                                                       public readonly struct TestApp : IAppId
                                                       {
                                                           public const byte Value = IAppId.TestAppId;
                                                           public static byte AppId => Value;
                                                       }

                                                       [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
                                                       public class TestEntityTypeAttribute(byte entityType) : EntityTypeAttribute<TestApp>(entityType);

                                                       public enum FixtureKind : byte
                                                       {
                                                           One = 1, Two = 2, Three = 3, Four = 4, Five = 5,
                                                           Seven = 7, Ten = 10, Twenty = 20, Thirty = 30, FortyTwo = 42
                                                       }

                                                       public sealed class FixtureTypeAttribute<TApp>(FixtureKind kind)
                                                           : EntityTypeAttribute<TApp>((byte)kind) where TApp : IAppId;

                                                       [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
                                                       public abstract class EntityTypeAttribute(byte entityType, byte appId) : Attribute
                                                       {
                                                           public byte EntityType { get; } = entityType;
                                                           public byte AppId { get; } = appId;
                                                       }

                                                       [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
                                                       public class EntityTypeAttribute<TApp>(byte entityType)
                                                           : EntityTypeAttribute(entityType, TApp.AppId)
                                                           where TApp : IAppId;

                                                       public abstract class SourceKnownEntity(long id = 0)
                                                       {
                                                           public long Id { get; set; } = id;
                                                           public Guid EntityId => default;
                                                           public SourceKnownEntityId EntityIdSource { get; internal set; }
                                                           public static byte GetEntityType<TEntity>() where TEntity : SourceKnownEntity => default;
                                                           public static byte GetEntityType<TEntity>(TEntity entity) where TEntity : SourceKnownEntity => default;
                                                           public static byte GetEntityType(Type type) => default;
                                                           public static EntityTypeId GetEntityTypeId<TEntity>() where TEntity : SourceKnownEntity => default;
                                                           public static EntityTypeId GetEntityTypeId<TEntity>(TEntity entity) where TEntity : SourceKnownEntity => default;
                                                           public static EntityTypeId GetEntityTypeId(Type type) => default;
                                                           public static byte GetAppId<TEntity>() where TEntity : SourceKnownEntity => default;
                                                           public static byte GetAppId<TEntity>(TEntity entity) where TEntity : SourceKnownEntity => default;
                                                           public static byte GetAppId(Type type) => default;
                                                           public SourceKnownEntityId GetEntityId<TEntity>(Guid id, SourceKnownEntityIdFormat format = default) where TEntity : SourceKnownEntity => default;
                                                           public SourceKnownEntityId? GetEntityId<TEntity>(Guid? id, SourceKnownEntityIdFormat format = default) where TEntity : SourceKnownEntity => default;
                                                           public SourceKnownEntityId GetEntityId<TEntity>(long id) where TEntity : SourceKnownEntity => default;
                                                           public SourceKnownEntityId? GetEntityId<TEntity>(long? id) where TEntity : SourceKnownEntity => default;
                                                           public SourceKnownEntityId ToSecure(SourceKnownEntityId id) => default;
                                                           public SourceKnownEntityId? ToSecure(SourceKnownEntityId? id) => default;
                                                           public SourceKnownEntityId ToPlain(SourceKnownEntityId id) => default;
                                                           public SourceKnownEntityId? ToPlain(SourceKnownEntityId? id) => default;
                                                       }

                                                       public abstract class AggregateRoot(long id = 0) : SourceKnownEntity(id);
                                                       public readonly record struct EntityTypeId(byte EntityType, byte AppId);
                                                       public enum SourceKnownEntityIdFormat { ConfiguredDefault, Secure, Plain, Auto }
                                                       public readonly record struct SourceKnownEntityId
                                                       {
                                                           public void Validate<TEntity>() where TEntity : SourceKnownEntity { }
                                                           public bool HasSameEntityType<TEntity>() where TEntity : SourceKnownEntity => false;
                                                           public bool HasSameEntityTypeId<TEntity>() where TEntity : SourceKnownEntity => false;
                                                       }
                                                       public interface ISourceKnownEntityIdOperations
                                                       {
                                                           SourceKnownEntityId Parse(Guid id, SourceKnownEntityIdFormat format = default);
                                                           SourceKnownEntityId Validate<TEntity>(Guid id, SourceKnownEntityIdFormat format = default) where TEntity : SourceKnownEntity;
                                                           SourceKnownEntityId Validate<TApp>(Guid id, byte entityType, SourceKnownEntityIdFormat format = default) where TApp : IAppId;
                                                       }
                                                       public static class EntityTypeRegistry
                                                       {
                                                           public static EntityTypeId GetEntityTypeId(Type type) => default;
                                                       }
                                                   }
                                                   """;

    private static readonly Lazy<ImmutableArray<MetadataReference>> BaseReferences = new(() =>
        AnalyzerTestCompilation.CoreReferences.Add(AnalyzerTestCompilation.ConsoleReference)
            .Add(CreateSharedKernelReference(SharedKernelDomainStubs)));

    internal static MetadataReference CreateSharedKernelReference(string source)
    {
        var stub = CSharpCompilation.Create("DRN.Framework.SharedKernel",
            [CSharpSyntaxTree.ParseText(source, path: "SharedKernelDomainStubs.cs")], AnalyzerTestCompilation.CoreReferences,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        return AnalyzerTestCompilation.EmitReference(stub);
    }

    internal static ImmutableArray<MetadataReference> GetBaseReferences() => BaseReferences.Value;

    internal static Task<ImmutableArray<Diagnostic>> RunAnalyzerAsync(params string[] sources)
        => RunAnalyzerAsync(sources, assemblyName: "TestAssembly");

    internal static Task<ImmutableArray<Diagnostic>> RunAnalyzerAsync(
        string[] sources,
        string assemblyName,
        Dictionary<string, string>? buildProperties = null) =>
        AnalyzeEntities(CreateEntityCompilation(sources, assemblyName), buildProperties);

    internal static CSharpCompilation CreateEntityCompilation(string[] sources, string assemblyName = "TestAssembly")
    {
        var syntaxTrees = sources
            .Select((s, i) => CSharpSyntaxTree.ParseText(s, path: $"Source{i}.cs"))
            .ToArray();

        return CSharpCompilation.Create(
            assemblyName,
            syntaxTrees,
            GetBaseReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    internal static Task<ImmutableArray<Diagnostic>> RunAnalyzerWithReferenceAsync(
        string referencedSource,
        string consumingSource,
        string consumingAssemblyName = "ConsumingAssembly",
        Dictionary<string, string>? buildProperties = null) =>
        RunAnalyzerWithMultipleReferencesAsync([("ReferencedDomainAssembly", referencedSource)], consumingSource, consumingAssemblyName, buildProperties);

    internal static Task<ImmutableArray<Diagnostic>> RunAnalyzerWithMultipleReferencesAsync(
        (string AssemblyName, string Source)[] referencedSources,
        string consumingSource = "",
        string consumingAssemblyName = "ConsumingAssembly",
        Dictionary<string, string>? buildProperties = null)
    {
        var baseReferences = GetBaseReferences();
        var additionalReferences = new List<MetadataReference>();

        foreach (var (assemblyName, source) in referencedSources)
        {
            var refSyntaxTree = CSharpSyntaxTree.ParseText(source, path: $"{assemblyName}.cs");
            var refCompilation = CSharpCompilation.Create(
                assemblyName,
                [refSyntaxTree],
                baseReferences.Concat(additionalReferences),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            additionalReferences.Add(AnalyzerTestCompilation.EmitReference(refCompilation));
        }

        var consumingTrees = string.IsNullOrWhiteSpace(consumingSource)
            ? []
            : new[] { CSharpSyntaxTree.ParseText(consumingSource, path: "ConsumingSource.cs") };

        var consumingCompilation = CSharpCompilation.Create(
            consumingAssemblyName,
            consumingTrees,
            baseReferences.Concat(additionalReferences),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        return AnalyzeEntities(consumingCompilation, buildProperties);
    }

    internal static Task<ImmutableArray<Diagnostic>> AnalyzeEntities(CSharpCompilation compilation, Dictionary<string, string>? properties) =>
        AnalyzerTestCompilation.Analyze(compilation, new SourceKnownEntityTypeAnalyzer(),
            properties == null ? null : AnalyzerTestCompilation.OptionsWithProperties(properties));
}
