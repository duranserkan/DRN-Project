import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { mkdtempDisposableSync, symlinkSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, relative } from 'node:path';
import test from 'node:test';
import { coverageReportPath, coverageSummary } from '../../../.github/actions/frontend-validation/coverage.mts';

test('unit test selectors cannot add a Node option', () => {
    const result = spawnSync(process.execPath, ['buildwww/tests/run-tests.ts', '--', '--version'], {
        cwd: new URL('../../', import.meta.url),
        encoding: 'utf8',
    });
    assert.equal(result.status, 1);
    assert.match(result.stderr, /No matching frontend unit tests found/);
});

test('a matching unit test cannot mask an unmatched selector', () => {
    const result = spawnSync(
        process.execPath,
        ['buildwww/tests/run-tests.ts', 'buildwww/tests/reporters/summary.test.ts', 'missing.test.ts'],
        { cwd: new URL('../../', import.meta.url), encoding: 'utf8' },
    );
    assert.equal(result.status, 1);
    assert.match(result.stderr, /No matching frontend unit tests found for missing\.test\.ts/);
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
