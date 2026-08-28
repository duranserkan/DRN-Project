using Sample.Hosted.Pages.Shared.Models;

namespace DRN.Test.Unit.Tests.Sample.Pages;

public class MainContentLayoutOptionsTests
{
    [Theory]
    [DataInlineUnit(BootstrapTextAlignment.TextStart, "text-start")]
    [DataInlineUnit(BootstrapTextAlignment.TextCenter, "text-center")]
    [DataInlineUnit(BootstrapTextAlignment.TextEnd, "text-end")]
    public void CssTextAlignment_Should_Map_Every_Alignment(BootstrapTextAlignment alignment, string expectedCssClass)
    {
        var options = new MainContentLayoutOptions { TextAlignment = alignment };

        options.CssTextAlignment().Should().Be(expectedCssClass);
    }

    [Theory]
    [DataInlineUnit(BootstrapGridTier.Xs, "col")]
    [DataInlineUnit(BootstrapGridTier.Sm, "col-sm")]
    [DataInlineUnit(BootstrapGridTier.Md, "col-md")]
    [DataInlineUnit(BootstrapGridTier.Lg, "col-lg")]
    [DataInlineUnit(BootstrapGridTier.Xl, "col-xl")]
    [DataInlineUnit(BootstrapGridTier.Xxl, "col-xxl")]
    [DataInlineUnit(BootstrapGridTier.None, "col")]
    public void CssColumnTier_Should_Map_Every_Grid_Tier(BootstrapGridTier gridTier, string expectedCssClass)
    {
        var options = new MainContentLayoutOptions { GridTier = gridTier };

        options.CssColumnTier().Should().Be(expectedCssClass);
    }

    [Theory]
    [DataInlineUnit(BootstrapColumnSize.One, "col-md-1")]
    [DataInlineUnit(BootstrapColumnSize.Two, "col-md-2")]
    [DataInlineUnit(BootstrapColumnSize.Three, "col-md-3")]
    [DataInlineUnit(BootstrapColumnSize.Four, "col-md-4")]
    [DataInlineUnit(BootstrapColumnSize.Five, "col-md-5")]
    [DataInlineUnit(BootstrapColumnSize.Six, "col-md-6")]
    [DataInlineUnit(BootstrapColumnSize.Seven, "col-md-7")]
    [DataInlineUnit(BootstrapColumnSize.Eight, "col-md-8")]
    [DataInlineUnit(BootstrapColumnSize.Nine, "col-md-9")]
    [DataInlineUnit(BootstrapColumnSize.Ten, "col-md-10")]
    [DataInlineUnit(BootstrapColumnSize.Eleven, "col-md-11")]
    [DataInlineUnit(BootstrapColumnSize.Twelve, "col-md-12")]
    [DataInlineUnit(BootstrapColumnSize.Auto, "col-md-auto")]
    [DataInlineUnit(BootstrapColumnSize.None, "col-md")]
    public void CssColumnSize_Should_Map_Every_Column_Size(BootstrapColumnSize columnSize, string expectedCssClass)
    {
        var options = new MainContentLayoutOptions { ColumnSize = columnSize };

        options.CssColumnSize().Should().Be(expectedCssClass);
    }
}
