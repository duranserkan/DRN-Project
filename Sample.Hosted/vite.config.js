// vite.config.js
import {defineConfig} from 'vite';
import {resolve} from 'path'; // Import resolve for path management
import drnUtils from './buildwww/app/js/drn/drnUtils.js';

// Rollup plugin: wraps every JS chunk in an IIFE for scope isolation.
// This avoids minified-name collisions between independently bundled libraries
function iifeWrap() {
    return {
        name: 'iife-wrap',
        renderChunk(code, chunk) {
            if (chunk.fileName.endsWith('.js')) {
                return { code: `(function(){"use strict";\n${code}\n})();`, map: null };
            }
            return null;
        }
    };
}

const sharedConfig = {
    // Set the base public path for assets (important for ASP.NET)
    // This should match the virtual path where your dist folder is served from
    // E.g., if served from ~/dist/, set to '/dist/'
    base: '/',
    build: {
        // Ensure the output directory is cleaned before each build
        emptyOutDir: true,
        // Generate manifest for asset references in .NET
        manifest: true,
        rollupOptions: {
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
            rollupOptions: {
                // Define entry points. These are the files Vite will bundle.
                input: {
                    // Key is the output name (e.g., app_css), value is the input file path
                    app: resolve(__dirname, 'buildwww/app/css/app.css'), // This will output app.[hash].css, app.css is not used yet.
                    appPreload: resolve(__dirname, 'buildwww/app/js/appPreload.js'),
                    appPostload: resolve(__dirname, 'buildwww/app/js/appPostload.js')
                }
            },
        },
        esbuild: {
            keepNames: true
        },
    },
    htmx: {
        build: {
            // Output directory relative to the project root
            outDir: 'wwwroot/lib/htmx',
            rollupOptions: {
                // Define entry points. These are the files Vite will bundle.
                input: {
                    // Key is the output name (e.g., app_css), value is the input file path
                    htmxBundle: resolve(__dirname, 'buildwww/lib/htmx/htmxBundle.js'), // This will output htmx_bundle.[hash].css
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
            preserveEntrySignatures: 'strict',
            rollupOptions: {
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
                    silenceDeprecations: [
                        'import',
                        'color-functions',
                        'global-builtin',
                        'if-function',
                    ],
                    additionalData: `
                `
                }
            }
        }
    },
};

// Select build based on environment variable
const buildType = process.env.BUILD_TYPE || 'app';

// Merge shared config with build-specific config
export default defineConfig(drnUtils.deepMerge(sharedConfig, builds[buildType]));