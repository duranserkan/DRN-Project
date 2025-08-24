using DRN.Framework.Hosting.Endpoints;
using Sample.Hosted.Controllers.QA;

namespace Sample.Hosted.Controllers;

public class QaApiFor
{
    public const string Prefix = "/Api/QA";
    public const string ControllerRouteTemplate = $"{Prefix}/[controller]";

    public TagFor Tag { get; } = new();
    public CategoryFor Category { get; } = new();
}

public class TagFor()
    : ControllerForBase<TagController>(QaApiFor.ControllerRouteTemplate)
{
    //By convention, Endpoint name should match Action name and property should have setter;
    public ApiEndpoint PaginateAsync { get; private set; } = null!;
    public ApiEndpoint PaginateWithQueryAsync { get; private set; } = null!;
    public ApiEndpoint PaginateWithBodyAsync { get; private set; } = null!;
    public ApiEndpoint GetAsync { get; private set; } = null!;
    public ApiEndpoint PostAsync { get; private set; } = null!;
    public ApiEndpoint DeleteAsync { get; private set; } = null!;
}

public class CategoryFor()
    : ControllerForBase<CategoryController>(QaApiFor.ControllerRouteTemplate)
{
    //By convention, Endpoint name should match Action name and property should have setter;
    public ApiEndpoint GetAsync { get; private set; } = null!;
    public ApiEndpoint PostAsync { get; private set; } = null!;
}