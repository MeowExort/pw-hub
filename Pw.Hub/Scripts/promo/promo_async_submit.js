(function(){
  try{
    function promoLog(eventName, payload){
      try{
        var msg = { type:'promo_log', event:'asyncSubmit_' + String(eventName||''), data:payload||null, ts:Date.now() };
        if (window.chrome && window.chrome.webview && window.chrome.webview.postMessage){
          window.chrome.webview.postMessage(JSON.stringify(msg));
        }
      }catch(_){ }
    }

    function collectPromoData(form){
      var data = { type: 'promo_form_submit', do: '', cart_items: [], acc_info: '' };
      try{ var doEl = form.querySelector("[name='do']"); var doVal = doEl && typeof doEl.value !== 'undefined' ? (doEl.value||'') : ''; data.do = doVal || 'process'; }catch(e){ data.do = 'process'; }
      try{ var accEl = form.querySelector("[name='acc_info']"); if (accEl) data.acc_info = (accEl.value||''); }catch(e){}
      try{ var items = form.querySelectorAll("input[name='cart_items[]']:checked"); if (items && items.length){ items.forEach(function(i){ if (i && i.value!=null) data.cart_items.push(String(i.value)); }); } }catch(e){}
      return data;
    }

    function postToHost(obj){
      try{
        if (window.chrome && window.chrome.webview && window.chrome.webview.postMessage){
          window.chrome.webview.postMessage(JSON.stringify(obj));
        }
      }catch(_){ }
    }

    function findPromoForm(start){
      try{
        var scope = (start && start.closest) ? start.closest('#promo_container') : null;
        var form = (scope && scope.querySelector('form.js-transfer-form'))
                || (scope && scope.querySelector('form'))
                || document.querySelector('form.js-transfer-form')
                || document.querySelector('form');
        return form;
      }catch(_){ return null; }
    }

    function toFormData(form){
      try{ return new FormData(form); }catch(_){ return null; }
    }

    function markBusy(btn, busy, text){
      try{
        if (!btn) return;
        if (busy){
          if (!btn.dataset.origText){ btn.dataset.origText = (btn.textContent||'').trim(); }
          if (text) btn.textContent = text;
          btn.classList && btn.classList.add('is-busy');
          btn.setAttribute('aria-busy','true');
          btn.style.pointerEvents = 'none';
        } else {
          btn.classList && btn.classList.remove('is-busy');
          btn.removeAttribute('aria-busy');
          btn.style.pointerEvents = '';
          if (btn.dataset.origText){ btn.textContent = btn.dataset.origText; }
        }
      }catch(_){ }
    }

    function submitAsync(btn){
      try{
        var form = findPromoForm(btn);
        if (!form){ promoLog('no_form', null); return; }

        // Синхронно отправим событие в .NET для сохранения acc_info
        try{ postToHost(collectPromoData(form)); }catch(__){}

        // Отправка формы через fetch
        var fd = toFormData(form);
        var action = (form.getAttribute('action')||'').trim();
        if (!action){ action = (location && (location.href||'')) || ''; }
        if (!action){ promoLog('no_action', null); return; }

        markBusy(btn, true, 'Идёт перевод…');
        promoLog('start', { action: action });

        function handleSuccess(meta){
          try{ promoLog('resp', meta || null); }catch(__){}
          // Перезагрузим страницу, чтобы обновить список предметов, и откроем историю после reload по флагу
          try{ sessionStorage.setItem('__pwShowTransferHistory','1'); }catch(__){}
          try{ promoLog('reload', null); }catch(__){}
          try{ location.reload(); }catch(__){}
        }

        fetch(action, {
          method: (form.getAttribute('method')||'POST').toUpperCase(),
          body: fd,
          credentials: 'same-origin',
          redirect: 'follow'
        }).then(function(resp){
          // Считаем успешным любые 2xx/3xx, а также случаи с редиректом
          var status = resp && typeof resp.status === 'number' ? resp.status : 0;
          var ok = status >= 200 && status < 400;
          var meta = { status: status, redirected: resp && resp.redirected, type: resp && resp.type, ok: ok };
          try{ promoLog('ok', meta); }catch(__){}
          if (ok){
            handleSuccess(meta);
          } else {
            // HTTP ошибка
            try{ promoLog('err_http', meta); }catch(__){}
            markBusy(btn, false);
            try{ alert('Не удалось выполнить перевод. Повторите попытку.'); }catch(__){}
          }
        }).catch(function(err){
          // В WebView2 fetch может завершиться ошибкой на 302/CORS (opaqueredirect), хотя перевод успешен.
          // Трактуем это как успех и выполняем перезагрузку.
          try{ promoLog('fallback_success', { message: (err && (err.message||err)) || 'unknown' }); }catch(__){}
          handleSuccess({ status: 0, redirected: true, type: 'opaqueredirect', ok: true });
        });
      }catch(_){ }
    }

    function attach(){
      try{
        if (document.documentElement.getAttribute('data-pw-async-submit') === '1') return;
        document.documentElement.setAttribute('data-pw-async-submit','1');
        document.addEventListener('click', function(e){
          try{
            var t = e.target;
            if (!t) return;
            var go = t.closest ? t.closest('.js-transfer-go') : null;
            if (!go) return;
            e.preventDefault(); e.stopPropagation();
            submitAsync(go);
          }catch(__){}
        }, true);
        promoLog('attached', null);
      }catch(_){ }
    }

    function run(){
      if (document.readyState === 'loading'){
        document.addEventListener('DOMContentLoaded', function(){ try{ attach(); }catch(__){} }, { once:true });
      } else {
        attach();
      }
    }

    if (!window.Promo || !Promo.register){ run(); return; }
    Promo.register('AsyncSubmit', { run: run });
  }catch(_){ }
})();
