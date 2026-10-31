import type { DrnNotification } from './DrnNotifications';

export interface IDrnErrorHandler {
    install(): void;
    /** Call only for a failed user action. Never use exception text as the public message. Returns null if feedback is unavailable or fails. */
    reportActionError(error: unknown, context: {
        operation: string;
        message?: string;
        persist?: boolean;
    }): DrnNotification | null;
}
