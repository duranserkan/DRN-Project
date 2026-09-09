namespace DRN.Test.Utils.Testing;

/// <summary>
/// Placeholder assembly entry point to satisfy WebApplicationFactory deps file discovery
/// while routing all test programs uniformly through DrnProgramHostBuilder.
/// </summary>
public static class TestUtilsPlaceholderProgram
{
    public static async Task Main(string[] args)
    {
        // Preserve the no-op entry point used for WebApplicationFactory discovery.
        if (args.Length == 0)
            return;
        if (args is ["--verify-source-known-startup", var scenario])
            await Hosting.SourceKnownStartupTestProgram.VerifyAsync(scenario);
        else
            throw new ArgumentException("Unknown test-support verification arguments.", nameof(args));

        Console.WriteLine("Source-Known verification passed.");
    }
}
