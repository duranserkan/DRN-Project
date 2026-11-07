export interface IDrnUtils {
    urlSafeBase64Encode(str: string): string;
    urlSafeBase64Decode(str: string): string;
    getRequestElementSelector(requestElement: Element | null): string;
    isPlainObject(value: unknown): value is Record<string, unknown>;
    deepMerge<T extends object, U extends object>(target: T, source: U): T & U;
    escapeHtml(str: string): string;
}
