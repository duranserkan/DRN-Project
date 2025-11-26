namespace DRN.Framework.Hosting.Endpoints;

public abstract class PageCollectionBase<TPageCollection>
    where TPageCollection : PageCollectionBase<TPageCollection>, new()
{
    private static readonly Lazy<HashSet<string>> AllPages = new(InitializePages);
    private static HashSet<string> GetAllPages() => AllPages.Value;

    public static TPageCollection PageCollection { get; } = new();

    public HashSet<string> All => GetAllPages();

    private static HashSet<string> InitializePages()
    {
        var properties = typeof(TPageCollection).GetProperties()
            .Where(p => p.PropertyType.IsAssignableTo(typeof(PageForBase)));

        var pageList = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var propertyInfo in properties)
        {
            var pageForBase = (PageForBase?)propertyInfo.GetValue(PageCollection);
            if (pageForBase == null) continue;

            foreach (var page in pageForBase.GetPages())
                pageList.Add(page);
        }

        return pageList;
    }
}