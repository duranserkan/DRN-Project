using DRN.Framework.Utils.Data.App;

namespace DRN.Test.Unit.Tests.Framework.Utils.Data.App;

public class AppDataTests
{
    [Theory]
    [DataInlineUnit]
    public void Constructor_WithConfiguredPaths_ShouldCreateDataDirectoryWithoutCreatingTempDirectory(DrnTestContextUnit context)
    {
        var tempRoot = context.GetTempPath();
        var tempPath = Path.Combine(tempRoot, "temp-not-created");
        var localPath = Path.Combine(tempRoot, "local-not-created");
        var settings = new DrnAppDataSettings
        {
            TempPath = tempPath,
            DataPath = localPath
        };
        var appData = new AppData(settings);

        appData.Temp.Path.Should().Be(Path.TrimEndingDirectorySeparator(Path.GetFullPath(tempPath)));
        appData.Data.Path.Should().Be(Path.TrimEndingDirectorySeparator(Path.GetFullPath(localPath)));
        appData.Temp.DirectoryExists.Should().BeFalse();
        appData.Temp.Status.Should().Be(AppDataPathStatus.PathNotFound);
        appData.Data.DirectoryExists.Should().BeTrue();
        appData.Data.Status.Should().Be(AppDataPathStatus.Valid);
        Directory.Exists(localPath).Should().BeTrue();
    }

    [Fact]
    public void Constructor_WithExistingConfiguredPath_ShouldReportDirectoryExists()
    {
        var tempPath = Path.GetTempPath();
        var settings = new DrnAppDataSettings
        {
            TempPath = tempPath
        };

        var appData = new AppData(settings);

        appData.Temp.Path.Should().Be(Path.TrimEndingDirectorySeparator(Path.GetFullPath(tempPath)));
        appData.Temp.DirectoryExists.Should().BeTrue();
        appData.Temp.Status.Should().Be(AppDataPathStatus.Valid);
    }

    [Fact]
    public void Constructor_WithInvalidTempPathFormat_ShouldReturnInvalidPathResult()
    {
        var settings = new DrnAppDataSettings
        {
            TempPath = "invalid\0path"
        };
        var appData = new AppData(settings);

        appData.Temp.Path.Should().Be(string.Empty);
        appData.Temp.DirectoryExists.Should().BeFalse();
        appData.Temp.Status.Should().Be(AppDataPathStatus.InvalidPath);
    }

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
    public void GetPath_WithTraversalSegments_ShouldThrowArgumentException(DrnTestContextUnit context)
    {
        var tempRoot = context.GetTempPath();
        var settings = new DrnAppDataSettings
        {
            TempPath = Path.Combine(tempRoot, "temp")
        };
        var appData = new AppData(settings);

        FluentActions.Invoking(() => appData.Temp.GetPath("..", "outside"))
            .Should().Throw<ArgumentException>().WithMessage("*must stay within directory*");
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
        appData.Temp.DirectoryExists.Should().BeFalse();
        appData.Temp.Status.Should().Be(AppDataPathStatus.PathNotFound);
        appData.Data.DirectoryExists.Should().BeTrue();
        appData.Data.Status.Should().Be(AppDataPathStatus.Valid);
        Directory.Exists(fallbackTemp).Should().BeFalse();
        Directory.Exists(fallbackData).Should().BeTrue();
    }
}
