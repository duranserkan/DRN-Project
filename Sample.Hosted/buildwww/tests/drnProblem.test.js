import test from 'node:test';
import assert from 'node:assert/strict';
import problem from '../app/js/drn/drnProblem.js';

test('public fields and validation survive parsing while HTTP status wins', () => {
    const result = problem.parse(JSON.stringify({
        status: 500, type: 'urn:sample:problem:category-not-found', title: 'Not found',
        detail: 'Choose another category.', requestId: 'request-1',
        errors: { Name: ['Required'], Invalid: 'wrong shape' }, stack: 'private'
    }), 'Application/Problem+JSON; charset=utf-8', 404);
    assert.equal(result.status, 404);
    assert.equal(result.type, 'urn:sample:problem:category-not-found');
    assert.equal(result.detail, 'Choose another category.');
    assert.equal(result.requestId, 'request-1');
    assert.deepEqual(result.errors.Name, ['Required']);
    assert.equal(result.errors.Invalid, undefined);
    assert.equal(result.stack, undefined);
});

test('malformed bodies and incorrect field types have safe defaults', () => {
    const fallback = problem.parse('', '', 500).title;
    for (const body of ['', '{', 'null', '[]', '42', '"text"', '<html>private</html>']) {
        assert.equal(problem.parse(body, 'application/problem+json', 500).title, fallback);
    }
    assert.equal(problem.parse('{"title":"private"}', 'text/html', 500).title, fallback);
    const result = problem.parse('{"title":{},"detail":[],"type":12,"requestId":false,"errors":[]}',
        'application/problem+json', 400);
    assert.equal(result.title, fallback);
    assert.equal(result.detail, '');
    assert.equal(result.type, '');
    assert.equal(result.requestId, '');
    assert.equal(Object.keys(result.errors).length, 0);
});

test('display fields and validation output are bounded without altering identifiers', () => {
    const result = problem.parse(JSON.stringify({ title: 'x'.repeat(1000), requestId: 'r'.repeat(201),
        type: 't'.repeat(2049), errors: { Name: Array(20).fill('e'.repeat(1000)) }
    }), 'application/problem+json', 400);
    assert.equal(result.title.length, 500);
    assert.equal(result.errors.Name.length, 5);
    assert.equal(result.errors.Name[0].length, 500);
    assert.equal(result.requestId, '');
    assert.equal(result.type, '');
    const oversizedBody = JSON.stringify({ title: 'Oversized problem', detail: 'x'.repeat(65536) });
    const oversized = problem.parse(oversizedBody, 'application/problem+json', 500);
    assert.equal(oversized.title, problem.parse('', '', 500).title);
});

test('field keys cannot mutate prototypes and unknown types remain usable identifiers', () => {
    const result = problem.parse('{"type":"urn:other:unknown","errors":{"__proto__":["message"]}}',
        'application/problem+json', 422);
    assert.equal(Object.getPrototypeOf(result.errors), null);
    assert.deepEqual(result.errors.__proto__, ['message']);
    assert.equal(result.type, 'urn:other:unknown');
});

test('HTML-like public messages are passed as text to the escaping notification service', () => {
    const originalWindow = globalThis.window;
    let notification;
    globalThis.window = { DRN: { Toast: { error: (message) => { notification = message; } } } };
    try {
        const result = problem.parse('{"title":"<img src=x onerror=alert(1)>","requestId":"reference"}',
            'application/problem+json', 400);
        problem.show(result);
        assert.equal(notification, '<img src=x onerror=alert(1)>\nRequest reference: reference');
    } finally {
        if (originalWindow === undefined) delete globalThis.window;
        else globalThis.window = originalWindow;
    }
});

test('XHR and Fetch share parsing and Fetch leaves the original body readable', async () => {
    const body = '{"title":"Invalid","requestId":"request-2"}';
    const xhr = { responseText: body, status: 400, getResponseHeader: () => 'application/problem+json' };
    const response = new Response(body, { status: 400, headers: { 'Content-Type': 'application/problem+json' } });
    assert.deepEqual(await problem.fromResponse(response), problem.fromXhr(xhr));
    assert.equal(await response.text(), body);
    assert.equal(problem.fromXhr(null).status, 0);
    const html = new Response('<html>private</html>', { status: 500, headers: { 'Content-Type': 'text/html' } });
    assert.equal((await problem.fromResponse(html)).title, problem.parse('', '', 500).title);
});
