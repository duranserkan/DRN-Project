using DRN.Framework.Testing.Extensions;

namespace DRN.Test.Tests.Framework.Testing.DataAttributes;

public class NSubstituteTests
{
    [Theory]
    [DataInline]
    public void Mockable_Should_Be_Replaced_In_Context_ServiceProvider(TestContext context, IMockable mockable, IMockable mockable2, IDisposable disposable)
    {
        mockable.IsSubstitute().Should().BeTrue();
        mockable2.IsSubstitute().Should().BeTrue();
        disposable.IsSubstitute().Should().BeTrue();
        context.MethodContext.SubstitutePairs.Count.Should().Be(3);

        var serviceProvider = context.BuildServiceProvider();

        var expectedMockables=serviceProvider.GetServices<IMockable>().ToArray();
        expectedMockables.Contains(mockable).Should().BeTrue();
        expectedMockables.Contains(mockable2).Should().BeTrue();
        var expectedDisposable = serviceProvider.GetRequiredService<IDisposable>();
        expectedDisposable.Should().Be(disposable);
    }
}