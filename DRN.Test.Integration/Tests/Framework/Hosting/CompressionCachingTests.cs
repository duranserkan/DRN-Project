using System.Net;
using System.Net.Http.Headers;
using DRN.Framework.Hosting.Utils.Vite;
using Sample.Hosted;

namespace DRN.Test.Integration.Tests.Framework.Hosting;

/// <summary>
/// Verifies that ResponseCaching middleware caches compressed static assets server-side.
/// Cache hits are proven by the Age header (not byte equality — compression is deterministic).
/// <list type="bullet">
///   <item><see href="https://learn.microsoft.com/en-us/aspnet/core/performance/caching/middleware">Response Caching Middleware</see></item>
///   <item><see href="https://learn.microsoft.com/en-us/aspnet/core/performance/response-compression">Response Compression</see></item>
/// </list>
/// </summary>
public class CompressionCachingTests(ITestOutputHelper outputHelper)
{
    public static readonly string[] Encodings = ["br", "gzip", ""];
    
    /// <summary>
    /// For each encoding (br, gzip, identity):
    ///   1. First request → cache miss (no Age), correct Content-Encoding
    ///   2. Second request → cache hit (Age present), identical bytes
    /// Also verifies Cache-Control: public and Vary: Accept-Encoding on compressed responses,
    /// and that different encodings produce distinct bytes (separate Vary keys).
    /// </summary>
    [Theory]
    [DataInline]
    public async Task StaticAsset_CompressedResponses_Should_Be_Cached_Per_Encoding(DrnTestContext context)
    {
        var client = await context.ApplicationContext.CreateClientAsync<SampleProgram>(outputHelper);
        var manifest = context.GetRequiredService<IViteManifest>();
        var manifestItem = manifest.GetAllManifestItems().First();
        
        var firstResponses = new Dictionary<string, CacheResult>();
        var cachedResponses = new Dictionary<string, CacheResult>();

        // --- Phase 1: First request per encoding → cache miss ---
        foreach (var encoding in Encodings)
        {
            var result = await RequestAndCapture(client, manifestItem.Path, encoding);
            firstResponses[encoding] = result;

            result.StatusCode.Should().Be(HttpStatusCode.OK);
            result.Age.Should().BeNull($"first '{Label(encoding)}' request should be a cache miss");
            result.IsPublic.Should().BeTrue("static assets must have Cache-Control: public");
            result.ContentLength.Should().BePositive();

            if (encoding.Length > 0)
            {
                result.ContentEncoding.Should().Contain(encoding);
                result.Vary.Should().Contain("Accept-Encoding",
                    "Vary header must include Accept-Encoding for correct cache keying");
            }
            else
            {
                result.ContentEncoding.Should().BeEmpty();
                result.Vary.Should().Contain("Accept-Encoding",
                    "Vary must be present even for identity responses to ensure correct cache keying");
            }
        }

        // --- Phase 2: Second request per encoding → cache hit ---
        foreach (var encoding in Encodings)
        {
            var result = await RequestAndCapture(client, manifestItem.Path, encoding);
            cachedResponses[encoding] = result;

            result.StatusCode.Should().Be(HttpStatusCode.OK);
            result.Age.Should().NotBeNull($"second '{Label(encoding)}' request should be a cache hit");
            result.ContentEncoding.Should().BeEquivalentTo(firstResponses[encoding].ContentEncoding,
                $"cached '{Label(encoding)}' response must preserve Content-Encoding");
            result.Bytes.Should().BeEquivalentTo(firstResponses[encoding].Bytes);
        }

        firstResponses.Count.Should().Be(cachedResponses.Count);
        // --- Phase 3: Distinct bytes per encoding (separate Vary cache keys) ---
        firstResponses["br"].Bytes.Should().NotBeEquivalentTo(firstResponses["gzip"].Bytes,
            "brotli and gzip must produce different bytes (separate Vary cache keys)");
        firstResponses["br"].Bytes.Should().NotBeEquivalentTo(firstResponses[""].Bytes,
            "brotli bytes must differ from uncompressed identity bytes");
        firstResponses["gzip"].Bytes.Should().NotBeEquivalentTo(firstResponses[""].Bytes,
            "gzip bytes must differ from uncompressed identity bytes");
    }

    /// <summary>
    /// Verifies cache poisoning risk: if an identity request (no Accept-Encoding) caches first
    /// without Vary header, a subsequent brotli request may receive uncompressed content
    /// because ResponseCachingMiddleware treats it as the same cache key.
    /// </summary>
    [Theory]
    [DataInline]
    public async Task IdentityRequestFirst_Should_Not_Poison_Cache_For_Compressed_Requests(DrnTestContext context)
    {
        var client = await context.ApplicationContext.CreateClientAsync<SampleProgram>(outputHelper);
        var manifest = context.GetRequiredService<IViteManifest>();
        var manifestItem = manifest.GetAllManifestItems().First();
        
        // Identity request first — even with Vary header, verify no cross-encoding cache poisoning
        var identity = await RequestAndCapture(client, manifestItem.Path, "");
        identity.StatusCode.Should().Be(HttpStatusCode.OK);
        identity.ContentEncoding.Should().BeEmpty();

        // Brotli request — does it get the identity cached response or a fresh compressed one?
        var brotli = await RequestAndCapture(client, manifestItem.Path, "br");
        brotli.StatusCode.Should().Be(HttpStatusCode.OK);

        if (brotli.Age.HasValue)
        {
            // Cache hit — identity response was served to brotli request (cache poisoning)
            outputHelper.WriteLine("CACHE POISONING DETECTED: brotli request received cached identity response");
            brotli.ContentEncoding.Should().BeEmpty("poisoned cache serves uncompressed content");
            brotli.Bytes.Should().BeEquivalentTo(identity.Bytes, "poisoned response returns identity bytes");
            Assert.Fail("Cache poisoning detected: brotli request received cached identity response");
        }
        else
        {
            // Cache miss — ResponseCachingMiddleware correctly differentiated the requests
            outputHelper.WriteLine("NO POISONING: brotli request was a cache miss (correctly keyed)");
            brotli.ContentEncoding.Should().Contain("br");
            brotli.Bytes.Should().NotBeEquivalentTo(identity.Bytes);
        }
    }

    private static async Task<CacheResult> RequestAndCapture(HttpClient client, string path, string encoding)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (encoding.Length > 0)
            request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue(encoding));

        var response = await client.SendAsync(request);
        var bytes = await response.Content.ReadAsByteArrayAsync();

        return new CacheResult(
            StatusCode: response.StatusCode,
            Age: response.Headers.Age,
            ContentEncoding: response.Content.Headers.ContentEncoding,
            Vary: response.Headers.Vary,
            IsPublic: response.Headers.CacheControl?.Public ?? false,
            ContentLength: bytes.Length,
            Bytes: bytes);
    }

    private static string Label(string encoding) => encoding.Length > 0 ? encoding : "identity";
}

public record CacheResult(
    HttpStatusCode StatusCode,
    TimeSpan? Age,
    ICollection<string> ContentEncoding,
    ICollection<string> Vary,
    bool IsPublic,
    long ContentLength,
    byte[] Bytes);