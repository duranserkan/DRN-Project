/*
* This file is licensed to you under the BSD Zero Clause License.
* Source codes under this file obtained and modified from https://github.com/MichaelWest22/htmx-extensions/blob/main/src/safe-nonce/safe-nonce.js
*/

import {htmx} from './htmx.js'

htmx.defineExtension('safe-nonce', {
    transformResponse: function (text, xhr, elt) {
        if (!htmx.config.refreshOnHistoryMiss) // disable ajax fetching on history miss because it doesn't handle nonce replacement
            htmx.config.refreshOnHistoryMiss = true

        let nonce = xhr.getResponseHeader('HX-Nonce')
        if (!nonce) {
            const csp = xhr.getResponseHeader('content-security-policy')
            if (csp) {
                const cspMatch = csp.match(/(default|script)-src[^;]*'nonce-([^']*)'/i)
                if (cspMatch)
                    nonce = cspMatch[2]
            }
        }

        if (window.location.hostname) {
            const responseURL = new URL(xhr.responseURL)
            if (responseURL.hostname !== window.location.hostname)
                nonce = '' // ignore nonce header if request is not some domain
        }

        text = text.replace(/ignore:safe-nonce/g, '')
        if (!window.__globalDOMParser)
            window.__globalDOMParser = new DOMParser();

        const doc = window.__globalDOMParser.parseFromString(text, 'text/html');
        const scripts = doc.querySelectorAll('script');
        scripts.forEach(script => {
            const validNonce = typeof nonce === "string" && nonce.trim() !== "" && script.getAttribute('nonce') === nonce;
            if (!validNonce)
                script.remove();
        });

        return doc.documentElement.outerHTML;
    }
})