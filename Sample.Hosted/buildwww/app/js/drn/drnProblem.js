import { mediaType } from './drnHttp.js';

const FALLBACK_TITLE = 'The request could not be completed.';
const MAX_BODY_LENGTH = 65536;
const MAX_MESSAGE_LENGTH = 500;
const MAX_ERRORS = 100;
const FRIENDLY_TITLES = new Map([
    ['bad request', 'Please try again. If the problem continues, contact support and share the error details from Notifications.'],
    ['unauthorized', 'Please sign in to continue.'],
    ['forbidden', 'You do not have access to this action.'],
    ['not found', 'This item is no longer available.'],
    ['request timeout', 'The request took too long. Check the current state before trying again.'],
    ['conflict', 'Something changed. Refresh the page before trying again.'],
    ['unprocessable entity', 'Please review your input and try again.'],
    ['unprocessable content', 'Please review your input and try again.'],
    ['too many requests', 'Please wait a moment before trying again.'],
    ['internal server error', 'Something went wrong. Check the current state before trying again.'],
    ['bad gateway', 'The service is temporarily unavailable. Please try again later.'],
    ['service unavailable', 'The service is temporarily unavailable. Please try again later.'],
    ['gateway timeout', 'The request took too long. Check the current state before trying again.']
]);

function isObject(value) {
    return value !== null && typeof value === 'object' && !Array.isArray(value);
}

function displayText(value) {
    return typeof value === 'string' ? value.trim().slice(0, MAX_MESSAGE_LENGTH) : '';
}

// Identifiers must remain exact, rather than being shortened into another identifier.
function identifier(value, maxLength) {
    return typeof value === 'string' && value.length <= maxLength ? value : '';
}

/** Parse untrusted response text. The HTTP status takes precedence over the JSON status. */
function parse(body, contentType, status) {
    const problem = { status, type: '', title: FALLBACK_TITLE, detail: '', requestId: '', errors: Object.create(null) };
    if (mediaType(contentType) !== 'application/problem+json' || typeof body !== 'string' || body.length > MAX_BODY_LENGTH)
        return problem;

    let value;
    try {
        value = JSON.parse(body);
    } catch {
        return problem;
    }
    if (!isObject(value)) return problem;

    problem.type = identifier(value.type, 2048);
    problem.title = displayText(value.title) || FALLBACK_TITLE;
    problem.detail = displayText(value.detail);
    problem.requestId = identifier(value.requestId, 200);

    if (isObject(value.errors)) {
        let remaining = MAX_ERRORS;
        for (const [field, messages] of Object.entries(value.errors)) {
            if (remaining === 0) break;
            if (field.length > 200 || !Array.isArray(messages)) continue;
            const accepted = [];
            for (const message of messages) {
                const text = displayText(message);
                if (!text) continue;
                accepted.push(text);
                if (--remaining === 0) break;
            }
            if (accepted.length) problem.errors[field] = accepted;
        }
    }
    return problem;
}

/** Read an HTMX XMLHttpRequest without changing its body or response processing. */
function fromXhr(xhr) {
    try {
        return parse(xhr.responseText, xhr.getResponseHeader('Content-Type'), xhr.status);
    } catch {
        return parse('', '', xhr?.status ?? 0);
    }
}

/** Read a clone so callers can still consume the original Fetch response. */
async function fromResponse(response) {
    try {
        const contentType = response.headers.get('Content-Type');
        if (mediaType(contentType) !== 'application/problem+json')
            return parse('', '', response.status);
        return parse(await response.clone().text(), contentType, response.status);
    } catch {
        return parse('', '', response.status);
    }
}

const presentations = new WeakMap();
let messageSequence = 0;

function regionFor(context) {
    const element = context.element instanceof Element ? context.element : null;
    const target = context.target instanceof Element ? context.target : null;
    return element?.closest('form, [data-problem-region]') || target?.closest('form, [data-problem-region]') ||
        document.getElementById('drn-problem-summary');
}

/** Clear only errors owned by this presenter, leaving native validation and other forms intact. */
function clear(context = {}) {
    const region = regionFor(context);
    const presentation = region && presentations.get(region);
    if (context.operation && presentation?.operation !== context.operation) return;
    if (context.target && presentation?.target && context.target !== presentation.target) return;
    clearRegion(region);
}

function clearRegion(region) {
    const previous = region && presentations.get(region);
    if (!previous) return;
    previous.summary.remove();
    if (region.id === 'drn-problem-summary') {
        region.hidden = true;
        region.classList.add('d-none');
    }
    for (const { field, message, invalid } of previous.fields) {
        const references = (field.getAttribute('aria-describedby') || '').split(/\s+/).filter(id => id && id !== message.id);
        if (references.length) field.setAttribute('aria-describedby', references.join(' '));
        else field.removeAttribute('aria-describedby');
        if (invalid === null) field.removeAttribute('aria-invalid');
        else field.setAttribute('aria-invalid', invalid);
        message.remove();
    }
    presentations.delete(region);
}

function clearAll() {
    for (const summary of document.querySelectorAll('[data-drn-problem-summary]'))
        clearRegion(summary.parentElement);
}

function appendText(parent, tag, value, className = '') {
    const element = document.createElement(tag);
    element.className = className;
    element.textContent = value;
    parent.appendChild(element);
    return element;
}

/** Keep validation beside the form; other failures use a toast and one history entry. */
function show(problem, context = {}) {
    if (window.DRN.Notifications.isActive?.() === false) return null;
    const title = FRIENDLY_TITLES.get(problem.title.toLowerCase()) || problem.title;
    const region = regionFor(context);
    const record = window.DRN.Notifications.publish({
        type: 'error', title, message: problem.detail,
        problemType: problem.type, status: problem.status, requestId: problem.requestId,
        operation: context.operation || '',
        target: region?.id || (region ? `region-${Array.from(document.querySelectorAll('form, [data-problem-region]')).indexOf(region)}` : ''),
        persist: context.persist !== false
    });
    if (!region || Object.keys(problem.errors).length === 0) {
        clearRegion(region);
        window.DRN.Toast.show({ type: 'error', message: problem.detail || title, record: false });
        return record;
    }

    clearRegion(region);
    const summary = document.createElement('section');
    summary.className = 'alert alert-danger text-break';
    summary.dataset.drnProblemSummary = '';
    summary.tabIndex = -1;
    summary.setAttribute('aria-label', 'Request error');
    appendText(summary, 'h2', title, 'h6');
    if (problem.detail && problem.detail !== title) appendText(summary, 'p', problem.detail);
    const list = document.createElement('ul');
    const fields = [];
    const form = region instanceof HTMLFormElement ? region : null;
    for (const [name, errors] of Object.entries(problem.errors)) {
        // Match names directly. Server field names are never interpreted as CSS selectors.
        const matches = form ? Array.from(form.elements).filter(field => field.name === name) : [];
        const first = matches.find(field => field instanceof HTMLElement && field.type !== 'hidden' && !field.disabled);
        const item = appendText(list, 'li', '');
        const message = `${name ? `${name}: ` : ''}${errors.join(' ')}`;
        if (first) {
            const link = appendText(item, 'button', message, 'btn btn-link p-0 text-start text-danger');
            link.type = 'button';
            link.addEventListener('click', () => first.focus());
            const error = document.createElement('div');
            error.id = `drn-field-error-${++messageSequence}`;
            error.className = 'text-danger small';
            error.textContent = errors.join(' ');
            first.insertAdjacentElement('afterend', error);
            for (const field of matches) {
                fields.push({ field, message: error, invalid: field.getAttribute('aria-invalid') });
                field.setAttribute('aria-invalid', 'true');
                const references = field.getAttribute('aria-describedby') || '';
                field.setAttribute('aria-describedby', `${references} ${error.id}`.trim());
            }
        } else item.textContent = message;
    }
    if (list.childElementCount) summary.appendChild(list);
    if (problem.requestId) {
        const details = document.createElement('details');
        appendText(details, 'summary', 'Details');
        appendText(details, 'p', `Request reference: ${problem.requestId}`, 'small user-select-all mb-0');
        appendText(details, 'p', 'Share this reference with support so they can find the request.',
            'small text-body-secondary mb-0');
        summary.appendChild(details);
    }
    region.prepend(summary);
    if (region.id === 'drn-problem-summary') {
        region.hidden = false;
        region.classList.remove('d-none');
    }
    presentations.set(region, { summary, fields, operation: context.operation, target: context.target });
    if (context.focus) summary.focus();
    else {
        const live = document.getElementById('drn-notification-assertive');
        if (live) live.textContent = `${title} ${problem.detail}`.trim();
    }
    return record;
}

export default { parse, fromXhr, fromResponse, show, clear, clearAll };
