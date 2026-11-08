import type { FullConfig, FullResult, Reporter, Suite, TestError } from '@playwright/test/reporter';
import { printCoverageReport, testSummary, type Failure } from './summary.ts';

export default class PlaywrightSummary implements Reporter {
    private readonly listMode: boolean;
    private suite?: Suite;
    private readonly failures: Failure[] = [];

    constructor(options: { _mode?: string } = {}) {
        this.listMode = options._mode === 'list';
    }

    onBegin(_config: FullConfig, suite: Suite) {
        this.suite = suite;
    }

    onError(error: TestError) {
        this.failures.push({ name: 'Playwright runner', message: error.stack ?? error.message ?? String(error.value) });
    }

    async onEnd(result: FullResult) {
        if (this.listMode) return;
        const counts = { passed: 0, failed: 0, skipped: 0, flaky: 0, didNotRun: 0 };
        for (const test of this.suite?.allTests() ?? []) {
            const outcome = test.outcome();
            const interrupted =
                outcome === 'skipped' && test.results.findLast((attempt) => attempt.status === 'interrupted');
            if (outcome === 'unexpected' || interrupted) {
                counts.failed++;
                const attempt = interrupted || test.results.at(-1);
                const errors = (attempt?.errors ?? []).map((error) => error.stack ?? error.message ?? error.value);
                if (interrupted) errors.unshift('Test was interrupted.');
                this.failures.push({
                    name: test.titlePath().filter(Boolean).join(' / '),
                    duration: attempt?.duration ?? 0,
                    message:
                        errors.join('\n') ||
                        `Expected ${test.expectedStatus}, received ${attempt?.status ?? 'no result'}`,
                });
            } else if (outcome === 'skipped' && (test.results.length === 0 || test.expectedStatus !== 'skipped')) {
                counts.didNotRun++;
            } else if (outcome === 'expected') counts.passed++;
            else counts[outcome]++;
        }
        const success = result.status === 'passed' && counts.passed + counts.flaky > 0;
        console.log(
            testSummary('Frontend browser tests (Playwright)', success, result.duration, counts, this.failures),
        );
        const coverageOk = printCoverageReport(process.env.PLAYWRIGHT_LCOV_REPORT);
        if (!success || !coverageOk) return { status: 'failed' as const };
    }
}
