import type { TestEvent } from 'node:test/reporters';
import { inspect } from 'node:util';
import { testSummary, type Failure } from './summary.ts';

export default async function* report(events: AsyncIterable<TestEvent>) {
    const failures: Failure[] = [];
    for await (const event of events) {
        if (event.type === 'test:fail' && !event.data.todo && !event.data.skip) {
            const { details, name } = event.data;
            if ('failureType' in details.error && details.error.failureType === 'subtestsFailed') continue;
            failures.push({
                name,
                duration: details.duration_ms,
                message: inspect(details.error.cause ?? details.error),
            });
        }
        if (event.type === 'test:summary' && event.data.file === undefined) {
            const { counts, success, duration_ms } = event.data;
            const skipped = counts.skipped + counts.todo;
            const failed = counts.tests - counts.passed - skipped;
            yield testSummary(
                'Frontend units (Node.js)',
                success,
                duration_ms,
                { passed: counts.passed, failed, skipped, flaky: 0 },
                failures,
            );
        }
    }
}
