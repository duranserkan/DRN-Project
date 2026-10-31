export interface IDrnDiagnosticCapture {
    text(value: string): string;
    url(value: string): string;
    value(path: string, input: unknown): unknown;
    status: { redacted: boolean; limited: boolean };
}

export interface IDrnDiagnostics {
    configure(options: { sensitiveFields?: string[]; sensitiveFieldPaths?: string[]; sensitivePathSegments?: string[] }): boolean;
    createCapture(): IDrnDiagnosticCapture;
}
