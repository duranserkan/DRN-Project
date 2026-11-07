DRN.Onmount.register('[data-js-drn-footer]', function (options) {
    if (!(this instanceof HTMLElement)) return;
    const cookieName = this.dataset.preferencesCookieName!;
    const navbar = this.querySelector('[data-js-footer-navbar]')!;
    const alert = this.querySelector('[data-js-cookie-alert]')!;
    const analytics = this.querySelector<HTMLInputElement>('[data-js-analytics-consent]')!;
    const marketing = this.querySelector<HTMLInputElement>('[data-js-marketing-consent]')!;
    const controller = new AbortController();
    let preferences = DRN.Cookie.get<{ AnalyticsConsent?: boolean; MarketingConsent?: boolean }>(cookieName, {encoding: 'base64'}) || {};

    function loadPreferences() {
        analytics.checked = preferences.AnalyticsConsent || false;
        marketing.checked = preferences.MarketingConsent || false;
    }

    function showBanner(visible: boolean) {
        navbar.classList.toggle('d-flex', !visible);
        navbar.classList.toggle('d-none', visible);
        alert.classList.toggle('d-none', !visible);
        alert.classList.toggle('d-flex', visible);
    }

    function savePreferences(analyticsConsent: boolean, marketingConsent: boolean) {
        preferences = {AnalyticsConsent: analyticsConsent, MarketingConsent: marketingConsent};
        DRN.Cookie.set(cookieName, preferences, {days: 365, encoding: 'base64'});
        showBanner(false);
        loadPreferences();
    }

    this.querySelector('[data-js-accept-consent]')!.addEventListener('click', () => {
        savePreferences(true, true);
    }, {signal: controller.signal});
    this.querySelector('[data-js-reject-consent]')!.addEventListener('click', () => {
        savePreferences(false, false);
    }, {signal: controller.signal});
    this.querySelector('[data-js-save-consent]')!.addEventListener('click', () => {
        savePreferences(analytics.checked, marketing.checked);
    }, {signal: controller.signal});

    loadPreferences();
    DRN.App.ShowCookieBanner = this.dataset.showCookieBanner === 'true';
    showBanner(DRN.App.ShowCookieBanner);
    options.disposable = {dispose: () => controller.abort()};
});
