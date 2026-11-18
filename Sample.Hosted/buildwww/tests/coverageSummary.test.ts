import { test } from 'vitest';
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import {
    copyFileSync,
    cpSync,
    mkdirSync,
    mkdtempDisposableSync,
    readFileSync,
    symlinkSync,
    writeFileSync,
} from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join, relative, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import {
    coverageReportPath,
    coverageSummary,
    readCoverageSummary,
} from '../../../.github/actions/frontend-validation/coverage.mts';

const unitRunnerPath = fileURLToPath(new URL('./run-tests.ts', import.meta.url));

test('unit test selectors cannot add a Node option', () => {
    const result = spawnSync(process.execPath, [unitRunnerPath, '--', '--version'], {
        cwd: new URL('../../', import.meta.url),
        encoding: 'utf8',
    });
    assert.equal(result.status, 1);
    assert.match(result.stderr, /No matching frontend unit tests found/);
});

test('a matching unit test cannot mask an unmatched selector', () => {
    const result = spawnSync(
        process.execPath,
        [unitRunnerPath, 'buildwww/tests/reporters/summary.test.ts', 'missing.test.ts'],
        { cwd: new URL('../../', import.meta.url), encoding: 'utf8' },
    );
    assert.equal(result.status, 1);
    assert.match(result.stderr, /No matching frontend unit tests found for missing\.test\.ts/);
});

test('filtered coverage respects source globs and excludes unselected unit tests', () => {
    using directory = mkdtempDisposableSync(join(process.cwd(), '.coverage-test-'));
    const report = join(directory.path, 'report.info');
    const component = 'buildwww/lib/react/components/HelloReact.tsx';
    const testFile = 'buildwww/tests/unit/react/reactIslands.test.ts';
    const selectedFile = join(directory.path, testFile);
    mkdirSync(dirname(selectedFile), { recursive: true });
    copyFileSync(testFile, selectedFile);
    cpSync('buildwww/lib/react', join(directory.path, 'buildwww/lib/react'), { recursive: true });
    copyFileSync('tsconfig.json', join(directory.path, 'tsconfig.json'));
    copyFileSync('vitest.config.ts', join(directory.path, 'vitest.config.ts'));
    symlinkSync(resolve('buildwww/tests/reporters'), join(directory.path, 'buildwww/tests/reporters'), 'junction');
    // A Vitest positional filter would also match this nested test path.
    const unselectedFile = join(directory.path, 'buildwww/tests/nested', testFile);
    mkdirSync(dirname(unselectedFile), { recursive: true });
    writeFileSync(unselectedFile, "throw new Error('Unselected unit test was executed');\n");
    const result = spawnSync(
        process.execPath,
        [
            unitRunnerPath,
            testFile,
            '--coverage',
            '--include',
            component,
            '--include',
            'buildwww/lib/react/Island*.tsx',
            '--include',
            'buildwww/tests/nested/**/*.ts',
            '--exclude',
            '**/*.d.ts',
            '--report',
            report,
        ],
        {
            cwd: directory.path,
            encoding: 'utf8',
            env: { ...process.env, GITHUB_STEP_SUMMARY: '' },
            timeout: 30_000,
            killSignal: 'SIGKILL',
        },
    );
    if (result.error) throw result.error;
    assert.equal(result.status, 0, result.stderr || result.stdout);
    readCoverageSummary(report, [component, 'buildwww/lib/react/IslandErrorBoundary.tsx']);
    const lcov = readFileSync(report, 'utf8');
    assert.doesNotMatch(lcov, /^SF:(?:.*[/\\])?PortalContext\.tsx\r?$/m);
    assert.doesNotMatch(lcov, /^SF:.*\.test\.(?:js|ts)\r?$/m);
});

test('coverage paths stay inside the package for both reads and writes', () => {
    using local = mkdtempDisposableSync(join(process.cwd(), '.coverage-test-'));
    using outside = mkdtempDisposableSync(join(tmpdir(), 'coverage-test-'));
    const external = join(outside.path, 'report.info');
    writeFileSync(external, '');
    assert.throws(() => coverageReportPath(relative(process.cwd(), external)), /inside the package/);
    assert.throws(() => coverageReportPath(external), /inside the package/);
    assert.throws(() => coverageReportPath('package.json'), /\.info or \.lcov/);
    symlinkSync(external, join(local.path, 'link.info'));
    assert.throws(() => coverageReportPath(join(local.path, 'link.info')), /regular/);
    symlinkSync(outside.path, join(local.path, 'external'), 'dir');
    assert.throws(() => coverageReportPath(join(local.path, 'external', 'new.lcov')), /inside the package/);
    assert.equal(coverageReportPath(join(local.path, 'new.lcov')), join(local.path, 'new.lcov'));
});

test('coverage merges repeated lines per file and groups counts by extension', () => {
    const report = [
        'SF:components/card.tsx',
        'DA:1,0',
        'DA:2,0',
        'end_of_record',
        'SF:components/card.tsx',
        'DA:1,2,checksum',
        'end_of_record',
        'SF:lib/card.ts',
        'DA:1,3',
        'end_of_record',
    ].join('\r\n');

    const summary = coverageSummary(report, ['components/card.tsx']);
    assert.ok(summary.includes('| All frontend sources | 2 | 3 | 66.7% |'));
    assert.ok(summary.includes('| .tsx | 1 | 2 | 50.0% |'));
    assert.ok(summary.includes('| .ts | 1 | 1 | 100.0% |'));
});

test('coverage rejects reports without line data for a source', () => {
    assert.throws(() => coverageSummary('SF:empty.ts\nend_of_record\nDA:1,1'), /Missing frontend line coverage/);
});

test('coverage requires line data for each explicitly required source', () => {
    const report = 'SF:app.ts\nDA:1,0\nend_of_record\nSF:card.tsx\nend_of_record';
    assert.throws(() => coverageSummary(report, ['card.tsx']), /Missing React line coverage for card\.tsx/);
    assert.ok(coverageSummary(report).includes('| All frontend sources | 0 | 1 | 0.0% |'));
});
