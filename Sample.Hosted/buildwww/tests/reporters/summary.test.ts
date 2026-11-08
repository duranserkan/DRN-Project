import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import { mkdtempDisposableSync, readFileSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';
import type { TestEvent } from 'node:test/reporters';
import type { FullConfig, Suite } from '@playwright/test/reporter';
import PlaywrightSummary from './playwright.ts';
import nodeSummary from './node.ts';
import { testSummary } from './summary.ts';

test('a clean run fits on one terminal line', (t) => {
    t.mock.property(process, 'env', { ...process.env });
    delete process.env.GITHUB_STEP_SUMMARY;
    const summary = testSummary(
        'Frontend browser tests (Playwright)',
        true,
        11300,
        { passed: 50, failed: 0, skipped: 0, flaky: 0 },
        [],
    );
    assert.equal(summary.trim(), '✅ Frontend browser tests (Playwright) | 50/50 passed in 11.30s');
});

test('Playwright --list discovers tests without reading coverage or publishing a run summary', () => {
    using directory = mkdtempDisposableSync(join(tmpdir(), 'frontend-discovery-'));
    const jobSummary = join(directory.path, 'summary.md');
    writeFileSync(jobSummary, '');
    const cli = fileURLToPath(import.meta.resolve('@playwright/test/cli'));
    const output = execFileSync(
        process.execPath,
        [cli, 'test', '--list', '--config', 'buildwww/tests/hosted/playwright.config.ts'],
        {
            cwd: new URL('../../../', import.meta.url),
            env: {
                ...process.env,
                GITHUB_STEP_SUMMARY: jobSummary,
                PLAYWRIGHT_LCOV_REPORT: join(directory.path, 'missing.info'),
            },
            encoding: 'utf8',
        },
    );
    assert.match(output, /home\.spec\.ts/);
    assert.doesNotMatch(output, /Test Run Summary|Frontend coverage failed/);
    assert.equal(readFileSync(jobSummary, 'utf8'), '');
});

test('Node reports aggregate counts and escapes failure details in the GitHub summary', async (t) => {
    using directory = mkdtempDisposableSync(join(tmpdir(), 'node-summary-'));
    const jobSummary = join(directory.path, 'summary.md');
    t.mock.property(process, 'env', { ...process.env, GITHUB_STEP_SUMMARY: jobSummary });
    async function* events() {
        yield {
            type: 'test:fail',
            data: { name: '<test>', details: { duration_ms: 10, error: new Error('unexpected <value>') } },
        };
        yield {
            type: 'test:summary',
            data: { duration_ms: 250, counts: { tests: 4, passed: 1, skipped: 1, todo: 1 }, success: false },
        };
    }
    const output: string[] = [];
    for await (const summary of nodeSummary(events() as AsyncIterable<TestEvent>)) output.push(summary);
    assert.equal(output.length, 1);
    assert.equal(output[0]?.trim(), '❌ Frontend units (Node.js) | 1/4 passed, 1 failed, 2 skipped in 0.25s');
    const markdown = readFileSync(jobSummary, 'utf8');
    assert.ok(markdown.includes('| 4 | 1 | 1 | 2 | 0 | 0.25s |'));
    assert.match(markdown, /&lt;test&gt;/);
    assert.match(markdown, /unexpected &lt;value&gt;/);
});

test('Playwright prints the run locally and includes only the requested coverage in the job summary', async (t) => {
    using directory = mkdtempDisposableSync(join(tmpdir(), 'frontend-reporter-'));
    const jobSummary = join(directory.path, 'summary.md');
    const report = join(directory.path, 'coverage.info');
    writeFileSync(report, 'SF:app.ts\nDA:1,1\nend_of_record\n');
    t.mock.property(process, 'env', { ...process.env, GITHUB_STEP_SUMMARY: jobSummary });
    delete process.env.PLAYWRIGHT_LCOV_REPORT;
    delete process.env.FRONTEND_ACTION_SUMMARIZES_COVERAGE;
    const log = t.mock.method(console, 'log', () => {});
    const reporter = new PlaywrightSummary();
    const tests = [
        { outcome: () => 'expected' },
        { outcome: () => 'skipped', expectedStatus: 'skipped', results: [{ status: 'skipped' }] },
        { outcome: () => 'skipped', expectedStatus: 'passed', results: [] },
    ];
    reporter.onBegin({} as FullConfig, { allTests: () => tests } as unknown as Suite);
    const result = { status: 'passed', startTime: new Date(), duration: 1500 } as const;
    await reporter.onEnd(result);
    const firstSummary = readFileSync(jobSummary, 'utf8');
    assert.match(log.mock.calls[0]?.arguments[0], /1\/3 passed, 1 skipped, 1 did not run in 1\.50s/);
    assert.match(firstSummary, /Did not run/);
    assert.ok(firstSummary.includes('| 3 | 1 | 0 | 1 | 0 | 1 | 1.50s |'));
    assert.doesNotMatch(firstSummary, /Frontend unit coverage/);

    process.env.PLAYWRIGHT_LCOV_REPORT = report;
    await reporter.onEnd(result);
    const coverage = log.mock.calls.at(-1)?.arguments[0];
    assert.match(coverage, /All frontend sources \| 1 \| 1 \| 100\.0%/);
    assert.ok(readFileSync(jobSummary, 'utf8').endsWith(coverage));

    reporter.onBegin({} as FullConfig, { allTests: () => [tests[1]] } as unknown as Suite);
    assert.deepEqual(await reporter.onEnd(result), { status: 'failed' });
    assert.match(log.mock.calls.at(-2)?.arguments[0], /❌ .*0\/1 passed, 1 skipped/);
    assert.equal(log.mock.calls.at(-1)?.arguments[0], coverage);

    reporter.onBegin({} as FullConfig, { allTests: () => tests } as unknown as Suite);
    writeFileSync(report, '');
    assert.deepEqual(await reporter.onEnd(result), { status: 'failed' });
    assert.match(readFileSync(jobSummary, 'utf8'), /Missing frontend line coverage/);
});
