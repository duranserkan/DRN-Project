namespace DRN.Test.Unit.Tests.Framework.Utils.Extensions;

public class ObjectExtensionsTests
{
    private interface ITestSubtype;

    private class MySubtypeProperty : ITestSubtype;

    private class Level1
    {
        public Level2 A { get; set; } = new();
        public Level2 B { get; set; } = new();
    }

    private class Level2
    {
        public Level3 A { get; set; } = new();
        public Level3 B { get; set; } = new();
    }

    private class Level3
    {
        public MySubtypeProperty Target { get; set; } = new();
    }

    [Theory]
    [DataInlineUnit(0)]
    [DataInlineUnit(-1)]
    [DataInlineUnit(-5)]
    public void GetGroupedPropertiesOfSubtype_WithInvalidMaxRecursionLevel_ShouldThrowArgumentOutOfRangeException(int invalidLimit)
    {
        var root = new Level1();
        var act = () => root.GetGroupedPropertiesOfSubtype(typeof(ITestSubtype), invalidLimit);
        
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("maxRecursionLevel");
    }

    [Fact]
    public void GetGroupedPropertiesOfSubtype_ShouldRespectMaxRecursionLevelAndCorrectlyIncrementDepth()
    {
        var root = new Level1();

        // Act & Assert 1: At maxRecursionLevel = 2, Level3 properties (depth 2) should NOT be reached or returned
        var resultDepth2 = root.GetGroupedPropertiesOfSubtype(typeof(ITestSubtype), maxRecursionLevel: 2);
        resultDepth2.Should().BeEmpty();

        // Act & Assert 2: At maxRecursionLevel = 3, Level3 properties (depth 2) should be reached and returned
        var resultDepth3 = root.GetGroupedPropertiesOfSubtype(typeof(ITestSubtype), maxRecursionLevel: 3);
        resultDepth3.Should().NotBeEmpty();

        // We expect four Level3 instances in the result keys because:
        // Level1 (1) -> Level2 (2: A and B) -> Level3 (4: A.A, A.B, B.A, B.B)
        resultDepth3.Keys.Count(k => k is Level3).Should().Be(4);

        foreach (var key in resultDepth3.Keys)
        {
            if (key is not Level3 level3Instance)
                continue;

            var properties = resultDepth3[level3Instance];
            properties.Should().ContainSingle();
            properties.Single().Name.Should().Be(nameof(Level3.Target));
        }
    }
}