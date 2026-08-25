// IslandErrorBoundary.tsx
import React, { Component, type ReactNode, type ErrorInfo } from 'react';
import type { IslandErrorBoundaryProps, IslandErrorBoundaryState } from '@/types/DrnReactTypes.ts';

export type { IslandErrorBoundaryProps, IslandErrorBoundaryState };

export class IslandErrorBoundary extends Component<IslandErrorBoundaryProps, IslandErrorBoundaryState> {
    constructor(props: IslandErrorBoundaryProps) {
        super(props);
        this.state = {
            hasError: false,
            prevResetKey: props.resetKey
        };
    }

    static getDerivedStateFromError(): Partial<IslandErrorBoundaryState> {
        return { hasError: true };
    }

    static getDerivedStateFromProps(
        props: IslandErrorBoundaryProps,
        state: IslandErrorBoundaryState
    ): IslandErrorBoundaryState | null {
        if (props.resetKey !== undefined && state.prevResetKey !== props.resetKey) {
            return {
                hasError: false,
                prevResetKey: props.resetKey
            };
        }
        return null;
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
