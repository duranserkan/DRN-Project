using DRN.Framework.Hosting.DrnProgram;
using DRN.Framework.Utils.Logging;
using DRN.Test.Utils.Hosting;

namespace DRN.Test.Unit.Tests.Framework.Hosting.DrnProgram;

public class DrnProgramActionsTests
{
    [Fact]
    public void Discovery_Should_Create_The_Only_Valid_Implementation()
    {
        var assembly = AssemblyWith(typeof(DrnProgramActions), typeof(string), typeof(ValidActions));

        assembly.CreateSubType<DrnProgramActions>().Should().BeOfType<ValidActions>();
    }

    [Fact]
    public void Discovery_Should_Return_Null_When_No_Implementation_Exists()
    {
        AssemblyWith(typeof(DrnProgramActions), typeof(string)).CreateSubType<DrnProgramActions>().Should().BeNull();
        typeof(DrnProgramActions).Assembly.CreateSubType<DrnProgramActions>().Should().BeNull();
        ((Assembly?)null).CreateSubType<DrnProgramActions>().Should().BeNull();
    }

    [Theory]
    [DataInlineUnit(typeof(AbstractActions))]
    [DataInlineUnit(typeof(OpenGenericActions<>))]
    [DataInlineUnit(typeof(PrivateConstructorActions))]
    [DataInlineUnit(typeof(ProtectedConstructorActions))]
    [DataInlineUnit(typeof(ParameterizedActions))]
    [DataInlineUnit(typeof(ThrowingConstructorActions))]
    public void Discovery_Should_Skip_Unconstructible_Types(Type invalidType)
    {
        AssemblyWith(invalidType).CreateSubType<DrnProgramActions>().Should().BeNull();
        AssemblyWith(invalidType, typeof(ValidActions)).CreateSubType<DrnProgramActions>()
            .Should().BeOfType<ValidActions>();
    }

    [Fact]
    public void Discovery_Should_Reject_Multiple_Constructible_Implementations()
    {
        var assembly = AssemblyWith(typeof(ValidActions), typeof(OtherValidActions), typeof(ParameterizedActions));

        var discover = () => assembly.CreateSubType<DrnProgramActions>();

        discover.Should().ThrowExactly<InvalidOperationException>();
    }

    [Fact]
    public void Discovery_Should_Propagate_Assembly_Enumeration_Failures()
    {
        var assembly = Substitute.For<Assembly>();
        var failure = new ReflectionTypeLoadException([], []);
        assembly.GetTypes().Returns(_ => throw failure);

        var discover = () => assembly.CreateSubType<DrnProgramActions>();

        discover.Should().ThrowExactly<ReflectionTypeLoadException>().Which.Should().BeSameAs(failure);
    }

    [Fact]
    public async Task Default_Hooks_Should_Complete_Without_Customization()
    {
        var actions = new ValidActions();
        var program = new TemporaryLifecycleProgram();
        using var settings = SettingsProvider.Development();
        var log = Substitute.For<IScopedLog>();

        await actions.ApplicationBuilderCreatedAsync(program, null!, settings, log);
        await actions.ApplicationBuiltAsync(program, null!, settings, log);
        await actions.ApplicationValidatedAsync(program, null!, settings, log);
    }

    private static Assembly AssemblyWith(params Type[] types)
    {
        var assembly = Substitute.For<Assembly>();
        assembly.GetTypes().Returns(types);
        return assembly;
    }

    public sealed class ValidActions : DrnProgramActions;
    public sealed class OtherValidActions : DrnProgramActions;
    public abstract class AbstractActions : DrnProgramActions;
    public sealed class OpenGenericActions<T> : DrnProgramActions;
    public sealed class ParameterizedActions(string value) : DrnProgramActions
    {
        public string Value { get; } = value;
    }

    public sealed class PrivateConstructorActions : DrnProgramActions
    {
        private PrivateConstructorActions() { }
    }

    public class ProtectedConstructorActions : DrnProgramActions
    {
        protected ProtectedConstructorActions() { }
    }

    public sealed class ThrowingConstructorActions : DrnProgramActions
    {
        public ThrowingConstructorActions() => throw new InvalidOperationException("Constructor failure");
    }
}
