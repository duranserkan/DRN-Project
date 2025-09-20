typeof $<"u"&&typeof $.onmount=="function"?(window.onmount=$.onmount,onmount()):console.warn("$.onmount is not available.");document.addEventListener("DOMContentLoaded",onmount,{once:!0});document.addEventListener("htmx:load",onmount);DRN.Onmount.register('[data-bs-toggle="tooltip"]',function(e){e.disposable=new bootstrap.Tooltip(this,{animation:!1})});DRN.App.isDev&&document.addEventListener("htmx:responseError",function(e){if(!e.detail){console.error("htmx:responseError fired without detail");return}const n=e.detail.xhr,t=e.detail.requestConfig;if(!n||!t){console.error("Missing xhr or requestConfig in htmx:responseError detail");return}const o=t.path||"unknown endpoint",s=t.method||"GET",r=t.body||"No data sent",a=n.status,i=n.statusText,d=t.headers||{},u=Object.entries(d).map(([p,f])=>`${p}: ${f}`).join(`
`)||"No headers sent",l=DRN.Utils.getRequestElementSelector(e.target),c=DRN.Utils.getRequestElementSelector(t.target)||"Unknown Target",m=`
Request failed:
- Endpoint: ${o}
- Method: ${s}
- Status: ${a} (${i})
- Request Element: ${l}
- Target Selector: ${c}
- Request Headers:
------- 
${u}
-------
- Data Sent:
------- 
${r}
-------
`;alert(m.trim())});
