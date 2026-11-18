import { test } from 'vitest';
import assert from 'node:assert/strict';
import { installBrowserGlobals } from '../browserGlobals.ts';

test('loading the React bundle before appPreload reports the missing prerequisite', async () => {
    installBrowserGlobals({ window: {} });
    await assert.rejects(import('../../../lib/react/reactBundle.tsx'), /appPreload\.ts.*DRN\.React is missing/);
});
