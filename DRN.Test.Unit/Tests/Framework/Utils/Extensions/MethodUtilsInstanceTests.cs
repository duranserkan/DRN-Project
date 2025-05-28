using DRN.Framework.Utils.Extensions;
using AwesomeAssertions;
using Xunit;

namespace DRN.Test.Unit.Tests.Framework.Utils.Extensions;

public class MethodUtilsInstanceTests
{
    private static readonly Type Type = typeof(MethodUtilsInstanceTests);
    private const string InstanceMethodName = nameof(GetInstance);

    [Fact]
    public void FindNonGenericMethod_Should_Find_Method()
    {
        var method = Type.FindNonGenericMethod(InstanceMethodName, 0, BindingFlag.Instance);
        method.Should().NotBeNull();
        method.Name.Should().Be(InstanceMethodName);
        method.IsGenericMethod.Should().BeFalse();
    }

    [Fact]
    public void FindNonGenericMethodUncached_Should_Find_Method()
    {
        var method = Type.FindNonGenericMethodUncached(InstanceMethodName, 0, BindingFlag.Instance);
        method.Should().NotBeNull();
        method.Name.Should().Be(InstanceMethodName);
        method.IsGenericMethod.Should().BeFalse();
    }

    [Fact]
    public void FindGenericMethod_Should_Find_Method()
    {
        var method = Type.FindGenericMethod(InstanceMethodName, [Type], 0, BindingFlag.Instance);
        method.Should().NotBeNull();
        method.Name.Should().Be(InstanceMethodName);
        method.IsGenericMethod.Should().BeTrue();
    }

    [Fact]
    public void FindGenericMethodUncached_Should_Find_Method()
    {
        var method = Type.FindGenericMethodUncached(InstanceMethodName, [Type], 0, BindingFlag.Instance);
        method.Should().NotBeNull();
        method.Name.Should().Be(InstanceMethodName);
        method.IsGenericMethod.Should().BeTrue();
    }

    [Fact]
    public void MethodUtils_Should_Invoke_Static_Method()
    {
        var value = this.InvokeMethod(InstanceMethodName);
        value.Should().Be(2);
    }

    [Fact]
    public void MethodUtils_Should_Invoke_Static_Method_With_Parameter()
    {
        var value = this.InvokeMethod(InstanceMethodName, 9);
        value.Should().Be(9);
    }

    [Fact]
    public void MethodUtils_Should_Invoke_Static_Generic_Method()
    {
        var value = this.InvokeGenericMethod(InstanceMethodName, Type);
        value.Should().Be(3);
    }

    [Fact]
    public void MethodUtils_Should_Invoke_Static_Generic_Method_With_Parameter()
    {
        var value = this.InvokeGenericMethod(InstanceMethodName, [Type], 12);
        value.Should().Be(12);
    }

    public object GetInstance() => 2;
    public object GetInstance(int a) => a;
    public object GetInstance<T>() => 3;
    public object GetInstance<T>(int b) => b;
}