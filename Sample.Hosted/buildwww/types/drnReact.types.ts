import type React from 'react';
import type {Root} from 'react-dom/client';
import type {HelloReactProps} from "@/lib/react/components/HelloReact";

// The registry matches string keys to Component Types
export type ReactComponentRegistry = {
    'HelloReact': React.ComponentType<HelloReactProps>;
};

export interface IDrnReact {
    /** Mounts a registered component with its declared props. */
    mount: <K extends keyof ReactComponentRegistry>(
        name: K,
        domElement: HTMLElement | null,
        props: React.ComponentProps<ReactComponentRegistry[K]>,
        options?: ReactMountOptions
    ) => ReactMountedIsland<React.ComponentProps<ReactComponentRegistry[K]>> | null;
}

export type RootData<P = unknown> = {
    root: Root;
    name: string;
    isShadow: boolean;
    currentProps?: P;
};

export interface Disposable {
    dispose: () => void;
}

export interface ReactMountedIsland<P> extends Disposable {
    update: (newProps: Partial<P>) => void;
    /** Returns a shallow copy of the current merged props, or `null` after `dispose()`. */
    getProps: () => P | null;
}

export interface ReactMountOptions {
    /** If true, creates a Shadow DOM and mounts inside it.
     * If false, mounts directly to the provided element (Light DOM).
     * @default true
     */
    useShadow?: boolean;
}

/** Props for the <IslandErrorBoundary> component wrapping mounted islands. */
export interface IslandErrorBoundaryProps {
    children: React.ReactNode;
    resetKey?: unknown;
}

export interface IslandErrorBoundaryState {
    hasError: boolean;
    prevResetKey?: unknown;
}

/** Props for the <IslandPortal> component targeting the global shadow portal container. */
export interface IslandPortalProps {
    children: React.ReactNode;
}
