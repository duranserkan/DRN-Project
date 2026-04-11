//DrnReactTypes.ts
import React from 'react';
import {type Root} from 'react-dom/client';
import type {HelloReactProps} from "@/lib/react/components/HelloReactComponent.tsx";

// The registry matches string keys to Component Types
export type ReactComponentRegistry = {
    'HelloReact': React.ComponentType<HelloReactProps>;
};

export type RootData = {
    root: Root;
    name: string;
    isShadow: boolean;
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
