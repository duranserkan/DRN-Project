import notificationPanel from './drn/drnNotificationPanel.ts';
import { installNotificationEvents } from './drn/drnNotificationEvents.ts';
import { installActionbarEvents } from './drn/drnActionbar.ts';

installActionbarEvents();
installNotificationEvents(notificationPanel);
if (typeof window.onmount === 'function') {
    window.onmount();
} else {
    console.warn('window.onmount is not available.');
}

// Initialize onmount.js globally
document.addEventListener('DOMContentLoaded', window.onmount, { once: true });

// Reinitialize onmount after HTMX partial updates
document.addEventListener('htmx:load', window.onmount);
