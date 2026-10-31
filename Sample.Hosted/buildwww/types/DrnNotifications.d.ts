export type NotificationType = 'error' | 'warning' | 'info' | 'success';

export interface NotificationInput {
    type?: NotificationType;
    title?: string;
    message?: string;
    problemType?: string;
    status?: number;
    requestId?: string;
    /** Optional occurrence identity, up to 200 characters. Reuses a retained record when repeated. */
    sourceId?: string;
    operation?: string;
    target?: string;
    /** False keeps a retained notification in memory only. */
    persist?: boolean;
}

export interface DrnNotification extends Required<NotificationInput> {
    id: string;
    createdAt: number;
    seen: boolean;
}

export interface IDrnNotifications {
    /** Retains history locally immediately without a toast. Shared persistence completes asynchronously. */
    publish(input: NotificationInput): DrnNotification | null;
    list(): DrnNotification[];
    /** Marks existing history as seen without dismissing it. Opening the panel calls this. */
    markSeen(): void;
    /** Refreshes saved history and seen state from other tabs without replaying toasts. */
    sync(): void;
    subscribe(listener: () => void): () => void;
    dismiss(ids: string[]): DrnNotification[];
    clear(): DrnNotification[];
    restore(entries: DrnNotification[]): void;
    forget(): void;
    suspend(): void;
    isActive(): boolean;
    expire(): void;
    configure(context: { app: string; owner: string }): boolean;
}

export interface IDrnToast {
    show(options: NotificationInput & {
        message: string;
        /** Toast display time in milliseconds, default 5000, clamped to 1000-30000. Does not expire history. */
        duration?: number;
        /** False shows only a toast. Otherwise history survives toast timeout, close, and UI cleanup. */
        record?: boolean;
    }): void;
    success(message: string, duration?: number): void;
    error(message: string, duration?: number): void;
    warning(message: string, duration?: number): void;
    info(message: string, duration?: number): void;
    /** Disposes toasts within root without dismissing their history entries. */
    cleanup(root: Element): void;
}
