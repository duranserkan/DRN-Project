DRN.Onmount.register('[data-js-drn-footer]', function (options) {
    const footer = this;
    const cookieName = footer.dataset.preferencesCookieName;
    const navbar = footer.querySelector('[data-js-footer-navbar]');
    const alert = footer.querySelector('[data-js-cookie-alert]');
    const analytics = footer.querySelector('[data-js-analytics-consent]');
    const marketing = footer.querySelector('[data-js-marketing-consent]');
    const controller = new AbortController();
    let preferences = DRN.Cookie.get(cookieName, {encoding: 'base64'}) || {};

    function loadPreferences() {
        analytics.checked = preferences.AnalyticsConsent || false;
        marketing.checked = preferences.MarketingConsent || false;
    }

    function showBanner(visible) {
        navbar.classList.toggle('d-flex', !visible);
        navbar.classList.toggle('d-none', visible);
        alert.classList.toggle('d-none', !visible);
        alert.classList.toggle('d-flex', visible);
    }

    function savePreferences(analyticsConsent, marketingConsent) {
        preferences = {AnalyticsConsent: analyticsConsent, MarketingConsent: marketingConsent};
        DRN.Cookie.set(cookieName, preferences, {days: 365, encoding: 'base64'});
        showBanner(false);
        loadPreferences();
    }

    footer.querySelector('[data-js-accept-consent]').addEventListener('click', () => {
        savePreferences(true, true);
    }, {signal: controller.signal});
    footer.querySelector('[data-js-reject-consent]').addEventListener('click', () => {
        savePreferences(false, false);
    }, {signal: controller.signal});
    footer.querySelector('[data-js-save-consent]').addEventListener('click', () => {
        savePreferences(analytics.checked, marketing.checked);
    }, {signal: controller.signal});

    loadPreferences();
    DRN.App.ShowCookieBanner = footer.dataset.showCookieBanner === 'true';
    showBanner(DRN.App.ShowCookieBanner);
    options.disposable = {dispose: () => controller.abort()};
});
