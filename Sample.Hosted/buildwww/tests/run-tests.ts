import { spawn } from 'node:child_process';
import { globSync } from 'node:fs';
import { parseArgs } from 'node:util';
import { printCoverageReport } from './reporters/summary.ts';

const { values, positionals: testFiles } = parseArgs({
    allowPositionals: true,
    options: {
        browser: { type: 'boolean' },
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
const report = values.report ?? 'coverage-frontend.info';
if (values.coverage) {
    args.push('--enable-source-maps', '--experimental-test-coverage', '--test-coverage-include-all');
    for (const glob of values.include ?? ['buildwww/{app,lib}/**/*.{ts,tsx,js}'])
        args.push(`--test-coverage-include=${glob}`);
    for (const glob of values.exclude ?? ['**/*.d.ts']) args.push(`--test-coverage-exclude=${glob}`);
    args.push('--test-reporter=lcov', `--test-reporter-destination=${report}`);
} else if (values.include || values.exclude || values.report) {
    throw new Error('Coverage options require --coverage.');
}

const [unitStatus, browserStatus] = await Promise.all([
    runNode([...args, ...testFiles]),
    values.browser ? runNode(['--run', 'test:browser']) : 0,
]);
process.exitCode = unitStatus || browserStatus;
if (unitStatus === 0 && values.coverage) {
    const requiredSources = values.include || values.exclude ? [] : globSync('buildwww/{app,lib}/**/*.tsx');
    if (!printCoverageReport(report, requiredSources)) process.exitCode = 1;
}
