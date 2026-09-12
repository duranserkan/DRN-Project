using DRN.Framework.Hosting.DrnProgram;
using DRN.Framework.SharedKernel.Domain;
using DRN.Framework.Utils.Ids;
using DRN.Framework.Utils.Logging;
using DRN.Framework.Utils.Settings;
using DRN.Framework.Utils.Time;
using DRN.Framework.Utils.Entity;

namespace DRN.Test.Utils.Hosting;

/// <summary>Executed only in isolated test-support processes; never resets the production policy.</summary>
public sealed class SourceKnownStartupTestProgram : DrnProgramBase<SourceKnownStartupTestProgram>, IDrnProgram
{
    private static DateTimeOffset _expectedMinimum;
    private static readonly DateTimeOffset ExpectedEpoch = SourceKnownGenerationTimePolicy.DefaultEpoch.AddDays(1);
    private static bool _constructorCalled;
    private static bool _servicesCalled;

    public SourceKnownStartupTestProgram()
    {
        _constructorCalled = true;
        VerifyHook();
    }

    public static Task Main(string[] args) => RunAsync(args);

    public static async Task VerifyAsync(string scenario)
    {
        if (scenario == "historical-freeze")
        {
            var epoch = ExpectedEpoch;
            var historicalMinimum = DateTimeOffset.UtcNow.AddDays(1);
            SourceKnownGenerationTime.Initialize(historicalMinimum.ToString("O"), epoch.ToString("O"));
            if (SourceKnownIdUtils.ParseId(long.MinValue).CreatedAt != epoch)
                throw new InvalidOperationException("Historical decoding did not use the configured epoch.");
            try
            {
                SourceKnownGenerationTime.Initialize(historicalMinimum.ToString("O"), epoch.AddDays(1).ToString("O"));
            }
            catch (Exception exception) when (exception.Message.Contains("epoch is frozen", StringComparison.Ordinal))
            {
                return;
            }
            throw new InvalidOperationException("Historical decoding did not freeze the epoch.");
        }
        if (scenario is "static" or "static-reject")
        {
            SourceKnownGenerationTime.Initialize((scenario == "static"
                ? SourceKnownGenerationTimePolicy.DefaultEpoch
                : DateTimeOffset.UtcNow.AddDays(1)).ToString("O"), ExpectedEpoch.ToString("O"));
            long id;
            try
            {
                id = SourceKnownIdUtils.Generate<ProbeEntity>(1, 1);
            }
            catch (InvalidOperationException exception) when (scenario == "static-reject" &&
                exception.Message.Contains("below minimum generation UTC", StringComparison.Ordinal))
            {
                return;
            }
            if (scenario == "static-reject")
                throw new InvalidOperationException("Static generation bypassed initialization.");

            var parsed = SourceKnownIdUtils.ParseId(id);
            if (parsed.CreatedAt < ExpectedEpoch || parsed.CreatedAt > TimeStampManager.UtcNow)
                throw new InvalidOperationException("Static generation did not use the configured epoch.");
            var time = new EpochTimeUtils();
            if (time.Epoch != ExpectedEpoch || time.ConvertToDatetime(0) != ExpectedEpoch ||
                time.ConvertToTicks(ExpectedEpoch) != 0 || SourceKnownIdUtils.ParseId(long.MinValue).CreatedAt != ExpectedEpoch)
                throw new InvalidOperationException("Generation and historical conversion disagree on the configured epoch.");
            var query = new[] { new ProbeEntity(long.MinValue) }.AsQueryable();
            if (new EntityDateTimeUtils().CreatedAfter(query, ExpectedEpoch, inclusive: false).Any())
                throw new InvalidOperationException("Date filters did not use the configured epoch.");
            return;
        }
        if (scenario is not ("startup-reject" or "startup-only"))
            throw new ArgumentException("Unknown Source-Known startup verification scenario.", nameof(scenario));

        _expectedMinimum = scenario == "startup-only"
            ? SourceKnownGenerationTimePolicy.DefaultEpoch
            : DateTimeOffset.UtcNow.AddDays(1);
        string[] arguments =
        [
            "--Environment=Development",
            "--DrnDevelopmentSettings:TemporaryApplication=true",
            $"--SourceKnownIdSettings:MinimumUtc={_expectedMinimum:O}",
            $"--SourceKnownIdSettings:DefaultEpoch={ExpectedEpoch:O}",
            "--NLog:targets:null:type=Null",
            "--NLog:rules:0:logger=*",
            "--NLog:rules:0:minLevel=Off",
            "--NLog:rules:0:writeTo=null"
        ];
        if (scenario == "startup-reject")
        {
            try
            {
                await Main(arguments);
            }
            catch (InvalidOperationException exception) when (exception.Message.Contains("below minimum generation UTC", StringComparison.Ordinal))
            {
                if (_constructorCalled || _servicesCalled)
                    throw new InvalidOperationException("Mandatory startup validation ran after constructors/hooks.");
                return;
            }
            throw new InvalidOperationException("Mandatory startup validation accepted a future floor.");
        }

        await Main(arguments);
        if (!_constructorCalled || !_servicesCalled)
            throw new InvalidOperationException("Startup verification did not reach both constructor and service hook.");
        try
        {
            SourceKnownGenerationTime.Initialize(_expectedMinimum.AddDays(1).ToString("O"));
        }
        catch (Exception exception) when (exception.Message.Contains("floor is frozen", StringComparison.Ordinal))
        {
            return;
        }
        throw new InvalidOperationException("Startup validation did not freeze the generation policy.");
    }

    private static void VerifyHook()
    {
        if (SourceKnownGenerationTime.Policy.MinimumUtc != _expectedMinimum)
            throw new InvalidOperationException("Configured override was not initialized before the startup hook.");
        if (SourceKnownGenerationTime.Policy.Epoch != ExpectedEpoch)
            throw new InvalidOperationException("Configured epoch was not initialized before the startup hook.");
    }

    protected override void ConfigureApplicationBuilder(WebApplicationBuilder builder, IAppSettings settings)
    {
        builder.Logging.ClearProviders();
        ConfigureWebHostBuilder(settings, builder.WebHost);
    }

    protected override Task AddServicesAsync(WebApplicationBuilder builder, IAppSettings settings, IScopedLog scopedLog)
    {
        _servicesCalled = true;
        VerifyHook();
        return Task.CompletedTask;
    }

    protected override void ConfigureApplication(WebApplication application, IAppSettings settings) { }
    protected override void ValidateEndpoints(WebApplication application, IAppSettings settings) { }
    protected override Task ValidateServicesAsync(WebApplication application, IScopedLog scopedLog) => Task.CompletedTask;

    private sealed class ProbeEntity(long id = 0) : SourceKnownEntity(id);
}
