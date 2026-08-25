// styleManager.ts
import { getInlineStyleNonce } from './nonceHelper';

let drnSharedSheet: CSSStyleSheet | null = null;

/**
 * Initializes constructable CSSStyleSheet with bundle styles.
 */
export function initSharedStyleSheet(bundleStyles: string): CSSStyleSheet | null {
    if (typeof window !== 'undefined' && window.CSSStyleSheet) {
        try {
            drnSharedSheet = new CSSStyleSheet();
            drnSharedSheet.replaceSync(bundleStyles);
        } catch (e) {
            drnSharedSheet = null;
            console.warn("[DRN] Constructable stylesheets not supported, falling back to <style> tags", e);
        }
    }
    return drnSharedSheet;
}

export function getSharedStyleSheet(): CSSStyleSheet | null {
    return drnSharedSheet;
}

/**
 * Ensures bundle styles are present in the document (Light DOM support).
 */
export function ensureDocumentStyles(bundleStyles: string): void {
    if (typeof document === 'undefined') return;

    if (drnSharedSheet && document.adoptedStyleSheets) {
        if (!document.adoptedStyleSheets.includes(drnSharedSheet)) {
            document.adoptedStyleSheets = [...document.adoptedStyleSheets, drnSharedSheet];
        }
        return;
    }

    const styleId = 'drn-react-bundle-styles';
    if (document.getElementById(styleId)) return;

    const styleTag = document.createElement('style');
    styleTag.id = styleId;
    const nonce = getInlineStyleNonce();
    if (nonce) {
        styleTag.nonce = nonce;
        styleTag.setAttribute('nonce', nonce);
    }
    styleTag.textContent = bundleStyles;
    document.head.appendChild(styleTag);
}

/**
 * Sets up Shadow DOM container and attaches adopted or fallback stylesheets.
 */
export function setupShadowDomContainer(domElement: HTMLElement, bundleStyles: string): HTMLElement {
    const shadow = domElement.shadowRoot || domElement.attachShadow({ mode: 'open' });
    if (drnSharedSheet && shadow.adoptedStyleSheets) {
        if (!shadow.adoptedStyleSheets.includes(drnSharedSheet)) {
            shadow.adoptedStyleSheets = [...shadow.adoptedStyleSheets, drnSharedSheet];
        }
    } else {
        const styleId = 'drn-shadow-dom-styles';
        if (!shadow.querySelector(`#${styleId}`)) {
            const styleTag = document.createElement('style');
            styleTag.id = styleId;
            const nonce = getInlineStyleNonce();
            if (nonce) {
                styleTag.nonce = nonce;
                styleTag.setAttribute('nonce', nonce);
            }
            styleTag.textContent = bundleStyles;
            shadow.appendChild(styleTag);
        }
    }

    let portalHost = shadow.querySelector('#drn-portal-root') as HTMLDivElement | null;
    if (!portalHost) {
        portalHost = document.createElement('div');
        portalHost.id = 'drn-portal-root';
        portalHost.className = 'drn-react-root drn-react-portal-root';
        shadow.appendChild(portalHost);
    }
    return portalHost;
}

/**
 * Resolves whether to mount inside Shadow DOM or Light DOM.
 */
export function resolveMountContainer(domElement: HTMLElement, useShadow: boolean, bundleStyles: string): HTMLElement {
    ensureDocumentStyles(bundleStyles);
    if (useShadow) {
        return setupShadowDomContainer(domElement, bundleStyles);
    }
    if (!domElement.classList.contains('drn-react-root')) {
        domElement.classList.add('drn-react-root');
    }
    return domElement;
}
