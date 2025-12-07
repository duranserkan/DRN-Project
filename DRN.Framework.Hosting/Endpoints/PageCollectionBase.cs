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
        var pageList = PageForBase.GetPages(PageCollection);

        return pageList;
    }
}