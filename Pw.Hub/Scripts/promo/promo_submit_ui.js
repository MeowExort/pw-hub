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
          '  margin:12px auto 0 !important;',
          '  float:none !important;',
          '}',
          // ВАЖНО: Стили применяются ТОЛЬКО внутри окна «Управление» (#promo_popup),
          // чтобы не менять кнопку на основной странице в режиме «Список»
          '#promo_popup .js-transfer-go{',
          '  display:inline-flex !important;',
          '  align-items:center !important;',
          '  justify-content:center !important;',
          '  width:max-content !important;',
          '  margin:12px auto 0 !important;', // центрирование по горизонтали
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
          '#promo_popup .js-transfer-go[aria-busy="true"], #promo_popup .js-transfer-go.is-busy{ opacity:0.7; pointer-events:none; }'
        ].join('\n');
        var style = document.createElement('style');
        style.id = id;
        style.textContent = css;
        (document.head || document.documentElement).appendChild(style);
        try{ promoLog('style_injected', { scope: '#promo_popup' }); }catch(_){ }
      }catch(_){ }
    }

    function normalizeBtn(el){
      try{
        if (!el) return false;
        if (!el.getAttribute('role')) el.setAttribute('role', 'button');
        if (!el.hasAttribute('tabindex')) el.setAttribute('tabindex','0');
        var txt = (el.textContent||'').trim();
        if (!txt) el.textContent = 'Передать';
        return true;
      }catch(_){ return false; }
    }

    function applyToAll(){
      try{
        ensureStyle();
        // Работать только внутри окна «Управление»
        var host = document.getElementById('promo_popup');
        if (!host){ try{ promoLog('skipped_page', null); }catch(__){}; return; }
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
        try{ promoLog('applied_popup', { count: total }); }catch(_){ }
      }catch(_){ }
    }

    function observe(){
      try{
        if (window.__pwSubmitUiObserver) return;
        var host = document.getElementById('promo_popup');
        if (!host){ try{ promoLog('observer_skipped_no_popup', null); }catch(__){}; return; }
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
                }
                var found = n.querySelectorAll ? n.querySelectorAll('.js-transfer-go') : [];
                for (var fi=0; fi<found.length; fi++) normalizeBtn(found[fi]);
                // Также подхватим любые появившиеся .go_items и присвоим класс
                var foundRaw = n.querySelectorAll ? n.querySelectorAll('.go_items') : [];
                for (var fj=0; fj<foundRaw.length; fj++){
                  try{ foundRaw[fj].classList.add('js-transfer-go'); normalizeBtn(foundRaw[fj]); }catch(__){}
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
