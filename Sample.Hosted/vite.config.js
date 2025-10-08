// vite.config.js
import {defineConfig} from 'vite';
import {resolve} from 'path'; // Import resolve for path management
import drnUtils from './buildwww/app/js/drnUtils.js';

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
                assetFileNames: `[name].[hash:16].[ext]`,
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
                    // app: resolve(__dirname, 'buildwww/app/css/app.css'), // This will output app.[hash].css, app.css is not used yet.
                    app_preload: resolve(__dirname, 'buildwww/app/js/app_preload.js'),
                    app_postload: resolve(__dirname, 'buildwww/app/js/app_postload.js')
                }
            },
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
                    htmx_bundle: resolve(__dirname, 'buildwww/lib/htmx/htmx_bundle.js'), // This will output htmx_bundle.[hash].css
                }
            },
        },
    },
    bootstrap: {
        build: {
            // Output directory relative to the project root
            outDir: 'wwwroot/lib/bootstrap',
            rollupOptions: {
                // Define entry points. These are the files Vite will bundle.
                input: {
                    bootstrap: resolve(__dirname, 'buildwww/lib/bootstrap/bootstrap.scss'), // This will output bootstrap.[hash].css
                    bootstrap_bundle: resolve(__dirname, 'buildwww/lib/bootstrap/bootstrap.js') // This will output bootstrap_bundle.[hash].js
                }
            }
        },
        plugins: [],
        css: {
            preprocessorOptions: {
                scss: {
                    api: 'modern-compiler', // for Dart Sass modern API
                    silenceDeprecations: [
                        'import',
                        'color-functions',
                        'global-builtin',
                    ],
                    //global variables
                    additionalData: `
                `
                }
            }
        }
    }
};

// Select build based on environment variable
const buildType = process.env.BUILD_TYPE || 'app';

// Merge shared config with build-specific config
export default defineConfig(drnUtils.deepMerge(sharedConfig, builds[buildType]));