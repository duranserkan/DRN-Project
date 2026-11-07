import assert from 'node:assert/strict';
import test from 'node:test';
import problem from '../../../app/ts/drn/drnProblem.ts';
import { installBrowserGlobals } from '../browserGlobals.ts';

test('field validation focuses the enabled control and restores prior accessibility attributes only for the matching request', (t) => {
    class Node extends EventTarget {
        id = '';
        dataset = {};
        children: Node[] = [];
        attributes = new Map<string, string>();
        textContent = '';
        parentElement?: Node;
        focus = t.mock.fn();
        get childElementCount() {
            return this.children.length;
        }
        appendChild(child: Node) {
            this.children.push(child);
            child.parentElement = this;
            return child;
        }
        prepend(child: Node) {
            this.children.unshift(child);
            child.parentElement = this;
        }
        remove() {
            if (this.parentElement)
                this.parentElement.children = this.parentElement.children.filter((item) => item !== this);
        }
        getAttribute(key: string) {
            return this.attributes.get(key) ?? null;
        }
        setAttribute(key: string, value: string) {
            this.attributes.set(key, value);
        }
        removeAttribute(key: string) {
            this.attributes.delete(key);
        }
    }
    class Field extends Node {
        name = 'Input.Name';
        disabled = false;
        after(message: Node) {
            this.parentElement?.appendChild(message);
        }
    }
    class Form extends Node {
        elements: Field[] = [];
        closest() {
            return this;
        }
    }
    const form = new Form();
    form.id = 'profile';
    const disabled = new Field();
    disabled.disabled = true;
    const field = new Field();
    form.elements.push(disabled, field);
    form.appendChild(disabled);
    form.appendChild(field);
    field.setAttribute('aria-invalid', 'false');
    field.setAttribute('aria-describedby', 'hint');
    const buttons: Node[] = [];
    const publish = t.mock.fn();
    const document = {
        createElement(tag: string) {
            const node = new Node();
            if (tag === 'button') buttons.push(node);
            return node;
        },
    };
    installBrowserGlobals(t, {
        Element: Node,
        HTMLElement: Node,
        HTMLFormElement: Form,
        document,
        window: { DRN: { Notifications: { publish } } },
    });
    const failure = problem.parse(
        JSON.stringify({
            title: 'Review the form',
            detail: 'Check the name',
            requestId: 'request-1',
            errors: { 'Input.Name': ['Required'], '': ['Review the remaining values'] },
        }),
        'application/problem+json',
        422,
    );
    const context = { element: form as unknown as Element, operation: 'save', focus: true };
    problem.show(failure, context);
    assert.equal(form.children[0]?.focus.mock.callCount(), 1);
    assert.equal(buttons[0]?.textContent, 'Input.Name: Required');
    buttons[0]?.dispatchEvent(new Event('click'));
    assert.equal(field.focus.mock.callCount(), 1);
    assert.equal(disabled.focus.mock.callCount(), 0);
    assert.equal(publish.mock.callCount(), 1);
    problem.clear({ ...context, operation: 'older' });
    assert.equal(field.getAttribute('aria-invalid'), 'true');
    assert.match(field.getAttribute('aria-describedby') ?? '', /^hint drn-field-error-/);
    problem.clear(context);
    assert.deepEqual(form.children, [disabled, field]);
    assert.equal(field.getAttribute('aria-invalid'), 'false');
    assert.equal(field.getAttribute('aria-describedby'), 'hint');
    assert.equal(disabled.getAttribute('aria-invalid'), null);
    assert.equal(disabled.getAttribute('aria-describedby'), null);
});
