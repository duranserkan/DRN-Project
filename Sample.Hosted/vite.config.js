// vite.config.js
import {defineConfig} from 'vite';
import {resolve} from 'path'; // Import resolve for path management
import drnUtils from './buildwww/app/js/drn/drnUtils.js';
import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';

// Rolldown plugin: wraps every JS chunk in an IIFE for scope isolation.
// This avoids minified-name collisions between independently bundled libraries
// (e.g. Bootstrap vs Syncfusion) without requiring format:'iife' (which
// doesn't support multiple entry points).
function iifeWrap() {
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
function stripHtmxEval() {
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

const sharedConfig = {
    // Set the base public path for assets (important for ASP.NET)
    // This should match the virtual path where your dist folder is served from
    // E.g., if served from ~/dist/, set to '/dist/'
    base: '/',
    build: {
        chunkSizeWarningLimit: 6000,
        // Ensure the output directory is cleaned before each build
        emptyOutDir: true,
        // Generate manifest for asset references in .NET
        manifest: true,
        rolldownOptions: {
            // Control output file naming
            output: {
                // Add hashes for cache busting
                entryFileNames: `[name].[hash:16].js`,
                chunkFileNames: `[name].[hash:16].js`,
                assetFileNames: `[name].[hash:16].[ext]`
            }
        }
    },
    resolve: {
        alias: {
            '@scss': resolve(__dirname, 'buildwww/scss'),
            '@css': resolve(__dirname, 'buildwww/css'),
            '@js': resolve(__dirname, 'buildwww/js'),
            '@ts': resolve(__dirname, 'buildwww/ts'),
            '@plugins': resolve(__dirname, 'buildwww/plugins'),
        }
    }
};

const builds = {
    app: {
        build: {
            // Output directory relative to the project root
            outDir: 'wwwroot/app',
            rolldownOptions: {
                // Define entry points. These are the files Vite will bundle.
                input: {
                    // Key is the output name (e.g., app_css), value is the input file path
                    app: resolve(__dirname, 'buildwww/app/css/app.css'),
                    appPreload: resolve(__dirname, 'buildwww/app/js/appPreload.js')
                }
            },
        },
    },
    appPostload: {
        build: {
            // Output directory relative to the project root
            outDir: 'wwwroot/appPostload',
            rolldownOptions: {
                // Define entry points. These are the files Vite will bundle.
                input: {
                    appPostload: resolve(__dirname, 'buildwww/app/js/appPostload.js')
                }
            },
        },
    },
    htmx: {
        plugins: [stripHtmxEval()],
        build: {
            // Output directory relative to the project root
            outDir: 'wwwroot/lib/htmx',
            rolldownOptions: {
                input: {
                    htmxBundle: resolve(__dirname, 'buildwww/lib/htmx/htmxBundle.js'),
                }
            },
        },
    },
    bootstrap: {
        // Relative base ensures @font-face url() in compiled CSS resolves
        // relative to the CSS file's location (wwwroot/lib/bootstrap/)
        base: './',
        build: {
            outDir: 'wwwroot/lib/bootstrap',
            rolldownOptions: {
                preserveEntrySignatures: 'strict',
                input: {
                    bootstrap: resolve(__dirname, 'buildwww/lib/bootstrap/bootstrap.scss'),
                    bootstrapBundle: resolve(__dirname, 'buildwww/lib/bootstrap/bootstrapBundle.js'),
                }
            }
        },
        plugins: [iifeWrap()],
        css: {
            preprocessorOptions: {
                scss: {
                    api: 'modern-compiler',
                    // TODO: revisit when Bootstrap 6 removes legacy Sass APIs
                    // These are triggered by Bootstrap 5 source, not our custom SCSS
                    silenceDeprecations: [
                        'import',
                        'color-functions',
                        'global-builtin',
                        'if-function',
                    ]
                }
            }
        }
    },
    react: {
        plugins: [
            react(), // Enable JSX support specifically for this bundle
            tailwindcss()
        ],
        build: {
            outDir: 'wwwroot/lib/react',
            rolldownOptions: {
                input: {
                    // This outputs reactBundle.[hash].js and reactBundle.[hash].css
                    reactBundle: resolve(__dirname, 'buildwww/lib/react/reactBundle.tsx'),
                },
                output: {
                    format: 'iife',
                    name: 'DrnReactMicroFrontend'
                },
                // IIFE format does not support import.meta; React/ReactDOM internals may
                // reference it for environment detection. Vite resolves these at build time,
                // so the residual import.meta in IIFE output is inert. Suppress the warning.
                transform: {
                    define: {'import.meta': '{}'}
                }
            }
        }
    },
};

// Select build based on environment variable
const buildType = process.env.BUILD_TYPE || 'app';

// Merge shared config with build-specific config
export default defineConfig(drnUtils.deepMerge(sharedConfig, builds[buildType]));