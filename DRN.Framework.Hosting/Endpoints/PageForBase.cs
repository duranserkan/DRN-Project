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
}