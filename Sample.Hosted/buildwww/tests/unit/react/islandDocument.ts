// Minimal tree for checking node identity and attachment across shadow roots and body swaps.
export class IslandNode {
    id = '';
    textContent = '';
    nonce = '';
    style = { cssText: '' };
    attributes = new Map<string, string>();
    children: IslandNode[] = [];
    adoptedStyleSheets: CSSStyleSheet[] = [];
    parent: IslandNode | null = null;
    shadowRoot: IslandNode | null = null;
    readonly documentRoot: boolean;
    constructor(documentRoot = false) {
        this.documentRoot = documentRoot;
    }
    get isConnected(): boolean {
        return this.documentRoot || Boolean(this.parent?.isConnected);
    }
    appendChild(child: IslandNode) {
        child.parent = this;
        this.children.push(child);
        return child;
    }
    attachShadow() {
        this.shadowRoot = new IslandNode();
        this.shadowRoot.parent = this;
        return this.shadowRoot;
    }
    setAttribute(name: string, value: string) {
        this.attributes.set(name, value);
    }
    querySelector(selector: string) {
        return this.children.find((child) => '#' + child.id === selector);
    }
}

export function islandDocument() {
    const root = new IslandNode(true);
    const body = root.appendChild(new IslandNode());
    const head = root.appendChild(new IslandNode());
    return {
        body,
        head,
        createElement: () => new IslandNode(),
        getElementById(id: string) {
            return this.body.querySelector('#' + id) ?? head.querySelector('#' + id);
        },
        querySelector: () => null,
    };
}
