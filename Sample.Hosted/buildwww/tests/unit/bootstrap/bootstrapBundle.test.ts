import assert from 'node:assert/strict';
import { registerHooks } from 'node:module';
import test from 'node:test';
import { installBrowserGlobals } from '../browserGlobals.ts';

test('the Bootstrap bundle exposes the shipped component constructors on window', async (t) => {
    const document = Object.assign(new EventTarget(), { readyState: 'loading', documentElement: { dir: 'ltr' } });
    installBrowserGlobals(t, { window: new EventTarget(), document });
    // Vite resolves extensionless Bootstrap imports. Node ESM requires the .js suffix.
    const hook = registerHooks({
        resolve(specifier, context, nextResolve) {
            return nextResolve(/^bootstrap\/js\/dist\/\w+$/.test(specifier) ? specifier + '.js' : specifier, context);
        },
    });
    t.after(() => hook.deregister());
    await import('../../../lib/bootstrap/bootstrapBundle.ts');
    for (const name of [
        'Alert',
        'Button',
        'Modal',
        'Tooltip',
        'Toast',
        'Collapse',
        'Dropdown',
        'Tab',
        'Popover',
        'Offcanvas',
    ] as const) {
        assert.equal(typeof window.bootstrap[name], 'function');
        assert.equal(Reflect.get(window.bootstrap[name], 'NAME'), name.toLowerCase());
    }
});
