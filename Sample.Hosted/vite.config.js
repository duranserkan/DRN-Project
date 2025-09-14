// vite.config.js
import {defineConfig} from 'vite';
import {resolve} from 'path'; // Import resolve for path management
import drnUtils from './buildwww/js/drnUtils.js';

const sharedConfig = {
    // Set the base public path for assets (important for ASP.NET)
    // This should match the virtual path where your dist folder is served from
    // E.g., if served from ~/dist/, set to '/dist/'
    base: '/site-dist/',
    build: {
        // Ensure the output directory is cleaned before each build
        emptyOutDir: true,
        // Generate manifest for asset references in .NET
        manifest: true,
        rollupOptions: {
            // Control output file naming
            output: {
                // Add hashes for cache busting
                entryFileNames: `[name].[hash].js`,
                chunkFileNames: `[name].[hash].js`,
                assetFileNames: `[name].[hash].[ext]`
            }
        }
    },
    resolve: {
        alias: {
            // It should point to your node_modules directory
            // Vite usually handles this automatically, but good to be explicit if issues arise
            '@scss': resolve(__dirname, 'buildwww/scss'),
            '@css': resolve(__dirname, 'buildwww/css'),
            '@js': resolve(__dirname, 'buildwww/js'),
            '@ts': resolve(__dirname, 'buildwww/ts'),
        }
    }
};

const builds = {
    app: {
        build: {
            // Output directory relative to the project root
            outDir: 'wwwroot/site-dist/app',
            rollupOptions: {
                input: {
                    site_preload: resolve(__dirname, 'buildwww/js/site-preload.js')
                }
            },
        },
    },
    bootstrap: {
        // Set the base public path for assets (important for ASP.NET)
        // This should match the virtual path where your dist folder is served from
        // E.g., if served from ~/dist/, set to '/dist/'
        build: {
            // Output directory relative to the project root
            outDir: 'wwwroot/site-dist/lib/bootstrap',
            rollupOptions: {
                // Define entry points. These are the files Vite will bundle.
                input: {
                    // Key is the output name (e.g., bootstrap), value is the input file path
                    bootstrap: resolve(__dirname, 'buildwww/scss/bootstrap.scss') // This will output bootstrap.[hash].css
                }
            }
        },
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
                // Color Palette
                $primary: #3d5f6c; // Example: Change primary color to green pastel
                
                // Design Preferences
                $enable-shadows: false;
                $enable-gradients: true;
                
                // Optional: Ensure text contrast
                $min-contrast-ratio: 4.5;
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