import type { Reporter } from 'vitest/node';
import { testSummary, type Failure } from './summary.ts';

export default class VitestSummary implements Reporter {
    private started = 0;

    onTestRunStart() {
        this.started = performance.now();
    }

    onTestRunEnd(...[modules, errors, reason]: Parameters<NonNullable<Reporter['onTestRunEnd']>>) {
        const counts = { passed: 0, failed: 0, skipped: 0, flaky: 0, didNotRun: 0 };
        const failures: Failure[] = errors.map((error) => ({
            name: 'Vitest runner',
            message: error.stack ?? error.message,
        }));
        for (const module of modules) {
            for (const error of module.errors()) {
                failures.push({ name: module.moduleId, message: error.stack ?? error.message });
            }
            for (const test of module.children.allTests()) {
                const result = test.result();
                if (result.state === 'pending') counts.didNotRun++;
                else if (result.state === 'passed' && test.diagnostic()?.flaky) counts.flaky++;
                else counts[result.state]++;
                if (result.state === 'failed') {
                    failures.push({
                        name: test.fullName,
                        duration: test.diagnostic()?.duration,
                        message: result.errors.map((e) => e.stack ?? e.message).join('\n'),
                    });
                }
            }
        }
        const success = reason === 'passed' && failures.length === 0 && counts.passed + counts.flaky > 0;
        if (!success) process.exitCode = 1;
        console.log(
            testSummary('Frontend units (Vitest)', success, performance.now() - this.started, counts, failures),
        );
    }
}
