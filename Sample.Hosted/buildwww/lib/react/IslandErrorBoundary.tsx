// IslandErrorBoundary.tsx
import React, { Component, type ReactNode, type ErrorInfo } from 'react';

export interface IslandErrorBoundaryProps {
    children: ReactNode;
}

export interface IslandErrorBoundaryState {
    hasError: boolean;
}

export class IslandErrorBoundary extends Component<IslandErrorBoundaryProps, IslandErrorBoundaryState> {
    constructor(props: IslandErrorBoundaryProps) {
        super(props);
        this.state = { hasError: false };
    }

    static getDerivedStateFromError(): IslandErrorBoundaryState {
        return { hasError: true };
    }

    override componentDidCatch(error: Error, info: ErrorInfo): void {
        console.error("[DRN] Island crashed:", error, info);
    }

    override render(): ReactNode {
        if (this.state.hasError) {
            return <div className="drn-error-fallback">Failed to load component</div>;
        }

        return this.props.children;
    }
}
