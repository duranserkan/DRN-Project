using DRN.Framework.Hosting.DrnProgram;
using DRN.Framework.Hosting.DrnProgram.Configurators;
using DRN.Framework.Hosting.Utils.Vite;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace DRN.Test.Unit.Tests.Framework.Hosting;

public class StaticFileHeaderTests
{
    [Fact]
    public async Task Startup_Configuration_Should_Override_The_Default_Through_Options()
    {
        var services = new ServiceCollection();
        services.Configure<DrnStaticFileHeaderOptions>(options =>
            options.Set(new("/robots.txt", HeaderNames.CacheControl, "no-store")));
        services.Configure<StaticFileOptions>(DrnStaticFileConfigurator.ConfigureStaticFileOptions);
        services.AddOptions<StaticFileOptions>()
            .PostConfigure<IOptions<DrnStaticFileHeaderOptions>>(DrnStaticFileConfigurator.ConfigureHeaderRules);
        await using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<StaticFileOptions>>().Value;
        var context = new DefaultHttpContext
        {
            Request =
            {
                Path = "/robots.txt"
            }
        };

        await Prepare(options, context);

        context.Response.Headers.CacheControl.ToString().Should().Be("no-store");
    }

    [Theory]
    [DataInlineUnit("/robots.txt", "", "", "no-cache")]
    [DataInlineUnit("/ROBOTS.TXT", "/app", "", "no-cache")]
    [DataInlineUnit("/static/robots.txt", "/app", "/static", "no-cache")]
    [DataInlineUnit("/STATIC/ROBOTS.TXT", "", "/static", "no-cache")]
    [DataInlineUnit("/static/app.js", "", "/static", "public,max-age=31536000")]
    [DataInlineUnit("/static-files/robots.txt", "", "/static", "public,max-age=31536000")]
    [DataInlineUnit("/static", "", "/static", "public,max-age=31536000")]
    [DataInlineUnit("/static/robots.txt/", "", "/static", "public,max-age=31536000")]
    [DataInlineUnit("/static//robots.txt", "", "/static", "public,max-age=31536000")]
    [DataInlineUnit("/robots.txt.bak", "", "", "public,max-age=31536000")]
    [DataInlineUnit("/images/robots.txt", "", "", "public,max-age=31536000")]
    [DataInlineUnit("/app.js", "", "", "public,max-age=31536000")]
    public async Task Defaults_Should_Match_Only_The_Exact_Static_Path(
        string path, string pathBase, string requestPath, string expected)
    {
        var options = new StaticFileOptions { RequestPath = requestPath };
        DrnStaticFileConfigurator.ConfigureStaticFileOptions(options);
        DrnStaticFileConfigurator.ConfigureViteSidecars(options, Substitute.For<IViteManifest>());
        DrnStaticFileConfigurator.ConfigureHeaderRules(options, Options.Create(new DrnStaticFileHeaderOptions()));
        var context = new DefaultHttpContext
        {
            Request =
            {
                Path = path,
                PathBase = pathBase
            }
        };

        await Prepare(options, context);

        context.Response.Headers.CacheControl.ToString().Should().Be(expected);
    }

    [Theory]
    [DataInlineUnit(DrnStaticFileHeaderBehavior.Override, "configured")]
    [DataInlineUnit(DrnStaticFileHeaderBehavior.IfMissing, "async")]
    public async Task Rules_Should_Run_After_Both_Callbacks_And_Honor_Write_Behavior(
        DrnStaticFileHeaderBehavior behavior, string expected)
    {
        var options = new StaticFileOptions
        {
            OnPrepareResponse = response => response.Context.Response.Headers["X-Test"] = "sync",
            OnPrepareResponseAsync = response =>
            {
                response.Context.Response.Headers["X-Test"].ToString().Should().Be("sync");
                response.Context.Response.Headers["X-Test"] = "async";
                return Task.CompletedTask;
            }
        };
        var rules = new DrnStaticFileHeaderOptions();
        rules.Set(new DrnStaticFileHeaderRule("/robots.txt", "X-Test", "configured", behavior));
        rules.Set(new DrnStaticFileHeaderRule("/robots.txt", "X-Missing", "added", DrnStaticFileHeaderBehavior.IfMissing));
        DrnStaticFileConfigurator.ConfigureHeaderRules(options, Options.Create(rules));
        var context = new DefaultHttpContext
        {
            Request =
            {
                Path = "/robots.txt"
            }
        };

        await Prepare(options, context);

        context.Response.Headers["X-Test"].ToString().Should().Be(expected);
        context.Response.Headers["X-Missing"].ToString().Should().Be("added");
    }

    [Fact]
    public async Task Rules_Should_Upsert_Remove_And_Snapshot_Startup_Configuration()
    {
        var rules = new DrnStaticFileHeaderOptions();
        var view = rules.Rules;
        rules.Set(new DrnStaticFileHeaderRule("/ROBOTS.TXT", "cache-control", "private,no-cache"));
        rules.Rules.Should().BeSameAs(view);
        rules.Rules.Should().ContainSingle();
        var options = new StaticFileOptions();
        DrnStaticFileConfigurator.ConfigureStaticFileOptions(options);
        DrnStaticFileConfigurator.ConfigureHeaderRules(options, Options.Create(rules));
        rules.Remove("/robots.txt", HeaderNames.CacheControl).Should().BeTrue();
        rules.Remove("/robots.txt", HeaderNames.CacheControl).Should().BeFalse();
        view.Should().BeEmpty();
        var context = new DefaultHttpContext
        {
            Request =
            {
                Path = "/robots.txt"
            }
        };
        await Prepare(options, context);
        context.Response.Headers.CacheControl.ToString().Should().Be("private,no-cache");

        var withoutRule = new StaticFileOptions();
        DrnStaticFileConfigurator.ConfigureStaticFileOptions(withoutRule);
        var prepareAsync = withoutRule.OnPrepareResponseAsync;
        DrnStaticFileConfigurator.ConfigureHeaderRules(withoutRule, Options.Create(rules));
        withoutRule.OnPrepareResponseAsync.Should().BeSameAs(prepareAsync);
        await Prepare(withoutRule, context);
        context.Response.Headers.CacheControl.ToString().Should().Be("public,max-age=31536000");
    }

    [Theory]
    [DataInlineUnit("/static/ASSETS/app.css", "stylesheet", "nosniff")]
    [DataInlineUnit("/static/assets/app.js", "script", null)]
    [DataInlineUnit("/static/assets/missing.css", null, null)]
    public async Task Rules_Should_Index_Paths_Without_Mixing_Headers_Or_Changing_The_Snapshot(
        string path, string? expectedAsset, string? expectedContentTypeOptions)
    {
        var rules = new DrnStaticFileHeaderOptions();
        rules.Set(new("/assets/app.css", "X-Asset", "stylesheet"));
        rules.Set(new("/ASSETS/APP.CSS", HeaderNames.XContentTypeOptions, "nosniff"));
        rules.Set(new("/assets/app.js", "X-Asset", "script"));
        var options = new StaticFileOptions { RequestPath = "/static" };
        DrnStaticFileConfigurator.ConfigureStaticFileOptions(options);
        DrnStaticFileConfigurator.ConfigureHeaderRules(options, Options.Create(rules));
        rules.Set(new("/assets/app.css", "X-Asset", "changed"));
        rules.Remove("/assets/app.js", "X-Asset");
        var context = new DefaultHttpContext
        {
            Request =
            {
                Path = path
            }
        };

        await Prepare(options, context);

        context.Response.Headers["X-Asset"].ToString().Should().Be(expectedAsset ?? string.Empty);
        context.Response.Headers[HeaderNames.XContentTypeOptions].ToString().Should().Be(expectedContentTypeOptions ?? string.Empty);
        context.Response.Headers.CacheControl.ToString().Should().Be("public,max-age=31536000");
    }

    [Theory]
    [DataInlineUnit("", "X-Test", "ok", DrnStaticFileHeaderBehavior.Override)]
    [DataInlineUnit("//external", "X-Test", "ok", DrnStaticFileHeaderBehavior.Override)]
    [DataInlineUnit("/file?query", "X-Test", "ok", DrnStaticFileHeaderBehavior.Override)]
    [DataInlineUnit("/file#fragment", "X-Test", "ok", DrnStaticFileHeaderBehavior.Override)]
    [DataInlineUnit("/*", "X-Test", "ok", DrnStaticFileHeaderBehavior.Override)]
    [DataInlineUnit("/file\\name", "X-Test", "ok", DrnStaticFileHeaderBehavior.Override)]
    [DataInlineUnit("/file\tname", "X-Test", "ok", DrnStaticFileHeaderBehavior.Override)]
    [DataInlineUnit("/file", "Invalid Header", "ok", DrnStaticFileHeaderBehavior.Override)]
    [DataInlineUnit("/file", "X-Test", "value\r\nInjected: true", DrnStaticFileHeaderBehavior.Override)]
    [DataInlineUnit("/file", "X-Test", "value\u007f", DrnStaticFileHeaderBehavior.Override)]
    [DataInlineUnit("/file", "X-Test", "ok", (DrnStaticFileHeaderBehavior)99)]
    public void Rules_Should_Reject_Invalid_Startup_Input(string path, string name, string value, DrnStaticFileHeaderBehavior behavior)
    {
        var create = () => new DrnStaticFileHeaderRule(path, name, value, behavior);
        create.Should().Throw<ArgumentException>();
    }

    private static async Task Prepare(StaticFileOptions options, HttpContext context)
    {
        var response = new StaticFileResponseContext(context, new NotFoundFileInfo("fixture"));
        options.OnPrepareResponse(response);
        await options.OnPrepareResponseAsync(response);
    }
}
