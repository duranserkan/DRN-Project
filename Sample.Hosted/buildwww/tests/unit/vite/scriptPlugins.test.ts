import assert from 'node:assert/strict';
import { resolve } from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { createContext, Script } from 'node:vm';
import { build, type Plugin } from 'vite';
import { iifeWrap, stripHtmxEval } from '../../../../vite.config.plugin.ts';

async function buildScript(entryPath: string, plugin: Plugin, source?: string) {
    const entry = resolve(entryPath);
    const plugins = [plugin];
    if (source !== undefined) {
        plugins.unshift({
            name: 'fixture-entry',
            enforce: 'pre',
            resolveId: (id) => (id === entry ? entry : null),
            load: (id) => (id === entry ? source : null),
        });
    }
    const result = await build({
        configFile: false,
        publicDir: false,
        logLevel: 'silent',
        plugins,
        build: { write: false, minify: false, lib: { entry, formats: ['es'], fileName: () => 'fixture.js' } },
    });
    const [bundle] = Array.isArray(result) ? result : [result];
    assert.ok(bundle && 'output' in bundle);
    const chunk = bundle.output.find((file) => file.type === 'chunk' && file.isEntry);
    assert.ok(chunk?.type === 'chunk');
    return chunk.code;
}

test('classic scripts have independent bindings and run in strict mode', async () => {
    const code = await buildScript(
        'fixture.js',
        iifeWrap(),
        `
        var count = 0;
        globalThis.increment = () => ++count;
        globalThis.strictReceiver = function () { return this; };
    `,
    );
    const context = createContext({});
    const script = new Script(code);
    script.runInContext(context);
    const firstIncrement = context.increment;
    const receiver = context.strictReceiver;
    assert.equal(receiver(), undefined);
    script.runInContext(context);
    assert.equal(firstIncrement(), 1);
    assert.equal(context.increment(), 1);
});

test('the bundled htmx package contains no direct eval', async () => {
    const entry = fileURLToPath(import.meta.resolve('htmx.org'));
    const code = await buildScript(entry, stripHtmxEval());
    assert.doesNotMatch(code, /\beval\s*\(/);
});

test('the htmx transform preserves identical code in a similarly named package', async () => {
    const code = await buildScript(
        'htmx.org-extra/index.js',
        stripHtmxEval(),
        'export function evaluate(str) { return eval(str) }',
    );
    assert.match(code, /\beval\s*\(/);
});
