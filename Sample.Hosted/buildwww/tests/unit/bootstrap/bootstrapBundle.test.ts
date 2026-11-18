import { test } from 'vitest';
import assert from 'node:assert/strict';
import { installBrowserGlobals } from '../browserGlobals.ts';

test('the Bootstrap bundle exposes the shipped component constructors on window', async () => {
    const document = Object.assign(new EventTarget(), { readyState: 'loading', documentElement: { dir: 'ltr' } });
    installBrowserGlobals({ window: new EventTarget(), document });
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
