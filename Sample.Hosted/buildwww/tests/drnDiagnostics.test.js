import test from 'node:test';
import assert from 'node:assert/strict';
import cases from '../../../DRN.Test.Unit/Tests/Framework/Utils/Logging/diagnostic-golden-cases.json' with { type: 'json' };
import { createDiagnosticSanitizer, REDACTED, LIMITED } from '../app/js/drn/drnDiagnostics.js';

test('shared golden cases preserve browser and server conventions', () => {
    for (const item of cases) {
        const capture = createDiagnosticSanitizer().createCapture();
        const actual = item.kind === 'field' ? capture.value(item.field, item.value) : capture[item.kind](item.value);
        assert.deepEqual(actual, item.expected, JSON.stringify(item));
        if ('limited' in item) assert.equal(capture.status.limited, item.limited);
        if ('redacted' in item) assert.equal(capture.status.redacted, item.redacted);
        if (item.kind === 'text') assert.equal(createDiagnosticSanitizer().createCapture().text(actual), actual);
    }
});

test('configured rules match normalized substrings', () => {
    const policy = createDiagnosticSanitizer();
    assert.equal(policy.configure({
        sensitiveFields: ['opaque', '---'],
        sensitiveFieldPaths: ['Checkout.Value'],
        sensitivePathSegments: ['handoff']
    }), true);
    const capture = policy.createCapture();
    assert.equal(capture.value('CustomOpaqueSuffix', 'probe'), REDACTED);
    assert.equal(capture.value('Root.Checkout.ValueSuffix', 'probe'), REDACTED);
    assert.equal(capture.value('Result.Count', 2), 2);
    assert.equal(capture.url('/v2-handoff-flow/probe?attempt=2'), '/v2-handoff-flow/redacted?attempt=2');
});

test('text content is preserved while explicit URL structure is redacted', () => {
    const policy = createDiagnosticSanitizer();
    const capture = policy.createCapture();
    assert.equal(capture.text('Failed opaque/probe at Service.Save'), 'Failed opaque/probe at Service.Save');
    assert.equal(capture.url('/?returnUrl=https%3A%2F%2Fexample.test%2F%3Fpassword%3Dnested-probe').includes('nested-probe'), true);
    assert.equal(capture.url('/?value=%GGsecret-probe').includes('secret-probe'), false);
    assert.equal(capture.url('/?value=opaque%2Fprobe'), '/?value=opaque%2Fprobe');
    assert.equal(capture.url('/token/opaque-probe?count=2'), '/token/redacted?count=2');
    assert.equal(capture.status.redacted, true);
    assert.equal(policy.configure({ sensitiveFields: ['x'.repeat(257)] }), false);
    assert.equal(policy.createCapture().text('probe'), LIMITED);
    assert.equal(createDiagnosticSanitizer().createCapture().text('opaque/probe'), 'opaque/probe');
});

test('URL decoding preserves decoded literal percents while bounding nested encoding', () => {
    const capture = createDiagnosticSanitizer().createCapture();
    assert.equal(capture.url('/100%25?value=50%25#progress=75%25'), '/100%25?value=50%25#progress=75%25');
    assert.equal(capture.url('/?value=100%2525'), '/?value=100%25');
    assert.equal(capture.url('/?value=%GGsecret-probe'), '[diagnostic field unavailable]');
    assert.equal(capture.url('/?value=%252541'), '/?value=A');
    assert.equal(capture.url('/?value=%25252541'), '/?value=%5Bdiagnostic%20field%20unavailable%5D');
    assert.equal(capture.url('/?%2570assword=secret-probe'), '/?password=redacted');
});

test('mixed URL escapes cannot hide sensitive keys or path segments', () => {
    for (const url of [
        '/?%2570assword%25=secret-probe',
        '/#%2570assword%25=secret-probe',
        '/%2574oken%25/secret-probe',
        '/?%252570assword%2525=secret-probe',
        '/?%2570assword%25FF=secret-probe'
    ]) {
        const capture = createDiagnosticSanitizer().createCapture();
        assert.equal(capture.url(url), '[diagnostic field unavailable]', url);
        assert.equal(capture.status.limited, true, url);
    }
});

test('structured data skips getters, bounds cycles and redacts matching substrings', () => {
    const capture = createDiagnosticSanitizer().createCapture();
    const input = { HasRecoveryCodes: true, EmailConfirmed: false, id: 42 };
    Object.defineProperty(input, 'Password', { enumerable: true, get() { throw new Error('must not read'); } });
    input.self = input;
    const result = capture.value('Input', input);
    assert.equal(result.Password, REDACTED);
    assert.equal(result.HasRecoveryCodes, REDACTED);
    assert.equal(result.EmailConfirmed, REDACTED);
    assert.equal(result.id, 42);
    assert.equal(result.self, '[diagnostic cycle]');
    assert.equal(capture.text('a'.repeat(65537)), LIMITED);
});

test('nested URL fields retain authentication context without changing safe fields', () => {
    const input = { report: { Url: '/User/ConfirmEmail?code=url-probe&id=42', Count: 2 } };
    const capture = createDiagnosticSanitizer().createCapture();
    const safe = capture.value('', input);
    assert.equal(safe.report.Url, '/User/ConfirmEmail?code=redacted&id=42');
    assert.equal(safe.report.Count, 2);
    assert.equal(capture.status.redacted, true);
    assert.equal(input.report.Url.includes('url-probe'), true);
});

test('sparse arrays produce bounded serializable diagnostics without reading omitted entries', () => {
    const capture = createDiagnosticSanitizer().createCapture();
    const input = [];
    input[0] = { Password: 'private', Count: 2 };
    Object.defineProperty(input, 1000000, {
        enumerable: true,
        get() { throw new Error('omitted getter must not run'); }
    });

    const result = capture.value('Input', input);
    assert.equal(result.length, 257);
    assert.deepEqual(result[0], Object.assign(Object.create(null), { Password: REDACTED, Count: 2 }));
    assert.ok(result.slice(1, 256).every(item => item === null));
    assert.equal(result[256], LIMITED);
    assert.equal(capture.status.limited, true);
    assert.equal(capture.status.redacted, true);
    const serialized = JSON.stringify(result);
    assert.ok(serialized.length < 2000);
    assert.equal(JSON.parse(serialized).length, 257);
    assert.equal(input.length, 1000001);
});

test('array capture preserves dense boundaries and marks overflow', () => {
    for (const length of [0, 255, 256, 257]) {
        const capture = createDiagnosticSanitizer().createCapture();
        const input = Array.from({ length }, (_, index) => index);
        const expected = input.slice(0, 256);
        if (length > 256) expected.push(LIMITED);
        assert.deepEqual(capture.value('Input', input), expected);
        assert.equal(capture.status.limited, length > 256);
    }
});

test('array capture preserves positions and skips own and inherited getters', () => {
    const capture = createDiagnosticSanitizer().createCapture();
    const input = new Array(3);
    const prototype = Object.create(Array.prototype);
    Object.defineProperty(prototype, 0, { get() { throw new Error('inherited getter must not run'); } });
    Object.setPrototypeOf(input, prototype);
    Object.defineProperty(input, 1, { enumerable: true, get() { throw new Error('own getter must not run'); } });
    input[2] = 'last';

    assert.deepEqual(capture.value('Input', input), [null, '[diagnostic getter unavailable]', 'last']);
    assert.equal(capture.status.limited, false);
});
