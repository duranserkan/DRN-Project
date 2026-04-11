//reactBundle.tsx
import './reactBundle.css'
import bundleStyles from './reactBundle.css?inline';

import React from 'react';
import { createRoot, type Root } from 'react-dom/client';
import type { ReactComponentRegistry, RootData } from "@/types/DrnReactTypes.ts";
import { HelloReactComponent } from './components/HelloReactComponent';

const rootMap = new WeakMap<HTMLElement, RootData>();
const COMPONENT_REGISTRY: ReactComponentRegistry = {
    'HelloReact': HelloReactComponent
};

class IslandErrorBoundary extends React.Component<{ children: React.ReactNode }, { hasError: boolean }> {
    constructor(props: { children: React.ReactNode }) {
        super(props);
        this.state = { hasError: false };
    }

    static getDerivedStateFromError() {
        return { hasError: true };
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

if (!window.DRN || !window.DRN.React) {
    console.error("Critical Error: 'appPreload.js' has not been loaded. DRN namespace is missing.");
}

// --- Initialize Stylesheet Once ---
let drnSharedSheet: CSSStyleSheet | null = null;
if (window.CSSStyleSheet)
    try {
        drnSharedSheet = new CSSStyleSheet();
        drnSharedSheet.replaceSync(bundleStyles);
    } catch (e) {
        console.warn("[DRN] Constructable stylesheets not supported, falling back to <style> tags");
    }

window.DRN.React.mount = (name, domElement, initialProps, options = {}) => {
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

    const { useShadow = true } = options; // Default to TRUE — Shadow DOM provides style isolation from Bootstrap
    let record = rootMap.get(domElement);
    // Clean up existing roots if re-mounting different component
    if (record && (record.name !== name || record.isShadow !== useShadow)) {
        record.root.unmount();
        rootMap.delete(domElement);
        record = undefined;
    }

    let root: Root;
    let mountNode: HTMLElement | ShadowRoot = domElement;
    let portalHost: HTMLDivElement | null = null;
    // -----------------------------------------------------------
    // SHADOW DOM SETUP
    // -----------------------------------------------------------
    if (useShadow) {
        const shadow = domElement.shadowRoot || domElement.attachShadow({ mode: 'open' });
        if (drnSharedSheet && shadow.adoptedStyleSheets) {
            if (!shadow.adoptedStyleSheets.includes(drnSharedSheet)) // Avoid adding it duplicate times
                shadow.adoptedStyleSheets = [...shadow.adoptedStyleSheets, drnSharedSheet];
        } else {
            // Fallback for older browsers: <style> tag
            const styleId = 'drn-shadow-dom-styles';
            if (!shadow.querySelector(`#${styleId}`)) {
                const styleTag = document.createElement('style');
                styleTag.id = styleId;
                styleTag.textContent = bundleStyles;
                shadow.appendChild(styleTag);
            }
        }

        portalHost = shadow.querySelector('#drn-portal-root') as HTMLDivElement;
        if (!portalHost) {
            portalHost = document.createElement('div');
            portalHost.id = 'drn-portal-root';
            portalHost.className = 'drn-react-root';
            portalHost.style.display = 'contents';
            shadow.appendChild(portalHost);
        }
        mountNode = portalHost;
    } else {
        // Light DOM: add scoping class so .drn-react-root selectors match
        if (!domElement.classList.contains('drn-react-root'))
            domElement.classList.add('drn-react-root');
    }
    // -----------------------------------------------------------
    if (record) {
        root = record.root;
    } else {
        root = createRoot(mountNode); // createRoot takes the container (either shadowRoot or the element itself)
        rootMap.set(domElement, { root, name, isShadow: useShadow });
    }

    let currentProps = initialProps;
    // React.createElement on the line below avoids TS2769 from JSX spreading
    // generic indexed-access component types; the outer JSX wrappers are fine.
    const renderApp = (props: React.ComponentProps<ReactComponentRegistry[typeof name]>) => (
        <React.StrictMode>
            <IslandErrorBoundary>
                {React.createElement(Component, props)}
            </IslandErrorBoundary>
        </React.StrictMode>
    );

    root.render(renderApp(currentProps));

    return {
        update: (newProps: Partial<typeof currentProps>) => {
            if (!rootMap.has(domElement)) return; // guard against post-dispose calls
            currentProps = { ...currentProps, ...newProps };
            root.render(renderApp(currentProps));
        },
        dispose: () => {
            const current = rootMap.get(domElement);
            if (!current)
                return;

            current.root.unmount();
            rootMap.delete(domElement);
        }
    };
};