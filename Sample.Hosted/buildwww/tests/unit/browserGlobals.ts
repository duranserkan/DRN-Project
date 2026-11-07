import type { TestContext } from 'node:test';

export function installBrowserGlobals(t: TestContext, globals: Record<string, unknown>) {
    for (const [key, value] of Object.entries(globals)) {
        const original = Object.getOwnPropertyDescriptor(globalThis, key);
        t.after(() => {
            if (original) Object.defineProperty(globalThis, key, original);
            else Reflect.deleteProperty(globalThis, key);
        });
        Object.defineProperty(globalThis, key, { configurable: true, value });
    }
}
