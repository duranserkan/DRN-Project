typeof window.onmount=="function"?onmount():console.warn("window.onmount is not available.");document.addEventListener("DOMContentLoaded",onmount,{once:!0});document.addEventListener("htmx:load",onmount);DRN.Onmount.register('[data-bs-toggle="tooltip"]',function(t){t.disposable=new bootstrap.Tooltip(this,{animation:!1})});DRN.App.IsDev&&document.addEventListener("htmx:responseError",function(t){if(!t.detail){console.error("htmx:responseError fired without detail");return}const e=t.detail.xhr,n=t.detail.requestConfig;if(!e||!n){console.error("Missing xhr or requestConfig in htmx:responseError detail");return}const o=n.path||"unknown endpoint",s=n.method||"GET",r=n.body||"No data sent",a=e.status,i=e.statusText,d=n.headers||{},u=Object.entries(d).map(([p,g])=>`${p}: ${g}`).join(`
`)||"No headers sent",c=DRN.Utils.getRequestElementSelector(t.target),l=DRN.Utils.getRequestElementSelector(n.target)||"Unknown Target",m=`
Request failed:
- Endpoint: ${o}
- Method: ${s}
- Status: ${a} (${i})
- Request Element: ${c}
- Target Selector: ${l}
- Request Headers:
------- 
${u}
-------
- Data Sent:
------- 
${r}
-------
`;alert(m.trim())});document.addEventListener("showToast",function(t){const e=t.detail;e&&window.DRN&&window.DRN.Toast&&window.DRN.Toast.show({type:e.type||"info",message:e.message||"",duration:e.duration})});
