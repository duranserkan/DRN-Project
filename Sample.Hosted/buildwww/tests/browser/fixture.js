import 'bootstrap/dist/css/bootstrap.css';
import 'bootstrap-icons/font/bootstrap-icons.css';
import '../../app/css/site.css';
import '../../lib/bootstrap/bootstrapBundle.js';
import '../../lib/htmx/htmx.js';
import Notifications from '../../app/js/drn/drnNotifications.js';
import Problem from '../../app/js/drn/drnProblem.js';
import Toast from '../../app/js/drn/drnToast.js';
import panel from '../../app/js/drn/drnNotificationPanel.js';
import { installNotificationEvents } from '../../app/js/drn/drnNotificationEvents.js';
import { installActionbarEvents } from '../../app/js/drn/drnActionbar.js';
import triggerMarkup from '../../../Pages/Shared/_NotificationTrigger.cshtml?raw';
import panelMarkup from '../../../Pages/Shared/_SidebarActionbarItemNotifications.cshtml?raw';

document.getElementById('notification-trigger').innerHTML = triggerMarkup;
document.getElementById('notification-content').innerHTML = panelMarkup;
window.DRN = { Notifications, Problem, Toast };
installActionbarEvents();
installNotificationEvents(panel);

// Observe completion without timing sleeps or replacing HTMX's response handling.
document.documentElement.dataset.completedRequests = '0';
document.addEventListener('htmx:afterRequest', () => {
    document.documentElement.dataset.completedRequests = String(
        Number(document.documentElement.dataset.completedRequests) + 1);
});
document.addEventListener('htmx:beforeOnLoad', event => {
    if (event.detail.requestConfig.path === '/operations/pending')
        document.documentElement.dataset.pendingResponseRejected = String(event.defaultPrevented);
});
document.documentElement.dataset.fixtureReady = 'true';
