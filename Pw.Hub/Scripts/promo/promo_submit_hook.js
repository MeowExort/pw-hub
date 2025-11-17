(function(){
    try{
        // if modular system not available, keep legacy behavior
        if (!window.Promo || !Promo.register){
            (function legacy(){
                try{
                    if (window.__pwPromoHookInstalled) return; // once per page load
                    window.__pwPromoHookInstalled = true;
                    window.__pwPromoLastSentTs = 0;
                    function safePost(obj){
                        try{
                            var now = Date.now();
                            if (Math.abs(now - (window.__pwPromoLastSentTs||0)) < 900) return;
                            window.__pwPromoLastSentTs = now;
                            if (window.chrome && window.chrome.webview && window.chrome.webview.postMessage){
                                window.chrome.webview.postMessage(JSON.stringify(obj));
                            }
                        }catch(e){}
                    }
                    function collectPromoData(form){
                        var data = { type: 'promo_form_submit', do: '', cart_items: [], acc_info: '' };
                        try{ var doEl = form.querySelector("[name='do']"); var doVal = doEl && typeof doEl.value !== 'undefined' ? (doEl.value||'') : ''; data.do = doVal || 'process'; }catch(e){ data.do = 'process'; }
                        try{ var accEl = form.querySelector("[name='acc_info']"); if (accEl) data.acc_info = (accEl.value||''); }catch(e){}
                        try{ var items = form.querySelectorAll("input[name='cart_items[]']:checked"); if (items && items.length){ items.forEach(function(i){ if (i && i.value!=null) data.cart_items.push(String(i.value)); }); } }catch(e){}
                        return data;
                    }
                    function attachToForm(form){ if (!form || form.__pwPromoSubmitAttached) return; form.__pwPromoSubmitAttached = true; form.addEventListener('submit', function(){ try{ safePost(collectPromoData(form)); }catch(e){} }, true); }
                    function attachClickInitiators(root){ try{ if (root && !root.__pwPromoClickAttached){ root.__pwPromoClickAttached = true; document.addEventListener('click', function(e){ try{ var t = e.target; if (!t) return; var go = t.closest ? t.closest('.js-transfer-go') : null; if (!go) return; var form = (go.closest && go.closest('form')) || document.querySelector('form.js-transfer-form') || document.querySelector('form'); if (!form) return; safePost(collectPromoData(form)); }catch(_){ } }, true); } }catch(e){} }
                    var promoForm = document.querySelector('form.js-transfer-form'); if (promoForm) attachToForm(promoForm);
                    var forms = document.getElementsByTagName('form'); if (forms && forms.length){ for (var i=0;i<forms.length;i++){ var f = forms[i]; try{ var ok = true; var act = (f.getAttribute('action')||'').toLowerCase(); if (act && act.indexOf('promo_items.php') === -1){ ok = location.pathname.toLowerCase().indexOf('promo_items.php') !== -1; } if (ok) attachToForm(f); }catch(e){ attachToForm(f); } } }
                    attachClickInitiators(document.documentElement || document.body);
                    try{ var mo = new MutationObserver(function(muts){ muts.forEach(function(m){ (m.addedNodes||[]).forEach(function(n){ try{ if (!n) return; if (n.tagName && n.tagName.toLowerCase() === 'form') attachToForm(n); var innerForms = n.querySelectorAll ? n.querySelectorAll('form') : []; if (innerForms && innerForms.length){ innerForms.forEach(attachToForm); } }catch(e){} }); }); }); mo.observe(document.documentElement || document.body, { childList:true, subtree:true }); }catch(e){}
                }catch(e){}
            })();
            return;
        }

        Promo.register('FormSubmit', {
            run: function(){
                try{
                    if (window.__pwPromoHookInstalled) return; // once per page load
                    window.__pwPromoHookInstalled = true;
                    window.__pwPromoLastSentTs = 0;
                    function safePost(obj){
                        try{
                            var now = Date.now();
                            if (Math.abs(now - (window.__pwPromoLastSentTs||0)) < 900) return;
                            window.__pwPromoLastSentTs = now;
                            if (window.chrome && window.chrome.webview && window.chrome.webview.postMessage){
                                window.chrome.webview.postMessage(JSON.stringify(obj));
                            }
                        }catch(e){}
                    }
                    function collectPromoData(form){
                        var data = { type: 'promo_form_submit', do: '', cart_items: [], acc_info: '' };
                        try{ var doEl = form.querySelector("[name='do']"); var doVal = doEl && typeof doEl.value !== 'undefined' ? (doEl.value||'') : ''; data.do = doVal || 'process'; }catch(e){ data.do = 'process'; }
                        try{ var accEl = form.querySelector("[name='acc_info']"); if (accEl) data.acc_info = (accEl.value||''); }catch(e){}
                        try{ var items = form.querySelectorAll("input[name='cart_items[]']:checked"); if (items && items.length){ items.forEach(function(i){ if (i && i.value!=null) data.cart_items.push(String(i.value)); }); } }catch(e){}
                        return data;
                    }
                    function attachToForm(form){ if (!form || form.__pwPromoSubmitAttached) return; form.__pwPromoSubmitAttached = true; form.addEventListener('submit', function(){ try{ safePost(collectPromoData(form)); }catch(e){} }, true); }
                    function attachClickInitiators(root){ try{ if (root && !root.__pwPromoClickAttached){ root.__pwPromoClickAttached = true; document.addEventListener('click', function(e){ try{ var t = e.target; if (!t) return; var go = t.closest ? t.closest('.js-transfer-go') : null; if (!go) return; var form = (go.closest && go.closest('form')) || document.querySelector('form.js-transfer-form') || document.querySelector('form'); if (!form) return; safePost(collectPromoData(form)); }catch(_){ } }, true); } }catch(e){} }
                    var promoForm = document.querySelector('form.js-transfer-form'); if (promoForm) attachToForm(promoForm);
                    var forms = document.getElementsByTagName('form'); if (forms && forms.length){ for (var i=0;i<forms.length;i++){ var f = forms[i]; try{ var ok = true; var act = (f.getAttribute('action')||'').toLowerCase(); if (act && act.indexOf('promo_items.php') === -1){ ok = location.pathname.toLowerCase().indexOf('promo_items.php') !== -1; } if (ok) attachToForm(f); }catch(e){ attachToForm(f); } } }
                    attachClickInitiators(document.documentElement || document.body);
                    try{ var mo = new MutationObserver(function(muts){ muts.forEach(function(m){ (m.addedNodes||[]).forEach(function(n){ try{ if (!n) return; if (n.tagName && n.tagName.toLowerCase() === 'form') attachToForm(n); var innerForms = n.querySelectorAll ? n.querySelectorAll('form') : []; if (innerForms && innerForms.length){ innerForms.forEach(attachToForm); } }catch(e){} }); }); }); mo.observe(document.documentElement || document.body, { childList:true, subtree:true }); }catch(e){}
                }catch(e){}
            }
        });
    }catch(e){}
})();