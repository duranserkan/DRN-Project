import type { Reporter, TestCase } from 'vitest/node';
import { testSummary, type Failure } from './summary.ts';

function recordTestResult(test: TestCase, failures: Failure[]) {
    const result = test.result();
    if (result.state === 'pending') return 'didNotRun';
    if (result.state === 'passed' && test.diagnostic()?.flaky) return 'flaky';
    if (result.state === 'failed') {
        failures.push({
            name: test.fullName,
            duration: test.diagnostic()?.duration,
            message: result.errors.map((error) => error.stack ?? error.message).join('\n'),
        });
    }
    return result.state;
}

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
            for (const suite of module.children.allSuites()) {
                for (const error of suite.errors()) {
                    failures.push({ name: suite.fullName, message: error.stack ?? error.message });
                }
            }
            for (const test of module.children.allTests()) {
                const outcome = recordTestResult(test, failures);
                counts[outcome]++;
            }
        }
        const success = reason === 'passed' && failures.length === 0 && counts.passed + counts.flaky > 0;
        if (!success) process.exitCode = 1;
        console.log(
            testSummary('Frontend units (Vitest)', success, performance.now() - this.started, counts, failures),
        );
    }
}
