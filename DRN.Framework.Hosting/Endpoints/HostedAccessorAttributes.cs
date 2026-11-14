namespace DRN.Framework.Hosting.Endpoints;

/// <summary>Includes, aliases or excludes a controller for one automatically discovered hosted program.</summary>
/// <param name="program">The public, concrete, non-generic, top-level DRN program declared in the SDK Web project.</param>
/// <param name="controller">The MVC controller type to include, customize or exclude.</param>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class AccessorControllerAttribute(Type program, Type controller) : Attribute
{
    /// <summary>Includes or customizes a controller at an explicit accessor path.</summary>
    /// <param name="program">The automatically discovered hosted program whose accessors are customized.</param>
    /// <param name="controller">The MVC controller type to include or customize.</param>
    /// <param name="path">The dot-separated accessor path, including groups and the controller member. This is not a URL route.</param>
    public AccessorControllerAttribute(Type program, Type controller, string path) : this(program, controller)
        => Path = path;

    /// <summary>Gets the hosted program to which this mapping applies.</summary>
    public Type Program { get; } = program;

    /// <summary>Gets the MVC controller type to which this mapping applies.</summary>
    public Type Controller { get; } = controller;

    /// <summary>
    /// Gets the dot-separated accessor path. When omitted, groups follow the namespace below the program's
    /// Controllers namespace, and the member name is the controller type name without its Controller suffix.
    /// </summary>
    public string? Path { get; }

    /// <summary>Gets or sets the generated type name. Defaults to the controller name without its Controller suffix, followed by For.</summary>
    public string? TypeName { get; set; }

    /// <summary>
    /// Gets or sets the controller route metadata override. When not explicitly supplied, the generator infers
    /// a single standard controller route template. This does not register or change runtime MVC routes.
    /// </summary>
    public string RouteTemplate { get; set; } = "";

    /// <summary>Gets or sets whether to omit this controller and its actions from generated accessors. Defaults to false.</summary>
    public bool Exclude { get; set; }
}

/// <summary>Preserves a host-owned API group type name and its partial customization.</summary>
/// <param name="program">The automatically discovered hosted program whose accessors are customized.</param>
/// <param name="path">The dot-separated path of an existing generated controller group, such as User.Identity.</param>
/// <param name="typeName">The generated group type name to use instead of the conventional name.</param>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class AccessorGroupAttribute(Type program, string path, string typeName) : Attribute
{
    /// <summary>Gets the hosted program to which this group customization applies.</summary>
    public Type Program { get; } = program;

    /// <summary>Gets the dot-separated controller group path. This is not a URL route.</summary>
    public string Path { get; } = path;

    /// <summary>Gets the generated group type name, replacing the default path segments joined together and followed by ApiFor.</summary>
    public string TypeName { get; } = typeName;
}

/// <summary>Aliases or excludes a generated action or page by its accessor path.</summary>
/// <param name="program">The automatically discovered hosted program whose accessors are customized.</param>
/// <param name="path">The dot-separated accessor path prefixed with Endpoint or Page, such as Endpoint.User.Profile.Get or Page.Root.Index.</param>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class AccessorMemberAttribute(Type program, string path) : Attribute
{
    /// <summary>Gets the hosted program to which this member customization applies.</summary>
    public Type Program { get; } = program;

    /// <summary>Gets the accessor path before member aliasing, including its Endpoint or Page prefix. This is not a URL route.</summary>
    public string Path { get; } = path;

    /// <summary>Gets or sets the generated property name. Defaults to the CLR action method name or Razor Page file name without its extension.</summary>
    public string? Name { get; set; }

    /// <summary>Gets or sets whether to omit this member from generated accessors. Takes precedence over Name and defaults to false.</summary>
    public bool Exclude { get; set; }
}

/// <summary>Binds an accessor property to a CLR action signature independently of its member name.</summary>
/// <param name="methodName">The CLR action method name, including any Async suffix, rather than the effective MVC action name.</param>
/// <param name="parameterTypes">The action's CLR parameter types in declaration order. Omit for a parameterless action.</param>
[AttributeUsage(AttributeTargets.Property)]
public sealed class EndpointActionAttribute(string methodName, params Type[] parameterTypes) : Attribute
{
    /// <summary>Gets the CLR action method name used to bind the accessor to its controller action.</summary>
    public string MethodName { get; } = methodName;

    /// <summary>Gets the ordered CLR parameter types used to identify the action signature. An empty array identifies a parameterless action.</summary>
    public Type[] ParameterTypes { get; } = parameterTypes;
}
