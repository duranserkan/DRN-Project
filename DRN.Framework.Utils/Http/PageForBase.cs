using System.Reflection;

namespace DRN.Framework.Utils.Http;

public abstract class PageForBase
{
    protected PageForBase()
    {
        var props = GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public);
        foreach (var prop in props)
            prop.SetValue(this, GetPath(prop.Name));
    }

    protected abstract string[] PathSegments { get; }
    private string GetPath(string page) => $"/{string.Join('/', PathSegments.Append(page)).Trim('/')}";

    public IEnumerable<string> GetPages() => GetType()
        .GetProperties(BindingFlags.Instance | BindingFlags.Public)
        .Where(p => p.PropertyType == typeof(string))
        .Select(p => p.GetValue(this))
        .Where(v => v != null).Cast<string>();
}