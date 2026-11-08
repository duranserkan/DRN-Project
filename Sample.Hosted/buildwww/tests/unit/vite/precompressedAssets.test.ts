import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { brotliDecompressSync } from 'node:zlib';
import { build } from 'vite';
import { precompressAssets } from '../../../../vite.config.plugin.ts';

for (const referenced of [false, true])
    test(`Vite hashes decoded bytes and respects original URL references (referenced=${referenced})`, async () => {
        const key = 'buildwww/app/ts/drn/drnUtils.ts';
        const entry = fileURLToPath(new URL('../../../app/ts/drn/drnUtils.ts', import.meta.url));
        const result = await build({
            configFile: false,
            publicDir: false,
            logLevel: 'silent',
            plugins: [
                {
                    name: 'fixture-reference',
                    generateBundle() {
                        if (referenced)
                            this.emitFile({
                                type: 'asset',
                                fileName: 'page.html',
                                source: '<script src="app.js"></script>',
                            });
                    },
                },
                precompressAssets([key]),
            ],
            build: { manifest: true, write: false, lib: { entry, formats: ['es'], fileName: () => 'app.js' } },
        });
        const [bundle] = Array.isArray(result) ? result : [result];
        assert.ok(bundle && 'output' in bundle);
        const original = bundle.output.find((file) => file.fileName === 'app.js');
        const br = bundle.output.find((file) => file.fileName === 'app.js.br');
        assert.ok(br?.type === 'asset' && typeof br.source !== 'string');
        const decoded = brotliDecompressSync(br.source);
        assert.ok(br.source.length < decoded.length);
        const manifest = bundle.output.find((file) => file.fileName === '.vite/manifest.json');
        assert.ok(manifest?.type === 'asset');
        const item = JSON.parse(Buffer.from(manifest.source).toString())[key];
        assert.equal(item.hash, createHash('sha256').update(decoded).digest('base64'));
        assert.equal(item.brotliOnly === true, !referenced);
        if (referenced) {
            assert.ok(original?.type === 'chunk');
            assert.equal(decoded.toString(), original.code);
        } else assert.equal(original, undefined);
        assert.ok(!bundle.output.some((file) => file.fileName.endsWith('.gz')));
    });
