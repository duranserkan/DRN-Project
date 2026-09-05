namespace DRN.Test.Unit.Tests.Framework.Utils.Extensions;

public class MethodUtilsInstanceTests
{
    private static readonly Type Type = typeof(MethodUtilsInstanceTests);
    private const string InstanceMethodName = nameof(GetInstance);

    private const string TypeMethodName = nameof(GetWithType);

    [Theory]
    [DataInlineUnit(false, false)]
    [DataInlineUnit(true, false)]
    [DataInlineUnit(false, true)]
    [DataInlineUnit(true, true)]
    public void Discovery_Should_Find_Instance_Method(bool uncached, bool generic)
    {
        var method = (uncached, generic) switch
        {
            (false, false) => Type.FindMethod(InstanceMethodName, 0, BindingFlag.Instance),
            (true, false) => Type.FindMethodUncached(InstanceMethodName, 0, BindingFlag.Instance),
            (false, true) => Type.FindMethod(InstanceMethodName, [Type], 0, BindingFlag.Instance),
            (true, true) => Type.FindMethodUncached(InstanceMethodName, [Type], 0, BindingFlag.Instance)
        };
        method.Should().NotBeNull();
        method.Name.Should().Be(InstanceMethodName);
        method.IsGenericMethod.Should().Be(generic);
    }

    [Fact]
    public void MethodUtils_Should_Invoke_Instance_Method_Parameterless()
    {
        var value = this.InvokeMethod(InstanceMethodName);
        value.Should().Be(2);
    }

    [Fact]
    public void MethodUtils_Should_Invoke_Instance_Method_With_1_Parameter()
    {
        var value = this.InvokeMethod(InstanceMethodName, 9);
        value.Should().Be(9);
    }

    [Fact]
    public void MethodUtils_Should_Invoke_Instance_Method_With_2_Parameters()
    {
        var value = this.InvokeMethod(InstanceMethodName, 4, 5);
        value.Should().Be(9);
    }

    [Fact]
    public void MethodUtils_Should_Invoke_Instance_Method_With_3_Parameters()
    {
        var value = this.InvokeMethod(InstanceMethodName, 1, 2, 3);
        value.Should().Be(6);
    }

    [Fact]
    public void MethodUtils_Should_Invoke_Instance_Generic_Method_Parameterless()
    {
        var value = this.InvokeMethod(InstanceMethodName, [Type]);
        value.Should().Be(3);
    }

    [Fact]
    public void MethodUtils_Should_Invoke_Instance_Generic_Method_With_1_Parameter()
    {
        var value = this.InvokeMethod(InstanceMethodName, [Type], 12);
        value.Should().Be(12);
    }

    [Fact]
    public void MethodUtils_Should_Invoke_Instance_NonGeneric_Method_With_Type_Parameter()
    {
        var value = this.InvokeMethod(TypeMethodName, Type);
        value.Should().Be(10);
    }

    public object GetInstance() => 2;
    public object GetInstance(int a) => a;
    public object GetInstance(int a, int b) => a + b;
    public object GetInstance(int a, int b, int c) => a + b + c;
    public object GetWithType(Type t) => 10;
    public object GetInstance<T>() => 3;
    public object GetInstance<T>(int b) => b;
}
