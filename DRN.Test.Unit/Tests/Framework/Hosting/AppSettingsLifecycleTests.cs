using System.Runtime.CompilerServices;

namespace DRN.Test.Unit.Tests.Framework.Hosting;

public class AppSettingsLifecycleTests
{
    [Fact]
    public void ConfigurationExtensions_Should_Dispose_Temporary_AppSettings_After_Environment_Discovery()
    {
        var source = ReadRepositoryFile("DRN.Framework.Hosting/Extensions/ConfigurationExtensions.cs");

        source.Should().Contain("using var serviceProvider = sc?.BuildServiceProvider();");
        source.Should().Contain("using var tempSettings = new AppSettings(builder.Build());");
    }

    [Fact]
    public void DrnProgramBase_RunAsync_Should_Own_Startup_AppSettings_With_Using()
    {
        var source = ReadRepositoryFile("DRN.Framework.Hosting/DrnProgram/DrnProgramBase.cs");

        source.Should().Contain("using var appSettings = new AppSettings(configuration);");
        source.Should().Contain("using var services = applicationBuilder.Services.BuildServiceProvider();");
    }

    private static string ReadRepositoryFile(string relativePath, [CallerFilePath] string testFilePath = "")
    {
        var repositoryRoot = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(testFilePath)!, "../../../.."));

        return File.ReadAllText(Path.Combine(repositoryRoot, relativePath));
    }
}
