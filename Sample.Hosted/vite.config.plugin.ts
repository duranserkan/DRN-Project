import { createHash } from 'node:crypto';
import { basename } from 'node:path';
import { brotliCompressSync, constants } from 'node:zlib';
import type { ManifestChunk, Plugin, Rolldown } from 'vite';

function collectRequiredOriginals(
    manifest: Record<string, ManifestChunk>,
    razorEntries: readonly string[],
): Set<string> {
    const keepOriginal = new Set<string>();
    for (const [key, entry] of Object.entries(manifest)) {
        if (!entry.isEntry || !razorEntries.includes(key)) keepOriginal.add(entry.file);
        for (const dependency of [...(entry.imports ?? []), ...(entry.dynamicImports ?? [])])
            keepOriginal.add(manifest[dependency]!.file);
        for (const file of [...(entry.assets ?? []), ...(entry.css ?? [])]) keepOriginal.add(file);
    }
    return keepOriginal;
}

function outputBytes(output: Rolldown.OutputBundle[string]): Buffer {
    return Buffer.from(output.type === 'chunk' ? output.code : output.source);
}

function compressIfSmaller(file: string, original: Buffer): Buffer | undefined {
    if (!/\.(?:js|css|svg|json|txt|xml|wasm|ttf|otf)$/i.test(file)) return undefined;
    const compressed = brotliCompressSync(original, {
        params: { [constants.BROTLI_PARAM_QUALITY]: constants.BROTLI_MAX_QUALITY },
    });
    return compressed.length < original.length ? compressed : undefined;
}

export function precompressAssets(razorEntries: readonly string[]): Plugin {
    return {
        name: 'precompress-assets',
        apply: 'build',
        generateBundle: {
            order: 'post',
            handler(_options, bundle) {
                const manifestAsset = bundle['.vite/manifest.json'];
                if (manifestAsset?.type !== 'asset') throw new Error('Expected the Vite build manifest');
                const manifest: Record<string, ManifestChunk & { hash?: string; brotliOnly?: boolean }> = JSON.parse(
                    Buffer.from(manifestAsset.source).toString('utf8'),
                );
                const keepOriginal = collectRequiredOriginals(manifest, razorEntries);
                // Literal URLs in HTML, CSS or scripts can also bypass the manifest.
                const outputs = Object.values(bundle).filter((output) => output.fileName !== '.vite/manifest.json');
                const bodies = outputs.map(outputBytes);
                for (const [file, entries] of Map.groupBy(Object.values(manifest), (entry) => entry.file)) {
                    const output = bundle[file];
                    if (!output) throw new Error(`Missing manifest output: ${file}`);
                    const original = outputBytes(output);
                    const hash = createHash('sha256').update(original).digest('base64');
                    const br = compressIfSmaller(file, original);
                    if (br) this.emitFile({ type: 'asset', fileName: file + '.br', source: br });
                    const brotliOnly =
                        br !== undefined &&
                        !keepOriginal.has(file) &&
                        !bodies.some((body) => body.includes(basename(file)));
                    if (brotliOnly) delete bundle[file];
                    for (const entry of entries) {
                        entry.hash = hash;
                        if (brotliOnly) entry.brotliOnly = true;
                    }
                }
                manifestAsset.source = JSON.stringify(manifest, null, 2) + '\n';
            },
        },
    };
}

// Rolldown plugin: wraps every JS chunk in an IIFE for scope isolation.
// This avoids minified-name collisions between independently bundled libraries
// (e.g. Bootstrap vs Syncfusion) without requiring format:'iife' (which
// doesn't support multiple entry points).
export function iifeWrap(): Plugin {
    return {
        name: 'iife-wrap',
        outputOptions(options) {
            // The minifier assumes ESM strictness. Preserve it when the chunk runs as a classic script.
            options.postBanner = '(function(){"use strict";';
            options.postFooter = '})();';
            return options;
        },
    };
}

// Strips direct eval from htmx.org's internalEval function during bundling.
// htmx uses eval for hx-vars, hx-on:*, and trigger conditions — none of which
// this project uses. config.allowEval is already false at runtime; this plugin
// physically removes the eval token for defense-in-depth and CSP auditability.
export function stripHtmxEval(): Plugin {
    const htmxPathRegex = /(?:^|[/\\])htmx\.org(?:[/\\]|$)/;
    return {
        name: 'strip-htmx-eval',
        transform(code, id) {
            // Precise path matching resolves CodeQL "Incomplete URL substring sanitization" warning
            if (!htmxPathRegex.test(id)) return null;
            const target = 'return eval(str)';
            if (!code.includes(target)) return null;
            return {
                code: code.replace(target, 'return undefined'),
                map: null,
            };
        },
    };
}
