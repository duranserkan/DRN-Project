import { appendFileSync } from 'node:fs';
import { stripVTControlCharacters } from 'node:util';
import { readCoverageSummary } from '../../../../.github/actions/frontend-validation/coverage.mts';

function publishSummary(markdown: string) {
    const output = `\n${markdown}\n`;
    if (process.env.GITHUB_STEP_SUMMARY) appendFileSync(process.env.GITHUB_STEP_SUMMARY, output);
    return output;
}

export type Failure = { name: string; message: string; duration?: number };

export function printCoverageReport(report?: string, requiredSources: readonly string[] = []) {
    if (!report) return true;
    try {
        const markdown = readCoverageSummary(report, requiredSources);
        console.log(process.env.FRONTEND_ACTION_SUMMARIZES_COVERAGE === 'true' ? markdown : publishSummary(markdown));
        return true;
    } catch (error) {
        const message = error instanceof Error ? error.message : String(error);
        console.log(publishSummary(`### ❌ Frontend coverage failed\n\n<pre>${escapeHtml(message)}</pre>`));
        return false;
    }
}

function escapeHtml(text: string) {
    return stripVTControlCharacters(text).replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;');
}

export function testSummary(
    suite: string,
    success: boolean,
    duration: number,
    counts: { passed: number; failed: number; skipped: number; flaky: number; didNotRun?: number },
    failures: Failure[],
) {
    const { passed, failed, skipped, flaky, didNotRun = 0 } = counts;
    const total = passed + failed + skipped + flaky + didNotRun;
    const elapsed = `${(duration / 1000).toFixed(2)}s`;
    const outcomes = [`${passed}/${total} passed`];
    if (failed) outcomes.push(`${failed} failed`);
    if (skipped) outcomes.push(`${skipped} skipped`);
    if (flaky) outcomes.push(`${flaky} flaky`);
    if (didNotRun) outcomes.push(`${didNotRun} did not run`);
    const status = success ? '✅' : '❌';
    const consoleSummary = `\n${status} ${suite} | ${outcomes.join(', ')} in ${elapsed}\n`;
    if (!process.env.GITHUB_STEP_SUMMARY) return consoleSummary;

    const headers = ['Total', 'Passed', 'Failed', 'Skipped', 'Flaky'];
    const values: (number | string)[] = [total, passed, failed, skipped, flaky];
    if (didNotRun) {
        headers.push('Did not run');
        values.push(didNotRun);
    }
    headers.push('Duration');
    values.push(elapsed);
    const lines = [
        `## ${status} Test Run Summary - ${suite}`,
        '',
        `| ${headers.join(' | ')} |`,
        `| ${headers.map(() => '---:').join(' | ')} |`,
        `| ${values.join(' | ')} |`,
    ];
    if (failures.length) lines.push('', `### ❌ Failures (${failures.length})`);
    for (const failure of failures.slice(0, 25)) {
        const elapsed = failure.duration === undefined ? '' : ` - ${Math.round(failure.duration)}ms`;
        lines.push(
            '',
            `<details><summary><code>${escapeHtml(failure.name.slice(0, 512))}</code>${elapsed}</summary>`,
            '',
            `<pre>${escapeHtml(failure.message.slice(0, 4000))}</pre>`,
            '</details>',
        );
    }
    if (failures.length > 25) lines.push('', 'See the test log for additional failures.');
    publishSummary(lines.join('\n'));
    return consoleSummary;
}
