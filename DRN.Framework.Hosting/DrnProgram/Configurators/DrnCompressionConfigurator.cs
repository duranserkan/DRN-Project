using System.Collections.Frozen;
using DRN.Framework.Hosting.Utils.Vite;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.ResponseCaching;
using Microsoft.AspNetCore.StaticFiles;

namespace DRN.Framework.Hosting.DrnProgram.Configurators;

internal static class DrnCompressionConfigurator
{
    internal static void ConfigureResponseCachingOptions(ResponseCachingOptions options)
    {
        options.MaximumBodySize = 16 * 1024 * 1024; // 16 MB safety limit for memory preservation
        options.UseCaseSensitivePaths = false;
    }

    internal static void ConfigureStaticFileOptions(StaticFileOptions options)
    {
        options.OnPrepareResponse = context =>
        {
            context.Context.Response.Headers.CacheControl = "public,max-age=31536000"; // 1 year
        };
    }

    internal static void ConfigureViteSidecars(StaticFileOptions options, IViteManifest manifest)
    {
        var types = new ViteContentTypeProvider(options.ContentTypeProvider ?? new FileExtensionContentTypeProvider(), manifest);
        options.ContentTypeProvider = types;
        var prepare = options.OnPrepareResponse;
        options.OnPrepareResponse = context =>
        {
            if (context.Context.Request.Path.StartsWithSegments(options.RequestPath, out var path) &&
                path.Value is { } value && types.IsSidecar(value))
                context.Context.Response.Headers.ContentEncoding = "br";
            prepare?.Invoke(context);
        };
    }

    private sealed class ViteContentTypeProvider(IContentTypeProvider original, IViteManifest manifest) : IContentTypeProvider
    {
        private readonly Lazy<FrozenSet<string>> _assets = new(() =>
            manifest.GetAllManifestItems().Select(item => item.Path + ".br").ToFrozenSet(StringComparer.Ordinal));

        public bool IsSidecar(string path) => path.EndsWith(".br", StringComparison.Ordinal) && _assets.Value.Contains(path);

        public bool TryGetContentType(string subpath, out string contentType) =>
            original.TryGetContentType(IsSidecar(subpath) ? subpath[..^3] : subpath, out contentType!);
    }
}
