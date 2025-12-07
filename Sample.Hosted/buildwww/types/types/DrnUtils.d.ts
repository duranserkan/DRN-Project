export interface IDrnUtils {
    urlSafeBase64Encode(str: string): string;
    urlSafeBase64Decode(str: string): string;
    getRequestElementSelector(requestElement: Element | null): string;
    isPlainObject(value: any): value is Record<string, any>; // Type predicate
    deepMerge<T extends Record<string, any>, U extends Record<string, any>>(target: T, source: U): T & U;
    // Add other utility method signatures here as needed
}