import { spawn } from 'node:child_process';
import { copyFileSync, globSync, mkdtempDisposableSync, statSync } from 'node:fs';
import { basename, dirname, join, matchesGlob, normalize, relative, sep } from 'node:path';
import { tmpdir } from 'node:os';
import { parseArgs } from 'node:util';
import { startVitest, type CoverageV8Options } from 'vitest/node';
import { defaultCoverageExcludes, unitTestPattern } from '../../vitest.config.ts';
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

const report = values.coverage ? coverageReportPath(values.report ?? 'coverage-frontend.info') : undefined;
using output = report ? mkdtempDisposableSync(join(tmpdir(), 'sample-unit-coverage-')) : undefined;
const coverage: CoverageV8Options = { enabled: output !== undefined };
if (output) {
    coverage.reportsDirectory = output.path;
    if (values.include) {
        coverage.include = values.include.map((pattern) => {
            if (!statSync(pattern, { throwIfNoEntry: false })?.isFile()) return pattern;
            // Vitest treats plain paths as directories. A single-choice glob matches the exact filename.
            const file = relative('.', pattern);
            return join(dirname(file), `@(${basename(file)})`)
                .split(sep)
                .join('/');
        });
    }
    // Exact test selection narrows Vitest's automatic coverage exclusions.
    coverage.exclude = [...(values.exclude ?? defaultCoverageExcludes), unitTestPattern];
} else if (values.include || values.exclude || values.report) {
    throw new Error('Coverage options require --coverage.');
}

// Resolve selectors against discovered unit tests before configuring Vitest.
const defaultPatterns = values['default-pattern'];
const unitFiles = globSync(unitTestPattern).filter(
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
// Vitest's positional filters also match substrings of other paths.
const include = Array.from(files, (file) =>
    file
        .split(sep)
        .join('/')
        .replace(/[\\()[\]{}*?!+@|]/g, '\\$&'),
);
const browser = values.browser ? runNode(['--run', 'test:browser']) : 0;
await startVitest([], { run: true, include, coverage });
const unitStatus = process.exitCode ?? 0;
const browserStatus = await browser;
process.exitCode = unitStatus || browserStatus;
if (unitStatus === 0 && report && output) {
    copyFileSync(join(output.path, 'lcov.info'), coverageReportPath(report));
    const requiredSources = values.include || values.exclude ? [] : globSync('buildwww/{app,lib}/**/*.tsx');
    if (!printCoverageReport(report, requiredSources)) process.exitCode = 1;
}
