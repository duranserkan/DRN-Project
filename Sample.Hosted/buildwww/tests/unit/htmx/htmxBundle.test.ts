import assert from 'node:assert/strict';
import { registerHooks } from 'node:module';
import { before, test } from 'node:test';
import { installBrowserGlobals } from '../browserGlobals.ts';

type Transform = (text: string, xhr: Pick<XMLHttpRequest, 'getResponseHeader' | 'responseURL'>) => string;
const extensions = new Map<string, { transformResponse: Transform }>();
const events: [object, string][] = [];
const htmx = {
    config: { selfRequestsOnly: false, allowEval: true, refreshOnHistoryMiss: false, historyCacheSize: 10, timeout: 0 },
    defineExtension: (name: string, extension: { transformResponse: Transform }) => extensions.set(name, extension),
    trigger: (element: object, event: string) => events.push([element, event]),
};
class Element {}
const document = Object.assign(new EventTarget(), { readyState: 'loading', body: new Element() });
const bootstrapCalls: unknown[] = [];
const removeCalls: unknown[] = [];
class ValidationService {
    bootstrap(options: unknown) {
        bootstrapCalls.push(options);
    }
    remove(root: unknown) {
        removeCalls.push(root);
    }
}

before(async (t) => {
    assert.ok('mock' in t);
    installBrowserGlobals(t, {
        document,
        Element,
        window: { location: new URL('https://app.example/profile') },
        testHtmx: htmx,
        testValidationService: ValidationService,
    });
    const hook = registerHooks({
        resolve(specifier, context, nextResolve) {
            if (specifier === 'htmx.org')
                return { shortCircuit: true, url: 'data:text/javascript,export default globalThis.testHtmx' };
            if (specifier === 'aspnet-client-validation')
                return {
                    shortCircuit: true,
                    url: 'data:text/javascript,export const ValidationService = globalThis.testValidationService',
                };
            return nextResolve(specifier, context);
        },
    });
    await import('../../../lib/htmx/htmxBundle.ts').finally(() => hook.deregister());
});

test('bundle configures HTMX, exposes onmount and aborts requests only on removed elements', async () => {
    const { default: onmount } = await import('onmount');
    assert.equal(window.htmx, htmx);
    assert.equal(window.onmount, onmount);
    assert.deepEqual(htmx.config, {
        selfRequestsOnly: true,
        allowEval: false,
        refreshOnHistoryMiss: true,
        historyCacheSize: 0,
        timeout: 30000,
    });
    const removed = new Element();
    for (const elt of [removed, null])
        document.dispatchEvent(new CustomEvent('htmx:beforeCleanupElement', { detail: { elt } }));
    assert.deepEqual(events, [[removed, 'htmx:abort']]);
});

test('safe-nonce requires the header and all effective CSP nonce lists to agree', (t) => {
    const transform = extensions.get('safe-nonce')?.transformResponse;
    assert.ok(transform);
    const candidates = ['header', 'policy', '', null];
    class Parser {
        parseFromString(text: string, type: string) {
            assert.equal(text, '<main></main>');
            assert.equal(type, 'text/html');
            const retained = new Set(candidates);
            return {
                querySelectorAll(selector: string) {
                    if (selector === 'template' || selector === '[hx-ext]' || selector === '[data-hx-ext]') return [];
                    assert.equal(selector, 'script');
                    return candidates.map((nonce) => ({
                        getAttribute: () => nonce,
                        remove: () => retained.delete(nonce),
                    }));
                },
                documentElement: {
                    get outerHTML() {
                        return JSON.stringify([...retained]);
                    },
                },
            };
        }
    }
    installBrowserGlobals(t, { DOMParser: Parser });
    const policy = "default-src 'self'; script-src 'nonce-policy'";
    for (const [responseURL, header, csp, allowed] of [
        ['/same-origin', 'header', null, ['header']],
        ['/same-origin', 'header', policy, []],
        ['/same-origin', 'header', "script-src 'nonce-header' 'nonce-policy'", ['header']],
        ['/same-origin', null, "default-src 'nonce-policy'; script-src 'none'", []],
        ['/same-origin', null, "script-src 'nonce-policy'; script-src-elem 'none'", []],
        ['/same-origin', null, "script-src 'none'; script-src 'nonce-policy'", []],
        ['/same-origin', null, "script-src-attr 'nonce-policy'", []],
        ['/same-origin', 'header', "img-src 'none'", ['header']],
        ['/same-origin', null, "script-src 'nonce-header' 'nonce-policy', default-src 'nonce-policy'", ['policy']],
        ['/same-origin', null, "script-src 'nonce-policy', script-src-elem 'none'", []],
        ['/same-origin', null, policy, ['policy']],
        ['/same-origin', '', policy, ['policy']],
        ['/same-origin', null, "default-src 'nonce-policy'", ['policy']],
        ['/same-origin', null, "script-src 'nonce-Policy'", []],
        ['/same-origin', null, null, []],
        ['/same-origin', '   ', null, []],
        ['https://other.example/', 'header', null, []],
        ['https://[', 'header', null, []],
    ] as const) {
        const headers = new Headers();
        if (header !== null) headers.set('HX-Nonce', header);
        if (csp !== null) headers.set('Content-Security-Policy', csp);
        assert.equal(
            transform('<main></main>', {
                responseURL,
                getResponseHeader: (name) => headers.get(name),
            }),
            JSON.stringify(allowed),
        );
    }
});

test('safe-nonce returns an empty response when filtering throws', (t) => {
    const transform = extensions.get('safe-nonce')!.transformResponse;
    class Parser {
        parseFromString() {
            throw new Error('parser unavailable');
        }
    }
    installBrowserGlobals(t, { DOMParser: Parser });
    assert.equal(transform('<script></script>', { responseURL: '/swap', getResponseHeader: () => null }), '');
});

test('validation waits for the body and releases removed form indexes without dropping surviving fields', (t) => {
    assert.deepEqual(bootstrapCalls, []);
    document.dispatchEvent(new Event('DOMContentLoaded'));
    assert.deepEqual(bootstrapCalls, [{ root: document.body, watch: true }]);
    const detached = new Element();
    const kept = new Element();
    const removedField = t.mock.fn();
    const removedForm = t.mock.fn();
    const retainedHandler = t.mock.fn();
    const indexes = {
        elementUIDs: [
            { node: detached, uid: 'old' },
            { node: kept, uid: 'kept' },
        ],
        formInputs: { mixed: ['old', 'kept'], empty: ['old'] },
        formEvents: { empty: { remove: removedForm }, mixed: { remove: retainedHandler } },
        inputEvents: { old: { remove: removedField }, kept: { remove: retainedHandler } },
        messageFor: { empty: {}, mixed: {} },
        elementByUID: { old: detached, kept, empty: {} },
        validators: { old: {}, kept: {} },
        summary: { empty: {} },
    };
    const service = window.DRN.Validation;
    assert.ok(service);
    Object.assign(service, indexes);
    const root = { contains: (node: Element) => node === detached } as unknown as ParentNode;
    service.remove(root);
    assert.deepEqual(removeCalls, [root]);
    assert.deepEqual(Reflect.get(service, 'elementUIDs'), [{ node: kept, uid: 'kept' }]);
    assert.deepEqual(indexes.formInputs, { mixed: ['kept'] });
    assert.deepEqual(indexes.elementByUID, { kept });
    assert.deepEqual(indexes.validators, { kept: {} });
    assert.deepEqual(indexes.summary, {});
    assert.deepEqual(indexes.messageFor, { mixed: {} });
    assert.equal(removedField.mock.callCount(), 1);
    assert.equal(removedForm.mock.callCount(), 1);
    assert.equal(retainedHandler.mock.callCount(), 0);
});
