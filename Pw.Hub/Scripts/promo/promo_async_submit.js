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

    function isInsideManagePopup(el){
      try{ return !!(el && el.closest && el.closest('#promo_popup')); }catch(_){ return false; }
    }

    // Частичное обновление списка предметов только в попапе «Управление»
    function refreshItemsInPopup(btn){
      try{
        var popup = document.getElementById('promo_popup');
        if (!popup){ promoLog('refresh_items_skip_no_popup', null); return; }
        var container = popup.querySelector('#promo_container') || popup;
        var currentForm = (container.querySelector('form.js-transfer-form') || container.querySelector('form'));
        promoLog('refresh_items_start', { hasForm: !!currentForm });
        fetch(location.href, { credentials: 'same-origin' })
          .then(function(r){ return r.text(); })
          .then(function(html){
            try{
              var dp = new DOMParser();
              var doc = dp.parseFromString(html, 'text/html');
              if (!doc){ throw new Error('no document'); }
              // Найти форму с товарами (ищем input[name='cart_items[]'])
              var forms = doc.getElementsByTagName('form');
              var foundForm = null;
              for (var i=0;i<forms.length;i++){
                var f = forms[i];
                try{
                  if (f.querySelector("input[name='cart_items[]']") || f.querySelector("input[name='cart_items']")){
                    foundForm = f; break;
                  }
                }catch(__){}
              }
              if (!foundForm){ throw new Error('new form not found'); }

              // Заменяем содержимое текущей формы, если она есть; иначе — вставляем новую форму
              var newForm = foundForm.cloneNode(true);
              if (currentForm && currentForm.parentNode){
                currentForm.parentNode.replaceChild(newForm, currentForm);
              } else {
                container.appendChild(newForm);
              }

              // Обновлённые элементы появились — наши глобальные обработчики кликов по .js-transfer-go уже активны
              // Наблюдатель из promo_submit_ui переоформит внешний вид кнопки (если есть внутри попапа)
              promoLog('refresh_items_ok', { replaced: true });
            }catch(ex){
              promoLog('refresh_items_fail', { message: (ex && (ex.message||ex)) || 'unknown' });
            }
          })
          .catch(function(err){ promoLog('refresh_items_err', { message: (err && (err.message||err)) || 'unknown' }); });
      }catch(__){}
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
          // Всегда показываем историю через флаг + немедленный вызов
          try{ sessionStorage.setItem('__pwShowTransferHistory','1'); }catch(__){}
          try{
            if (window.PromoTransferHistoryPopup && window.PromoTransferHistoryPopup.openNow){
              window.PromoTransferHistoryPopup.openNow();
              promoLog('open_history_direct', null);
            } else {
              document.dispatchEvent(new CustomEvent('pw:openTransferHistory'));
              promoLog('open_history_event', null);
            }
          }catch(__){}

          // Если мы внутри окна «Управление» — частично обновим только список предметов без перезагрузки страницы
          if (isInsideManagePopup(btn)){
            promoLog('context_popup', null);
            markBusy(btn, false);
            refreshItemsInPopup(btn);
          } else {
            // На основной странице — оставляем прежнее поведение с reload
            promoLog('context_page', null);
            try{ location.reload(); }catch(__){}
          }
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
          // Трактуем это как успех и продолжаем по успешному сценарию (reload только вне попапа)
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
