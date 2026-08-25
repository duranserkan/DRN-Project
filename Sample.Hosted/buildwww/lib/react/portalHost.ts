// portalHost.ts
let globalPortalContainer: HTMLElement | null = null;

export function getGlobalPortalContainer(
    sharedSheet: CSSStyleSheet | null,
    fallbackStyles: string,
    nonce: string
): HTMLElement {
    // Re-verify that container is still connected to the active DOM (HTMX body swap guard)
    if (globalPortalContainer?.isConnected) {
        return globalPortalContainer;
    }

    let host = document.getElementById('drn-global-portals');
    if (!host || !host.isConnected) {
        host = document.createElement('div');
        host.id = 'drn-global-portals';
        // Zero-box host to avoid altering document.body flow
        host.style.cssText = 'position:fixed;top:0;left:0;width:0;height:0;z-index:1050;pointer-events:none;';
        document.body.appendChild(host);
    }

    const shadow = host.shadowRoot || host.attachShadow({mode: 'open'});

    // Adopt shared stylesheet or fallback <style>
    if (sharedSheet && shadow.adoptedStyleSheets) {
        if (!shadow.adoptedStyleSheets.includes(sharedSheet)) {
            shadow.adoptedStyleSheets = [...shadow.adoptedStyleSheets, sharedSheet];
        }
    } else if (!shadow.querySelector('#drn-portal-styles')) {
        const style = document.createElement('style');
        style.id = 'drn-portal-styles';
        if (nonce) {
            style.nonce = nonce;
            style.setAttribute('nonce', nonce);
        }
        style.textContent = fallbackStyles;
        shadow.appendChild(style);
    }

    let container = shadow.querySelector('#portal-root') as HTMLElement | null;
    if (!container) {
        container = document.createElement('div');
        container.id = 'portal-root';
        // Scoped class for component styles and Tailwind utilities
        container.className = 'drn-react-root';
        shadow.appendChild(container);
    }

    globalPortalContainer = container;
    return globalPortalContainer;
}
