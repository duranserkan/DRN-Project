import assert from 'node:assert/strict';
import test from 'node:test';
import { coverageSummary } from '../../../.github/actions/frontend-validation/coverage.mts';

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
