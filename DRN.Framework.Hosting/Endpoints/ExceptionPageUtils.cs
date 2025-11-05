using DRN.Framework.Utils.DependencyInjection.Attributes;
using DRN.Framework.Utils.Scope;

namespace DRN.Framework.Hosting.Endpoints;

public interface IExceptionPageAccessor
{
    PageEndpoint RuntimeExceptionPage { get; }
    PageEndpoint CompilationExceptionPage { get; }
}

[Singleton<IExceptionPageAccessor>]
public class ExceptionPageAccessor(IEndpointAccessor accessor) : IExceptionPageAccessor
{
    public const string DeveloperPageName = "DeveloperView";
    public const string RuntimeExceptionPageName = "RuntimeExceptionPage";
    public const string RuntimeExceptionPagePath = $"/Areas/Developer/Pages/{RuntimeExceptionPageName}.cshtml";
    public const string CompilationExceptionPageName = "CompilationExceptionPage";
    public const string CompilationExceptionPagePath = $"/Areas/Developer/Pages/{CompilationExceptionPageName}.cshtml";

    public PageEndpoint RuntimeExceptionPage { get; } = accessor.PageEndpointByPaths[RuntimeExceptionPagePath];
    public PageEndpoint CompilationExceptionPage { get; } = accessor.PageEndpointByPaths[CompilationExceptionPagePath];

    public static bool IsExceptionPage(string? path, bool isDevEnvironment)
    {
        if (path == null)
            return false;

        if (isDevEnvironment && path.Contains(DeveloperPageName))
            throw new DeveloperViewException();

        return path.Contains(RuntimeExceptionPageName) || path.Contains(CompilationExceptionPageName);
    }
}

internal class DeveloperViewException : Exception
{
}