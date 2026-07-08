// vite.config.plugin.js — Custom Vite/Rolldown plugins for CSP compliance and scope isolation.

// Rolldown plugin: wraps every JS chunk in an IIFE for scope isolation.
// This avoids minified-name collisions between independently bundled libraries
// (e.g. Bootstrap vs Syncfusion) without requiring format:'iife' (which
// doesn't support multiple entry points).
export function iifeWrap() {
    return {
        name: 'iife-wrap',
        renderChunk(code, chunk) {
            if (chunk.fileName.endsWith('.js')) {
                return {code: `(function(){"use strict";\n${code}\n})();`, map: null};
            }
            return null;
        }
    };
}

// Strips direct eval from htmx.org's internalEval function during bundling.
// htmx uses eval for hx-vars, hx-on:*, and trigger conditions — none of which
// this project uses. config.allowEval is already false at runtime; this plugin
// physically removes the eval token for defense-in-depth and CSP auditability.
export function stripHtmxEval() {
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
                map: null
            };
        }
    };
}