namespace DRN.Test.Unit.Tests.Framework.Utils.Extensions;

public class MethodUtilsStaticTests
{
    private static readonly Type Type = typeof(MethodUtilsStaticTests);
    private const string StaticMethodName = nameof(GetStatic);

    private const string TypeMethodName = nameof(GetWithTypeStatic);

    [Fact]
    public void FindMethod_Should_Find_NonGeneric_Method()
    {
        var method = Type.FindMethod(StaticMethodName, 0, BindingFlag.StaticPublic);
        method.Should().NotBeNull();
        method.Name.Should().Be(StaticMethodName);
        method.IsGenericMethod.Should().BeFalse();
    }

    [Fact]
    public void FindMethodUncached_Should_Find_NonGeneric_Method()
    {
        var method = Type.FindMethodUncached(StaticMethodName, 0, BindingFlag.StaticPublic);
        method.Should().NotBeNull();
        method.Name.Should().Be(StaticMethodName);
        method.IsGenericMethod.Should().BeFalse();
    }

    [Fact]
    public void FindMethod_Should_Find_Generic_Method()
    {
        var method = Type.FindMethod(StaticMethodName, [Type], 0, BindingFlag.StaticPublic);
        method.Should().NotBeNull();
        method.Name.Should().Be(StaticMethodName);
        method.IsGenericMethod.Should().BeTrue();
    }

    [Fact]
    public void FindMethodUncached_Should_Find_Generic_Method()
    {
        var method = Type.FindMethodUncached(StaticMethodName, [Type], 0, BindingFlag.StaticPublic);
        method.Should().NotBeNull();
        method.Name.Should().Be(StaticMethodName);
        method.IsGenericMethod.Should().BeTrue();
    }

    [Fact]
    public void MethodUtils_Should_Invoke_Static_Method_Parameterless()
    {
        var value = Type.InvokeStaticMethod(StaticMethodName);
        value.Should().Be(2);
    }

    [Fact]
    public void MethodUtils_Should_Invoke_Static_Method_With_1_Parameter()
    {
        var value = Type.InvokeStaticMethod(StaticMethodName, 9);
        value.Should().Be(9);
    }

    [Fact]
    public void MethodUtils_Should_Invoke_Static_Method_With_2_Parameters()
    {
        var value = Type.InvokeStaticMethod(StaticMethodName, 4, 5);
        value.Should().Be(9);
    }

    [Fact]
    public void MethodUtils_Should_Invoke_Static_Method_With_3_Parameters()
    {
        var value = Type.InvokeStaticMethod(StaticMethodName, 1, 2, 3);
        value.Should().Be(6);
    }

    [Fact]
    public void MethodUtils_Should_Invoke_Static_Generic_Method_Parameterless()
    {
        var value = Type.InvokeStaticMethod(StaticMethodName, [Type]);
        value.Should().Be(3);
    }

    [Fact]
    public void MethodUtils_Should_Invoke_Static_Generic_Method_With_1_Parameter()
    {
        var value = Type.InvokeStaticMethod(StaticMethodName, [Type], 12);
        value.Should().Be(12);
    }

    [Fact]
    public void MethodUtils_Should_Invoke_Static_NonGeneric_Method_With_Type_Parameter()
    {
        var value = Type.InvokeStaticMethod(TypeMethodName, Type);
        value.Should().Be(10);
    }

    [Fact]
    public void MethodUtils_Validate_ExtensionMethod()
    {
        typeof(ExtensionMethodContainer).GetMethod(nameof(ExtensionMethodContainer.ExtensionMethod))!
            .IsExtensionMethod().Should().BeTrue();
        typeof(ExtensionMethodContainer).GetMethod(nameof(ExtensionMethodContainer.StaticMethod))!
            .IsExtensionMethod().Should().BeFalse();
    }

    public static object? GetStatic() => 2;
    public static object? GetStatic(int a) => a;
    public static object? GetStatic(int a, int b) => a + b;
    public static object? GetStatic(int a, int b, int c) => a + b + c;
    public static object? GetWithTypeStatic(Type t) => 10;
    public static object? GetStatic<T>() => 3;
    public static object? GetStatic<T>(int b) => b;
}

public static class ExtensionMethodContainer
{
    public static void ExtensionMethod(this object obj)
    {
    }

    public static void StaticMethod(object obj)
    {
    }
}