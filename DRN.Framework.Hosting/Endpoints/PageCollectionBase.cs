using System.Reflection;

namespace DRN.Framework.Hosting.Endpoints;

public abstract class PageCollectionBase<TPageCollection> where TPageCollection : PageCollectionBase<TPageCollection>
{
    private static readonly Lazy<HashSet<string>> AllPages = new(InitializePages);
    public static HashSet<string> GetAllPages() => AllPages.Value;

    private static HashSet<string> InitializePages()
    {
        var properties = typeof(TPageCollection)
            .GetProperties(BindingFlags.Static | BindingFlags.Public)
            .Where(p => p.PropertyType.IsAssignableTo(typeof(PageForBase)));

        HashSet<string> pageList = [];
        foreach (var propertyInfo in properties)
        {
            var pageForBase = (PageForBase?)propertyInfo.GetValue(null);
            if (pageForBase == null) continue;

            foreach (var page in pageForBase.GetPages())
                pageList.Add(page);
        }

        return pageList;
    }
}