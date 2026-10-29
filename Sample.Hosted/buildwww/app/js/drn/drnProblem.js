const FALLBACK_TITLE = 'The request could not be completed.';
const MAX_BODY_LENGTH = 65536;
const MAX_MESSAGE_LENGTH = 500;
const MAX_ERRORS = 5;

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
    const mediaType = typeof contentType === 'string' ? contentType.split(';')[0].trim().toLowerCase() : '';
    if (mediaType !== 'application/problem+json' || typeof body !== 'string' || body.length > MAX_BODY_LENGTH)
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
        if (contentType?.split(';')[0].trim().toLowerCase() !== 'application/problem+json')
            return parse('', '', response.status);
        return parse(await response.clone().text(), contentType, response.status);
    } catch {
        return parse('', '', response.status);
    }
}

/** Display through the existing text-escaping toast. Never render response HTML. */
function show(problem) {
    const messages = [problem.title];
    if (problem.detail && problem.detail !== problem.title) messages.push(problem.detail);
    for (const [field, errors] of Object.entries(problem.errors)) {
        for (const error of errors) messages.push(field ? `${field}: ${error}` : error);
    }
    if (problem.requestId) messages.push(`Request reference: ${problem.requestId}`);
    window.DRN.Toast.error(messages.join('\n'), 15000);
}

export default { parse, fromXhr, fromResponse, show };
