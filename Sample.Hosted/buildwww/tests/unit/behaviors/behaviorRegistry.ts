import assert from 'node:assert/strict';
import type { IDrnOnmount } from '../../../types/drnOnmount.types.ts';

export function behaviorRegistry() {
    type Callback = Parameters<IDrnOnmount['register']>[1];
    const callbacks = new Map<string, Callback>();
    return {
        register(selector: string, callback: Callback) {
            callbacks.set(selector, callback);
        },
        mount(selector: string, element: object) {
            const callback = callbacks.get(selector);
            assert.ok(callback, `Missing registered behavior: ${selector}`);
            const options: Parameters<Callback>[0] = { selector, id: 'test' };
            const result = callback.call(element as Element, options);
            return { result, disposable: options.disposable };
        },
    };
}
