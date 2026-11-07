import { htmx } from './htmx.ts';
import './htmxSafeNonce.ts';
import './htmxValidation.ts';
import onmount from 'onmount';

// Make it globally available
if (typeof window !== 'undefined') {
    window.htmx = htmx;
    window.onmount = onmount;
}

export { htmx };
