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
    fromResponse(response: Response): Promise<DrnProblem>;
    show(problem: DrnProblem): void;
}
