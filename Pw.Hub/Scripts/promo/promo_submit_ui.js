(function(){
  try{
    // Универсальный логгер
    function promoLog(eventName, payload){
      try{
        var msg = { type:'promo_log', event:'submitUI_' + String(eventName||''), data:payload||null, ts:Date.now() };
        if (window.chrome && window.chrome.webview && window.chrome.webview.postMessage){
          window.chrome.webview.postMessage(JSON.stringify(msg));
        }
      }catch(_){ }
    }

    // Инжекция CSS для кнопки «Передать»
    function ensureStyle(){
      try{
        var id = 'pw_submit_btn_style';
        if (document.getElementById(id)) return;
        var css = [
          // На всякий случай ослабим жёсткую высоту сайта для .go_items внутри попапа
          '#promo_popup .go_items{',
          '  height:auto !important;',
          '  min-height:auto !important;',
          '  line-height:1.25 !important;',
          '  padding:4px 10px !important;',
          '  display:inline-flex !important;',
          '  align-items:center !important;',
          '  justify-content:center !important;',
          '  float:none !important;',
          '}',
          // ВАЖНО: Стили применяются ТОЛЬКО внутри окна «Управление» (#promo_popup),
          // чтобы не менять кнопку на основной странице в режиме «Список»
          '#promo_popup .js-transfer-go{',
          '  display:inline-flex !important;',
          '  align-items:center !important;',
          '  justify-content:center !important;',
          '  width:max-content !important;',
          '  padding:4px 10px !important;', // компактнее по высоте
          '  background:#2c4a8d;',
          '  color:#fff;',
          '  border:none;',
          '  border-radius:6px;',
          '  cursor:pointer;',
          '  font-weight:700;',
          '  font-size:12px !important;',
          '  line-height:1.25 !important;',
          '  text-align:center !important;',
          '  height:auto !important;',
          '  min-height:auto !important;',
          '  float:none !important;',
          '  box-shadow: 0 2px 8px rgba(0,0,0,0.15);',
          '  user-select:none;',
          '}',
          '#promo_popup .js-transfer-go:hover{ filter: brightness(1.05); }',
          '#promo_popup .js-transfer-go:active{ transform: translateY(1px); }',
          '#promo_popup .js-transfer-go[aria-busy="true"], #promo_popup .js-transfer-go.is-busy{ opacity:0.7; pointer-events:none; }',
          '',
          '#promo_popup .pw-selected-wrap {',
          '  display: flex !important;',
          '  flex-direction: row !important;',
          '  align-items: center !important;',
          '  justify-content: flex-start !important;',
          '  gap: 12px !important;',
          '  width: 100% !important;',
          '  flex-wrap: wrap !important;',
          '}',
          '#promo_popup .pw-selected-counter {',
          '  display: inline-block;',
          '  font-size: 12px;',
          '  color: #7a6a54;',
          '  font-weight: bold;',
          '  white-space: nowrap;',
          '  cursor: help;',
          '}',
          '#promo_popup .pw-selected-counter.pw-has-warning {',
          '  color: #d9534f !important;',
          '}',
          '#promo_popup .pw-selected-warning {',
          '  display: none !important;',
          '}'
        ].join('\n');
        var style = document.createElement('style');
        style.id = id;
        style.textContent = css;
        (document.head || document.documentElement).appendChild(style);
        try{ promoLog('style_injected', { scope: '#promo_popup' }); }catch(_){ }
      }catch(_){ }
    }

    function updateSelectedCount(){
      try{
        // Ищем чекбоксы по всему документу, так как они могут быть вне #promo_popup (например, в основном списке)
        // Но приоритет отдаем тем, что в контейнерах предметов
        var count = 0;
        try {
          // Ищем только чекбоксы предметов: 
          // 1. С классом js-item-checkbox (наш класс для унификации)
          // 2. Внутри .items_container (стандартный список сайта)
          // 3. Внутри #promo_items_composite (наша плитка/сетка)
          // При этом исключаем те, что могут попасть в общие селекторы, но не являются предметами (например, "Группировать по типу")
          var checkboxes = document.querySelectorAll('.items_container input[type=checkbox]:checked, #promo_items_composite input[type=checkbox]:checked, input[type=checkbox].js-item-checkbox:checked');
          
          // Дополнительная фильтрация для исключения технических чекбоксов, если они вдруг попали в выборку
          var finalCount = 0;
          for (var k=0; k<checkboxes.length; k++) {
            var cb = checkboxes[k];
            // Исключаем чекбокс "Группировать по типу", если он вдруг попал (хотя по селекторам выше не должен)
            if (cb.nextSibling && cb.nextSibling.textContent === 'Группировать по типу') continue;
            // На сайте MyGames/VKPlay чекбоксы предметов обычно имеют определенную структуру, 
            // но мы полагаемся на контейнеры .items_container и #promo_items_composite
            finalCount++;
          }
          count = finalCount;
        } catch(e) {
          // Fallback на случай ошибки в сложных селекторах
          count = document.querySelectorAll('.items_container input[type=checkbox]:checked, #promo_items_composite input[type=checkbox]:checked').length;
        }
        
        var host = document.getElementById('promo_popup') || document;
        var counters = host.querySelectorAll('.pw-selected-counter');
        var warnings = host.querySelectorAll('.pw-selected-warning');
        
        for (var i=0; i<counters.length; i++) {
          var c = counters[i];
          c.textContent = 'Выбрано предметов: ' + count;
          if (count > 40) {
            c.classList.add('pw-has-warning');
            c.setAttribute('title', 'Внимание: выбрано более 40 предметов. Часть может не дойти из-за лимита почты.');
          } else {
            c.classList.remove('pw-has-warning');
            c.removeAttribute('title');
          }
        }
      }catch(_){}
    }

    function normalizeBtn(el){
      try{
        if (!el) return false;
        if (!el.getAttribute('role')) el.setAttribute('role', 'button');
        if (!el.hasAttribute('tabindex')) el.setAttribute('tabindex','0');
        var txt = (el.textContent||'').trim();
        if (!txt) el.textContent = 'Передать';

        // Инжектим счетчик под кнопку, если его еще нет
        if (el.parentNode && !el.parentNode.querySelector('.pw-selected-counter')){
          var wrap = document.createElement('div');
          wrap.className = 'pw-selected-wrap';
          
          el.parentNode.insertBefore(wrap, el);
          wrap.appendChild(el);

          var counter = document.createElement('div');
          counter.className = 'pw-selected-counter';
          counter.textContent = 'Выбрано предметов: 0';
          wrap.appendChild(counter);
          
          updateSelectedCount();
        }
        return true;
      }catch(_){ return false; }
    }

    function applyToAll(){
      try{
        ensureStyle();
        // Работать только внутри окна «Управление»
        var host = document.getElementById('promo_popup');
        if (!host){ try{ promoLog('skipped_page', null); }catch(__){}; return; }
        
        // Сразу принудительно обновим счетчик в начале, чтобы "0" не висело долго
        updateSelectedCount();

        // Если на странице уже есть «родная» .go_items в попапе — добавим ей наш класс для унификации
        try{
          var rawBtns = host.querySelectorAll('.go_items');
          if (rawBtns && rawBtns.length){
            for (var r=0;r<rawBtns.length;r++){
              try{ rawBtns[r].classList.add('js-transfer-go'); }catch(__){}
            }
          }
        }catch(__){}
        var btns = host.querySelectorAll('.js-transfer-go');
        var total = 0;
        if (btns && btns.length){
          for (var i=0;i<btns.length;i++){
            try{ if (normalizeBtn(btns[i])) total++; }catch(__){}
          }
        }
        
        // Повторный вызов после нормализации кнопок
        updateSelectedCount();
        
        try{ promoLog('applied_popup', { count: total }); }catch(_){ }
      }catch(_){ }
    }

    function observe(){
      try{
        if (window.__pwSubmitUiObserver) return;
        var host = document.getElementById('promo_popup');
        if (!host){ try{ promoLog('observer_skipped_no_popup', null); }catch(__){}; return; }

        // Добавим обработчик кликов для отслеживания чекбоксов
        // Слушаем на document, так как элементы могут перемещаться
        document.addEventListener('change', function(e){
          if (e.target && e.target.type === 'checkbox') {
            updateSelectedCount();
          }
        }, true);

        // Также слушаем клики
        document.addEventListener('click', function(e){
          if (e.target && (e.target.type === 'checkbox' || e.target.tagName === 'LABEL' || e.target.closest('button') || e.target.classList.contains('js-item-checkbox'))) {
            setTimeout(updateSelectedCount, 50);
            setTimeout(updateSelectedCount, 250); // Повторный вызов для случаев с анимацией или задержкой скриптов
          }
        }, true);

        var obs = new MutationObserver(function(muts){
          try{
            for (var mi=0; mi<muts.length; mi++){
              var m = muts[mi];
              for (var ni=0; ni<m.addedNodes.length; ni++){
                var n = muts[mi].addedNodes[ni];
                if (!(n instanceof Element)) continue;
                // Обрабатываем только узлы внутри popup
                var scope = n.nodeType === 1 ? (n.closest ? n.closest('#promo_popup') : null) : null;
                if (!scope) continue;
                if (n.classList){
                  if (n.classList.contains('go_items')){ try{ n.classList.add('js-transfer-go'); }catch(__){} }
                  if (n.classList.contains('js-transfer-go')){ normalizeBtn(n); continue; }
                  if (n.classList.contains('items_container')){ setTimeout(updateSelectedCount, 100); continue; }
                }
                var found = n.querySelectorAll ? n.querySelectorAll('.js-transfer-go') : [];
                for (var fi=0; fi<found.length; fi++) normalizeBtn(found[fi]);
                // Также подхватим любые появившиеся .go_items и присвоим класс
                var foundRaw = n.querySelectorAll ? n.querySelectorAll('.go_items') : [];
                for (var fj=0; fj<foundRaw.length; fj++){
                  try{ foundRaw[fj].classList.add('js-transfer-go'); normalizeBtn(foundRaw[fj]); }catch(__){}
                }
                // Если добавились чекбоксы
                if (n.querySelectorAll && n.querySelectorAll('input[type=checkbox]').length > 0) {
                  setTimeout(updateSelectedCount, 100);
                }

                // Доп. проверка: если пришел узел items_container или promo_items_composite в любом виде
                if (n.nodeType === 1 && (n.classList.contains('items_container') || n.querySelector('.items_container') || n.id === 'promo_items_composite' || n.querySelector('#promo_items_composite'))){
                  setTimeout(updateSelectedCount, 100);
                }
              }
            }
          }catch(__){}
        });
        obs.observe(host, { childList:true, subtree:true });
        window.__pwSubmitUiObserver = obs;
        try{ promoLog('observer_created', { scope: '#promo_popup' }); }catch(_){ }
      }catch(_){ }
    }

    function run(){
      try{ promoLog('ready', { rs: document.readyState }); }catch(_){ }
      if (document.readyState === 'loading'){
        document.addEventListener('DOMContentLoaded', function(){ try{ applyToAll(); observe(); }catch(__){} }, { once:true });
      } else {
        applyToAll();
        observe();
      }
    }

    if (!window.Promo || !Promo.register){ run(); return; }
    Promo.register('SubmitUI', { run: run });
  }catch(_){ }
})();
