/** Normalize a Content-Type header without its optional parameters. */
export function mediaType(contentType: unknown) {
    return typeof contentType === 'string' ? contentType.split(';')[0]!.trim().toLowerCase() : '';
}
