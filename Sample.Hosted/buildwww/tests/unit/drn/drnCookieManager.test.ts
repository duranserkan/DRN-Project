import test from 'node:test';
import assert from 'node:assert/strict';
import type { TestContext } from 'node:test';
import type { DrnCookieOptions } from '@/types/drnCookieManager.types';

let fixtureId = 0;

async function fixture(t: TestContext, { protocol = 'https:', cookies = '' } = {}) {
    const descriptors = new Map(
        ['window', 'document'].map((name) => [name, Object.getOwnPropertyDescriptor(globalThis, name)]),
    );
    const writes: string[] = [];
    const document = {
        get cookie() {
            return cookies;
        },
        set cookie(value: string) {
            writes.push(value);
        },
    };
    t.after(() => {
        for (const [name, descriptor] of descriptors) {
            if (descriptor) Object.defineProperty(globalThis, name, descriptor);
            else Reflect.deleteProperty(globalThis, name);
        }
    });
    Object.defineProperty(globalThis, 'window', {
        configurable: true,
        value: { location: { protocol } },
    });
    Object.defineProperty(globalThis, 'document', { configurable: true, value: document });
    const { default: manager }: typeof import('@ts/drn/drnCookieManager') = await import(
        `../../../app/ts/drn/drnCookieManager.ts?fixture=${++fixtureId}`
    );
    return {
        manager,
        writes,
        // Only echo the name/value pair. Browser attribute enforcement is outside unit scope.
        echoLastWrite(this: void) {
            const lastWrite = writes.at(-1);
            assert.ok(lastWrite);
            cookies = lastWrite.split(';')[0] ?? '';
        },
    };
}

for (const encoding of ['uri', 'base64', 'none'] as const) {
    test(`${encoding} encoding round-trips strings and objects`, async (t) => {
        const { manager, writes, echoLastWrite } = await fixture(t);
        for (const value of ['hello +/=', { theme: 'dark', enabled: false, count: 0 }]) {
            manager.set('cookie name', value, { encoding });
            assert.ok(writes.at(-1)?.startsWith('cookie%20name='));
            echoLastWrite();
            assert.deepEqual(manager.get('cookie name', { encoding }), value);
            assert.equal(
                manager.get('cookie name', { encoding, deserialize: false }),
                typeof value === 'string' ? value : JSON.stringify(value),
            );
        }
    });
}

test('URI encoding preserves Unicode and cookie delimiter characters', async (t) => {
    const { manager, writes, echoLastWrite } = await fixture(t);
    manager.set('name;=', 'İstanbul 🍪;= %');
    assert.equal(writes[0]?.split(';')[0], 'name%3B%3D=%C4%B0stanbul%20%F0%9F%8D%AA%3B%3D%20%25');
    echoLastWrite();
    assert.equal(manager.get('name;='), 'İstanbul 🍪;= %');
});

for (const protocol of ['http:', 'https:']) {
    test(`${protocol} defaults create a root-scoped Strict session cookie`, async (t) => {
        const { manager, writes } = await fixture(t, { protocol });
        manager.set('theme', 'dark');
        assert.equal(writes[0], `theme=dark; path=/${protocol === 'https:' ? '; Secure' : ''}; SameSite=Strict`);
    });
}

test('explicit options override defaults and maxAge takes precedence over days', async (t) => {
    const { manager, writes } = await fixture(t);
    const options: DrnCookieOptions = {
        path: '/settings',
        domain: 'example.test',
        secure: false,
        sameSite: 'Lax',
        maxAge: 60,
        days: 7,
    };
    const original = { ...options };
    manager.set('theme', 'dark', options);
    assert.equal(writes[0], 'theme=dark; Max-Age=60; domain=example.test; path=/settings; SameSite=Lax');
    assert.deepEqual(options, original);
    manager.set('theme', 'dark', { maxAge: 0, days: 7, sameSite: 'None', secure: true });
    assert.equal(writes[1], 'theme=dark; Max-Age=0; path=/; Secure; SameSite=None');
});

test('days produces an expiration date when maxAge is absent', async (t) => {
    const { manager, writes } = await fixture(t);
    const before = Date.now();
    manager.set('theme', 'dark', { days: 2 });
    const after = Date.now();
    const written = writes[0];
    assert.ok(written);
    const expiry = written.split('; ').find((part) => part.startsWith('expires='));
    assert.ok(expiry);
    const timestamp = Date.parse(expiry.slice('expires='.length));
    const duration = 2 * 24 * 60 * 60 * 1000;
    assert.ok(timestamp >= Math.floor((before + duration) / 1000) * 1000);
    assert.ok(timestamp <= Math.floor((after + duration) / 1000) * 1000);
    assert.ok(!written.includes('Max-Age='));
});

test('reads match exact names and preserve equals signs and invalid JSON', async (t) => {
    const { manager } = await fixture(t, {
        cookies: 'prefixname=wrong; name=left=right; json=%7Binvalid; empty=; zero=0; disabled=false',
    });
    assert.equal(manager.get('name'), 'left=right');
    assert.equal(manager.get('missing'), null);
    assert.equal(manager.get('json'), '{invalid');
    for (const [name, value] of [
        ['empty', ''],
        ['zero', 0],
        ['disabled', false],
    ] as const) {
        assert.equal(manager.get(name), value);
        assert.equal(manager.exists(name), true);
    }
    assert.equal(manager.exists('missing'), false);
});

test('malformed URI and Base64 values return null without throwing', async (t) => {
    const { manager } = await fixture(t, { cookies: 'uri=%GG; base64=***' });
    const warning = t.mock.method(console, 'warn', () => {});
    assert.equal(manager.get('uri'), null);
    assert.equal(manager.get('base64', { encoding: 'base64' }), null);
    assert.equal(warning.mock.callCount(), 2);
});

test('list exposes raw cookie pairs without decoding', async (t) => {
    const { manager } = await fixture(t, { cookies: ' name=left=right; encoded=a%20b; empty=; malformed' });
    assert.deepEqual(manager.list(), [
        { name: 'name', value: 'left=right', raw: 'name=left=right' },
        { name: 'encoded', value: 'a%20b', raw: 'encoded=a%20b' },
        { name: 'empty', value: '', raw: 'empty=' },
        { name: 'malformed', value: '', raw: 'malformed' },
    ]);
});

test('invalid names and circular objects do not write cookies', async (t) => {
    const { manager, writes } = await fixture(t);
    const error = t.mock.method(console, 'error', () => {});
    for (const name of ['', null, undefined, 42]) {
        // @ts-expect-error Exercise runtime rejection of invalid JavaScript callers.
        manager.set(name, 'value');
        // @ts-expect-error Exercise runtime rejection of invalid JavaScript callers.
        assert.equal(manager.get(name), null);
    }
    const circular: { self?: unknown } = {};
    circular.self = circular;
    manager.set('circular', circular);
    assert.deepEqual(writes, []);
    assert.equal(error.mock.callCount(), 9);
});

test('remove preserves scope and forces zero maxAge even for missing cookies', async (t) => {
    const { manager, writes } = await fixture(t, { cookies: 'theme=dark' });
    const warning = t.mock.method(console, 'warn', () => {});
    const options = { path: '/settings', domain: 'example.test', maxAge: 60, days: 7 };
    manager.remove('theme', options);
    assert.equal(writes[0], 'theme=; Max-Age=0; domain=example.test; path=/settings; Secure; SameSite=Strict');
    assert.equal(options.maxAge, 60);
    assert.equal(warning.mock.callCount(), 0);
    manager.remove('missing', options);
    assert.equal(writes[1], 'missing=; Max-Age=0; domain=example.test; path=/settings; Secure; SameSite=Strict');
    assert.equal(warning.mock.callCount(), 1);
});

test('exists checks presence independently of decoding and deserialization', async (t) => {
    const { manager } = await fixture(t, { cookies: 'present=null; malformed=%GG; empty=' });
    const warning = t.mock.method(console, 'warn', () => {});
    assert.equal(manager.get('present'), null);
    assert.equal(manager.exists('present'), true);
    assert.equal(manager.exists('malformed'), true);
    assert.equal(manager.exists('empty'), true);
    assert.equal(manager.exists('missing'), false);
    manager.remove('present');
    manager.remove('malformed');
    assert.equal(warning.mock.callCount(), 0);
});

test('exists rejects invalid names', async (t) => {
    const { manager } = await fixture(t, { cookies: 'null=value; undefined=value; 42=value' });
    const error = t.mock.method(console, 'error', () => {});
    for (const name of ['', null, undefined, 42]) {
        // @ts-expect-error Exercise runtime rejection of invalid JavaScript callers.
        assert.equal(manager.exists(name), false);
    }
    assert.equal(error.mock.callCount(), 4);
});
