import { fileURLToPath } from 'node:url';
import { defineConfig } from 'vite';

export default defineConfig({
    root: fileURLToPath(new URL('.', import.meta.url)),
    server: {
        host: '127.0.0.1',
        port: 4175,
        strictPort: true,
        fs: { allow: [fileURLToPath(new URL('../../../', import.meta.url))] }
    }
});
