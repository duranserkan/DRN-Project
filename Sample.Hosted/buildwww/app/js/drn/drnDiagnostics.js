import rules from '../../../../../DRN.Framework.Utils/Logging/Diagnostics/diagnostic-rules.json' with { type: 'json' };

export const REDACTED = '[redacted]';
const URL_REDACTED = 'redacted';
export const LIMITED = '[diagnostic capture limited]';
const INVALID = '[diagnostic field unavailable]';
const encoder = new TextEncoder();
const normalize = value => value.replace(/[^a-z0-9]/gi, '').toLowerCase();
const builtins = new Set([...rules.secretFields, ...rules.personalFields]);

function containsRule(value, rules) {
    for (const rule of rules) if (rule.length > 0 && value.includes(rule)) return true;
    return false;
}

/** Page-local structural redaction and bounded capture. Free-text contents are preserved. */
export function createDiagnosticSanitizer() {
    const fields = new Set();
    const paths = new Set(rules.sensitiveFieldPaths);
    const segments = new Set(rules.secretPathSegments);
    let configurationFailed = false;

    function configure(options = {}) {
        const additions = [options.sensitiveFields || [], options.sensitiveFieldPaths || [], options.sensitivePathSegments || []];
        if (additions.some(items => !Array.isArray(items)) ||
            additions.reduce((sum, items) => sum + items.length, fields.size + paths.size + segments.size) > 256 ||
            additions.some(items => items.some(value => typeof value !== 'string' || !value || value.length > 256))) {
            configurationFailed = true;
            return false;
        }
        additions.forEach((items, index) => items.forEach(value => [fields, paths, segments][index].add(normalize(value))));
        return true;
    }

    function isSensitive(path) {
        if (path.length > 256) return true;
        const whole = normalize(path);
        return containsRule(whole, paths) || containsRule(whole, fields) || containsRule(whole, builtins);
    }

    function createCapture() {
        let bytes = 0;
        let work = 1000000;
        let ordinal = 0;
        let nodes = 0;
        const status = { redacted: false, limited: configurationFailed };
        const redact = (marker = REDACTED) => { status.redacted = true; return marker; };
        const limited = () => { status.limited = true; return LIMITED; };
        function tick(cost = 1) { work -= cost; if (work < 0) throw new Error('Diagnostic work limit'); }
        function accept(value, max = 65536) {
            if (typeof value !== 'string' || value.length > max || configurationFailed) return false;
            const size = encoder.encode(value).byteLength;
            bytes += size;
            return size <= max && bytes <= 262144;
        }
        function guard(action) {
            try { return action(); }
            catch { status.limited = true; return INVALID; }
        }
        function safeKey(key) {
            if (!accept(key, 256) || /[\u0000-\u001f\u007f]/.test(key) || key.startsWith('_diagnostic_'))
                return '_diagnostic_field_' + ++ordinal;
            return key;
        }
        function pairs(value) {
            return value.split('&', 257).map((part, index) => {
                tick();
                if (index === 256) return '_diagnostic=' + encodeURIComponent(limited());
                const separator = part.indexOf('=');
                const key = decode((separator < 0 ? part : part.slice(0, separator)).replaceAll('+', ' '));
                const raw = decode((separator < 0 ? '' : part.slice(separator + 1)).replaceAll('+', ' '));
                const safe = isSensitive(key) ? redact(URL_REDACTED) : raw;
                return encodeURIComponent(safeKey(key)) + '=' + encodeURIComponent(safe);
            }).join('&');
        }
        function decode(value) {
            for (let round = 0; round < 3 && value.includes('%'); round++) value = decodeURIComponent(value);
            return value.includes('%') ? INVALID : value;
        }
        function urlCore(value) {
            tick(value.length);
            if (!value) return '';
            const relative = /^[/?#]/.test(value);
            const url = new URL(relative ? 'https://diagnostic.invalid' + (value.startsWith('/') ? '' : '/') + value : value);
            if (!['http:', 'https:', 'ftp:', 'file:', 'otpauth:'].includes(url.protocol)) return INVALID;
            if (url.username || url.password) redact();
            let redactNext = false;
            const path = url.pathname.split('/').map(segment => {
                tick();
                const decoded = decode(segment);
                const safe = redactNext ? redact(URL_REDACTED) : decoded;
                redactNext = containsRule(normalize(decoded), segments);
                return encodeURIComponent(safe);
            }).join('/');
            const query = url.search ? '?' + pairs(url.search.slice(1)) : '';
            const fragment = url.hash.includes('=') ? '#' + pairs(url.hash.slice(1)) : url.hash;
            return (relative ? '' : url.protocol + '//' + url.host) + path + query + fragment;
        }
        const text = value => guard(() => accept(value) ? value : limited());
        const url = value => guard(() => accept(value) ? urlCore(value) : limited());
        function stringValue(path, input) {
            if (!accept(input)) return limited();
            const name = normalize(path);
            if (name.endsWith('querystring')) return pairs(input.replace(/^\?+/, ''));
            if ((name.endsWith('url') || name.endsWith('uri')) && input) return urlCore(input);
            return input;
        }
        function value(path, input, depth = 0, ancestors = new Set()) {
            return guard(() => {
                tick();
                if (++nodes > 1024 || depth > 8) return limited();
                if (isSensitive(path)) return redact();
                if (input === null || typeof input === 'boolean') return input;
                if (typeof input === 'number') return Number.isFinite(input) ? input : INVALID;
                if (typeof input === 'string') return stringValue(path, input);
                if (typeof input !== 'object') return INVALID;
                if (ancestors.has(input)) return '[diagnostic cycle]';
                ancestors.add(input);
                try {
                    if (Array.isArray(input)) {
                        const output = [];
                        const length = Math.min(input.length, 256);
                        for (let index = 0; index < length; index++) {
                            const descriptor = Object.getOwnPropertyDescriptor(input, index);
                            output.push(!descriptor ? null : 'value' in descriptor
                                ? value(path, descriptor.value, depth + 1, ancestors) : '[diagnostic getter unavailable]');
                        }
                        if (input.length > length) output.push(limited());
                        return output;
                    }
                    const output = Object.create(null);
                    let count = 0;
                    for (const key in input) {
                        if (!Object.hasOwn(input, key)) continue;
                        if (count++ >= 256) {
                            output._diagnostic_limit = limited();
                            break;
                        }
                        if (key.length > 256) { output['_diagnostic_field_' + ++ordinal] = limited(); continue; }
                        const descriptor = Object.getOwnPropertyDescriptor(input, key);
                        const field = path + '.' + key;
                        output[safeKey(key)] = isSensitive(field) ? redact() : descriptor && 'value' in descriptor
                            ? value(field, descriptor.value, depth + 1, ancestors) : '[diagnostic getter unavailable]';
                    }
                    return output;
                } finally { ancestors.delete(input); }
            });
        }
        return { text, url, value, status };
    }
    return { configure, createCapture };
}

export default createDiagnosticSanitizer();
