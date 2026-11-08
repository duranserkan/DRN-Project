import { brotliCompressSync, constants } from 'node:zlib';
import type { Plugin } from 'vite';

export function precompressAssets(): Plugin {
    return {
        name: 'precompress-assets',
        apply: 'build',
        generateBundle: {
            order: 'post',
            handler(_options, bundle) {
                for (const output of Object.values(bundle)) {
                    if (!/\.(?:js|css|svg|json|txt|xml|wasm|ttf|otf)$/i.test(output.fileName)) continue;
                    if (output.fileName.startsWith('.vite/')) continue;

                    const source = output.type === 'chunk' ? output.code : output.source;
                    const original = typeof source === 'string' ? Buffer.from(source) : source;
                    const br = brotliCompressSync(original, {
                        params: { [constants.BROTLI_PARAM_QUALITY]: constants.BROTLI_MAX_QUALITY },
                    });
                    if (br.length < original.length)
                        this.emitFile({ type: 'asset', fileName: output.fileName + '.br', source: br });
                }
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
