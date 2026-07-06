using DRN.Framework.SharedKernel.Extensions;

namespace DRN.Test.Unit.Tests.Framework.SharedKernel;

public class AppConstantTests
{
    [Fact]
    public void LocalIpAddress_Should_Be_Obtained()
    {
        AppConstants.LocalIpAddress.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void AppDataPaths_Should_Be_Obtained()
    {
        var assemblyName = AppConstants.EntryAssemblyName.ToPascalCase();

        AppConstants.TempPath.Should().Contain(assemblyName);
        AppConstants.TempPath.Should().Be(Path.Combine(AppConstants.LocalAppDataPath, "Temp"));

        AppConstants.LocalAppDataPath.Should().Contain(assemblyName);
        AppConstants.LocalAppDataPath.Should().StartWith(Path.GetFullPath(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)));
    }

    [Theory]
    [DataInlineUnit("")]
    [DataInlineUnit("...")]
    public void GetAppSpecificDirectoryPath_WithEmptyNormalizedName_ShouldReturnEmptyPath(string entryAssemblyName)
    {
        var path = AppConstants.GetAppSpecificDirectoryPath(Path.GetTempPath(), entryAssemblyName);

        path.Should().BeEmpty();
    }

    [Fact]
    public void GetAppSpecificDirectoryPath_WithEmptyRootPath_ShouldReturnEmptyPath()
    {
        var path = AppConstants.GetAppSpecificDirectoryPath(string.Empty, "DRN.Framework.Hosting");

        path.Should().BeEmpty();
    }

    [Fact]
    public void GetAppSpecificDirectoryPath_ShouldReturnStrictChildOfRoot()
    {
        var root = Path.GetTempPath();

        var path = AppConstants.GetAppSpecificDirectoryPath(root, "DRN.Framework.Hosting");

        path.Should().NotBe(Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)));
        path.Should().StartWith(Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)));
        Path.GetFileName(path).Should().Be("DrnFrameworkHosting");
    }
}
