export interface DrnProblem {
    status: number;
    type: string;
    title: string;
    detail: string;
    requestId: string;
    /** Bounded public messages keyed by the server's field names. Render as text. */
    errors: Record<string, string[]>;
}

export interface IDrnProblem {
    parse(body: unknown, contentType: unknown, status: number): DrnProblem;
    fromXhr(xhr: XMLHttpRequest): DrnProblem;
    /** Reads a bounded clone, leaving the original body available to the caller. */
    fromResponse(response: Response): Promise<DrnProblem>;
    show(problem: DrnProblem, context?: DrnProblemContext): import('./DrnNotifications').DrnNotification | null;
    clear(context?: DrnProblemContext): void;
    clearAll(): void;
}

export interface DrnProblemContext {
    element?: Element;
    target?: Element;
    operation?: string;
    persist?: boolean;
    focus?: boolean;
}
