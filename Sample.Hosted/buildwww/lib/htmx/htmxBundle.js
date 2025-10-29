import {htmx} from './htmx.js'
import './htmxSafeNonce.js'

// Make it globally available
if (typeof window !== 'undefined') {
    window.htmx = htmx
}

export {htmx}