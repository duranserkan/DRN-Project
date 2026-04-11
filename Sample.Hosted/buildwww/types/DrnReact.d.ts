//DrnReact.d.ts
import React from 'react';
import {ReactComponentRegistry, ReactMountOptions, ReactMountedIsland} from './DrnReactTypes';

export interface IDrnReact {
    /**
     * Mounts a registered React component.
     * Generic <K> ensures 'name' must be a valid key of ComponentRegistry.
     */
    mount: <K extends keyof ReactComponentRegistry>(
        name: K,
        domElement: HTMLElement | null,
        props: React.ComponentProps<ReactComponentRegistry[K]>,
        options?: ReactMountOptions
    ) => ReactMountedIsland<React.ComponentProps<ReactComponentRegistry[K]>> | null;
}