using System.Reflection;

namespace DRN.Framework.Hosting.Endpoints;

public abstract class PageForBase
{
    protected PageForBase()
    {
        var props = GetType()
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(p => p.PropertyType == typeof(string));
        foreach (var prop in props)
        {
            var existingValue = prop.GetValue(this) as string;
            if (string.IsNullOrEmpty(existingValue))
                prop.SetValue(this, GetPath(prop.Name));
        }
    }

    protected abstract string[] PathSegments { get; }
    private string GetPath(string page) => $"/{string.Join('/', PathSegments.Append(page)).Trim('/')}";

    public IEnumerable<string> GetPages() => GetType()
        .GetProperties(BindingFlags.Instance | BindingFlags.Public)
        .Where(p => p.PropertyType == typeof(string))
        .Select(p => p.GetValue(this))
        .Where(v => v != null).Cast<string>();
    
    internal static HashSet<string> GetPages(object pageForSource)
    {
        var properties = GetPageForProperties(pageForSource.GetType());

        var pageList = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var propertyInfo in properties)
        {
            var pageForBase = (PageForBase?)propertyInfo.GetValue(pageForSource);
            if (pageForBase == null) continue;

            foreach (var page in pageForBase.GetPages())
                pageList.Add(page);
        }

        return pageList;
    }

    internal static PropertyInfo[] GetPageForProperties(Type type) => type.GetProperties()
        .Where(p => p.PropertyType.IsAssignableTo(typeof(PageForBase))).ToArray();
}