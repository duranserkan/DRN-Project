// Declare the minimal structure of onmount for typing purposes
// This assumes onmount is available globally (e.g., loaded before this script)
export interface OnMountStatic {
    (selector?: string, register?: (this: Element) => void, unregister?: (this: Element) => void): void;
    // Add other onmount properties/methods if needed
}

// Declare the global onmount function
declare const onmount: OnMountStatic;

export interface IDrnOnmount {
    _registry?: Set<string>; // Optional, internal use

    unregister(this: Element, options?: { disposable?: { dispose?: () => void; destroy?: () => void } }): void;

    register(
        selector: string,
        registerCallback: (this: Element) => void,
        idempotencyKey?: string
    ): void;

    registerFull(
        selector: string,
        registerCallback: (this: Element) => void,
        unregisterCallback?: (this: Element) => void,
        idempotencyKey?: string
    ): void;
    // Add other onmount related method signatures here as needed
}