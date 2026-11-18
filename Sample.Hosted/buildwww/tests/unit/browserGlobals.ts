import { onTestFinished } from 'vitest';
export function installBrowserGlobals(globals: Record<string, unknown>, cleanup = onTestFinished) {
    for (const [key, value] of Object.entries(globals)) {
        const original = Object.getOwnPropertyDescriptor(globalThis, key);
        cleanup(() => {
            if (original) Object.defineProperty(globalThis, key, original);
            else Reflect.deleteProperty(globalThis, key);
        });
        Object.defineProperty(globalThis, key, { configurable: true, value });
    }
}
