using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using DRN.Framework.Hosting.Utils.Vite;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.FileProviders;
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

        var asset = context.GetRequiredService<IViteManifest>().GetManifestItem("buildwww/app/ts/appPreload.ts")!;
        using var original = await client.GetAsync(asset.Path);
        original.StatusCode.Should().Be(HttpStatusCode.OK);
        original.Content.Headers.ContentEncoding.Should().BeEmpty();
    }

    [Theory]
    [DataInline]
    public async Task First_Get_Should_Serve_Exact_Brotli_File_Only_On_An_Allowed_Host(DrnTestContext context)
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            using var files = new PhysicalFileProvider(root.FullName);
            context.ServiceCollection.PostConfigure<StaticFileOptions>(options => options.FileProvider = files);
            context.AddToConfiguration("AllowedHosts", "assets.example");
            using var client = await context.ApplicationContext.CreateClientAsync<SampleProgram>(
                clientOptions: new() { BaseAddress = new Uri("https://assets.example"), AllowAutoRedirect = false });
            var manifest = context.GetRequiredService<IViteManifest>();
            var item = manifest.GetManifestItem("buildwww/app/ts/appPreload.ts")!;
            var path = Path.Combine(root.FullName, item.Path.TrimStart('/'));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var original = await File.ReadAllBytesAsync(Path.Combine(manifest.ManifestRootPath, item.Path.TrimStart('/')));
            await File.WriteAllBytesAsync(path, original);
            // An explicit mid-stream flush distinguishes this valid sidecar from runtime compression.
            using var buffer = new MemoryStream();
            using (var writer = new BrotliStream(buffer, CompressionLevel.Fastest, leaveOpen: true))
            {
                var split = original.Length / 2;
                writer.Write(original.AsSpan(0, split));
                writer.Flush();
                writer.Write(original.AsSpan(split));
            }
            var expected = buffer.ToArray();
            await File.WriteAllBytesAsync(path + ".br", expected);

            var sidecarUrl = item.Path + ".br";
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
        finally
        {
            root.Delete(true);
        }
    }
}
