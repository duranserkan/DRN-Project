import assert from 'node:assert/strict';
import test from 'node:test';
import utils from '../../../app/ts/drn/drnUtils.ts';
import { installBrowserGlobals } from '../browserGlobals.ts';

test('diagnostic selectors include data attributes and omit ordinary attribute values', (t) => {
    class Control {
        tagName = 'INPUT';
        id = 'email';
        classList = ['form-control', 'required'];
        attributes = [
            { name: 'value', value: 'private@example.test' },
            { name: 'data-action', value: 'save' },
        ];
    }
    installBrowserGlobals(t, { Element: Control });
    assert.equal(
        utils.getRequestElementSelector(new Control() as unknown as Element),
        'INPUT#email.form-control.required[data-action="save"]',
    );
    assert.equal(utils.getRequestElementSelector(null), 'Unknown Element');
});

test('base64 helpers reject non-string inputs and preserve text that btoa cannot encode', (t) => {
    t.mock.method(console, 'warn', () => {});
    t.mock.method(console, 'error', () => {});
    // @ts-expect-error Exercise the untyped runtime boundary.
    assert.equal(utils.urlSafeBase64Encode(null), '');
    // @ts-expect-error Exercise the untyped runtime boundary.
    assert.equal(utils.urlSafeBase64Decode(42), '');
    assert.equal(utils.urlSafeBase64Encode('界'), '界');
});

test('URL-safe cookie encoding round-trips both alphabet substitutions and omitted padding', () => {
    for (const [plain, encoded] of [
        ['\xfb', '-w'],
        ['\xfb\xff', '-_8'],
        ['\xfb\xff\xff', '-___'],
    ] as const) {
        assert.equal(utils.urlSafeBase64Encode(plain), encoded);
        assert.equal(utils.urlSafeBase64Decode(encoded), plain);
    }
});

test('deepMerge preserves nested defaults and replaces arrays without modifying either input', () => {
    const base = { panel: { enabled: true, channels: ['toast'], timeout: 5 } };
    const overrides = { panel: { channels: ['history'], timeout: 0 } };

    assert.deepEqual(utils.deepMerge(base, overrides), {
        panel: { enabled: true, channels: ['history'], timeout: 0 },
    });
    assert.deepEqual(base.panel, { enabled: true, channels: ['toast'], timeout: 5 });
    assert.deepEqual(overrides.panel, { channels: ['history'], timeout: 0 });
});

test('deepMerge ignores prototype-related keys inside nested configuration overrides', () => {
    const overrides: object = JSON.parse(
        '{"panel":{"__proto__":{"injected":true},"constructor":{"injected":true},"prototype":{},"enabled":true}}',
    );
    const result = utils.deepMerge({ panel: { enabled: false } }, overrides);

    assert.deepEqual(result.panel, { enabled: true });
    assert.equal(Object.getPrototypeOf(result.panel), Object.prototype);
    assert.equal('injected' in result.panel, false);
});
