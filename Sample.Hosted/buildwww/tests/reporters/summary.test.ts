import { test, vi } from 'vitest';
import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import { mkdtempDisposableSync, readFileSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';
import type { FullConfig, Suite } from '@playwright/test/reporter';
import PlaywrightSummary from './playwright.ts';
import VitestSummary from './vitest.ts';
import { testSummary } from './summary.ts';

function finishVitestRun(reporter: VitestSummary, ...args: Parameters<VitestSummary['onTestRunEnd']>) {
    const previous = process.exitCode;
    try {
        process.exitCode = 0;
        reporter.onTestRunEnd(...args);
        return process.exitCode;
    } finally {
        process.exitCode = previous;
    }
}

test('a clean run fits on one terminal line', () => {
    vi.stubEnv('GITHUB_STEP_SUMMARY', undefined);
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

test('Vitest reports aggregate counts and escapes failure details in the GitHub summary', () => {
    using directory = mkdtempDisposableSync(join(tmpdir(), 'vitest-summary-'));
    const jobSummary = join(directory.path, 'summary.md');
    vi.stubEnv('GITHUB_STEP_SUMMARY', jobSummary);
    const log = vi.spyOn(console, 'log').mockImplementation(() => {});
    const clock = vi.spyOn(performance, 'now').mockReturnValue(0);
    const reporter = new VitestSummary();
    reporter.onTestRunStart();
    clock.mockReturnValue(250);
    const tests = ['passed', 'failed', 'skipped', 'skipped'].map((state) => ({
        fullName: '<test>',
        result: () => ({ state, errors: [{ message: 'unexpected <value>' }] }),
        diagnostic: () => ({ duration: 10 }),
    }));
    const modules = [{ errors: () => [], children: { allTests: () => tests } }];
    assert.equal(
        finishVitestRun(reporter, modules as unknown as Parameters<VitestSummary['onTestRunEnd']>[0], [], 'failed'),
        1,
    );
    assert.match(log.mock.calls[0]?.[0], /1\/4 passed, 1 failed, 2 skipped/);
    const markdown = readFileSync(jobSummary, 'utf8');
    assert.ok(markdown.includes('| 4 | 1 | 1 | 2 | 0 | 0.25s |'));
    assert.match(markdown, /&lt;test&gt;/);
    assert.match(markdown, /unexpected &lt;value&gt;/);
    assert.match(markdown, /10ms/);
});

test('Vitest fails a passed run with an unhandled error or only skipped tests', () => {
    vi.stubEnv('GITHUB_STEP_SUMMARY', undefined);
    const log = vi.spyOn(console, 'log').mockImplementation(() => {});
    for (const { state, errors } of [
        { state: 'passed', errors: [{ message: 'unhandled rejection' }] },
        { state: 'skipped', errors: [] },
    ]) {
        const testCase = { result: () => ({ state }), diagnostic: () => undefined };
        const modules = [{ errors: () => [], children: { allTests: () => [testCase] } }];
        const status = finishVitestRun(
            new VitestSummary(),
            modules as unknown as Parameters<VitestSummary['onTestRunEnd']>[0],
            errors,
            'passed',
        );
        assert.equal(status, 1);
        assert.match(log.mock.calls.at(-1)?.[0], /❌ Frontend units/);
    }
});

test('Playwright prints the run locally and includes only the requested coverage in the job summary', async () => {
    using directory = mkdtempDisposableSync(join(process.cwd(), '.frontend-reporter-'));
    const jobSummary = join(directory.path, 'summary.md');
    const report = join(directory.path, 'coverage.info');
    writeFileSync(report, 'SF:app.ts\nDA:1,1\nend_of_record\n');
    vi.stubEnv('GITHUB_STEP_SUMMARY', jobSummary);
    vi.stubEnv('PLAYWRIGHT_LCOV_REPORT', undefined);
    vi.stubEnv('FRONTEND_ACTION_SUMMARIZES_COVERAGE', undefined);
    const log = vi.spyOn(console, 'log').mockImplementation(() => {});
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
    assert.match(log.mock.calls[0]?.[0], /1\/3 passed, 1 skipped, 1 did not run in 1\.50s/);
    assert.match(firstSummary, /Did not run/);
    assert.ok(firstSummary.includes('| 3 | 1 | 0 | 1 | 0 | 1 | 1.50s |'));
    assert.doesNotMatch(firstSummary, /Frontend unit coverage/);

    process.env.PLAYWRIGHT_LCOV_REPORT = report;
    await reporter.onEnd(result);
    const coverage = log.mock.calls.at(-1)?.[0];
    assert.match(coverage, /All frontend sources \| 1 \| 1 \| 100\.0%/);
    assert.ok(readFileSync(jobSummary, 'utf8').endsWith(coverage));

    reporter.onBegin({} as FullConfig, { allTests: () => [tests[1]] } as unknown as Suite);
    assert.deepEqual(await reporter.onEnd(result), { status: 'failed' });
    assert.match(log.mock.calls.at(-2)?.[0], /❌ .*0\/1 passed, 1 skipped/);
    assert.equal(log.mock.calls.at(-1)?.[0], coverage);

    reporter.onBegin({} as FullConfig, { allTests: () => tests } as unknown as Suite);
    writeFileSync(report, '');
    assert.deepEqual(await reporter.onEnd(result), { status: 'failed' });
    assert.match(readFileSync(jobSummary, 'utf8'), /Missing frontend line coverage/);
});
