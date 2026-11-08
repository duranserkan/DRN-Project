import assert from 'node:assert/strict';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { brotliDecompressSync } from 'node:zlib';
import { build } from 'vite';
import { precompressAssets } from '../../../../vite.config.plugin.ts';

test('Vite emits a smaller Brotli sidecar that decodes to the final bundle', async () => {
    const entry = fileURLToPath(new URL('../../../app/ts/drn/drnUtils.ts', import.meta.url));
    const result = await build({
        configFile: false,
        publicDir: false,
        logLevel: 'silent',
        plugins: [precompressAssets()],
        build: { write: false, lib: { entry, formats: ['es'], fileName: () => 'app.js' } },
    });
    const [bundle] = Array.isArray(result) ? result : [result];
    assert.ok(bundle && 'output' in bundle);
    const original = bundle.output.find((file) => file.fileName === 'app.js');
    const br = bundle.output.find((file) => file.fileName === 'app.js.br');
    assert.ok(original?.type === 'chunk');
    assert.ok(br?.type === 'asset' && typeof br.source !== 'string');
    assert.ok(br.source.length < Buffer.byteLength(original.code));
    assert.equal(brotliDecompressSync(br.source).toString(), original.code);
    assert.ok(!bundle.output.some((file) => file.fileName.endsWith('.gz')));
});
