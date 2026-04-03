import { htmx } from './htmx.js'
import './htmxSafeNonce.js'
import './htmxValidation.js'
import onmount from 'onmount'

// Make it globally available
if (typeof window !== 'undefined') {
    window.htmx = htmx
    window.onmount = onmount
}

export { htmx }
