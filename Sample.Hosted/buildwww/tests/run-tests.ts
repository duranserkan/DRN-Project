import { spawn } from 'node:child_process';
import { globSync } from 'node:fs';
import { matchesGlob, normalize } from 'node:path';
import { parseArgs } from 'node:util';
import { coverageReportPath } from '../../../.github/actions/frontend-validation/coverage.mts';
import { printCoverageReport } from './reporters/summary.ts';

const { values, positionals: testFiles } = parseArgs({
    allowPositionals: true,
    options: {
        browser: { type: 'boolean' },
        'default-pattern': { type: 'string', multiple: true },
        coverage: { type: 'boolean' },
        include: { type: 'string', multiple: true },
        exclude: { type: 'string', multiple: true },
        report: { type: 'string' },
    },
});

function runNode(args: string[]): Promise<number> {
    const child = spawn(process.execPath, args, { stdio: 'inherit' });
    return new Promise((resolve) => {
        child.once('error', (error) => {
            console.error(error);
            resolve(1);
        });
        child.once('close', (code) => resolve(code ?? 1));
    });
}

const args = [
    '--test',
    '--test-reporter=spec',
    '--test-reporter-destination=stdout',
    '--test-reporter=./buildwww/tests/reporters/node.ts',
    '--test-reporter-destination=stdout',
];
const report = values.coverage ? coverageReportPath(values.report ?? 'coverage-frontend.info') : undefined;
if (values.coverage) {
    args.push('--enable-source-maps', '--experimental-test-coverage', '--test-coverage-include-all');
    for (const glob of values.include ?? ['buildwww/{app,lib}/**/*.{ts,tsx,js}'])
        args.push(`--test-coverage-include=${glob}`);
    for (const glob of values.exclude ?? ['**/*.d.ts']) args.push(`--test-coverage-exclude=${glob}`);
    args.push('--test-reporter=lcov', `--test-reporter-destination=${report}`);
} else if (values.include || values.exclude || values.report) {
    throw new Error('Coverage options require --coverage.');
}

// Only discovered unit tests can become Node operands. CLI selectors never become Node options.
const defaultPatterns = values['default-pattern'];
const unitFiles = globSync('buildwww/tests/**/*.test.{js,ts}').filter(
    (file) => !defaultPatterns || defaultPatterns.some((pattern) => matchesGlob(file, pattern)),
);
if (!unitFiles.length) throw new Error('No matching frontend unit tests found');
const selectedFiles = new Set<string>();
for (const selector of testFiles) {
    const pattern = normalize(selector);
    const matches = unitFiles.filter((file) => matchesGlob(file, pattern));
    if (!matches.length) throw new Error(`No matching frontend unit tests found for ${selector}`);
    for (const file of matches) selectedFiles.add(file);
}
const files = testFiles.length ? selectedFiles : unitFiles;
const [unitStatus, browserStatus] = await Promise.all([
    runNode([...args, '--', ...files]),
    values.browser ? runNode(['--run', 'test:browser']) : 0,
]);
process.exitCode = unitStatus || browserStatus;
if (unitStatus === 0 && values.coverage) {
    const requiredSources = values.include || values.exclude ? [] : globSync('buildwww/{app,lib}/**/*.tsx');
    if (!printCoverageReport(report, requiredSources)) process.exitCode = 1;
}
