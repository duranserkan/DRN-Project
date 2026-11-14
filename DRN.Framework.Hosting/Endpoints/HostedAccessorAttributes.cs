namespace DRN.Framework.Hosting.Endpoints;

/// <summary>Opts a concrete hosted program into compiler-generated accessor collections.</summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class HostedAccessorsAttribute(Type program) : Attribute
{
    public Type Program { get; } = program;
    public string? Namespace { get; set; }
    public string? Name { get; set; }
    public bool Pages { get; set; }
}

/// <summary>Includes, aliases or excludes a controller for one opted-in program.</summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class AccessorControllerAttribute(Type program, Type controller, string path) : Attribute
{
    public Type Program { get; } = program;
    public Type Controller { get; } = controller;
    public string Path { get; } = path;
    public string? TypeName { get; set; }
    public string RouteTemplate { get; set; } = "";
    public bool Exclude { get; set; }
}

/// <summary>Preserves a host-owned API group type name and its partial customization.</summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class AccessorGroupAttribute(Type program, string path, string typeName) : Attribute
{
    public Type Program { get; } = program;
    public string Path { get; } = path;
    public string TypeName { get; } = typeName;
}

/// <summary>Aliases or excludes a generated action or page by its accessor path.</summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class AccessorMemberAttribute(Type program, string path) : Attribute
{
    public Type Program { get; } = program;
    public string Path { get; } = path;
    public string? Name { get; set; }
    public bool Exclude { get; set; }
}

/// <summary>Binds an accessor property to a CLR action signature independently of its member name.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class EndpointActionAttribute(string methodName, params Type[] parameterTypes) : Attribute
{
    public string MethodName { get; } = methodName;
    public Type[] ParameterTypes { get; } = parameterTypes;
}
