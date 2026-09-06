using System.IO.Compression;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.ResponseCaching;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.DependencyInjection;

namespace DRN.Framework.Hosting.DrnProgram.Configurators;

internal static class DrnCompressionConfigurator
{
    internal static void ConfigureResponseCachingOptions(ResponseCachingOptions options)
    {
        options.MaximumBodySize = 16 * 1024 * 1024; // 16 MB safety limit for memory preservation
        options.UseCaseSensitivePaths = false;
    }

    internal static void ConfigureResponseCompressionOptions(ResponseCompressionOptions options)
    {
        // Dynamic HTTPS responses remain excluded for BREACH prevention; static files opt in separately.
        options.EnableForHttps = false;
        options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
        [
            // Raw/uncompressed fonts only; WOFF and WOFF2 are already compressed.
            "font/ttf",
            "application/x-font-ttf",
            "font/otf",
            "font/opentype"
        ]);
        options.Providers.Add<BrotliCompressionProvider>();
        options.Providers.Add<GzipCompressionProvider>();
    }

    internal static void ConfigureCompressionProviders(IServiceCollection services,
        Func<CompressionLevel> configureBrotliCompressionLevel, Func<CompressionLevel> configureGzipCompressionLevel)
    {
        // Evaluate virtual hooks when options are configured, not when providers are registered.
        services.Configure<BrotliCompressionProviderOptions>(options => options.Level = configureBrotliCompressionLevel());
        services.Configure<GzipCompressionProviderOptions>(options => options.Level = configureGzipCompressionLevel());
    }

    internal static void ConfigureStaticFileOptions(StaticFileOptions options)
    {
        options.HttpsCompression = HttpsCompressionMode.Compress;
        options.OnPrepareResponse = context =>
        {
            // Allows the preceding response cache to store static bytes separately for each encoding.
            context.Context.Response.Headers.CacheControl = "public,max-age=31536000"; // 1 year
            context.Context.Response.Headers.Vary = "Accept-Encoding";
        };
    }
}
