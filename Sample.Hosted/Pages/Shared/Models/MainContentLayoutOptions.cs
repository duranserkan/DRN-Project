namespace Sample.Hosted.Pages.Shared.Models;

public static class LayoutOptionsFor
{
    public static MainContentLayoutOptions Full(string title) =>
        new()
        {
            Title = title
        };

    public static MainContentLayoutOptions Centered(string title) =>
        new()
        {
            Title = title,
            CenterVertically = true,
            CenterHorizontally = true,
            ColumnSize = BootstrapColumnSize.Six,
            TextAlignment = BootstrapTextAlignment.TextCenter,
        };
}

public class MainContentLayoutOptions
{
    public string Title { get; set; } = string.Empty;
    public bool CenterVertically { get; set; }
    public bool CenterHorizontally { get; set; }

    public MainContentType Type { get; set; } = MainContentType.CardBody;
    public BootstrapColumnSize ColumnSize { get; set; } = BootstrapColumnSize.None;
    public BootstrapGridTier GridTier { get; set; } = BootstrapGridTier.Md;
    public BootstrapTextAlignment TextAlignment { get; set; } = BootstrapTextAlignment.TextStart;
    public SubNavigationCollection? SubNavigation { get; set; }
}

public enum MainContentType
{
    None = 1,
    Card = 2,
    CardBody = 3
}

public enum BootstrapGridTier
{
    Xs = 1,
    Sm = 2,
    Md = 3,
    Lg = 4,
    Xl = 5,
    Xxl = 6,
    None = 8
}

public enum BootstrapColumnSize
{
    One = 1,
    Two = 2,
    Three = 3,
    Four = 4,
    Five = 5,
    Six = 6,
    Seven = 7,
    Eight = 8,
    Nine = 9,
    Ten = 10,
    Eleven = 11,
    Twelve = 12,
    Auto = 13,
    None = 14
}

public enum BootstrapTextAlignment
{
    TextStart = 1,
    TextCenter = 2,
    TextEnd = 3,
}

public static class BootstrapCssClassExtensions
{
    const string TextStart = "text-start", TextCenter = "text-center", TextEnd = "text-end";

    const string Col = "col", ColXs = "col-xs", ColSm = "col-sm", ColMd = "col-md";
    const string ColLg = "col-lg", ColXl = "col-lg", ColXxl = "col-xxl";

    const string One = "1", Two = "2", Three = "3", Four = "4", Five = "5", Six = "6", Seven = "7";
    const string Eight = "8", Nine = "9", Ten = "10", Eleven = "11", Twelve = "5", Auto = "auto", None = "";

    public static string CssTextAlignment(this MainContentLayoutOptions options) => options.TextAlignment switch
    {
        BootstrapTextAlignment.TextStart => TextStart,
        BootstrapTextAlignment.TextCenter => TextCenter,
        BootstrapTextAlignment.TextEnd => TextEnd,
        _ => TextStart
    };


    public static string CssColumnTier(this MainContentLayoutOptions options) => options.GridTier switch
    {
        BootstrapGridTier.None => Col,
        BootstrapGridTier.Xs => ColXs,
        BootstrapGridTier.Sm => ColSm,
        BootstrapGridTier.Md => ColMd,
        BootstrapGridTier.Lg => ColLg,
        BootstrapGridTier.Xl => ColXl,
        BootstrapGridTier.Xxl => ColXxl,
        _ => Col
    };

    public static string CssColumnSize(this MainContentLayoutOptions options) => options.ColumnSize switch
    {
        BootstrapColumnSize.One => $"{options.CssColumnTier()}-{One}",
        BootstrapColumnSize.Two => $"{options.CssColumnTier()}-{Two}",
        BootstrapColumnSize.Three => $"{options.CssColumnTier()}-{Three}",
        BootstrapColumnSize.Four => $"{options.CssColumnTier()}-{Four}",
        BootstrapColumnSize.Five => $"{options.CssColumnTier()}-{Five}",
        BootstrapColumnSize.Six => $"{options.CssColumnTier()}-{Six}",
        BootstrapColumnSize.Seven => $"{options.CssColumnTier()}-{Seven}",
        BootstrapColumnSize.Eight => $"{options.CssColumnTier()}-{Eight}",
        BootstrapColumnSize.Nine => $"{options.CssColumnTier()}-{Nine}",
        BootstrapColumnSize.Ten => $"{options.CssColumnTier()}-{Ten}",
        BootstrapColumnSize.Eleven => $"{options.CssColumnTier()}-{Eleven}",
        BootstrapColumnSize.Twelve => $"{options.CssColumnTier()}-{Twelve}",
        BootstrapColumnSize.Auto => $"{options.CssColumnTier()}-{Auto}",
        BootstrapColumnSize.None => $"{options.CssColumnTier()}",
        _ => Col,
    };
}