import { spawn } from 'node:child_process';
import { copyFileSync, globSync, mkdtempDisposableSync, statSync } from 'node:fs';
import { basename, dirname, join, matchesGlob, normalize, relative, sep } from 'node:path';
import { tmpdir } from 'node:os';
import { fileURLToPath } from 'node:url';
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

const args = [fileURLToPath(import.meta.resolve('vitest/package.json')).replace(/package\.json$/, 'vitest.mjs'), 'run'];
const report = values.coverage ? coverageReportPath(values.report ?? 'coverage-frontend.info') : undefined;
using output = report ? mkdtempDisposableSync(join(tmpdir(), 'sample-unit-coverage-')) : undefined;
if (values.coverage) {
    args.push('--coverage.enabled', '--coverage.reportsDirectory=' + output!.path);
    for (let pattern of values.include ?? []) {
        if (statSync(pattern, { throwIfNoEntry: false })?.isFile()) {
            // Vitest treats plain paths as directories. A single-choice glob matches the exact filename.
            const file = relative('.', pattern);
            pattern = join(dirname(file), `@(${basename(file)})`)
                .split(sep)
                .join('/');
        }
        args.push(`--coverage.include=${pattern}`);
    }
    for (const glob of values.exclude ?? []) args.push(`--coverage.exclude=${glob}`);
} else if (values.include || values.exclude || values.report) {
    throw new Error('Coverage options require --coverage.');
}

// Only discovered unit tests can become Node operands. CLI selectors never become Node options.
const defaultPatterns = values['default-pattern'];
const unitFiles = globSync('buildwww/tests/**/*.test.{js,ts}').filter(
    (file) => !defaultPatterns || defaultPatterns.some((pattern) => matchesGlob(file, pattern)),
);
if (!unitFiles.length) {
    const filter = defaultPatterns?.length ? ` for ${defaultPatterns.join(', ')}` : '';
    throw new Error(`No matching frontend unit tests found${filter}`);
}
const selectedFiles = new Set<string>();
for (const selector of testFiles) {
    const pattern = normalize(selector);
    const matches = unitFiles.filter((file) => matchesGlob(file, pattern));
    if (!matches.length) throw new Error(`No matching frontend unit tests found for ${selector}`);
    for (const file of matches) selectedFiles.add(file);
}
const files = testFiles.length ? selectedFiles : unitFiles;
const [unitStatus, browserStatus] = await Promise.all([
    runNode([...args, ...files]),
    values.browser ? runNode(['--run', 'test:browser']) : 0,
]);
process.exitCode = unitStatus || browserStatus;
if (unitStatus === 0 && report && output) {
    copyFileSync(join(output.path, 'lcov.info'), coverageReportPath(report));
    const requiredSources = values.include || values.exclude ? [] : globSync('buildwww/{app,lib}/**/*.tsx');
    if (!printCoverageReport(report, requiredSources)) process.exitCode = 1;
}
