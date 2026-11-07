import 'bootstrap/dist/css/bootstrap.css';
import 'bootstrap-icons/font/bootstrap-icons.css';
import '../../app/css/site.css';
import '../../lib/bootstrap/bootstrapBundle.ts';
import '../../lib/htmx/htmx.ts';
import Notifications from '../../app/ts/drn/drnNotifications.ts';
import Problem from '../../app/ts/drn/drnProblem.ts';
import Toast from '../../app/ts/drn/drnToast.ts';
import ErrorHandler from '../../app/ts/drn/drnErrorHandler.ts';
import panel from '../../app/ts/drn/drnNotificationPanel.ts';
import { installNotificationEvents } from '../../app/ts/drn/drnNotificationEvents.ts';
import { installActionbarEvents } from '../../app/ts/drn/drnActionbar.ts';
import triggerMarkup from '../../../Pages/Shared/_NotificationTrigger.cshtml?raw';
import panelMarkup from '../../../Pages/Shared/_SidebarActionbarItemNotifications.cshtml?raw';

document.getElementById('notification-trigger')!.innerHTML = triggerMarkup;
document.getElementById('notification-content')!.innerHTML = panelMarkup;
Object.assign(window, { DRN: { Notifications, Problem, Toast, ErrorHandler } satisfies Partial<IDRN> });
ErrorHandler.install();
installActionbarEvents();
installNotificationEvents(panel);

// Observe completion without timing sleeps or replacing HTMX's response handling.
document.documentElement.dataset.completedRequests = '0';
document.addEventListener('htmx:afterRequest', () => {
    document.documentElement.dataset.completedRequests = String(
        Number(document.documentElement.dataset.completedRequests) + 1,
    );
});
document.addEventListener('htmx:beforeOnLoad', (event) => {
    if (event.detail.requestConfig.path === '/operations/pending')
        document.documentElement.dataset.pendingResponseRejected = String(event.defaultPrevented);
});
document.documentElement.dataset.fixtureReady = 'true';
