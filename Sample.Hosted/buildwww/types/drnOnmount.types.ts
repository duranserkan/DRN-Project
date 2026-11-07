export interface MountOptions {
    id: string;
    selector: string;
    disposable?: { dispose?(): void; destroy?(): void };
}

export type MountCallback = (this: Element, options: MountOptions) => void | false;

export interface OnMountStatic {
    (selectorOrEvent?: string | Event): void;
    (selector: string, register: MountCallback, unregister?: MountCallback): void;
}

export interface IDrnOnmount {
    /** Returns a snapshot of the registration keys for inspection. */
    listRegistrationKeys(): string[];
    unregister: (this: Element, options?: Partial<MountOptions>) => void;
    register(selector: string, registerCallback: MountCallback, idempotencyKey?: string): void;
    registerFull(selector: string, registerCallback: MountCallback, unregisterCallback?: MountCallback, idempotencyKey?: string): void;
}
