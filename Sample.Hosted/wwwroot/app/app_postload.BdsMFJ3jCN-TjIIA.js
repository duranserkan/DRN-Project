if (typeof $ !== "undefined" && typeof $.onmount === "function") {
  window.onmount = $.onmount;
  onmount();
} else {
  console.warn("$.onmount is not available.");
}
document.addEventListener("DOMContentLoaded", onmount, { once: true });
document.addEventListener("htmx:load", onmount);
DRN.Onmount.register('[data-bs-toggle="tooltip"]', function(options) {
  options.disposable = new bootstrap.Tooltip(this, { animation: false });
});
if (DRN.App.IsDev) {
  document.addEventListener("htmx:responseError", function(evt) {
    if (!evt.detail) {
      console.error("htmx:responseError fired without detail");
      return;
    }
    const response = evt.detail.xhr;
    const request = evt.detail.requestConfig;
    if (!response || !request) {
      console.error("Missing xhr or requestConfig in htmx:responseError detail");
      return;
    }
    const endpoint = request.path || "unknown endpoint";
    const method = request.method || "GET";
    const requestData = request.body || "No data sent";
    const status = response.status;
    const statusText = response.statusText;
    const requestHeaders = request.headers || {};
    const headersString = Object.entries(requestHeaders).map(([key, value]) => `${key}: ${value}`).join("\n") || "No headers sent";
    const requestElementSelector = DRN.Utils.getRequestElementSelector(evt.target);
    const targetSelector = DRN.Utils.getRequestElementSelector(request.target) || "Unknown Target";
    const errorMessage = `
Request failed:
- Endpoint: ${endpoint}
- Method: ${method}
- Status: ${status} (${statusText})
- Request Element: ${requestElementSelector}
- Target Selector: ${targetSelector}
- Request Headers:
------- 
${headersString}
-------
- Data Sent:
------- 
${requestData}
-------
`;
    alert(errorMessage.trim());
  });
}
