//reactBundle.tsx
import bundleStyles from './reactBundle.css?inline';

import React from 'react';
import {createRoot, type Root} from 'react-dom/client';
import type {ReactComponentRegistry, RootData, ReactMountOptions} from "@/types/DrnReactTypes.ts";
import {HelloReactComponent} from './components/HelloReactComponent';

const rootMap = new WeakMap<HTMLElement, RootData>();
const COMPONENT_REGISTRY: ReactComponentRegistry = {
    'HelloReact': HelloReactComponent
};

const getInlineStyleNonce = () => {
    let nonce = '';
    const meta = document.querySelector<HTMLMetaElement>('meta[name="htmx-config"]');
    if (meta) {
        nonce = meta.getAttribute('inlineStyleNonce') || meta.getAttribute('inlineScriptNonce') || '';

        if (!nonce && meta.content) {
            try {
                const parsed = JSON.parse(meta.content);
                nonce = typeof parsed.inlineStyleNonce === 'string' ? parsed.inlineStyleNonce : (typeof parsed.inlineScriptNonce === 'string' ? parsed.inlineScriptNonce : '');
            } catch {
                // ignore
            }
        }
    }

    if (!nonce) {
        const nonceElement = document.querySelector<HTMLElement>('script[nonce], style[nonce]');
        nonce = nonceElement?.nonce || nonceElement?.getAttribute('nonce') || '';
    }

    if (nonce && typeof window !== 'undefined') {
        (window as any).__webpack_nonce__ = nonce;
    }

    return nonce;
};

class IslandErrorBoundary extends React.Component<{ children: React.ReactNode }, { hasError: boolean }> {
    constructor(props: { children: React.ReactNode }) {
        super(props);
        this.state = {hasError: false};
    }

    static getDerivedStateFromError() {
        return {hasError: true};
    }

    override componentDidCatch(error: Error, info: React.ErrorInfo) {
        console.error("DRN Island crashed:", error, info);
    }

    override render() {
        if (this.state.hasError) {
            return <div className="drn-error-fallback">Failed to load component</div>;
        }

        return this.props.children;
    }
}

if (!window.DRN?.React) {
    console.error("Critical Error: 'appPreload.js' has not been loaded. DRN namespace is missing.");
}

// --- Initialize Stylesheet Once ---
let drnSharedSheet: CSSStyleSheet | null = null;
if (window.CSSStyleSheet)
    try {
        drnSharedSheet = new CSSStyleSheet();
        drnSharedSheet.replaceSync(bundleStyles);
    } catch (e) {
        drnSharedSheet = null;
        console.warn("[DRN] Constructable stylesheets not supported, falling back to <style> tags", e);
    }

function ensureDocumentStyles(): void {
    getInlineStyleNonce();

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

if (typeof document !== 'undefined') {
    getInlineStyleNonce();
    ensureDocumentStyles();
}

function setupShadowDomContainer(domElement: HTMLElement): HTMLElement {
    getInlineStyleNonce();
    const shadow = domElement.shadowRoot || domElement.attachShadow({mode: 'open'});
    if (drnSharedSheet && shadow.adoptedStyleSheets) {
        if (!shadow.adoptedStyleSheets.includes(drnSharedSheet))
            shadow.adoptedStyleSheets = [...shadow.adoptedStyleSheets, drnSharedSheet];
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

    let portalHost = shadow.querySelector('#drn-portal-root') as HTMLDivElement;
    if (!portalHost) {
        portalHost = document.createElement('div');
        portalHost.id = 'drn-portal-root';
        portalHost.className = 'drn-react-root drn-react-portal-root';
        shadow.appendChild(portalHost);
    }
    return portalHost;
}

function resolveMountContainer(domElement: HTMLElement, useShadow: boolean): HTMLElement {
    getInlineStyleNonce();
    ensureDocumentStyles();
    if (useShadow) {
        return setupShadowDomContainer(domElement);
    }
    if (!domElement.classList.contains('drn-react-root'))
        domElement.classList.add('drn-react-root');
    return domElement;
}

window.DRN.React.mount = <K extends keyof ReactComponentRegistry>(
    name: K,
    domElement: HTMLElement | null,
    initialProps: React.ComponentProps<ReactComponentRegistry[K]>,
    options: ReactMountOptions = {}
) => {
    //  Safety Checks
    if (!domElement) {
        console.warn(`DRN.React: DOM element is null for component '${name}'`);
        return null;
    }

    const Component = COMPONENT_REGISTRY[name];
    if (!Component) {
        console.error(`[DRN] Component '${name}' not registered. Available: ${Object.keys(COMPONENT_REGISTRY).join(', ')}`);
        return null;
    }

    type Props = React.ComponentProps<ReactComponentRegistry[K]>;

    const {useShadow = true} = options; // Default to TRUE — Shadow DOM provides style isolation from Bootstrap
    let record = rootMap.get(domElement) as RootData<Props> | undefined;
    // Clean up existing roots if re-mounting different component
    if (record && (record.name !== name || record.isShadow !== useShadow)) {
        record.root.unmount();
        rootMap.delete(domElement);
        record = undefined;
    }

    let root: Root;
    if (!record) {
        // Clear pre-rendered/fallback content from the Light DOM before mounting React
        domElement.innerHTML = '';
    }

    const mountNode = resolveMountContainer(domElement, useShadow);

    if (record) {
        root = record.root;
        record.currentProps = initialProps;
    } else {
        root = createRoot(mountNode); // createRoot takes the container (either shadowRoot or the element itself)
        record = {root, name, isShadow: useShadow, currentProps: initialProps};
        rootMap.set(domElement, record);
    }

    // React.createElement on the line below avoids TS2769 from JSX spreading
    // generic indexed-access component types; the outer JSX wrappers are fine.
    const renderApp = (props: Props) => (
        <React.StrictMode>
            <IslandErrorBoundary>
                {React.createElement(Component as React.ElementType, props)}
            </IslandErrorBoundary>
        </React.StrictMode>
    );

    root.render(renderApp(record.currentProps as Props));

    const capturedRecord = record;

    return {
        update: (newProps: Partial<Props>) => {
            if (rootMap.get(domElement) !== capturedRecord) return;
            capturedRecord.currentProps = {...capturedRecord.currentProps, ...newProps} as Props;
            capturedRecord.root.render(renderApp(capturedRecord.currentProps));
        },
        getProps: () => {
            if (rootMap.get(domElement) !== capturedRecord) return null;
            return {...capturedRecord.currentProps} as Props;
        },
        dispose: () => {
            if (rootMap.get(domElement) !== capturedRecord) return;
            capturedRecord.root.unmount();
            rootMap.delete(domElement);
        }
    };
};
