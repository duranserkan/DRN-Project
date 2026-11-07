DRN.Onmount.register('[data-js-drn-footer]', function (options) {
    if (!(this instanceof HTMLElement)) return;
    const cookieName = this.dataset.preferencesCookieName;
    if (!cookieName?.trim()) {
        console.warn('Footer mount skipped: data-preferences-cookie-name is missing or blank.');
        return;
    }
    const navbar = this.querySelector('[data-js-footer-navbar]');
    const alert = this.querySelector('[data-js-cookie-alert]');
    const analytics = this.querySelector('[data-js-analytics-consent]');
    const marketing = this.querySelector('[data-js-marketing-consent]');
    const accept = this.querySelector('[data-js-accept-consent]');
    const reject = this.querySelector('[data-js-reject-consent]');
    const save = this.querySelector('[data-js-save-consent]');
    if (
        !navbar ||
        !alert ||
        !(analytics instanceof HTMLInputElement) ||
        !(marketing instanceof HTMLInputElement) ||
        !accept ||
        !reject ||
        !save
    ) {
        console.warn(
            'Footer mount skipped: expected a navbar, cookie alert, both consent inputs, and accept, reject, and save controls.',
            this,
        );
        return;
    }
    const controller = new AbortController();
    let preferences =
        DRN.Cookie.get<{ AnalyticsConsent?: boolean; MarketingConsent?: boolean }>(cookieName, {
            encoding: 'base64',
        }) || {};

    const loadPreferences = () => {
        analytics.checked = preferences.AnalyticsConsent || false;
        marketing.checked = preferences.MarketingConsent || false;
    };

    const showBanner = (visible: boolean) => {
        navbar.classList.toggle('d-flex', !visible);
        navbar.classList.toggle('d-none', visible);
        alert.classList.toggle('d-none', !visible);
        alert.classList.toggle('d-flex', visible);
    };

    const savePreferences = (analyticsConsent: boolean, marketingConsent: boolean) => {
        preferences = { AnalyticsConsent: analyticsConsent, MarketingConsent: marketingConsent };
        DRN.Cookie.set(cookieName, preferences, { days: 365, encoding: 'base64' });
        showBanner(false);
        loadPreferences();
    };

    accept.addEventListener(
        'click',
        () => {
            savePreferences(true, true);
        },
        { signal: controller.signal },
    );
    reject.addEventListener(
        'click',
        () => {
            savePreferences(false, false);
        },
        { signal: controller.signal },
    );
    save.addEventListener(
        'click',
        () => {
            savePreferences(analytics.checked, marketing.checked);
        },
        { signal: controller.signal },
    );

    loadPreferences();
    DRN.App.ShowCookieBanner = this.dataset.showCookieBanner === 'true';
    showBanner(DRN.App.ShowCookieBanner);
    options.disposable = { dispose: () => controller.abort() };
});
