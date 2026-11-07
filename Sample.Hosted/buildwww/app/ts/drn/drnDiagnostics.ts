import rules from '../../../../../DRN.Framework.Utils/Logging/Diagnostics/data-classification-rules.json' with { type: 'json' };

import type { IDrnDiagnostics } from '@/types/drnDiagnostics.types';

export const REDACTED = '[redacted]';
const URL_REDACTED = 'redacted';
export const LIMITED = '[diagnostic capture limited]';
const INVALID = '[diagnostic field unavailable]';
const GETTER_UNAVAILABLE = '[diagnostic getter unavailable]';
const MAX_RULE_COUNT = 256;
const MAX_RULE_LENGTH = 256;
const MAX_KEY_BYTES = 256;
const MAX_PATH_LENGTH = 256;
const MAX_FIELD_BYTES = 65536;
const MAX_CAPTURE_BYTES = 262144;
const MAX_COLLECTION_ENTRIES = 256;
const MAX_WORK = 1000000;
const MAX_NODES = 1024;
const MAX_DEPTH = 8;
const MAX_DECODE_ROUNDS = 3;
const encoder = new TextEncoder();
const normalize = (value: string) => value.replace(/[^a-z0-9]/gi, '').toLowerCase();
const builtins = new Set([...rules.secretFields, ...rules.personalFields].map(normalize));

function decode(value: string) {
    for (let round = 0; round < MAX_DECODE_ROUNDS && value.includes('%'); round++) {
        try {
            value = decodeURIComponent(value);
        } catch (error) {
            // Literal percents are safe only when no encoded bytes remain to hide a sensitive name.
            if (error instanceof URIError && round > 0 && !/%[0-9a-f]{2}/i.test(value)) return value;
            throw error;
        }
    }
    if (/%[0-9a-f]{2}/i.test(value)) throw new URIError('Diagnostic URL decoding limit');
    return value;
}

function containsRule(value: string, rules: Set<string>) {
    for (const rule of rules) if (rule.length > 0 && value.includes(rule)) return true;
    return false;
}

/** Page-local structural redaction and bounded capture. Free-text contents are preserved. */
export function createDiagnosticSanitizer(): IDrnDiagnostics {
    const fields = new Set<string>();
    const paths = new Set(rules.sensitiveFieldPaths.map(normalize));
    const segments = new Set(rules.secretPathSegments.map(normalize));
    let configurationFailed = false;

    function configure(options: Parameters<IDrnDiagnostics['configure']>[0] = {}) {
        const additions = [
            options.sensitiveFields || [],
            options.sensitiveFieldPaths || [],
            options.sensitivePathSegments || [],
        ];
        if (
            additions.some((items) => !Array.isArray(items)) ||
            additions.reduce((sum, items) => sum + items.length, fields.size + paths.size + segments.size) >
                MAX_RULE_COUNT ||
            additions.some((items) =>
                items.some((value) => typeof value !== 'string' || !value || value.length > MAX_RULE_LENGTH),
            )
        ) {
            configurationFailed = true;
            return false;
        }
        additions.forEach((items, index) =>
            items.forEach((value) => [fields, paths, segments][index]!.add(normalize(value))),
        );
        return true;
    }

    function isSensitive(path: string) {
        if (path.length > MAX_PATH_LENGTH) return true;
        const whole = normalize(path);
        return containsRule(whole, paths) || containsRule(whole, fields) || containsRule(whole, builtins);
    }

    function createCapture() {
        let bytes = 0;
        let work = MAX_WORK;
        let ordinal = 0;
        let nodes = 0;
        const status = { redacted: false, limited: configurationFailed };
        const redact = (marker = REDACTED) => {
            status.redacted = true;
            return marker;
        };
        const limited = () => {
            status.limited = true;
            return LIMITED;
        };
        const replacementKey = () => '_diagnostic_field_' + ++ordinal;
        function tick(cost = 1) {
            work -= cost;
            if (work < 0) throw new Error('Diagnostic work limit');
        }
        function accept(value: unknown, max = MAX_FIELD_BYTES) {
            if (typeof value !== 'string' || value.length > max || configurationFailed) return false;
            const size = encoder.encode(value).byteLength;
            bytes += size;
            return size <= max && bytes <= MAX_CAPTURE_BYTES;
        }
        function guard<T>(action: () => T) {
            try {
                return action();
            } catch {
                status.limited = true;
                return INVALID;
            }
        }
        function safeKey(key: string) {
            // oxlint-disable-next-line no-control-regex -- Reject control characters in diagnostic keys.
            if (!accept(key, MAX_KEY_BYTES) || /[\u0000-\u001f\u007f]/.test(key) || key.startsWith('_diagnostic_'))
                return replacementKey();
            return key;
        }
        function pairs(value: string) {
            return value
                .split('&', MAX_COLLECTION_ENTRIES + 1)
                .map((part, index) => {
                    tick();
                    if (index === MAX_COLLECTION_ENTRIES) return '_diagnostic=' + encodeURIComponent(limited());
                    const separator = part.indexOf('=');
                    const key = decode((separator < 0 ? part : part.slice(0, separator)).replaceAll('+', ' '));
                    const raw = decode((separator < 0 ? '' : part.slice(separator + 1)).replaceAll('+', ' '));
                    const safe = isSensitive(key) ? redact(URL_REDACTED) : raw;
                    return encodeURIComponent(safeKey(key)) + '=' + encodeURIComponent(safe);
                })
                .join('&');
        }
        function urlCore(value: string) {
            tick(value.length);
            if (!value) return '';
            const relative = /^[/?#]/.test(value);
            const prefix = value.startsWith('/') ? '' : '/';
            const url = new URL(relative ? 'https://diagnostic.invalid' + prefix + value : value);
            if (!['http:', 'https:', 'ftp:', 'file:', 'otpauth:'].includes(url.protocol)) return INVALID;
            if (url.username || url.password) redact();
            let redactNext = false;
            const path = url.pathname
                .split('/')
                .map((segment) => {
                    tick();
                    const decoded = decode(segment);
                    const safe = redactNext ? redact(URL_REDACTED) : decoded;
                    redactNext = containsRule(normalize(decoded), segments);
                    return encodeURIComponent(safe);
                })
                .join('/');
            const query = url.search ? '?' + pairs(url.search.slice(1)) : '';
            const fragment = url.hash.includes('=') ? '#' + pairs(url.hash.slice(1)) : url.hash;
            return (relative ? '' : url.protocol + '//' + url.host) + path + query + fragment;
        }
        const text = (value: string) => guard(() => (accept(value) ? value : limited()));
        const url = (value: string) => guard(() => (accept(value) ? urlCore(value) : limited()));
        function stringValue(path: string, input: string) {
            if (!accept(input)) return limited();
            const name = normalize(path);
            if (name.endsWith('querystring')) return pairs(input.replace(/^\?+/, ''));
            if ((name.endsWith('url') || name.endsWith('uri')) && input) return urlCore(input);
            return input;
        }
        function arrayValue(path: string, input: unknown[], depth: number, ancestors: Set<object>): unknown {
            const output = [];
            const length = Math.min(input.length, MAX_COLLECTION_ENTRIES);
            for (let index = 0; index < length; index++) {
                const descriptor = Object.getOwnPropertyDescriptor(input, index);
                output.push(!descriptor ? null : descriptorValue(path, descriptor, depth, ancestors));
            }
            if (input.length > length) output.push(limited());
            return output;
        }
        function descriptorValue(
            path: string,
            descriptor: PropertyDescriptor | undefined,
            depth: number,
            ancestors: Set<object>,
        ): unknown {
            return descriptor && 'value' in descriptor
                ? value(path, descriptor.value, depth + 1, ancestors)
                : GETTER_UNAVAILABLE;
        }
        function objectValue(path: string, input: object, depth: number, ancestors: Set<object>): unknown {
            const output: Record<string, unknown> = Object.create(null);
            let count = 0;
            for (const key in input) {
                if (!Object.hasOwn(input, key)) continue;
                if (count++ >= MAX_COLLECTION_ENTRIES) {
                    output._diagnostic_limit = limited();
                    break;
                }
                if (key.length > MAX_KEY_BYTES) {
                    output[replacementKey()] = limited();
                    continue;
                }
                const descriptor = Object.getOwnPropertyDescriptor(input, key);
                const field = path + '.' + key;
                output[safeKey(key)] = isSensitive(field)
                    ? redact()
                    : descriptorValue(field, descriptor, depth, ancestors);
            }
            return output;
        }
        function value(path: string, input: unknown, depth = 0, ancestors = new Set<object>()): unknown {
            return guard(() => {
                tick();
                if (++nodes > MAX_NODES || depth > MAX_DEPTH) return limited();
                if (isSensitive(path)) return redact();
                if (input === null || typeof input === 'boolean') return input;
                if (typeof input === 'number') return Number.isFinite(input) ? input : INVALID;
                if (typeof input === 'string') return stringValue(path, input);
                if (typeof input !== 'object') return INVALID;
                if (ancestors.has(input)) return '[diagnostic cycle]';
                ancestors.add(input);
                try {
                    return Array.isArray(input)
                        ? arrayValue(path, input, depth, ancestors)
                        : objectValue(path, input, depth, ancestors);
                } finally {
                    ancestors.delete(input);
                }
            });
        }
        return { text, url, value, status };
    }
    return { configure, createCapture };
}

export default createDiagnosticSanitizer();
