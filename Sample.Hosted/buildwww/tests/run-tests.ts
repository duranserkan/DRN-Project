import { spawnSync } from 'node:child_process';
import { globSync, readFileSync } from 'node:fs';
import { sep } from 'node:path';
import { parseArgs } from 'node:util';

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

function runNode(args: string[]) {
    const result = spawnSync(process.execPath, args, { stdio: 'inherit' });
    if (result.error) throw result.error;
    if (result.status !== 0) process.exit(result.status ?? 1);
}

const args = ['--test'];
const report = values.report ?? 'coverage-frontend.info';
if (values.coverage) {
    args.push('--enable-source-maps', '--experimental-test-coverage', '--test-coverage-include-all');
    for (const glob of values.include ?? ['buildwww/{app,lib}/**/*.{ts,tsx,js}'])
        args.push(`--test-coverage-include=${glob}`);
    for (const glob of values.exclude ?? ['**/*.d.ts']) args.push(`--test-coverage-exclude=${glob}`);
    args.push('--test-reporter=spec', '--test-reporter-destination=stdout', '--test-reporter=lcov');
    args.push(`--test-reporter-destination=${report}`);
} else if (values.include || values.exclude || values.report) {
    throw new Error('Coverage options require --coverage.');
}

runNode([...args, ...testFiles]);
if (values.coverage && !values.include && !values.exclude) {
    // Node discovers unloaded JS and TS files. Every TSX module must be exercised by the suite.
    const reported = new Set<string>();
    let source = '';
    for (const line of readFileSync(report, 'utf8').split(/\r?\n/)) {
        if (line.startsWith('SF:')) source = line.slice(3).split(sep).join('/');
        if (/^DA:[1-9]\d*,\d+$/.test(line)) reported.add(source);
    }
    for (const file of globSync('buildwww/{app,lib}/**/*.tsx')) {
        if (!reported.has(file.split(sep).join('/'))) throw new Error(`Missing React line coverage for ${file}`);
    }
}
if (values.browser) runNode(['--run', 'test:browser']);
