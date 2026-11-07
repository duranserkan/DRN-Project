// Custom Vite/Rolldown plugins for CSP compliance and scope isolation.
import type { Plugin } from 'vite';

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
