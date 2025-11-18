(function(){
  try{
    function promoLog(eventName, payload){
      try{
        var msg = { type:'promo_log', event:'history_' + String(eventName||''), data:payload||null, ts:Date.now() };
        if (window.chrome && window.chrome.webview && window.chrome.webview.postMessage){
          window.chrome.webview.postMessage(JSON.stringify(msg));
        }
      }catch(_){ }
    }

    // --- Persist позиции попапа через appConfig (persist между перезапусками приложения) ---
    // Ключ хранения
    var HIST_STATE_KEY = 'promo_history_popup_state';

    // In-memory кэш, чтобы читать синхронно между async-запросами
    var __hist_state = null;

    // Лёгкий shim для pwHubAppConfig (использует канал window.chrome.webview appConfig:get/set)
    function ensurePwHubAppConfigShim(){
      try{
        if (window.pwHubAppConfig && (window.pwHubAppConfig.get || window.pwHubAppConfig.set)) return;
        var hasChrome = !!(window.chrome && window.chrome.webview && window.chrome.webview.postMessage);
        if (!hasChrome) return;

        var pending = new Map();
        var nextId = 1;

        var shim = {
          get: function(key, def){
            return new Promise(function(resolve){
              var id = nextId++;
              pending.set(id, resolve);
              try{ window.chrome.webview.postMessage({ type:'appConfig:get', id:id, key:key }); }
              catch(_){ pending.delete(id); resolve(def); return; }
              // Таймаут на случай отсутствия ответа
              setTimeout(function(){ if (pending.has(id)){ pending.delete(id); resolve(def); } }, 800);
            });
          },
          set: function(key, val){
            return new Promise(function(resolve){
              var id = nextId++;
              pending.set(id, resolve);
              try{ window.chrome.webview.postMessage({ type:'appConfig:set', id:id, key:key, value: val }); }
              catch(_){ pending.delete(id); resolve(true); return; }
              setTimeout(function(){ if (pending.has(id)){ pending.delete(id); resolve(true); } }, 800);
            });
          }
        };

        window.pwHubAppConfig = shim;
        try{
          if (!window.appConfig){ window.appConfig = {}; }
          if (!window.appConfig.get) window.appConfig.get = shim.get;
          if (!window.appConfig.set) window.appConfig.set = shim.set;
        }catch(__){}

        if (window.chrome && window.chrome.webview){
          window.chrome.webview.addEventListener('message', function(ev){
            try{
              var msg = ev && ev.data;
              if (!msg || msg.type !== 'appConfig:resp') return;
              var res = pending.get(msg.id);
              if (!res) return;
              pending.delete(msg.id);
              if ('value' in msg) res(msg.value); else res(true);
            }catch(__){}
          });
        }
      }catch(__){}
    }

    // Получить актуальный appConfig API
    function getPromoAppConfig(){
      try{
        if (window.pwHubAppConfig && (window.pwHubAppConfig.get || window.pwHubAppConfig.set)) return window.pwHubAppConfig;
        if (window.appConfig && (window.appConfig.get || window.appConfig.set)) return window.appConfig;
      }catch(__){}
      return null;
    }

    // Синхронное чтение из кэша (для runtime-логики)
    function loadHistState(){
      try{ return __hist_state; }catch(__){ return null; }
    }

    // Асинхронно подтянуть state из appConfig и применить к попапу
    // onApplied?: optional callback invoked after successful apply
    function fetchHistStateAsyncAndApply(popup, onApplied){
      try{
        var appCfg = getPromoAppConfig();
        if (!appCfg || !appCfg.get) return;
        promoLog('pos_fetch_start', null);
        appCfg.get(HIST_STATE_KEY, null).then(function(st){
          try{
            __hist_state = st && typeof st === 'object' ? st : null;
            promoLog('pos_fetch_ok', __hist_state);
            if (__hist_state && popup){
              applyHistState(popup, __hist_state);
              try{ if (typeof onApplied === 'function') onApplied('fetched'); }catch(__){}
            }
          }catch(__){}
        }).catch(function(err){ try{ promoLog('pos_fetch_err', (err && (err.message||err))||'unknown'); }catch(__){} });
      }catch(__){}
    }

    // Ключ для быстрого bootstrap в рамках текущей сессии (исключительно для устранения «скачка» на reload)
    var HIST_BOOTSTRAP_KEY = 'promo_history_popup_bootstrap';

    function setBootstrapState(st){
      try{ sessionStorage.setItem(HIST_BOOTSTRAP_KEY, JSON.stringify(st)); }catch(__){}
    }
    function getBootstrapState(){
      try{
        var raw = sessionStorage.getItem(HIST_BOOTSTRAP_KEY);
        if (!raw) return null;
        var obj = JSON.parse(raw);
        return obj && typeof obj === 'object' ? obj : null;
      }catch(_){ return null; }
    }

    function saveHistState(st){
      try{
        if (!st || typeof st !== 'object') return;
        __hist_state = st;
        promoLog('pos_save', st);
        // Сохраним bootstrap-копию для мгновенного восстановления на следующем reload в рамках сессии
        try{ setBootstrapState(st); }catch(__){}
        var appCfg = getPromoAppConfig();
        if (appCfg && appCfg.set){
          appCfg.set(HIST_STATE_KEY, st)
            .then(function(){ try{ promoLog('pos_save_ok', null); }catch(__){} })
            .catch(function(err){ try{ promoLog('pos_save_err', (err && (err.message||err))||'unknown'); }catch(__){} });
        } else {
          // Нет appConfig — ничего не делаем (InPrivate: localStorage не используем по требованию)
          try{ promoLog('pos_save_no_appcfg', null); }catch(__){}
        }
      }catch(__){}
    }
    function clampToViewport(popup, left, top){
      try{
        var vw = (window.innerWidth || document.documentElement.clientWidth || 800);
        var vh = (window.innerHeight || document.documentElement.clientHeight || 600);
        var rect = popup.getBoundingClientRect();
        var w = rect && rect.width ? rect.width : 560;
        var h = rect && rect.height ? rect.height : 420;
        var margin = 8;
        var L = Math.max(margin, Math.min(left, vw - w - margin));
        var T = Math.max(margin, Math.min(top, vh - h - margin));
        if (L !== left || T !== top){
          promoLog('pos_clamp', { from:{left:left, top:top}, to:{left:L, top:T}, vw:vw, vh:vh, w:w, h:h });
        }
        return { left: L, top: T };
      }catch(_){ return { left:left, top:top }; }
    }
    function applyHistState(popup, st){
      try{
        if (!popup || !st) return;
        // Если в состоянии нет координат — ничего не делаем
        var useLeftTop = !!st.useLeftTop;
        // Клампим перед применением
        var lt = clampToViewport(popup, Number(st.left||0), Number(st.top||0));
        if (useLeftTop){
          popup.style.left = lt.left + 'px';
          popup.style.top = lt.top + 'px';
          popup.style.right = 'auto';
          popup.style.bottom = 'auto';
        } else {
          // Ветка right/bottom на будущее; по факту мы сохраняем только left/top после перетаскивания
          if (typeof st.right === 'number') popup.style.right = st.right + 'px';
          if (typeof st.bottom === 'number') popup.style.bottom = st.bottom + 'px';
          popup.style.left = 'auto';
          popup.style.top = 'auto';
        }
        promoLog('pos_apply', { useLeftTop:useLeftTop, left:lt.left, top:lt.top });
      }catch(_){ }
    }

    // Публичный API для немедленного открытия истории без перезагрузки
    // будет инициализирован после объявления openHistory (function hoisting позволяет ссылку ниже)
    try{
      if (!window.PromoTransferHistoryPopup){
        window.PromoTransferHistoryPopup = {
          openNow: function(){
            try{ promoLog('open_now', null); }catch(__){}
            if (document.readyState === 'loading'){
              document.addEventListener('DOMContentLoaded', function(){ try{ openHistory(); }catch(__){} }, { once:true });
            } else {
              openHistory();
            }
          }
        };
      }
    }catch(__){}

    function wantOpen(){
      try{
        var v = sessionStorage.getItem('__pwShowTransferHistory');
        return v === '1';
      }catch(_){ return false; }
    }

    function clearFlag(){
      try{ sessionStorage.removeItem('__pwShowTransferHistory'); }catch(_){ }
    }

    var HISTORY_URL = 'https://pwonline.ru/promo_items.php?do=history';
    function findHistoryUrl(){
      // URL истории фиксированный — поиск и вычисление не требуются
      return HISTORY_URL;
    }
    try{ promoLog('url_fixed', { url: HISTORY_URL }); }catch(__){}

    function ensureHost(){
      try{
        if (document.getElementById('promo_history_popup')) return true;

        // Немодальный плавающий попап (фиксированный размер, перетаскиваемый)
        var popup = document.createElement('div');
        popup.id = 'promo_history_popup';
        popup.style = [
          'position:fixed',
          // Старт без явных координат и в скрытом состоянии — покажем только после применения позиции,
          // чтобы избежать «скачка» из правого нижнего угла
          'visibility:hidden',
          'z-index:2147483647',
          'width:560px', 'height:420px',
          'max-width:90vw', 'max-height:85vh',
          'background:#F6F1E7',
          'border:1px solid #E2D8C9',
          'border-radius:10px',
          'box-shadow:0 8px 24px rgba(0,0,0,0.25)',
          'overflow:hidden',
          'display:flex', 'flex-direction:column'
        ].join(';');

        var header = document.createElement('div');
        header.id = 'promo_history_popup_header';
        header.style = 'display:flex;align-items:center;justify-content:space-between;padding:8px 10px;background:#EDE4D6;border-bottom:1px solid #E2D8C9;cursor:move;user-select:none;';
        var title = document.createElement('div');
        title.id = 'promo_history_popup_title';
        title.textContent = 'История передачи';
        title.style = 'font-weight:700;color:#2c4a8d;font-size:13px;letter-spacing:0.2px;';
        // Кнопки справа: Сворачивать и Обновить
        var headerBtns = document.createElement('div');
        headerBtns.id = 'promo_history_header_buttons';
        headerBtns.style = 'display:flex;gap:6px;align-items:center;';
        // Dock toggle button (встроить/отделить) — скрыт, т.к. теперь док/андок через Drag&Drop
        var dockBtn = document.createElement('button');
        dockBtn.id = 'promo_history_dock_btn';
        dockBtn.textContent = 'Встроить';
        dockBtn.title = 'Вставить окно в правый блок страницы';
        dockBtn.style = [
          'border:none',
          'background:#D2C0BE',
          'color:#333',
          'border-radius:12px',
          'padding:3px 10px',
          'cursor:pointer',
          'font-size:12px',
          'line-height:16px',
          'font-weight:700'
        ].join(';');
        // Скрываем кнопку, док/андок выполняется перетаскиванием
        dockBtn.style.display = 'none';
        try{ promoLog('dock_btn_hidden', null); }catch(__){}
        var collapseBtn = document.createElement('button');
        collapseBtn.id = 'promo_history_collapse_btn';
        collapseBtn.textContent = '−';
        collapseBtn.title = 'Свернуть';
        collapseBtn.style = 'border:none;background:#D2C0BE;color:#333;border-radius:12px;padding:1px 6px;cursor:pointer;font-size:12px;line-height:16px;';
        var refreshBtn = document.createElement('button');
        refreshBtn.id = 'promo_history_refresh_btn';
        refreshBtn.textContent = 'Обновить';
        refreshBtn.title = 'Обновить статусы без перезагрузки страницы';
        refreshBtn.style = [
          'border:none',
          'background:#D2C0BE',
          'color:#333',
          'border-radius:12px',
          'padding:3px 10px',
          'cursor:pointer',
          'font-size:12px',
          'line-height:16px',
          'font-weight:700'
        ].join(';');

        var body = document.createElement('div');
        body.id = 'promo_history_popup_body';
        body.style = 'position:relative;flex:1 1 auto;background:#fff;overflow:hidden;';

        var iframe = document.createElement('iframe');
        iframe.id = 'promo_history_popup_iframe';
        iframe.style = 'border:none;width:100%;height:100%;opacity:0;transition:opacity .15s ease-in-out';
        iframe.setAttribute('sandbox', 'allow-same-origin allow-scripts allow-forms');

        // Загрузочный оверлей поверх iframe (скрывает «мигание» исходной страницы при перезагрузке)
        var cover = document.createElement('div');
        cover.id = 'promo_history_loading_cover';
        cover.style = [
          'position:absolute','inset:0','display:none','align-items:center','justify-content:center',
          'background:#fff','z-index:2'
        ].join(';');
        var coverInner = document.createElement('div');
        coverInner.style = 'padding:12px 14px;border:1px solid #E2D8C9;border-radius:8px;background:#F6F1E7;color:#666;font-family:Arial,sans-serif;font-size:13px;box-shadow:0 2px 8px rgba(0,0,0,0.08)';
        coverInner.textContent = 'Загрузка истории…';
        cover.appendChild(coverInner);

        body.appendChild(iframe);
        body.appendChild(cover);
        header.appendChild(title);
        headerBtns.appendChild(dockBtn);
        headerBtns.appendChild(refreshBtn);
        headerBtns.appendChild(collapseBtn);
        header.appendChild(headerBtns);
        popup.appendChild(header);
        popup.appendChild(body);
        document.body.appendChild(popup);

        // Компактный вид (свёрнутая «кнопка») — как в окне «Управление»
        var compact = document.createElement('div');
        compact.id = 'promo_history_popup_compact';
        compact.style = 'display:none; margin:4px; padding:4px 10px; background:#EDE4D6; color:#2c4a8d; font-weight:700; font-size:12px; border-radius:14px; cursor:move; user-select:none; box-shadow: inset 0 0 0 1px #E2D8C9; width:max-content;';
        compact.title = 'Развернуть историю передачи';
        compact.innerText = 'История передачи';
        popup.appendChild(compact);
        try{ promoLog('compact_created', null); }catch(__){}

        // Подготовим shim и подтянем сохранённую позицию из appConfig/Bootstrap
        try{ ensurePwHubAppConfigShim(); }catch(__){}
        try{
          var boot = getBootstrapState();
          if (boot){
            promoLog('pos_bootstrap_used', boot);
            applyHistState(popup, boot);
            // Сразу покажем попап — координаты уже применены
            popup.style.visibility = 'visible';
            promoLog('pos_show_after_bootstrap', null);
          } else {
            promoLog('pos_bootstrap_absent', null);
            // Попробуем взять из in-memory кэша (на случай ранней инициализации)
            var s0 = loadHistState();
            if (s0){
              applyHistState(popup, s0);
              popup.style.visibility = 'visible';
              promoLog('pos_show_after_cache', null);
            } else {
              // Ждём appConfig и показываем после применения
              fetchHistStateAsyncAndApply(popup, function(){
                try{ popup.style.visibility = 'visible'; promoLog('pos_show_after_fetch', null); }catch(__){}
              });
              // Сторожевой фоллбек: если конфиг долго не приходит — покажем в правом нижнем углу через ~900мс
              setTimeout(function(){
                try{
                  if (popup.style.visibility !== 'visible'){
                    popup.style.right = '12px';
                    popup.style.bottom = '12px';
                    popup.style.left = 'auto';
                    popup.style.top = 'auto';
                    popup.style.visibility = 'visible';
                    promoLog('pos_show_fallback', null);
                  }
                }catch(__){}
              }, 900);
            }
          }
        }catch(__){}

        // Переключение свёрнутости
        function getCollapsed(){ try{ return sessionStorage.getItem('promo_history_collapsed') === '1'; }catch(_){ return false; } }
        var DEFAULT_W = 560, DEFAULT_H = 420;
        function setCollapsed(val){
          try{
            var collapsed = !!val;
            if (collapsed){
              // Показать компактную «кнопку», скрыть шапку и тело, ужать окно
              if (header) header.style.display = 'none';
              if (body) body.style.display = 'none';
              if (compact) compact.style.display = 'block';
              popup.style.minWidth = '120px';
              popup.style.minHeight = '28px';
              popup.style.width = 'auto';
              popup.style.height = 'auto';
              collapseBtn.textContent = '+';
              sessionStorage.setItem('promo_history_collapsed','1');
              promoLog('collapsed_set', { value: true });
            } else {
              // Вернуть полноценный вид; размеры зависят от режима (док или плавающий)
              if (compact) compact.style.display = 'none';
              if (header) header.style.display = 'flex';
              if (body) body.style.display = 'block';

              if (typeof isDocked === 'function' && isDocked()){
                // В док-режиме ширина/высота управляются setDockVisual и зависят от родителя —
                // не переопределяем ширину здесь, чтобы окно не выезжало за правый край,
                // но задаём минимальную высоту, чтобы список истории был хорошо виден
                popup.style.minWidth = '';
                popup.style.minHeight = '400px';
              } else {
                // Плавающий режим: восстанавливаем базовые размеры
                popup.style.minWidth = '240px';
                popup.style.minHeight = '120px';
                popup.style.width = DEFAULT_W + 'px';
                popup.style.height = DEFAULT_H + 'px';
              }

              collapseBtn.textContent = '−';
              sessionStorage.setItem('promo_history_collapsed','0');
              promoLog('collapsed_set', { value: false, docked: (typeof isDocked === 'function' ? isDocked() : null) });
            }
          }catch(__){}
        }
        collapseBtn.addEventListener('click', function(){ try{ setCollapsed(!getCollapsed()); }catch(__){} });

        // Сворачивание по клику на заголовок/шапку (кроме кликов по кнопкам)
        header.addEventListener('click', function(e){
          try{
            var t = e.target;
            // Игнорируем клики по интерактивам справа
            if (t && (t === collapseBtn || t === refreshBtn || t === dockBtn || (t.closest && t.closest('#promo_history_collapse_btn,#promo_history_refresh_btn,#promo_history_dock_btn,#promo_history_header_buttons')))) return;
            // Защита от клика сразу после перетаскивания
            if (header && (header.dataset.dragMoved === '1' || header.dataset.dragJustDragged === '1')){ e.preventDefault(); e.stopPropagation(); return; }
            promoLog('header_click_collapse', { collapsed: !getCollapsed() });
            setCollapsed(!getCollapsed());
          }catch(__){}
        });

        // Клик по компактному виду — разворачиваем (если это не окончание перетаскивания)
        compact.addEventListener('click', function(e){
          try{
            if (compact && (compact.dataset.dragMoved === '1' || compact.dataset.dragJustDragged === '1')){
              e.preventDefault(); e.stopPropagation(); return false;
            }
            promoLog('compact_click', null);
            setCollapsed(false);
          }catch(__){}
        });

        // Обновление содержимого iframe (без перезагрузки родителя)
        function doRefresh(){
          try{
            var ifr = document.getElementById('promo_history_popup_iframe');
            if (!ifr) return;
            refreshBtn.disabled = true;
            refreshBtn.textContent = 'Обновляем…';
            // Скрываем содержимое iframe и показываем плейсхолдер
            try{ ifr.style.opacity = '0'; promoLog('iframe_hide', null); }catch(__){}
            try{ var cv = document.getElementById('promo_history_loading_cover'); if (cv){ cv.style.display='flex'; promoLog('cover_show', { reason:'refresh' }); } }catch(__){}
            try{
              // Сторожевой таймер на случай зависшей загрузки
              window.__pwHistGuard && clearTimeout(window.__pwHistGuard);
              window.__pwHistGuard = setTimeout(function(){
                try{ var rb = document.getElementById('promo_history_refresh_btn'); if (rb){ rb.disabled=false; rb.textContent='Обновить'; } }catch(__){}
                try{ var cv2 = document.getElementById('promo_history_loading_cover'); if (cv2){ cv2.style.display='none'; promoLog('cover_hide', { reason:'timeout' }); } }catch(__){}
                try{ var ifr2 = document.getElementById('promo_history_popup_iframe'); if (ifr2){ ifr2.style.opacity='1'; promoLog('iframe_show', { reason:'timeout' }); } }catch(__){}
                try{ promoLog('refresh_timeout', null); }catch(__){}
              }, 15000);
            }catch(__){}
            var cur = findHistoryUrl();
            try{
              var u = new URL(cur);
              u.searchParams.set('_cb', String(Date.now()));
              ifr.src = u.toString();
            }catch(_){
              var sep = cur.indexOf('?') === -1 ? '?' : '&';
              ifr.src = cur + sep + '_cb=' + Date.now();
            }
          }catch(__){ }
        }
        refreshBtn.addEventListener('click', function(e){ try{ if (e && e.stopPropagation) e.stopPropagation(); promoLog('refresh_click', null); doRefresh(); }catch(__){} });

        // Dock/Undock: helpers
        function getDockHost(){
          try{
            var host = document.querySelector('.pagecontent_table_right');
            if (!host) return null;
            var dockWrap = host.querySelector('#promo_history_dock_host');
            if (!dockWrap){
              dockWrap = document.createElement('div');
              dockWrap.id = 'promo_history_dock_host';
              // Хост для док-режима: тянемся на всю ширину правой колонки и не выезжаем за её пределы
              dockWrap.style = 'margin:8px 0;width:100%;box-sizing:border-box;min-height:400px;';
              host.appendChild(dockWrap);
            } else {
              // На всякий случай обновим базовые стили, если они были перезаписаны
              try{
                dockWrap.style.width = '100%';
                dockWrap.style.boxSizing = 'border-box';
                if (!dockWrap.style.minHeight || parseInt(dockWrap.style.minHeight,10) < 400){ dockWrap.style.minHeight = '400px'; }
              }catch(__){}
            }
            return dockWrap;
          }catch(__){ return null; }
        }

        function mergeState(extra){
          try{
            var cur = loadHistState() || {};
            var st = {};
            for (var k in cur){ if (Object.prototype.hasOwnProperty.call(cur,k)) st[k]=cur[k]; }
            if (extra && typeof extra === 'object'){
              for (var k2 in extra){ if (Object.prototype.hasOwnProperty.call(extra,k2)) st[k2]=extra[k2]; }
            }
            return st;
          }catch(__){ return extra||{}; }
        }

        function setDockVisual(docked){
          try{
            popup.dataset.docked = docked ? '1' : '0';
            if (docked){
              // Встроенный режим: убираем фиксированное позиционирование, растягиваем по ширине
              popup.style.position = 'static';
              popup.style.width = '100%';
              popup.style.height = 'auto';
              popup.style.maxWidth = 'none';
              popup.style.maxHeight = 'none';
              popup.style.boxShadow = 'none';
              popup.style.borderRadius = '8px';
              // Цвет фона окна в док-режиме
              try{ popup.style.background = '#F6F1E7'; }catch(__){}
              // Встроенный режим: учитываем границы в общей ширине, чтобы окно не выезжало за пределы правого блока
              popup.style.boxSizing = 'border-box';
              popup.style.margin = '0';
              try{ var hdr = document.getElementById('promo_history_popup_header'); if (hdr) hdr.style.cursor='default'; }catch(__){}
              // Встроенный режим — перетаскивание не нужно, компактная кнопка скрыта
              compact.style.display = 'none';
              // Минимальная высота в доке (для хорошей видимости списка)
              try{ popup.style.minHeight = '400px'; }catch(__){}
              // Фон тела в док-режиме тоже делаем #F6F1E7
              try{ body.style.background = '#F6F1E7'; }catch(__){}
              // Пересчитать высоты тела/iframe
              try{ recalcDockHeights(); }catch(__){}
            } else {
              popup.style.position = 'fixed';
              popup.style.width = DEFAULT_W + 'px';
              popup.style.height = DEFAULT_H + 'px';
              popup.style.maxWidth = '90vw';
              popup.style.maxHeight = '85vh';
              popup.style.boxShadow = '0 8px 24px rgba(0,0,0,0.25)';
              // Возвращаем фон тела к белому во float-режиме
              try{ body.style.background = '#fff'; }catch(__){}
              try{ var hdr2 = document.getElementById('promo_history_popup_header'); if (hdr2) hdr2.style.cursor='move'; }catch(__){}
              // Компактная кнопка отображается только когда свёрнуто
              if (getCollapsed()) compact.style.display = 'block';
              // Сброс специальных ограничений дока
              try{ body.style.minHeight = ''; }catch(__){}
            }
          }catch(__){}
        }

        function isDocked(){ try{ return popup && popup.dataset && popup.dataset.docked === '1'; }catch(__){ return false; } }

        function applyDock(docked, opts){
          try{
            opts = opts || {};
            var silent = !!opts.silent; // не трогать контент (refresh) при временном undock во время drag
            var wantDock = !!docked;
            if (wantDock === isDocked()) return;
            if (wantDock){
              var host = getDockHost();
              if (!host){
                promoLog('dock_host_missing', null);
                return;
              }
              host.appendChild(popup);
              setDockVisual(true);
              dockBtn.textContent = 'Отделить';
              dockBtn.title = 'Вынести окно из правого блока';
              // При доке лучше развернуть окно
              try{ setCollapsed(false); }catch(__){}
              // Сохранить состояние
              saveHistState(mergeState({ docked:true }));
              promoLog('dock_applied', null);
              // Перестроим содержимое под режим списка
              if (!silent){ try{ doRefresh(); }catch(__){} }
            } else {
              // Возвращаем во float-режим в body
              document.body.appendChild(popup);
              setDockVisual(false);
              dockBtn.textContent = 'Встроить';
              dockBtn.title = 'Вставить окно в правый блок страницы';
              // Применим сохранённые координаты, если были
              var s2 = loadHistState() || {};
              // Если пришёл отрезок fromRect (временный undock во время drag) — применим текущие экранные координаты,
              // чтобы окно не «прыгало» при начале перетаскивания
              var fr = opts && opts.fromRect;
              if (fr && typeof fr.left === 'number' && typeof fr.top === 'number'){
                popup.style.left = Math.round(fr.left) + 'px';
                popup.style.top = Math.round(fr.top) + 'px';
                popup.style.right = 'auto';
                popup.style.bottom = 'auto';
                saveHistState(mergeState({ useLeftTop:true, left: Math.round(fr.left), top: Math.round(fr.top) }));
              } else {
                if (s2 && s2.useLeftTop){
                  applyHistState(popup, s2);
                } else {
                  // если координат нет — откроем в правом нижнем углу
                  popup.style.right = '12px';
                  popup.style.bottom = '12px';
                  popup.style.left = 'auto';
                  popup.style.top = 'auto';
                }
              }
              saveHistState(mergeState({ docked:false }));
              promoLog('undock_done', null);
              // Перестроим содержимое обратно в таблицу
              if (!silent){ try{ doRefresh(); }catch(__){} }
            }
          }catch(__){}
        }

        // Подсветка зоны докинга (.pagecontent_table_right)
        var dockHighlight = null;
        function getDockAcceptRect(){
          try{
            // Рисуем подсветку ровно там, где реально будет расположен док‑хост
            var host = getDockHost();
            if (host){
              var r = host.getBoundingClientRect();
              // Если по каким‑то причинам высота 0 — принудительно выставим minHeight и перечитаем
              if (!r.height || r.height < 1){
                try{ host.style.minHeight = host.style.minHeight || '400px'; }catch(__){}
                r = host.getBoundingClientRect();
              }
              try{ promoLog('dock_accept_host_rect', { left:r.left, top:r.top, width:r.width, height:r.height }); }catch(__){}
              return { left:r.left, top:r.top, right:r.right, bottom:r.bottom, width:r.width, height:r.height };
            }
            // Fallback: верх правой колонки высотой 400px
            var col = document.querySelector('.pagecontent_table_right');
            if (col){
              var b = col.getBoundingClientRect();
              var h = Math.min(400, Math.max(0, b.height));
              return { left:b.left, top:b.top, right:b.right, bottom:b.top + h, width:b.width, height:h };
            }
            return null;
          }catch(__){ return null; }
        }
        function showDockHighlight(){
          try{
            var r = getDockAcceptRect();
            if (!r) return;
            if (!dockHighlight){
              dockHighlight = document.createElement('div');
              dockHighlight.id = 'promo_history_dock_highlight';
              dockHighlight.style = [
                'position:fixed','z-index:2147483646','pointer-events:none',
                'border:2px dashed #2c4a8d','background:rgba(44,74,141,0.08)'
              ].join(';');
              document.body.appendChild(dockHighlight);
            }
            dockHighlight.style.left = r.left + 'px';
            dockHighlight.style.top = r.top + 'px';
            dockHighlight.style.width = r.width + 'px';
            var h = r.height; if (!h || h < 140) h = 140; // гарантированная минимальная высота (визуал подсветки)
            dockHighlight.style.height = h + 'px';
            dockHighlight.style.display = 'block';
            promoLog('dock_highlight_on', { rect: { left: r.left, top: r.top, width: r.width, height: h } });
          }catch(__){}
        }
        function hideDockHighlight(){
          try{ if (dockHighlight){ dockHighlight.style.display='none'; promoLog('dock_highlight_off', null); } }catch(__){}
        }

        function pointInDockHost(x, y){
          try{
            var r = getDockAcceptRect();
            if (!r) return false;
            var tol = 4; // меньший допуск от края, чтобы было проще «вытащить» окно
            return x >= (r.left - tol) && x <= (r.right + tol) && y >= (r.top - tol) && y <= (r.bottom + tol);
          }catch(__){ return false; }
        }

        // Пересчёт высот в док-режиме
        function recalcDockHeights(){
          try{
            if (!isDocked()) return;
            var hdr = document.getElementById('promo_history_popup_header');
            var hdrH = hdr ? hdr.offsetHeight : 0;
            // Общая минимальная высота попапа уже 400px — тело не меньше 400 - headerH
            var minBody = Math.max(200, 400 - hdrH);
            body.style.minHeight = minBody + 'px';
            // Попробуем выставить явную высоту тела, чтобы iframe занял всю область док-хоста
            try{
              var host = getDockHost();
              var hostRect = host ? host.getBoundingClientRect() : null;
              var hostH = hostRect && hostRect.height ? hostRect.height : 0;
              // fallback — по высоте правой колонки
              if (!hostH){
                var col = document.querySelector('.pagecontent_table_right');
                if (col){ var cr = col.getBoundingClientRect(); hostH = cr && cr.height ? cr.height : 0; }
              }
              // Если удалось измерить — выставим явную высоту тела
              if (hostH && hostH > 0){
                var targetH = Math.max(minBody, Math.floor(hostH - hdrH));
                body.style.height = targetH + 'px';
              } else {
                // иначе сбросим явную высоту — останется minHeight
                body.style.height = '';
              }
            }catch(__){ body.style.height = ''; }
            // На всякий случай проверим высоту iframe
            try{ var ifr = document.getElementById('promo_history_popup_iframe'); if (ifr){ ifr.style.height = '100%'; } }catch(__){}
            promoLog('dock_recalc_done', { headerH: hdrH, minBody: minBody, bodyH: body && body.offsetHeight });
          }catch(__){ }
        }

        // Drag helper — подключаем к header и compact (с порогом, чтобы клик по заголовку в доке не запускал DnD)
        (function(){
          try{
            function attachDrag(handle, isCompact){
              if (!handle) return;
              var THRESH = 8; // пикселей до активации drag
              var leftZoneOnce = false; // помечаем выход из зоны после первого входа
              var everInside = false;   // были ли мы когда‑нибудь внутри зоны в рамках текущего drag

              if (window.PointerEvent){
                // Реализация на Pointer Events с pointer capture — устойчиво при быстром выходе курсора за пределы заголовка
                var dragging = false, activated = false, sx=0, sy=0, sl=0, st=0, wasDocked=false, pid=null;

                function onPointerDown(e){
                  try{
                    // Только ЛКМ/pen/touch — игнорируем вторичные кнопки мыши
                    if (e.button != null && e.button !== 0) return;
                    // Не начинаем перетаскивание при кликах по кнопкам в хедере
                    var t = e.target;
                    if (t && (t === collapseBtn || t === refreshBtn || t === dockBtn || (t.closest && t.closest('#promo_history_collapse_btn,#promo_history_refresh_btn,#promo_history_dock_btn,#promo_history_header_buttons')))) return;
                    dragging = true; activated = false; pid = e.pointerId;
                    // Сбрасываем флаг выхода из зоны на начало каждого drag
                    leftZoneOnce = false;
                    everInside = false;
                    // Флаги для защиты от клика после перетаскивания
                    handle.dataset.dragMoved = '0';
                    var rect = popup.getBoundingClientRect();
                    sx = e.clientX; sy = e.clientY; sl = rect.left; st = rect.top;
                    wasDocked = isDocked();
                    try{ handle.setPointerCapture(pid); promoLog('drag_capture_set', { pid: pid }); }catch(__){}
                    handle.addEventListener('pointermove', onPointerMove);
                    handle.addEventListener('pointerup', onPointerUp);
                    handle.addEventListener('pointercancel', onPointerCancel);
                    handle.addEventListener('lostpointercapture', onLostCapture);
                    e.preventDefault();
                  }catch(__){}
                }

                function onPointerMove(e){
                  try{
                    if (!dragging) return;
                    var dx = e.clientX - sx, dy = e.clientY - sy;
                    var dist = Math.max(Math.abs(dx), Math.abs(dy));
                    if (!activated){
                      if (dist >= THRESH){
                        activated = true;
                        // Если окно было встроено — временно переведём во float для перетаскивания
                        wasDocked = isDocked();
                        if (wasDocked){
                          promoLog('undock_drag_start', null);
                          // Снимем текущие экранные координаты до undock
                          var r0 = popup.getBoundingClientRect();
                          applyDock(false, { silent:true, fromRect: r0 });
                          // Явное намерение вынести окно: в рамках текущего drag не выполнять док
                          leftZoneOnce = true;
                          try{ promoLog('drag_undock_intent', null); }catch(__){}
                          // Перехват указателя может потеряться при перепривязке — перепробуем захватить ещё раз
                          try{
                            handle.setPointerCapture(pid);
                            promoLog('drag_capture_reacquire', { pid: pid });
                          }catch(__){}
                        }
                        // Подсветим зону докинга
                        showDockHighlight();
                        promoLog('drag_threshold_reached', { thresh: THRESH });
                      } else {
                        return; // ещё не активировали перетаскивание
                      }
                    }
                    // Трекинг выхода из зоны приёма (для наглядности и логики)
                    var inside = pointInDockHost(e.clientX, e.clientY);
                    if (inside){
                      everInside = true;
                    } else if (everInside){
                      leftZoneOnce = true;
                    }
                    var left = sl + dx, top = st + dy;
                    popup.style.left = left + 'px';
                    popup.style.top = top + 'px';
                    popup.style.right = 'auto';
                    popup.style.bottom = 'auto';
                    handle.dataset.dragMoved = '1';
                  }catch(__){}
                }

                function finishDrag(ev){
                  try{
                    if (!dragging) return;
                    dragging = false;
                    try{ handle.releasePointerCapture(pid); }catch(__){}
                    try{ handle.removeEventListener('pointermove', onPointerMove); }catch(__){}
                    try{ handle.removeEventListener('pointerup', onPointerUp); }catch(__){}
                    try{ handle.removeEventListener('pointercancel', onPointerCancel); }catch(__){}
                    try{ handle.removeEventListener('lostpointercapture', onLostCapture); }catch(__){}
                    if (!activated){
                      // Не было реального перетаскивания — не меняем режим и не показываем подсветку
                      hideDockHighlight();
                      return;
                    }
                    var mx = ev && (ev.clientX||0), my = ev && (ev.clientY||0);
                    var forceUndock = !!(ev && (ev.altKey || ev.ctrlKey || ev.shiftKey));
                    var droppedInHost = pointInDockHost(mx, my);
                    // Докать только если курсор внутри зоны И за время текущего drag мы ни разу из неё не выходили
                    if (droppedInHost && !forceUndock && (!wasDocked ? true : !leftZoneOnce)){
                      applyDock(true);
                      promoLog('dock_drop_in', { x: mx, y: my });
                    } else {
                      if (droppedInHost && leftZoneOnce && !forceUndock){
                        // Курсор вернулся в зону к моменту отпускания, но пользователь уже уводил его из зоны — трактуем как желание вынести окно
                        promoLog('dock_drop_suppressed', { x: mx, y: my, reason: 'leftZoneOnce' });
                      }
                      var rect = popup.getBoundingClientRect();
                      var cl = clampToViewport(popup, rect.left, rect.top);
                      popup.style.left = cl.left + 'px';
                      popup.style.top = cl.top + 'px';
                      popup.style.right = 'auto';
                      popup.style.bottom = 'auto';
                      saveHistState(mergeState({ useLeftTop:true, left: cl.left, top: cl.top }));
                      promoLog('dock_drop_out', { x: mx, y: my, forceUndock: forceUndock, leftZoneOnce: leftZoneOnce });
                    }
                    hideDockHighlight();
                    if (handle.dataset.dragMoved === '1'){
                      handle.dataset.dragJustDragged = '1';
                      setTimeout(function(){ try{ handle.dataset.dragJustDragged = '0'; handle.dataset.dragMoved = '0'; }catch(__){} }, 60);
                    }
                    if (wasDocked) promoLog('undock_drag_end', null);
                  }catch(__){}
                }

                function onPointerUp(e){ finishDrag(e); }
                function onPointerCancel(e){ finishDrag(e); }
                function onLostCapture(e){
                  try{
                    promoLog('drag_capture_lost', null);
                    if (dragging){
                      try{ handle.setPointerCapture(pid); promoLog('drag_capture_reacquire', { pid: pid }); return; }catch(__){}
                    }
                  }catch(__){}
                  // если не удалось — завершим как обычно
                  finishDrag(e);
                }

                handle.addEventListener('pointerdown', onPointerDown);
                return; // не подключаем мышиные события, если есть Pointer Events
              }

              // Fallback для старых движков: мышиные события с обработкой на window
              var dragging = false, activated = false, sx=0, sy=0, sl=0, st=0, wasDocked=false;
              function onMouseDown(e){
                try{
                  if (e.button !== 0) return;
                  // Не начинаем перетаскивание при кликах по кнопкам в хедере
                  var t = e.target;
                  if (t && (t === collapseBtn || t === refreshBtn || t === dockBtn || (t.closest && t.closest('#promo_history_collapse_btn,#promo_history_refresh_btn,#promo_history_dock_btn,#promo_history_header_buttons')))) return;
                  dragging = true; activated = false;
                  // Сбрасываем флаг выхода из зоны на начало каждого drag
                  leftZoneOnce = false; everInside = false;
                  handle.dataset.dragMoved = '0';
                  var rect = popup.getBoundingClientRect();
                  sx = e.clientX; sy = e.clientY; sl = rect.left; st = rect.top;
                  wasDocked = isDocked();
                  window.addEventListener('mousemove', onMouseMove);
                  window.addEventListener('mouseup', onMouseUp, { once:true });
                  window.addEventListener('blur', onMouseCancel, { once:true });
                  e.preventDefault();
                }catch(__){}
              }
              function onMouseMove(e){
                try{
                  if (!dragging) return;
                  var dx = e.clientX - sx, dy = e.clientY - sy;
                  var dist = Math.max(Math.abs(dx), Math.abs(dy));
                  if (!activated){
                    if (dist >= THRESH){
                      activated = true;
                      wasDocked = isDocked();
                      if (wasDocked){
                        promoLog('undock_drag_start', null);
                        var r0 = popup.getBoundingClientRect();
                        applyDock(false, { silent:true, fromRect: r0 });
                        // Явное намерение вынести окно в этом drag — запретим докинг по отпусканию
                        leftZoneOnce = true;
                        try{ promoLog('drag_undock_intent', null); }catch(__){}
                      }
                      showDockHighlight();
                      promoLog('drag_threshold_reached', { thresh: THRESH });
                    } else { return; }
                  }
                  var inside2 = pointInDockHost(e.clientX, e.clientY);
                  if (inside2){ everInside = true; } else if (everInside){ leftZoneOnce = true; }
                  popup.style.left = (sl + dx) + 'px';
                  popup.style.top = (st + dy) + 'px';
                  popup.style.right = 'auto';
                  popup.style.bottom = 'auto';
                  handle.dataset.dragMoved = '1';
                }catch(__){}
              }
              function finishMouse(ev){
                try{
                  window.removeEventListener('mousemove', onMouseMove);
                  if (!dragging) return; dragging = false;
                  if (!activated){ hideDockHighlight(); return; }
                  var mx = ev && (ev.clientX||0), my = ev && (ev.clientY||0);
                  var forceUndock = !!(ev && (ev.altKey || ev.ctrlKey || ev.shiftKey));
                  var droppedInHost = pointInDockHost(mx, my);
                  if (droppedInHost && !forceUndock && (!wasDocked ? true : !leftZoneOnce)){
                    applyDock(true); promoLog('dock_drop_in', { x: mx, y: my });
                  } else {
                    if (droppedInHost && leftZoneOnce && !forceUndock){
                      promoLog('dock_drop_suppressed', { x: mx, y: my, reason: 'leftZoneOnce' });
                    }
                    var rect = popup.getBoundingClientRect();
                    var cl = clampToViewport(popup, rect.left, rect.top);
                    popup.style.left = cl.left + 'px';
                    popup.style.top = cl.top + 'px';
                    popup.style.right = 'auto'; popup.style.bottom = 'auto';
                    saveHistState(mergeState({ useLeftTop:true, left: cl.left, top: cl.top }));
                    promoLog('dock_drop_out', { x: mx, y: my, forceUndock: forceUndock, leftZoneOnce: leftZoneOnce });
                  }
                  hideDockHighlight();
                  if (handle.dataset.dragMoved === '1'){
                    handle.dataset.dragJustDragged='1';
                    setTimeout(function(){ try{ handle.dataset.dragJustDragged='0'; handle.dataset.dragMoved='0'; }catch(__){} }, 60);
                  }
                  if (wasDocked) promoLog('undock_drag_end', null);
                }catch(__){}
              }
              function onMouseUp(e){ finishMouse(e); }
              function onMouseCancel(){ finishMouse({ clientX:0, clientY:0 }); }

              handle.addEventListener('mousedown', onMouseDown);
            }
            attachDrag(header, false);
            attachDrag(compact, true);
          }catch(__){}
        })();

        // При изменении размеров окна — удерживаем попап в видимой области и обновляем state
        try{
          if (!window.__pwHistResizeHandler){
            window.addEventListener('resize', function(){
              try{
                var p = document.getElementById('promo_history_popup');
                if (!p) return;
                // Пересчёт высот в доке
                try{ recalcDockHeights(); }catch(__){}
                // Если окно во float и у него сохранённая позиция — удержим в вьюпорте
                var st = loadHistState();
                if (st && st.useLeftTop && !isDocked()){
                  var rect = p.getBoundingClientRect();
                  var cl = clampToViewport(p, rect.left, rect.top);
                  p.style.left = cl.left + 'px';
                  p.style.top = cl.top + 'px';
                  p.style.right = 'auto';
                  p.style.bottom = 'auto';
                  saveHistState({ useLeftTop:true, left: cl.left, top: cl.top });
                }
              }catch(__){}
            });
            window.__pwHistResizeHandler = true;
          }
        }catch(__){}

        // Применим начальное состояние свёрнутости
        try{ setCollapsed(getCollapsed()); }catch(__){}

        // Если после перевода установлен флаг открытия — развернём окно и сбросим флаг
        try{
          if (wantOpen()){
            setCollapsed(false);
            clearFlag();
            promoLog('expand_on_flag', null);
          }
        }catch(__){}

        // Если в bootstrap/appConfig указано, что окно должно быть встроено — применим докинг без миганий
        try{
          var boot2 = getBootstrapState();
          var st0 = loadHistState();
          var needDock = (boot2 && boot2.docked === true) || (st0 && st0.docked === true);
          if (needDock){
            // Попробуем найти хост сразу, иначе подождём немного
            var host = getDockHost();
            if (host){
              applyDock(true);
            } else {
              promoLog('dock_wait_host', null);
              var waited = 0;
              var docObs = new MutationObserver(function(muts){
                try{
                  if (getDockHost()){
                    docObs.disconnect();
                    applyDock(true);
                  }
                }catch(__){}
              });
              docObs.observe(document.body || document.documentElement, { childList:true, subtree:true });
              setTimeout(function(){ try{ docObs.disconnect(); if (!isDocked()) promoLog('dock_host_missing', { timeout:true }); }catch(__){} }, 2000);
            }
          }
        }catch(__){}

        promoLog('non_modal_ready', null);
        return true;
      }catch(_){ return false; }
    }

    // Перестройка содержимого iframe: оставить только таблицу истории и применить свои стили
    function sanitizeHistoryIframe(iframe){
      try{
        if (!iframe) return;
        var doc = iframe.contentDocument || iframe.contentWindow && iframe.contentWindow.document;
        if (!doc) return;
        promoLog('iframe_loaded', null);
        var srcTable = null;
        try{ srcTable = doc.querySelector('table.promo_history'); }catch(_){ srcTable = null; }
        if (!srcTable){
          try{ promoLog('table_missing', null); }catch(__){}
          // Показать дружелюбное сообщение
          doc.head.innerHTML = '';
          doc.body.innerHTML = '';
          var style = doc.createElement('style');
          style.textContent = [
            'body{margin:0;font-family:Arial,sans-serif;background:#fff;color:#333}',
            '.hist_wrap{padding:12px}',
            '.hist_empty{padding:24px;text-align:center;color:#666;background:#F6F1E7;border:1px solid #E2D8C9;border-radius:8px;font-size:13px}'
          ].join('\n');
          doc.head.appendChild(style);
          var wrap = doc.createElement('div');
          wrap.className = 'hist_wrap';
          var empty = doc.createElement('div');
          empty.className = 'hist_empty';
          empty.textContent = 'История передач пуста или недоступна.';
          wrap.appendChild(empty);
          doc.body.appendChild(wrap);
          // После отрисовки пустого состояния — скрываем плейсхолдер и показываем iframe
          try{ var cv0 = document.getElementById('promo_history_loading_cover'); if (cv0){ cv0.style.display='none'; promoLog('cover_hide', { reason:'empty' }); } }catch(__){}
          try{ var ifr0 = document.getElementById('promo_history_popup_iframe'); if (ifr0){ ifr0.style.opacity='1'; promoLog('iframe_show', { reason:'empty' }); } }catch(__){}
          return;
        }
        // Считать данные из исходной таблицы до очистки документа
        var rows = [];
        try{
          var trList = srcTable.querySelectorAll('tr');
          for (var i=0;i<trList.length;i++){
            var tr = trList[i];
            // пропускаем строку с заголовками
            if (tr.querySelector('th')) continue;
            var tds = tr.querySelectorAll('td');
            if (!tds || tds.length < 4) continue;
            var nameCell = tds[0];
            var toCell = tds[1];
            var statusCell = tds[2];
            var dateCell = tds[3];
            var nameMain = '';
            try{
              var b = nameCell.querySelector('b');
              nameMain = (b ? b.textContent : nameCell.textContent || '').trim();
            }catch(_){ nameMain = (nameCell.textContent||'').trim(); }
            var toText = (toCell.textContent||'').replace(/\s+\n\s+/g,'\n').trim();
            var statusText = (statusCell.textContent||'').trim();
            var dateText = (dateCell.textContent||'').trim();
            rows.push({ name:nameMain, to:toText, status:statusText, date:dateText });
          }
        }catch(_){ }
        try{ promoLog('table_found', { count: rows.length }); }catch(__){}

        // Helpers для компактного представления
        function parseToCompact(toRaw){
          try{
            var txt = (toRaw||'').replace(/\r/g,'').trim();
            var server = '', person = '';
            // Ищем строки вида "Персонаж: ..." и "Сервер: ..."
            var mPers = txt.match(/(^|\n)\s*Персонаж\s*:\s*([^\n]+)/i);
            if (mPers) person = (mPers[2]||'').trim();
            var mSrv = txt.match(/(^|\n)\s*Сервер\s*:\s*([^\n]+)/i);
            if (mSrv) server = (mSrv[2]||'').trim();
            if (server || person){
              return ((server||'') + (server&&person?' - ':'') + (person||'')) || txt;
            }
            return txt; // fallback — отдать как есть
          }catch(__){ return toRaw||''; }
        }

        function declRu(n, one, few, many){
          try{
            n = Math.abs(n) % 100; var n1 = n % 10;
            if (n > 10 && n < 20) return many;
            if (n1 === 1) return one;
            if (n1 > 1 && n1 < 5) return few;
            return many;
          }catch(__){ return many; }
        }

        function parseDateLocal(s){
          try{
            // Формат ожидаем: YYYY-MM-DD HH:mm:ss
            var m = (s||'').match(/^(\d{4})-(\d{2})-(\d{2})\s+(\d{2}):(\d{2}):(\d{2})$/);
            if (!m) return null;
            var y = parseInt(m[1],10), mo = parseInt(m[2],10)-1, d = parseInt(m[3],10);
            var h = parseInt(m[4],10), mi = parseInt(m[5],10), se = parseInt(m[6],10);
            return new Date(y, mo, d, h, mi, se);
          }catch(__){ return null; }
        }

        function humanizeDate(s){
          var exact = s || '';
          var dt = parseDateLocal(s);
          if (!dt) return { text: exact, exact: exact };
          try{
            var now = new Date();
            var diffMs = now - dt; if (diffMs < 0) diffMs = 0;
            var sec = Math.floor(diffMs/1000);
            var min = Math.floor(sec/60);
            var hr = Math.floor(min/60);
            var day = Math.floor(hr/24);
            if (sec < 30) return { text: 'только что', exact: exact };
            if (min < 5) return { text: 'несколько минут назад', exact: exact };
            if (min < 60) return { text: String(min) + ' минут назад', exact: exact };
            if (hr < 2) return { text: 'час назад', exact: exact };
            if (hr < 5) return { text: String(hr) + ' ' + declRu(hr, 'час', 'часа', 'часов') + ' назад', exact: exact };
            if (hr < 24) return { text: String(hr) + ' ' + declRu(hr, 'час', 'часа', 'часов') + ' назад', exact: exact };
            if (day < 2) return { text: 'вчера', exact: exact };
            if (day < 3) return { text: 'позавчера', exact: exact };
            return { text: String(day) + ' ' + declRu(day, 'день', 'дня', 'дней') + ' назад', exact: exact };
          }catch(__){ return { text: exact, exact: exact }; }
        }

        // Перестраиваем документ: список (в доке) или таблица (во float)
        var isDocked = false;
        try{
          var parentPopup = document.getElementById('promo_history_popup');
          isDocked = !!(parentPopup && parentPopup.dataset && parentPopup.dataset.docked === '1');
        }catch(__){ isDocked = false; }

        doc.head.innerHTML = '';
        doc.body.innerHTML = '';
        var style2 = doc.createElement('style');
        if (isDocked){
          style2.textContent = [
            'html,body{height:100%}',
            'body{margin:0;font-family:Arial,sans-serif;background:#fff;color:#333;font-size:12px;line-height:1.35}',
            '.hist_list_root{height:100%;display:flex;flex-direction:column}',
            '.hist_list_wrap{flex:1 1 auto;overflow:auto;padding:6px 8px 8px 8px}',
            '.hist_item{border:1px solid #E2D8C9;background:#FAF7F1;border-radius:8px;padding:6px 8px;margin:0 0 6px 0}',
            '.hist_item:nth-child(even){background:#FDFBF7}',
            '.hist_item_header{display:flex;align-items:center;justify-content:space-between;gap:8px;margin-bottom:2px}',
            '.hist_item .li_name{font-weight:700;color:#333;flex:1 1 auto;min-width:0;font-size:13px}',
            '.hist_item .li_status{margin-left:8px;white-space:nowrap;flex:0 0 auto}',
            '.status_badge{display:inline-block;padding:2px 8px;border-radius:12px;font-size:11px;font-weight:700}',
            '.status_ok{background:#d7f5d7;color:#146c2e;border:1px solid #b6e4b6}',
            '.status_proc{background:#fff1c2;color:#8a6c0a;border:1px solid #f0d98a}',
            '.status_other{background:#e7ecff;color:#2c4a8d;border:1px solid #c6d1ff}',
            '.hist_item .li_date{color:#666;font-size:11px}'
          ].join('\n');
          doc.head.appendChild(style2);
          var rootL = doc.createElement('div'); rootL.className = 'hist_list_root';
          var wrapL = doc.createElement('div'); wrapL.className = 'hist_list_wrap';
          var built = 0;
          for (var r1=0;r1<rows.length;r1++){
            var row1 = rows[r1];
            var item = doc.createElement('div'); item.className = 'hist_item';
            var headerRow = doc.createElement('div'); headerRow.className = 'hist_item_header';
            var nm1 = doc.createElement('div'); nm1.className = 'li_name'; nm1.textContent = row1.name || '';
            var stc = doc.createElement('div'); stc.className = 'li_status';
            var s1 = (row1.status||'').toLowerCase(); var cls1='status_other';
            if (s1.indexOf('передан') !== -1) cls1 = 'status_ok'; else if (s1.indexOf('обработ') !== -1) cls1 = 'status_proc';
            var badge = doc.createElement('span'); badge.className = 'status_badge ' + cls1; badge.textContent = row1.status || '';
            stc.appendChild(badge);
            var d1 = humanizeDate(row1.date || '');
            var dt1 = doc.createElement('div'); dt1.className = 'li_date'; dt1.textContent = d1.text; dt1.title = d1.exact;
            headerRow.appendChild(nm1);
            headerRow.appendChild(stc);
            item.appendChild(headerRow);
            item.appendChild(dt1);
            wrapL.appendChild(item); built++;
          }
          rootL.appendChild(wrapL); doc.body.appendChild(rootL);
          try{ promoLog('list_built', { rows: built }); }catch(__){}
        } else {
          style2.textContent = [
            'html,body{height:100%}',
            'body{margin:0;font-family:Arial,sans-serif;background:#fff;color:#333}',
            '.hist_root{height:100%;display:flex;flex-direction:column}',
            '.hist_table_wrap{flex:1 1 auto;overflow:auto;padding:0 8px 8px 8px}',
            'table.hist{width:100%;border-collapse:separate;border-spacing:0;background:#fff}',
            'table.hist thead th{position:sticky;top:0;background:#EDE4D6;color:#2c4a8d;text-align:left;font-size:12px;padding:8px;border-bottom:1px solid #E2D8C9;z-index:2}',
            'table.hist tbody td{padding:8px;border-bottom:1px solid #F0E8DC;vertical-align:top;font-size:13px}',
            'table.hist tbody tr:nth-child(odd){background:#FAF7F1}',
            '.name_main{font-weight:700}',
            '.status_badge{display:inline-block;padding:2px 8px;border-radius:12px;font-size:12px;font-weight:700}',
            '.status_ok{background:#d7f5d7;color:#146c2e;border:1px solid #b6e4b6}',
            '.status_proc{background:#fff1c2;color:#8a6c0a;border:1px solid #f0d98a}',
            '.status_other{background:#e7ecff;color:#2c4a8d;border:1px solid #c6d1ff}',
            '.to_compact{color:#444;font-size:12px}',
            '.date_rel{color:#666;font-size:12px}'
          ].join('\n');
          doc.head.appendChild(style2);
          var root = doc.createElement('div'); root.className = 'hist_root';
          var wrap = doc.createElement('div'); wrap.className = 'hist_table_wrap';
          var tbl = doc.createElement('table'); tbl.className = 'hist';
          var thead = doc.createElement('thead');
          var thr = doc.createElement('tr');
          ['Название','Кому передан','Статус','Дата'].forEach(function(h){ var th = doc.createElement('th'); th.textContent = h; thr.appendChild(th); });
          thead.appendChild(thr);
          var tbody = doc.createElement('tbody');
          var compacted = 0;
          for (var r=0;r<rows.length;r++){
            var row = rows[r];
            var tr = doc.createElement('tr');
            var td1 = doc.createElement('td');
            var nm = doc.createElement('div'); nm.className = 'name_main'; nm.textContent = row.name || '';
            td1.appendChild(nm);
            var td2 = doc.createElement('td');
            var compactTo = parseToCompact(row.to || '');
            var toComp = doc.createElement('div'); toComp.className = 'to_compact'; toComp.textContent = compactTo;
            td2.appendChild(toComp);
            var td3 = doc.createElement('td'); var st = doc.createElement('span');
            var s = (row.status||'').toLowerCase();
            var cls = 'status_other';
            if (s.indexOf('передан') !== -1) cls = 'status_ok';
            else if (s.indexOf('обработ') !== -1) cls = 'status_proc';
            st.className = 'status_badge ' + cls; st.textContent = row.status || '';
            td3.appendChild(st);
            var td4 = doc.createElement('td');
            var rel = humanizeDate(row.date || '');
            var dspan = doc.createElement('span'); dspan.className = 'date_rel'; dspan.textContent = rel.text; dspan.title = rel.exact;
            td4.appendChild(dspan);
            tr.appendChild(td1); tr.appendChild(td2); tr.appendChild(td3); tr.appendChild(td4);
            tbody.appendChild(tr);
            compacted++;
          }
          tbl.appendChild(thead); tbl.appendChild(tbody); wrap.appendChild(tbl);
          root.appendChild(wrap); doc.body.appendChild(root);
          try{ promoLog('table_rebuilt', { rows: rows.length }); }catch(__){}
          try{ promoLog('table_compacted', { rows: compacted }); }catch(__){}
        }
        // Успешная саниция — скрыть плейсхолдер и плавно показать iframe
        try{ var cv = document.getElementById('promo_history_loading_cover'); if (cv){ cv.style.display='none'; promoLog('cover_hide', { reason:'sanitized' }); } }catch(__){}
        try{ var ifr = document.getElementById('promo_history_popup_iframe'); if (ifr){ ifr.style.opacity='1'; promoLog('iframe_show', { reason:'sanitized' }); } }catch(__){}
      }catch(e){
        try{ promoLog('sanitize_err', (e && (e.message||e)) || 'unknown'); }catch(__){}
        // Даже при ошибке — снять плейсхолдер и показать то, что есть, чтобы не «зависать»
        try{ var cv2 = document.getElementById('promo_history_loading_cover'); if (cv2){ cv2.style.display='none'; promoLog('cover_hide', { reason:'error' }); } }catch(__){}
        try{ var ifr2 = document.getElementById('promo_history_popup_iframe'); if (ifr2){ ifr2.style.opacity='1'; promoLog('iframe_show', { reason:'error' }); } }catch(__){}
      }
    }

    function openHistory(options){
      try{
        var ok = ensureHost();
        if (!ok) return;
        var url = findHistoryUrl();
        try{ promoLog('open_url', { url: url }); }catch(__){}
        var iframe = document.getElementById('promo_history_popup_iframe');
        var body = document.getElementById('promo_history_popup_body');
        var collapseBtn = document.getElementById('promo_history_collapse_btn');
        // Применим состояние свёрнутости, если нужно
        try{
          var forceExpand = options && options.forceExpand;
          var collapsed = sessionStorage.getItem('promo_history_collapsed') === '1';
          if (forceExpand && collapsed){
            sessionStorage.setItem('promo_history_collapsed','0');
            if (body) body.style.display = 'block';
            if (collapseBtn) collapseBtn.textContent = '−';
            promoLog('expand_on_flag', null);
          } else if (collapsed){
            if (body) body.style.display = 'none';
            if (collapseBtn) collapseBtn.textContent = '+';
          }
        }catch(__){}
        if (iframe){
          // Перед загрузкой — спрятать iframe и показать общий плейсхолдер
          try{ iframe.style.opacity = '0'; promoLog('iframe_hide', { reason:'open' }); }catch(__){}
          try{ var cv = document.getElementById('promo_history_loading_cover'); if (cv){ cv.style.display='flex'; promoLog('cover_show', { reason:'open' }); } }catch(__){}
          // Сторожевой таймер против бесконечной загрузки
          try{
            window.__pwHistGuard && clearTimeout(window.__pwHistGuard);
            window.__pwHistGuard = setTimeout(function(){
              try{ var rb = document.getElementById('promo_history_refresh_btn'); if (rb){ rb.disabled=false; rb.textContent='Обновить'; } }catch(__){}
              try{ var cv2 = document.getElementById('promo_history_loading_cover'); if (cv2){ cv2.style.display='none'; promoLog('cover_hide', { reason:'timeout_open' }); } }catch(__){}
              try{ var ifr2 = document.getElementById('promo_history_popup_iframe'); if (ifr2){ ifr2.style.opacity='1'; promoLog('iframe_show', { reason:'timeout_open' }); } }catch(__){}
              try{ promoLog('open_timeout', null); }catch(__){}
            }, 15000);
          }catch(__){}
          iframe.onload = function(){
            try{
              sanitizeHistoryIframe(iframe);
              promoLog('refresh_done', null);
            }catch(__){}
            finally{
              try{ window.__pwHistGuard && clearTimeout(window.__pwHistGuard); }catch(__){}
              try{ var rb = document.getElementById('promo_history_refresh_btn'); if (rb){ rb.disabled = false; rb.textContent = 'Обновить'; } }catch(__){}
            }
          };
          iframe.src = url;
        }
        promoLog('open_done', null);
      }catch(e){ try{ promoLog('open_err', (e && (e.message||e)) || 'unknown'); }catch(__){} }
    }

    function run(){
      try{
        // Попап должен быть доступен всегда — создаём/показываем его сразу
        var ok = ensureHost();
        if (!ok) return;
        // Если был флаг после перевода — форсируем разворачивание и открываем
        if (wantOpen()){
          clearFlag();
          promoLog('flag_present', null);
          openHistory({ forceExpand: true });
        } else {
          promoLog('flag_absent', null);
          openHistory();
        }
      }catch(_){ }
    }

    // Слушатель пользовательского события для открытия истории без перезагрузки
    try{
      if (!window.__pwHistoryEventAttached){
        document.addEventListener('pw:openTransferHistory', function(){
          try{
            promoLog('event_fire', null);
            if (window.PromoTransferHistoryPopup && window.PromoTransferHistoryPopup.openNow){
              window.PromoTransferHistoryPopup.openNow();
            } else {
              openHistory();
            }
          }catch(__){}
        });
        window.__pwHistoryEventAttached = true;
        promoLog('event_listen', null);
      }
    }catch(__){}

    if (!window.Promo || !Promo.register){
      if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', run, { once:true });
      else run();
      return;
    }
    Promo.register('TransferHistoryPopup', { run: function(){
      if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', run, { once:true });
      else run();
    }});
  }catch(_){ }
})();
