using DRN.Framework.Utils.Data.App;

namespace DRN.Test.Unit.Tests.Framework.Utils.Data.App;

public class AppDataTests
{
    [Fact]
    public void GetPath_WithInvalidPathResult_ShouldThrowInvalidOperationException()
    {
        var result = AppDataPathResult.Invalid();

        FluentActions.Invoking(() => result.GetPath("child"))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*InvalidPath*");
    }

    [Fact]
    public void From_WithEmptyPath_ShouldReturnEmptyPathResult()
    {
        var result = AppDataPathResult.From(string.Empty);

        result.Path.Should().Be(string.Empty);
        result.DirectoryExists.Should().BeFalse();
        result.Status.Should().Be(AppDataPathStatus.EmptyPath);
    }

    [Theory]
    [DataInlineUnit]
    public void Constructor_WithNullConfiguredPaths_ShouldUseFallbackPaths(DrnTestContextUnit context)
    {
        var tempRoot = context.GetTempPath();
        var fallbackTemp = Path.Combine(tempRoot, "temp-fallback");
        var fallbackData = Path.Combine(tempRoot, "data-fallback");
        var settings = new DrnAppDataSettings();
        var appData = new AppData(settings, fallbackTemp, fallbackData);

        appData.Temp.Path.Should().Be(Path.TrimEndingDirectorySeparator(Path.GetFullPath(fallbackTemp)));
        appData.Data.Path.Should().Be(Path.TrimEndingDirectorySeparator(Path.GetFullPath(fallbackData)));
        appData.Temp.DirectoryExists.Should().BeTrue();
        appData.Temp.Status.Should().Be(AppDataPathStatus.Valid);
        appData.Data.DirectoryExists.Should().BeTrue();
        appData.Data.Status.Should().Be(AppDataPathStatus.Valid);
    }

    [Theory]
    [DataInlineUnit]
    public void Constructor_WithRequireTempAccessAndInvalidTempPath_ShouldThrowConfigurationException(DrnTestContextUnit context)
    {
        var tempRoot = context.GetTempPath();
        var invalidTemp = "invalid\0temp";
        var fallbackData = Path.Combine(tempRoot, "data-fallback");
        var settings = new DrnAppDataSettings
        {
            RequireTemp = true
        };

        FluentActions.Invoking(() => new AppData(settings, invalidTemp, fallbackData))
            .Should().Throw<ConfigurationException>()
            .WithMessage("*Temp path*required but not valid*");
    }

    [Theory]
    [DataInlineUnit]
    public void Constructor_WithRequireDataAccessAndInvalidDataPath_ShouldThrowConfigurationException(DrnTestContextUnit context)
    {
        var tempRoot = context.GetTempPath();
        var fallbackTemp = Path.Combine(tempRoot, "temp-fallback");
        var invalidData = "invalid\0data";
        var settings = new DrnAppDataSettings
        {
            RequireData = true
        };

        FluentActions.Invoking(() => new AppData(settings, fallbackTemp, invalidData))
            .Should().Throw<ConfigurationException>()
            .WithMessage("*Data path*required but not valid*");
    }

    [Theory]
    [DataInlineUnit]
    public void Constructor_WithRequireDataAccessAndValidDataPath_ShouldNotThrow(DrnTestContextUnit context)
    {
        var tempRoot = context.GetTempPath();
        var fallbackTemp = Path.Combine(tempRoot, "temp-fallback");
        var fallbackData = Path.Combine(tempRoot, "data-fallback");
        Directory.CreateDirectory(fallbackData);
        var settings = new DrnAppDataSettings
        {
            RequireData = true
        };

        FluentActions.Invoking(() => new AppData(settings, fallbackTemp, fallbackData))
            .Should().NotThrow();
    }

    [Theory]
    [DataInlineUnit]
    public void GetPath_WithTraversalSegments_ShouldThrowArgumentException(DrnTestContextUnit context)
    {
        var tempRoot = context.GetTempPath();
        var fallbackTemp = Path.Combine(tempRoot, "temp");
        var fallbackData = Path.Combine(tempRoot, "data");
        var settings = new DrnAppDataSettings();
        var appData = new AppData(settings, fallbackTemp, fallbackData);

        FluentActions.Invoking(() => appData.Temp.GetPath("..", "outside"))
            .Should().Throw<ArgumentException>().WithMessage("*must stay within directory*");
    }
}