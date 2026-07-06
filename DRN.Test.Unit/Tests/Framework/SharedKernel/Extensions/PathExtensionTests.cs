using DRN.Framework.SharedKernel.Extensions;

namespace DRN.Test.Unit.Tests.Framework.SharedKernel.Extensions;

public class PathExtensionTests
{
    [Theory]
    [DataInlineUnit]
    public void IsPathWithinDirectory_WithChildPath_ShouldReturnTrue(DrnTestContextUnit context)
    {
        var root = Path.Combine(context.GetTempPath(), "root");
        var childPath = Path.Combine(root, "child", "file.txt");

        root.IsPathWithinDirectory(childPath).Should().BeTrue();
    }

    [Theory]
    [DataInlineUnit]
    public void IsPathWithinDirectory_WithSiblingPrefixPath_ShouldReturnFalse(DrnTestContextUnit context)
    {
        var tempRoot = context.GetTempPath();
        var root = Path.Combine(tempRoot, "root");
        var siblingPath = Path.Combine(tempRoot, "root-sibling", "file.txt");

        root.IsPathWithinDirectory(siblingPath).Should().BeFalse();
    }

    [Theory]
    [DataInlineUnit]
    public void GetPathWithinDirectory_WithTraversalSegments_ShouldThrowArgumentException(DrnTestContextUnit context)
    {
        var root = Path.Combine(context.GetTempPath(), "root");

        FluentActions.Invoking(() => root.GetPathWithinDirectory("..", "outside"))
            .Should().Throw<ArgumentException>()
            .WithMessage("*must stay within directory*");
    }

    [Theory]
    [DataInlineUnit]
    public void GetPathWithinDirectory_WithDirectorySymlinkTraversal_ShouldThrowArgumentException(DrnTestContextUnit context)
    {
        var tempRoot = context.GetTempPath();
        var root = Path.Combine(tempRoot, "root");
        var outside = Path.Combine(tempRoot, "outside");
        var link = Path.Combine(root, "link");
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(outside);

        if (!TryCreateDirectorySymbolicLink(link, outside))
            Assert.Skip("Directory symbolic link creation is not available in this environment.");

        FluentActions.Invoking(() => root.GetPathWithinDirectory("link", "file.txt"))
            .Should().Throw<ArgumentException>()
            .WithMessage("*physically within directory*");
    }

    private static bool TryCreateDirectorySymbolicLink(string linkPath, string targetPath)
    {
        try
        {
            Directory.CreateSymbolicLink(linkPath, targetPath);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            return false;
        }
    }
}
