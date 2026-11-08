import { readFileSync } from 'node:fs';
import { extname, sep } from 'node:path';

export function coverageSummary(lcov: string, requiredSources: readonly string[] = []) {
    const files = new Map<string, Map<string, boolean>>();
    let source = '';
    for (const line of lcov.split(/\r?\n/)) {
        if (line.startsWith('SF:')) source = line.slice(3).split(sep).join('/');
        if (line === 'end_of_record') source = '';
        const match = /^DA:([1-9]\d*),(\d+)(?:,.*)?$/.exec(line);
        if (!source || !match) continue;
        const lines = files.get(source) ?? new Map<string, boolean>();
        lines.set(match[1]!, Number(match[2]) > 0 || lines.get(match[1]!) === true);
        files.set(source, lines);
    }
    if (!files.size) throw new Error('Missing frontend line coverage');
    for (const file of requiredSources) {
        if (!files.has(file.split(sep).join('/'))) throw new Error(`Missing React line coverage for ${file}`);
    }

    const extensions = new Map<string, { covered: number; total: number }>();
    let covered = 0;
    let total = 0;
    for (const [file, lines] of files) {
        const hits = [...lines.values()].filter(Boolean).length;
        covered += hits;
        total += lines.size;
        const ext = extname(file);
        if (!/^\.\w+$/.test(ext)) continue;
        const counts = extensions.get(ext) ?? { covered: 0, total: 0 };
        counts.covered += hits;
        counts.total += lines.size;
        extensions.set(ext, counts);
    }
    function row(scope: string, hits: number, count: number) {
        return `| ${scope} | ${hits} | ${count} | ${((100 * hits) / count).toFixed(1)}% |`;
    }
    return [
        '### Frontend unit coverage',
        '',
        '| Scope | Covered lines | Total lines | Coverage |',
        '| --- | ---: | ---: | ---: |',
        row('All frontend sources', covered, total),
        ...[...extensions.keys()].sort().map((ext) => {
            const counts = extensions.get(ext)!;
            return row(ext, counts.covered, counts.total);
        }),
    ].join('\n');
}

export function readCoverageSummary(report: string, requiredSources: readonly string[] = []) {
    return coverageSummary(readFileSync(report, 'utf8'), requiredSources);
}

if (import.meta.main) {
    const report = process.argv[2];
    if (!report) throw new Error('Expected an LCOV report path');
    console.log(readCoverageSummary(report));
}
