//reactBundle.tsx
import bundleStyles from './reactBundle.css?inline';

import React from 'react';
import { createRoot, type Root } from 'react-dom/client';
import type { ReactComponentRegistry, RootData, ReactMountOptions } from "@/types/DrnReactTypes.ts";
import { HelloReactComponent } from './components/HelloReactComponent';
import { IslandErrorBoundary } from './IslandErrorBoundary';
import { getGlobalPortalContainer } from './portalHost';
import { PortalContext } from './PortalContext';
import { getInlineStyleNonce } from './nonceHelper';
import { initSharedStyleSheet, ensureDocumentStyles, resolveMountContainer } from './styleManager';

const rootMap = new WeakMap<HTMLElement, RootData>();

const COMPONENT_REGISTRY: ReactComponentRegistry = {
    'HelloReact': HelloReactComponent
};

// --- Namespace Verification & Initialization ---
if (!window.DRN?.React) {
    throw new Error("[DRN] Critical Error: 'appPreload.js' has not been loaded. DRN namespace is missing.");
}

// --- Initialize Shared Stylesheet & Document Styles ---
const drnSharedSheet = initSharedStyleSheet(bundleStyles);
if (typeof document !== 'undefined') {
    ensureDocumentStyles(bundleStyles);
}

// --- Mount API ---
window.DRN.React.mount = <K extends keyof ReactComponentRegistry>(
    name: K,
    domElement: HTMLElement | null,
    initialProps: React.ComponentProps<ReactComponentRegistry[K]>,
    options: ReactMountOptions = {}
) => {
    // Safety Checks
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
    const { useShadow = true } = options;

    let record = rootMap.get(domElement) as RootData<Props> | undefined;

    // Clean up existing roots if re-mounting a different component or changing shadow mode
    if (record && (record.name !== name || record.isShadow !== useShadow)) {
        record.root.unmount();
        rootMap.delete(domElement);
        record = undefined;
    }

    if (!record) {
        domElement.innerHTML = '';
    }

    const mountNode = resolveMountContainer(domElement, useShadow, bundleStyles);

    let root: Root;
    if (record) {
        root = record.root;
        record.currentProps = initialProps;
    } else {
        root = createRoot(mountNode);
        record = { root, name, isShadow: useShadow, currentProps: initialProps };
        rootMap.set(domElement, record);
    }

    const portalContainer = typeof document !== 'undefined'
        ? getGlobalPortalContainer(drnSharedSheet, bundleStyles, getInlineStyleNonce())
        : null;

    // React.createElement avoids TS2769 from JSX spreading generic indexed-access component types
    const renderApp = (props: Props) => (
        <React.StrictMode>
            <IslandErrorBoundary>
                <PortalContext.Provider value={portalContainer}>
                    {React.createElement(Component as React.ElementType, props)}
                </PortalContext.Provider>
            </IslandErrorBoundary>
        </React.StrictMode>
    );

    root.render(renderApp(record.currentProps as Props));

    const capturedRecord = record;

    return {
        update: (newProps: Partial<Props>) => {
            if (rootMap.get(domElement) !== capturedRecord) return;
            capturedRecord.currentProps = { ...capturedRecord.currentProps, ...newProps } as Props;
            capturedRecord.root.render(renderApp(capturedRecord.currentProps));
        },
        getProps: () => {
            if (rootMap.get(domElement) !== capturedRecord) return null;
            return { ...capturedRecord.currentProps } as Props;
        },
        dispose: () => {
            if (rootMap.get(domElement) !== capturedRecord) return;
            capturedRecord.root.unmount();
            rootMap.delete(domElement);
        }
    };
};
