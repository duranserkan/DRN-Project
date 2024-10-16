using System.Reflection;

namespace DRN.Framework.Utils.Models.Page;

public class PageCollectionBase<TPageCollection> where TPageCollection : PageCollectionBase<TPageCollection>
{
    public static string[] GetAllPages()
    {
        var properties = typeof(TPageCollection)
            .GetProperties(BindingFlags.Static | BindingFlags.Public)
            .Where(p => p.PropertyType.IsAssignableTo(typeof(PageForBase)));

        List<string> pageList = [];
        foreach (var propertyInfo in properties)
        {
            var pageForBase = (PageForBase?)propertyInfo.GetValue(null);
            pageList.AddRange(pageForBase?.GetPages() ?? []);
        }

        return pageList.Order().ToArray();
    }
}