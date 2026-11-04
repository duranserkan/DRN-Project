//appPostload.js
import notificationPanel from './drn/drnNotificationPanel';
import { installNotificationEvents } from './drn/drnNotificationEvents';
import { installActionbarEvents } from './drn/drnActionbar';

installActionbarEvents();
installNotificationEvents(notificationPanel);
if (typeof window.onmount === 'function') {
    onmount();
} else {
    console.warn('window.onmount is not available.');
}

// Initialize onmount.js globally
document.addEventListener('DOMContentLoaded', onmount, {once: true});

// Reinitialize onmount after HTMX partial updates
document.addEventListener('htmx:load', onmount);
