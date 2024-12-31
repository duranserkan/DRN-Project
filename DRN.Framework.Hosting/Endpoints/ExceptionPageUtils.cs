using DRN.Framework.Utils.DependencyInjection.Attributes;

namespace DRN.Framework.Hosting.Endpoints;

public interface IExceptionPageAccessor
{
    PageEndpoint RuntimeExceptionPage { get; }
    PageEndpoint CompilationExceptionPage { get; }
}

[Singleton<IExceptionPageAccessor>]
public class ExceptionPageAccessor(IEndpointAccessor accessor) : IExceptionPageAccessor
{
    public const string RuntimeExceptionPageName = "RuntimeExceptionPage";
    public const string RuntimeExceptionPagePath = $"/Areas/Developer/Pages/{RuntimeExceptionPageName}.cshtml";
    public const string CompilationExceptionPageName = "CompilationExceptionPage";
    public const string CompilationExceptionPagePath = $"/Areas/Developer/Pages/{CompilationExceptionPageName}.cshtml";

    public PageEndpoint RuntimeExceptionPage { get; } = accessor.PageEndpointByPaths[RuntimeExceptionPagePath];
    public PageEndpoint CompilationExceptionPage { get; } = accessor.PageEndpointByPaths[CompilationExceptionPagePath];

    public static bool IsExceptionPage(string? path)
    {
        if (path == null)
            return false;

        return path.Contains(RuntimeExceptionPageName) || path.Contains(CompilationExceptionPageName);
    }
}