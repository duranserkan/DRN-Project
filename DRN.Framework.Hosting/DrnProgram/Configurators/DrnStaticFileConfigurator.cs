using System.Collections.Frozen;
using DRN.Framework.Hosting.Utils.Vite;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Options;

namespace DRN.Framework.Hosting.DrnProgram.Configurators;

internal static class DrnStaticFileConfigurator
{
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
            var requestPath = context.Context.Request.Path;
            var prefix = options.RequestPath;
            if (requestPath.StartsWithSegments(prefix) &&
                types.IsSidecar(requestPath.Value.AsSpan()[(prefix.Value?.Length ?? 0)..]))
                context.Context.Response.Headers.ContentEncoding = "br";
            prepare?.Invoke(context);
        };
    }

    internal static void ConfigureHeaderRules(StaticFileOptions options, IOptions<DrnStaticFileHeaderOptions> headerOptions)
    {
        var rules = headerOptions.Value.Rules.ToArray();
        if (rules.Length == 0)
            return;

        FrozenDictionary<string, DrnStaticFileHeaderRule[]>.AlternateLookup<ReadOnlySpan<char>>? rulesByPath = null;
        if (rules.Length > 1)
            rulesByPath = rules.GroupBy(rule => rule.Path.Value!, StringComparer.OrdinalIgnoreCase)
                .ToFrozenDictionary(group => group.Key, group => group.ToArray(), StringComparer.OrdinalIgnoreCase)
                .GetAlternateLookup<ReadOnlySpan<char>>();

        var prepareAsync = options.OnPrepareResponseAsync;
        options.OnPrepareResponseAsync = async context =>
        {
            await prepareAsync(context);
            var requestPath = context.Context.Request.Path;
            var prefix = options.RequestPath;
            if (!requestPath.StartsWithSegments(prefix))
                return;

            var path = requestPath.Value.AsSpan()[(prefix.Value?.Length ?? 0)..];
            var matchingRules = rules;
            if (rulesByPath is { } lookup)
            {
                if (!lookup.TryGetValue(path, out matchingRules))
                    return;
            }
            else if (!path.Equals(rules[0].Path.Value.AsSpan(), StringComparison.OrdinalIgnoreCase))
                return;

            var headers = context.Context.Response.Headers;
            foreach (var rule in matchingRules)
                if (rule.Behavior == DrnStaticFileHeaderBehavior.Override || !headers.ContainsKey(rule.HeaderName))
                    headers[rule.HeaderName] = rule.Value;
        };
    }

    private sealed class ViteContentTypeProvider(IContentTypeProvider original, IViteManifest manifest) : IContentTypeProvider
    {
        private readonly Lazy<FrozenDictionary<string, string>.AlternateLookup<ReadOnlySpan<char>>> _assets = new(() =>
            manifest.GetAllManifestItems()
                .Select(item => new KeyValuePair<string, string>(item.Path + ".br", item.Path))
                .ToFrozenDictionary(StringComparer.Ordinal)
                .GetAlternateLookup<ReadOnlySpan<char>>());

        public bool IsSidecar(ReadOnlySpan<char> path) =>
            path.EndsWith(".br", StringComparison.Ordinal) && _assets.Value.ContainsKey(path);

        public bool TryGetContentType(string subpath, out string contentType)
        {
            if (subpath.EndsWith(".br", StringComparison.Ordinal) && _assets.Value.TryGetValue(subpath.AsSpan(), out var originalPath))
                subpath = originalPath;

            return original.TryGetContentType(subpath, out contentType!);
        }
    }
}
