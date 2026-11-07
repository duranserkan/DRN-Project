import assert from 'node:assert/strict';
import test from 'node:test';
import { installBrowserGlobals } from '../browserGlobals.ts';
import { loadTsx } from './loadTsx.ts';

test('loading the React bundle before appPreload reports the missing prerequisite', async (t) => {
    loadTsx(t);
    installBrowserGlobals(t, { window: {} });
    await assert.rejects(import('../../../lib/react/reactBundle.tsx'), /appPreload\.ts.*DRN\.React is missing/);
});
