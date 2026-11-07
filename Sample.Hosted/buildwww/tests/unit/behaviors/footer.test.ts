import assert from 'node:assert/strict';
import test from 'node:test';
import { installBrowserGlobals } from '../browserGlobals.ts';

test('footer restores consent, writes each choice, and releases click handlers on disposal', async (t) => {
    class Input extends EventTarget {
        checked = false;
    }
    const analytics = new Input();
    const marketing = new Input();
    const controls = { accept: new EventTarget(), reject: new EventTarget(), save: new EventTarget() };
    const navbarClasses = new Set<string>();
    const bannerClasses = new Set<string>();
    const classList = (classes: Set<string>) => ({
        toggle(name: string, enabled: boolean) {
            if (enabled) classes.add(name);
            else classes.delete(name);
        },
    });
    const elements = new Map<string, unknown>([
        ['[data-js-footer-navbar]', { classList: classList(navbarClasses) }],
        ['[data-js-cookie-alert]', { classList: classList(bannerClasses) }],
        ['[data-js-analytics-consent]', analytics],
        ['[data-js-marketing-consent]', marketing],
        ...Object.entries(controls).map(([action, button]): [string, EventTarget] => [
            `[data-js-${action}-consent]`,
            button,
        ]),
    ]);
    class Footer {
        dataset = { preferencesCookieName: 'site-preferences', showCookieBanner: 'true' };
        querySelector(selector: string) {
            return elements.get(selector);
        }
    }
    const footer = new Footer();
    const options: { disposable?: { dispose(): void } } = {};
    const writes = t.mock.fn();
    const get = t.mock.fn(() => ({ AnalyticsConsent: true, MarketingConsent: false }));
    installBrowserGlobals(t, {
        HTMLElement: Footer,
        HTMLInputElement: Input,
        DRN: {
            App: {},
            Cookie: { get, set: writes },
            Onmount: {
                register(selector: string, callback: (this: Footer, options: object) => void) {
                    assert.equal(selector, '[data-js-drn-footer]');
                    callback.call(footer, options);
                },
            },
        },
    });
    await import('../../../app/ts/behaviors/layout/drn-footer.ts');
    assert.deepEqual(get.mock.calls[0]?.arguments, ['site-preferences', { encoding: 'base64' }]);
    assert.deepEqual([analytics.checked, marketing.checked], [true, false]);
    assert.deepEqual(navbarClasses, new Set(['d-none']));
    assert.deepEqual(bannerClasses, new Set(['d-flex']));
    controls.accept.dispatchEvent(new Event('click'));
    assert.deepEqual(navbarClasses, new Set(['d-flex']));
    assert.deepEqual(bannerClasses, new Set(['d-none']));
    controls.reject.dispatchEvent(new Event('click'));
    marketing.checked = true;
    controls.save.dispatchEvent(new Event('click'));
    const expected = [
        [true, true],
        [false, false],
        [false, true],
    ].map(([AnalyticsConsent, MarketingConsent]) => [
        'site-preferences',
        { AnalyticsConsent, MarketingConsent },
        { days: 365, encoding: 'base64' },
    ]);
    assert.deepEqual(
        writes.mock.calls.map((call) => call.arguments),
        expected,
    );
    assert.ok(options.disposable);
    options.disposable.dispose();
    for (const button of Object.values(controls)) button.dispatchEvent(new Event('click'));
    assert.equal(writes.mock.callCount(), 3);
});
