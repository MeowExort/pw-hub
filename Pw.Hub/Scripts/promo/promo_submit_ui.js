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
          '.js-transfer-go{',
          '  display:block;',
          '  width:max-content;',
          '  margin:12px auto 0;', // центрирование по горизонтали
          '  padding:6px 14px;',
          '  background:#2c4a8d;',
          '  color:#fff;',
          '  border:none;',
          '  border-radius:6px;',
          '  cursor:pointer;',
          '  font-weight:700;',
          '  font-size:13px;',
          '  text-align:center;',
          '  box-shadow: 0 2px 8px rgba(0,0,0,0.15);',
          '  user-select:none;',
          '}',
          '.js-transfer-go:hover{ filter: brightness(1.05); }',
          '.js-transfer-go:active{ transform: translateY(1px); }',
          '.js-transfer-go[aria-busy="true"], .js-transfer-go.is-busy{ opacity:0.7; pointer-events:none; }'
        ].join('\n');
        var style = document.createElement('style');
        style.id = id;
        style.textContent = css;
        (document.head || document.documentElement).appendChild(style);
        try{ promoLog('style_injected', null); }catch(_){ }
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
        var roots = document.querySelectorAll('#promo_container');
        var total = 0;
        if (roots && roots.length){
          for (var i=0;i<roots.length;i++){
            try{
              var btn = roots[i].querySelector('.js-transfer-go');
              if (btn){ if (normalizeBtn(btn)) total++; }
            }catch(__){}
          }
        } else {
          var b = document.querySelector('.js-transfer-go'); if (b && normalizeBtn(b)) total++;
        }
        try{ promoLog('applied', { count: total }); }catch(_){ }
      }catch(_){ }
    }

    function observe(){
      try{
        if (window.__pwSubmitUiObserver) return;
        var obs = new MutationObserver(function(muts){
          try{
            for (var mi=0; mi<muts.length; mi++){
              var m = muts[mi];
              for (var ni=0; ni<m.addedNodes.length; ni++){
                var n = muts[mi].addedNodes[ni];
                if (!(n instanceof Element)) continue;
                if (n.classList && n.classList.contains('js-transfer-go')){ normalizeBtn(n); continue; }
                var found = n.querySelectorAll ? n.querySelectorAll('.js-transfer-go') : [];
                for (var fi=0; fi<found.length; fi++) normalizeBtn(found[fi]);
              }
            }
          }catch(__){}
        });
        obs.observe(document.documentElement || document.body, { childList:true, subtree:true });
        window.__pwSubmitUiObserver = obs;
        try{ promoLog('observer_created', null); }catch(_){ }
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
