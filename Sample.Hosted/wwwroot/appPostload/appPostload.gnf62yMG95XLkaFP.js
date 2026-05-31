typeof window.onmount==`function`?onmount():console.warn(`window.onmount is not available.`),document.addEventListener(`DOMContentLoaded`,onmount,{once:!0}),document.addEventListener(`htmx:load`,onmount),DRN.Onmount.register(`[data-bs-toggle="tooltip"]`,function(e){e.disposable=new bootstrap.Tooltip(this,{animation:!1})}),DRN.App.IsDev&&document.addEventListener(`htmx:responseError`,function(e){if(!e.detail){console.error(`htmx:responseError fired without detail`);return}let t=e.detail.xhr,n=e.detail.requestConfig;if(!t||!n){console.error(`Missing xhr or requestConfig in htmx:responseError detail`);return}let r=n.path||`unknown endpoint`,i=n.method||`GET`,a=n.body||`No data sent`,o=t.status,s=t.statusText,c=n.headers||{},l=Object.entries(c).map(([e,t])=>`${e}: ${t}`).join(`
`)||`No headers sent`,u=`
Request failed:
- Endpoint: ${r}
- Method: ${i}
- Status: ${o} (${s})
- Request Element: ${DRN.Utils.getRequestElementSelector(e.target)}
- Target Selector: ${DRN.Utils.getRequestElementSelector(n.target)||`Unknown Target`}
- Request Headers:
------- 
${l}
-------
- Data Sent:
------- 
${a}
-------
`;alert(u.trim())}),document.addEventListener(`showToast`,function(e){let t=e.detail;t&&window.DRN&&window.DRN.Toast&&window.DRN.Toast.show({type:t.type||`info`,message:t.message||``,duration:t.duration})});