import {defineConfig, mergeConfig} from 'vite';
import type {UserConfig} from 'vite';
import {resolve} from 'node:path'; // Import resolve for path management
import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';
import {iifeWrap, stripHtmxEval} from './vite.config.plugin.ts';

const sharedConfig: UserConfig = {
    // Resolve generated asset URLs relative to each bundle under any Request.PathBase.
    base: './',
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
            '@css': resolve(import.meta.dirname, 'buildwww/app/css'),
            '@ts': resolve(import.meta.dirname, 'buildwww/app/ts'),
            '@lib': resolve(import.meta.dirname, 'buildwww/lib'),
            '@types': resolve(import.meta.dirname, 'buildwww/types'),
            '@plugins': resolve(import.meta.dirname, 'buildwww/plugins'),
        }
    }
};

// Scope isolation rule:
// - Use iifeWrap() for classic-script builds with mixed or multiple entries.
// - Use native format:'iife' only for single-JS-entry builds that can be named.
const builds: Record<string, UserConfig> = {
    app: {
        plugins: [iifeWrap()],
        build: {
            // Output directory relative to the project root
            outDir: 'wwwroot/app',
            rolldownOptions: {
                // Define entry points. These are the files Vite will bundle.
                input: {
                    // Key is the output name (e.g., app), value is the input file path
                    app: resolve(import.meta.dirname, 'buildwww/app/css/app.css'),
                    appPreload: resolve(import.meta.dirname, 'buildwww/app/ts/appPreload.ts')
                }
            },
        },
    },
    appPostload: {
        plugins: [iifeWrap()],
        build: {
            // Output directory relative to the project root
            outDir: 'wwwroot/appPostload',
            rolldownOptions: {
                // Define entry points. These are the files Vite will bundle.
                input: {
                    appPostload: resolve(import.meta.dirname, 'buildwww/app/ts/appPostload.ts')
                }
            },
        },
    },
    layoutBehaviors: {
        plugins: [iifeWrap()],
        build: {
            outDir: 'wwwroot/behaviors/layout',
            rolldownOptions: {
                input: {
                    layoutBehaviors: resolve(import.meta.dirname, 'buildwww/app/ts/layoutBehaviors.ts')
                }
            }
        }
    },
    homeBehaviors: {
        plugins: [iifeWrap()],
        build: {
            outDir: 'wwwroot/behaviors/home',
            rolldownOptions: {
                input: {
                    homeBehaviors: resolve(import.meta.dirname, 'buildwww/app/ts/homeBehaviors.ts')
                }
            }
        }
    },
    testBehaviors: {
        plugins: [iifeWrap()],
        build: {
            outDir: 'wwwroot/behaviors/test',
            rolldownOptions: {
                input: {
                    testBehaviors: resolve(import.meta.dirname, 'buildwww/app/ts/testBehaviors.ts')
                }
            }
        }
    },
    htmx: {
        plugins: [iifeWrap(), stripHtmxEval()],
        build: {
            // Output directory relative to the project root
            outDir: 'wwwroot/lib/htmx',
            rolldownOptions: {
                input: {
                    htmxBundle: resolve(import.meta.dirname, 'buildwww/lib/htmx/htmxBundle.ts'),
                }
            },
        },
    },
    bootstrap: {
        plugins: [iifeWrap()],
        build: {
            outDir: 'wwwroot/lib/bootstrap',
            rolldownOptions: {
                preserveEntrySignatures: 'strict',
                input: {
                    bootstrap: resolve(import.meta.dirname, 'buildwww/lib/bootstrap/bootstrap.scss'),
                    bootstrapBundle: resolve(import.meta.dirname, 'buildwww/lib/bootstrap/bootstrapBundle.ts'),
                }
            }
        },
        css: {
            preprocessorOptions: {
                scss: {
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
                    reactBundle: resolve(import.meta.dirname, 'buildwww/lib/react/reactBundle.tsx'),
                },
                output: {
                    // React is a single JS entry, so native IIFE output is valid here.
                    // Mixed-entry builds above use iifeWrap() instead.
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
export default defineConfig(mergeConfig(sharedConfig, builds[buildType] ?? {}));
