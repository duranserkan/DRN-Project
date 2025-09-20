import {htmx} from './htmx.js'
import './htmx_safe_nonce.js'

// Make it globally available
if (typeof window !== 'undefined') {
    window.htmx = htmx
}

export {htmx}