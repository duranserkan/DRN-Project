import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { registerHooks } from 'node:module';
import type { TestContext } from 'node:test';
import { fileURLToPath } from 'node:url';
import { transformSync } from 'rolldown/utils';

export function loadTsx(t: TestContext) {
    // Load the production TSX under its original URL so Node can attribute hits to source lines.
    const hooks = registerHooks({
        load(url, context, nextLoad) {
            if (url.endsWith('.css?inline')) {
                const css = readFileSync(new URL(url), 'utf8');
                return { format: 'module', shortCircuit: true, source: `export default ${JSON.stringify(css)}` };
            }
            if (!url.endsWith('.tsx')) return nextLoad(url, context);
            const filename = fileURLToPath(url);
            const { code, map, errors } = transformSync(filename, readFileSync(filename, 'utf8'), {
                jsx: { runtime: 'automatic' },
                sourcemap: true,
            });
            if (errors.length) throw errors[0];
            assert.ok(map);
            const encoded = Buffer.from(JSON.stringify({ ...map, sources: [url] })).toString('base64');
            return {
                format: 'module',
                shortCircuit: true,
                source: `${code}\n//# sourceMappingURL=data:application/json;base64,${encoded}`,
            };
        },
    });
    t.after(() => hooks.deregister());
}
