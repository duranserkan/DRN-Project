import React, { createContext, useContext, type FC } from 'react';
import { createPortal } from 'react-dom';
import type { IslandPortalProps } from '@/types/drnReact.types';

export const PortalContext = createContext<HTMLElement | null>(null);

const usePortalContainer = (): HTMLElement | null => useContext(PortalContext);

/**
 * Kept as a usage example for island authors.
 * @public
 */
export const IslandPortal: FC<IslandPortalProps> = ({ children }) => {
    const container = usePortalContainer();
    if (!container) return null;
    return createPortal(children, container);
};
