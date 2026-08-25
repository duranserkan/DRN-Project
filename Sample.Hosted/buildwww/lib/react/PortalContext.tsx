// PortalContext.tsx
import React, { createContext, useContext, type FC } from 'react';
import { createPortal } from 'react-dom';
import type { IslandPortalProps } from '@/types/DrnReactTypes.ts';

export const PortalContext = createContext<HTMLElement | null>(null);

export const usePortalContainer = (): HTMLElement | null => useContext(PortalContext);

export const IslandPortal: FC<IslandPortalProps> = ({ children }) => {
    const container = usePortalContainer();
    if (!container) return null;
    return createPortal(children, container);
};
