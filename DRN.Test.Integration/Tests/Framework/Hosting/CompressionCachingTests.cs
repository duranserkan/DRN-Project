using System.Net;
using System.Net.Http.Headers;
using DRN.Framework.Hosting.Utils.Vite;
using Microsoft.AspNetCore.Hosting;
using Sample.Hosted;

namespace DRN.Test.Integration.Tests.Framework.Hosting;

public class CompressionCachingTests
{
    [Theory]
    [DataInline(false)]
    [DataInline(true)]
    public async Task Home_And_Original_Assets_Should_Be_Served_Without_Runtime_Compression(DrnTestContext context, bool https)
    {
        using var client = await context.ApplicationContext.CreateClientAsync<SampleProgram>(https: https);
        client.DefaultRequestHeaders.AcceptEncoding.ParseAdd("br, gzip");
        using var home = await client.GetAsync("/Home");
        home.StatusCode.Should().Be(HttpStatusCode.OK);
        home.Content.Headers.ContentEncoding.Should().BeEmpty();

        const string assetDirectory = "lib/onmount";
        var webRoot = context.GetRequiredService<IWebHostEnvironment>().WebRootPath;
        var assetPath = Assert.Single(Directory.GetFiles(Path.Combine(webRoot, assetDirectory), "onmount.*.js"));
        var assetName = Path.GetFileName(assetPath);
        Assert.Matches(@"\Aonmount\.(?:[A-Za-z0-9_-]{8}|[A-Za-z0-9_-]{16})\.js\z", assetName);
        using var original = await client.GetAsync($"/{assetDirectory}/{assetName}");
        original.StatusCode.Should().Be(HttpStatusCode.OK);
        original.Content.Headers.ContentEncoding.Should().BeEmpty();
        var expected = await File.ReadAllBytesAsync(assetPath);
        (await original.Content.ReadAsByteArrayAsync()).Should().Equal(expected);
    }

    [Theory]
    [DataInline]
    public async Task First_Get_Should_Serve_Exact_Brotli_File_Only_On_An_Allowed_Host(DrnTestContext context)
    {
        context.AddToConfiguration("AllowedHosts", "assets.example");
        using var client = await context.ApplicationContext.CreateClientAsync<SampleProgram>(
            clientOptions: new() { BaseAddress = new Uri("https://assets.example"), AllowAutoRedirect = false });

        var manifest = context.GetRequiredService<IViteManifest>();
        var item = manifest.GetManifestItem("buildwww/app/ts/appPreload.ts");
        Assert.NotNull(item);
        var sidecarUrl = item.BrowserPath;
        sidecarUrl.Should().EndWith(".br");
        var expected = await File.ReadAllBytesAsync(Path.Combine(manifest.ManifestRootPath, sidecarUrl.TrimStart('/')));

        using var allowed = await client.GetAsync(sidecarUrl);
        allowed.StatusCode.Should().Be(HttpStatusCode.OK);
        allowed.Headers.Age.Should().BeNull();
        allowed.Content.Headers.ContentEncoding.Should().ContainSingle().Which.Should().Be("br");
        allowed.Content.Headers.ContentType!.MediaType.Should().BeOneOf("application/javascript", "text/javascript");
        (await allowed.Content.ReadAsByteArrayAsync()).Should().Equal(expected);

        using var cached = await client.GetAsync(sidecarUrl);
        cached.Headers.Age.Should().NotBeNull();
        cached.Content.Headers.ContentEncoding.Should().Equal("br");
        (await cached.Content.ReadAsByteArrayAsync()).Should().Equal(expected);

        using var request = new HttpRequestMessage(HttpMethod.Get, sidecarUrl);
        request.Headers.Host = "unlisted.example";
        request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("br"));
        using var denied = await client.SendAsync(request);
        denied.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
