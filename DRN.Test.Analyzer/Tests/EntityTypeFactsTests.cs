namespace DRN.Test.Analyzer.Tests;

public sealed class EntityTypeFactsTests
{
    [Fact]
    public void EntityFacts_Preserve_Base_Inclusion_And_Constructed_Inheritance()
    {
        var compilation = CreateCompilation("""
            public class EntityBase { }
            public class Intermediate<T> : EntityBase { }
            public class Entity : Intermediate<int> { }
            public class OtherBase { }
            public class Other : OtherBase { }
            """);
        var entityBase = compilation.GetTypeByMetadataName("EntityBase")!;
        var facts = new EntityAnalyzerHelper.EntityTypeFacts(entityBase);
        var entity = compilation.GetTypeByMetadataName("Entity")!;
        var other = compilation.GetTypeByMetadataName("Other")!;

        facts.IsDerived(entityBase, CancellationToken.None).Should().BeFalse();
        facts.IsEntity(entityBase, CancellationToken.None).Should().BeTrue();
        facts.IsDerived(entity.BaseType!, CancellationToken.None).Should().BeTrue();
        facts.IsDerived(entity, CancellationToken.None).Should().BeTrue();
        facts.IsEntity(entity, CancellationToken.None).Should().BeTrue();
        facts.IsDerived(other, CancellationToken.None).Should().BeFalse();
        facts.IsEntity(other, CancellationToken.None).Should().BeFalse();

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var canceledLookup = () => facts.IsDerived(entity, cancellation.Token);
        canceledLookup.Should().Throw<OperationCanceledException>();
        facts.IsDerived(entity, CancellationToken.None).Should().BeTrue();
    }

    [Fact]
    public void EntityFacts_Reflect_Changed_Inheritance_In_A_New_Compilation()
    {
        const string source = """
            public class EntityBase { }
            public class Intermediate : EntityBase { }
            public class Other { }
            public class Entity : Intermediate { }
            """;
        var original = CreateCompilation(source);
        var originalFacts = new EntityAnalyzerHelper.EntityTypeFacts(original.GetTypeByMetadataName("EntityBase")!);
        var originalEntity = original.GetTypeByMetadataName("Entity")!;
        originalFacts.IsDerived(originalEntity, CancellationToken.None).Should().BeTrue();

        var changed = original.ReplaceSyntaxTree(original.SyntaxTrees.Single(),
            CSharpSyntaxTree.ParseText(source.Replace("Entity : Intermediate", "Entity : Other", StringComparison.Ordinal)));
        changed.GetDiagnostics().Should().NotContain(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        var changedFacts = new EntityAnalyzerHelper.EntityTypeFacts(changed.GetTypeByMetadataName("EntityBase")!);
        var changedEntity = changed.GetTypeByMetadataName("Entity")!;
        changedFacts.IsDerived(changedEntity, CancellationToken.None).Should().BeFalse();
        changedFacts.IsEntity(changedEntity, CancellationToken.None).Should().BeFalse();
        originalFacts.IsDerived(originalEntity, CancellationToken.None).Should().BeTrue();
    }

    private static CSharpCompilation CreateCompilation(string source)
    {
        var compilation = CSharpCompilation.Create("EntityFacts", [CSharpSyntaxTree.ParseText(source)],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        compilation.GetDiagnostics().Should().NotContain(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        return compilation;
    }
}
