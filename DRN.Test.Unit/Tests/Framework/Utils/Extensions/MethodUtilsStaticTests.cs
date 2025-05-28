using DRN.Framework.Utils.Extensions;
using AwesomeAssertions;
using Xunit;

namespace DRN.Test.Unit.Tests.Framework.Utils.Extensions;

public class MethodUtilsStaticTests
{
    private static readonly Type Type = typeof(MethodUtilsStaticTests);
    private const string StaticMethodName = nameof(GetStatic);

    [Fact]
    public void FindNonGenericMethod_Should_Find_Method()
    {
        var method = Type.FindNonGenericMethod(StaticMethodName, 0, BindingFlag.StaticPublic);
        method.Should().NotBeNull();
        method.Name.Should().Be(StaticMethodName);
        method.IsGenericMethod.Should().BeFalse();
    }

    [Fact]
    public void FindNonGenericMethodUncached_Should_Find_Method()
    {
        var method = Type.FindNonGenericMethodUncached(StaticMethodName, 0, BindingFlag.StaticPublic);
        method.Should().NotBeNull();
        method.Name.Should().Be(StaticMethodName);
        method.IsGenericMethod.Should().BeFalse();
    }

    [Fact]
    public void FindGenericMethod_Should_Find_Method()
    {
        var method = Type.FindGenericMethod(StaticMethodName, [Type], 0, BindingFlag.StaticPublic);
        method.Should().NotBeNull();
        method.Name.Should().Be(StaticMethodName);
        method.IsGenericMethod.Should().BeTrue();
    }

    [Fact]
    public void FindGenericMethodUncached_Should_Find_Method()
    {
        var method = Type.FindGenericMethodUncached(StaticMethodName, [Type], 0, BindingFlag.StaticPublic);
        method.Should().NotBeNull();
        method.Name.Should().Be(StaticMethodName);
        method.IsGenericMethod.Should().BeTrue();
    }

    [Fact]
    public void MethodUtils_Should_Invoke_Static_Method()
    {
        var value = Type.InvokeStaticMethod(StaticMethodName);
        value.Should().Be(2);
    }

    [Fact]
    public void MethodUtils_Should_Invoke_Static_Method_With_Parameter()
    {
        var value = Type.InvokeStaticMethod(StaticMethodName, 9);
        value.Should().Be(9);
    }

    [Fact]
    public void MethodUtils_Should_Invoke_Static_Generic_Method()
    {
        var value = Type.InvokeStaticGenericMethod(StaticMethodName, Type);
        value.Should().Be(3);
    }

    [Fact]
    public void MethodUtils_Should_Invoke_Static_Generic_Method_With_Parameter()
    {
        var value = Type.InvokeStaticGenericMethod(StaticMethodName, [Type], 12);
        value.Should().Be(12);
    }

    public static object? GetStatic() => 2;
    public static object? GetStatic(int a) => a;
    public static object? GetStatic<T>() => 3;
    public static object? GetStatic<T>(int b) => b;
}