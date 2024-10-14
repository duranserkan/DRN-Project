using System.Reflection;

namespace DRN.Framework.Utils.Http;

public interface IPageCollectionBase<TPageCollection> where TPageCollection : IPageCollectionBase<TPageCollection>
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