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

    // ===== Helper: проверка видимости элемента (в пределах layout) =====
    function isElemVisible(el){
      try{
        if (!el) return false;
        if (el.offsetParent !== null) return true;
        var cs = window.getComputedStyle ? window.getComputedStyle(el) : null;
        if (!cs) return !!(el.getClientRects && el.getClientRects().length);
        if (cs.display === 'none' || cs.visibility === 'hidden' || cs.opacity === '0') return false;
        return !!(el.getClientRects && el.getClientRects().length);
      }catch(_){ return false; }
    }

    // ===== Helper: снимок режима отображения и признаков списка =====
    function snapshotViewMode(currentList){
      try{
        var composite = document.getElementById('promo_items_composite');
        var compositeVisible = !!(composite && isElemVisible(composite));

        // Попытка прочитать режим по кнопкам в попапе
        var btnMode = null;
        var modeSource = 'heuristic';
        try{
          var gridBtn = document.getElementById('viewModeGridBtn');
          var listBtn = document.getElementById('viewModeListBtn');
          function isActiveBtn(b){
            if (!b) return false;
            if (b.getAttribute('aria-pressed') === 'true') return true;
            if (b.dataset && (b.dataset.active === '1' || b.dataset.active === 'true')) return true;
            // Эвристика по стилю: более тёмный фон у активной
            return false;
          }
          if (isActiveBtn(gridBtn)) btnMode = 'grid';
          else if (isActiveBtn(listBtn)) btnMode = 'list';
        }catch(__){}

        var itemsDisplay = (currentList && currentList.style && currentList.style.display) ? currentList.style.display : '';
        var hasGridFlag = !!(currentList && currentList.hasAttribute && currentList.hasAttribute('data-grid-listener'));

        // Приоритет источников определения режима:
        // 1) Наличие и видимость композита (источник истины UI)
        // 2) Кнопки режима в попапе (если есть явная активность)
        // 3) Fallback: если композит скрыт — list, иначе grid
        var mode = 'list';
        if (composite) {
          mode = compositeVisible ? 'grid' : 'list';
          modeSource = 'composite';
        }
        if (!composite || (!compositeVisible && !isElemVisible(composite))){
          if (btnMode){ mode = btnMode; modeSource = 'buttons'; }
        }
        // Старую эвристику по itemsDisplay используем только в самом конце как последний шанс
        if (!btnMode && modeSource === 'heuristic'){
          if (itemsDisplay === 'none') mode = 'grid'; else mode = 'list';
        }

        try{ promoLog('view_mode_before', { mode: mode, source: modeSource, compositeVisible: compositeVisible, btnMode: btnMode, itemsDisplay: itemsDisplay||'(empty)', hasGridFlag: hasGridFlag }); }catch(__){}
        return { mode: mode, compositeVisible: compositeVisible, btnMode: btnMode, itemsDisplay: itemsDisplay, hasGridFlag: hasGridFlag };
      }catch(_){ return { mode:'list', compositeVisible:false, btnMode:null, itemsDisplay:'', hasGridFlag:false }; }
    }

    // ===== Helper: принудительное применение режима с двойным проходом =====
    function applyViewModeSafely(mode){
      try{
        var applied = false;
        if (window.__promoGrid_applyMode && (mode === 'grid' || mode === 'list')){
          try{ window.__promoGrid_applyMode(mode); applied = true; }catch(__){}
          try{ promoLog('view_mode_applied', { mode: mode, pass: 1 }); }catch(__){}
          try{ setTimeout(function(){ try{ window.__promoGrid_applyMode(mode); promoLog('view_mode_applied', { mode: mode, pass: 2 }); }catch(__){} }, 0); }catch(__){}
        }
        // Финальная фиксация видимости в соответствии с режимом
        try{
          // Выполним в двух тактах, чтобы победить поздние инициализации сторонних скриптов
          if (window.requestAnimationFrame){
            requestAnimationFrame(function(){ try{ ensureVisibilityByMode(mode); }catch(__){} });
          }
          setTimeout(function(){ try{ ensureVisibilityByMode(mode); }catch(__){} }, 30);
        }catch(__){}
        return applied;
      }catch(_){ return false; }
    }

    // ===== Helper: если композит видим — скрыть .items_container (быстрый хотфикс) =====
    function ensureListHiddenIfComposite(){
      try{
        var composite = document.getElementById('promo_items_composite');
        if (composite && isElemVisible(composite)){
          var list = document.querySelector('.items_container');
          if (list){ list.style.display = 'none'; }
        }
      }catch(__){}
    }

    // ===== Helper: выставление видимости списка/композита по режиму =====
    function ensureVisibilityByMode(mode){
      try{
        var composite = document.getElementById('promo_items_composite');
        var lists = [];
        try{ lists = document.querySelectorAll('.items_container'); }catch(__){ lists = []; }

        if (mode === 'grid'){
          // показать композит, скрыть все списки
          try{ if (composite) composite.style.display = 'block'; }catch(__){}
          try{
            for (var i=0;i<lists.length;i++){ try{ lists[i].style.display = 'none'; }catch(__){} }
          }catch(__){}
          promoLog('view_mode_final', { mode: 'grid' });
          promoLog('visibility_enforced', { mode: 'grid', listCount: (lists && lists.length)||0, compositeShown: !!composite });
        } else if (mode === 'list'){
          // показать все списки, скрыть композит
          try{
            for (var j=0;j<lists.length;j++){
              var el = lists[j];
              try{ if (el && el.style && typeof el.style.removeProperty === 'function') el.style.removeProperty('display'); else if (el) el.style.display = ''; }catch(__){}
              // если после снятия inline всё ещё скрыт CSS'ом — принудительно показать
              try{
                var cs = window.getComputedStyle ? window.getComputedStyle(el) : null;
                if (cs && cs.display === 'none'){ el.style.display = 'block'; }
              }catch(__){}
            }
          }catch(__){}
          try{ if (composite) composite.style.display = 'none'; }catch(__){}
          promoLog('view_mode_final', { mode: 'list' });
          promoLog('visibility_enforced', { mode: 'list', listCount: (lists && lists.length)||0, compositeHidden: !!composite });
        } else {
          // если режим неизвестен, хотя бы не прятать список по факту видимости композита
          ensureListHiddenIfComposite();
        }
      }catch(__){}
    }

    // ===== Helper: снапшот/восстановление селектов shard/char =====
    function snapshotCharSelects(){
      try{
        function take(sel){
          if (!sel) return null;
          var opts = [];
          try{
            for (var i=0; i<sel.options.length; i++){
              var o = sel.options[i];
              opts.push({ value: o.value, text: o.text, selected: o.selected });
            }
          }catch(__){}
          return { selectedValue: (sel.value||''), options: opts };
        }
        var s = document.querySelector('.js-shard');
        var c = document.querySelector('.js-char');
        var snap = { shard: take(s), chr: take(c) };
        try{ promoLog('chars_snapshot', { shardLen: (snap.shard && snap.shard.options ? snap.shard.options.length : 0), charLen: (snap.chr && snap.chr.options ? snap.chr.options.length : 0), shardSelected: snap.shard && snap.shard.selectedValue, charSelected: snap.chr && snap.chr.selectedValue }); }catch(__){}
        return snap;
      }catch(_){ return { shard:null, chr:null }; }
    }

    function restoreCharSelects(snapshot){
      try{
        // Вспомогательные билдеры/поисковики
        function getOrCreateCharSelectorContainer(){
          try{
            var cont = document.querySelector('.char_selector');
            if (cont) return cont;
            // Фича-флаг: можно отключить автосоздание при необходимости
            if (window.__pwKeepViewStateOnRefresh === false) return null;
            cont = document.createElement('div');
            cont.className = 'char_selector';
            // Точка вставки: перед кнопкой «Передать» в основной области страницы
            var goBtn = document.querySelector('.go_items .js-transfer-go, .js-transfer-go');
            if (goBtn && goBtn.parentNode){
              try{ goBtn.parentNode.parentNode.insertBefore(cont, goBtn.parentNode); }
              catch(__){ try{ goBtn.parentNode.insertBefore(cont, goBtn); }catch(___){} }
            }
            if (!cont.parentNode){
              // Фоллбек: сразу после .items_container
              var list = document.querySelector('.items_container');
              if (list && list.parentNode){
                try{ list.parentNode.insertBefore(cont, list.nextSibling); }catch(__){}
              }
            }
            try{ promoLog('chars_created', { inserted: !!cont.parentNode }); }catch(__){}
            return cont;
          }catch(__){ return null; }
        }

        function getOrCreateSelects(){
          var shardSel = document.querySelector('.js-shard');
          var charSel = document.querySelector('.js-char');
          if (shardSel && charSel) return { shardSel: shardSel, charSel: charSel, created: false };
          // создадим контейнер и селекты, если их нет
          var cont = getOrCreateCharSelectorContainer();
          if (!cont) return { shardSel: shardSel, charSel: charSel, created: false };
          if (!shardSel){
            shardSel = document.createElement('select');
            shardSel.className = 'js-shard';
            try{ shardSel.style.width = '150px'; }catch(__){}
            cont.appendChild(shardSel);
          }
          if (!charSel){
            charSel = document.createElement('select');
            charSel.className = 'js-char';
            charSel.setAttribute('name', 'acc_info');
            try{ charSel.style.width = '250px'; }catch(__){}
            cont.appendChild(charSel);
          }
          try{ promoLog('chars_selects_ensured', { created: true }); }catch(__){}
          return { shardSel: shardSel, charSel: charSel, created: true };
        }

        function populateShardSelect(shardSel, data, selectedShard){
          if (!shardSel || !data) return null;
          try{
            var shardIds = Object.keys(data);
            shardSel.innerHTML = '';
            for (var si=0; si<shardIds.length; si++){
              var sid = shardIds[si];
              var opt = document.createElement('option');
              opt.value = sid;
              opt.text = (data[sid] && data[sid].name) ? data[sid].name : sid;
              shardSel.appendChild(opt);
            }
            if (selectedShard){ try{ shardSel.value = selectedShard; }catch(__){} }
            return shardSel.value || (shardIds.length ? shardIds[0] : null);
          }catch(__){ return null; }
        }

        function populateCharSelect(charSel, data, shardId, selectedAccInfo, snapshot){
          if (!charSel || !data || !shardId) return false;
          try{
            charSel.innerHTML = '';
            var accounts = (data[shardId] && data[shardId].accounts) ? data[shardId].accounts : {};
            var accIds = Object.keys(accounts);
            var firstValue = null;
            for (var ai=0; ai<accIds.length; ai++){
              var accId = accIds[ai];
              var og = document.createElement('optgroup');
              og.label = accounts[accId].name || ('acc_'+accId);
              var chars = (accounts[accId] && accounts[accId].chars) ? accounts[accId].chars : [];
              for (var ci=0; ci<chars.length; ci++){
                var ch = chars[ci];
                var optc = document.createElement('option');
                optc.value = accId + '_' + shardId + '_' + ch.id;
                var nm = (ch.name||'');
                var occ = ch.occupation ? (', '+ch.occupation) : '';
                var lvl = ch.level ? (' ('+ch.level+')') : '';
                optc.text = nm + (occ||'') + (lvl||'');
                if (!firstValue) firstValue = optc.value;
                og.appendChild(optc);
              }
              if (chars.length){ charSel.appendChild(og); }
            }
            var targetVal = selectedAccInfo || (snapshot && snapshot.chr && snapshot.chr.selectedValue) || firstValue;
            if (targetVal){ try{ charSel.value = targetVal; }catch(__){} }
            return true;
          }catch(__){ return false; }
        }

        function bindShardChange(shardSel, charSel, data){
          if (!shardSel || !charSel || !data) return;
          try{
            if (shardSel.__pwBound) return;
            shardSel.__pwBound = true;
            shardSel.addEventListener('change', function(){
              try{ populateCharSelect(charSel, data, shardSel.value, null, null); promoLog('chars_shard_changed', { shard: shardSel.value }); }catch(__){}
            });
            promoLog('chars_change_bound', null);
          }catch(__){}
        }

        var ensured = getOrCreateSelects();
        var shardSel = ensured.shardSel;
        var charSel = ensured.charSel;

        function restoreFromSnap(sel, snap){
          if (!sel || !snap) return false;
          if (sel.options && sel.options.length > 0){
            // Попробуем просто восстановить выбранный
            try{
              if (snap.selectedValue){ sel.value = snap.selectedValue; }
              return true;
            }catch(__){}
            return true;
          }
          // Восстановим полностью
          try{
            sel.innerHTML = '';
            for (var i=0; i<(snap.options||[]).length; i++){
              var o = document.createElement('option');
              o.value = snap.options[i].value;
              o.text = snap.options[i].text;
              if (snap.options[i].selected) o.selected = true;
              sel.appendChild(o);
            }
            if (snap.selectedValue){ try{ sel.value = snap.selectedValue; }catch(__){} }
            return true;
          }catch(__){ return false; }
        }

        var ok = false;
        ok = restoreFromSnap(shardSel, snapshot.shard) || ok;
        ok = restoreFromSnap(charSel, snapshot.chr) || ok;

        // Если всё ещё пусто и есть window.shards — попробуем наполнить из него
        function countOptions(sel){ return sel && sel.options ? sel.options.length : 0; }
        var needBuild = (!shardSel || countOptions(shardSel) === 0) || (!charSel || countOptions(charSel) === 0);
        var shardsData = (window.shards || (window.window && window.window.shards)) || null;
        if (needBuild && shardsData){
          try{
            var chosenShard = shardSel && shardSel.value ? shardSel.value : (snapshot && snapshot.shard && snapshot.shard.selectedValue);
            chosenShard = populateShardSelect(shardSel, shardsData, chosenShard);
            populateCharSelect(charSel, shardsData, chosenShard, (snapshot && snapshot.chr && snapshot.chr.selectedValue) || null, snapshot);
            bindShardChange(shardSel, charSel, shardsData);
            promoLog('chars_populated', { shard: chosenShard });
          }catch(__){}
        } else if (needBuild && !shardsData){
          try{ promoLog('chars_build_skipped_no_data', null); }catch(__){}
        }

        // Финальный лог
        try{
          promoLog('chars_after_autorun', {
            shardLen: countOptions(shardSel),
            charLen: countOptions(charSel)
          });
        }catch(__){}

        if (countOptions(shardSel) || countOptions(charSel)){
          try{ promoLog('chars_restored', null); }catch(__){}
          return true;
        } else {
          try{ promoLog('chars_restore_skipped', null); }catch(__){}
          return false;
        }
      }catch(_){ try{ promoLog('chars_restore_skipped', { reason:'exception' }); }catch(__){} return false; }
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

    // Корректная загрузка HTML с учётом кодировки (UTF-8 / windows-1251 / KOI8-R)
    function fetchHtmlRespectingEncoding(url){
      try{
        return fetch(url, { credentials: 'same-origin' })
          .then(function(resp){
            try{
              var contentType = resp && resp.headers ? resp.headers.get('content-type') : null;
              var charset = null;
              try{
                if (contentType){
                  var m = contentType.match(/charset\s*=\s*([^\s;]+)/i);
                  if (m) charset = String(m[1]||'').replace(/["']/g,'').trim();
                }
              }catch(__){}

              return resp.arrayBuffer().then(function(buf){
                function decodeWith(cs){ try{ return new TextDecoder(cs).decode(buf); }catch(_){ return null; } }
                var chosen = null;
                var decoded = null;

                if (charset){
                  decoded = decodeWith(charset);
                  chosen = charset.toLowerCase();
                }
                if (!decoded){
                  decoded = decodeWith('utf-8');
                  chosen = 'utf-8';
                  // Попробуем подсмотреть <meta charset="..."> в заголовке документа
                  try{
                    var headPart = decoded ? decoded.slice(0, 4096) : '';
                    var m1 = headPart && headPart.match(/<meta[^>]+charset\s*=\s*["']?\s*([a-z0-9\-\_]+)\s*["']?/i);
                    var sniff = m1 ? (m1[1]||'').toLowerCase() : null;
                    if (!sniff){
                      var m2 = headPart && headPart.match(/<meta[^>]+content\s*=\s*"[^"]*charset\s*=\s*([a-z0-9\-\_]+)[^"]*"/i);
                      sniff = m2 ? (m2[1]||'').toLowerCase() : null;
                    }
                    if (sniff && sniff !== 'utf-8'){
                      var tryDec = decodeWith(sniff);
                      if (tryDec){ decoded = tryDec; chosen = sniff; }
                    } else {
                      // Эвристика: сравнить количество замещающих символов для популярных локалей
                      var dec1251 = decodeWith('windows-1251');
                      var decKoi = decodeWith('koi8-r');
                      function replCount(s){ if (!s) return 1e9; var m = s.match(/\uFFFD/g); return m ? m.length : 0; }
                      var rUtf = replCount(decoded);
                      var r1251 = replCount(dec1251);
                      var rKoi = replCount(decKoi);
                      var best = decoded, bestName = 'utf-8', bestScore = rUtf;
                      if (r1251 < bestScore){ best = dec1251; bestName = 'windows-1251'; bestScore = r1251; }
                      if (rKoi < bestScore){ best = decKoi; bestName = 'koi8-r'; bestScore = rKoi; }
                      decoded = best; chosen = bestName;
                    }
                  }catch(__){}
                }

                try{ promoLog('encoding_used', { charset: chosen||'(unknown)' }); }catch(__){}
                return decoded || '';
              });
            }catch(__){ return resp.text(); }
          });
      }catch(_){ return Promise.resolve(''); }
    }

    // Санитайз контейнера плиток: удаляет любые вложенные <form> и при необходимости
    // разворачивает сам контейнер, если он неожиданно является <form class="items_container">.
    function stripFormsFromContainer(container, where){
      try{
        if (!container) return container;
        // Если сам контейнер — FORM, заменим его на DIV с теми же классами/id и перенесём детей
        if (container.tagName && String(container.tagName).toLowerCase() === 'form'){
          var replacement = document.createElement('div');
          try{ replacement.className = container.className || ''; }catch(__){}
          try{ if (container.id) replacement.id = container.id; }catch(__){}
          // Перенесём data-* атрибуты (прочие форм‑специфичные не копируем)
          try{
            var attrs = container.attributes;
            for (var ai=0; ai<attrs.length; ai++){
              var a = attrs[ai]; var nm = String(a.name||'').toLowerCase();
              if (nm === 'class' || nm === 'id') continue;
              if (nm.indexOf('data-') === 0) replacement.setAttribute(a.name, a.value);
            }
          }catch(__){}
          while (container.firstChild){ replacement.appendChild(container.firstChild); }
          container = replacement;
        }

        // Удалим все вложенные формы (разворачивая их содержимое)
        var forms = container.querySelectorAll ? container.querySelectorAll('form') : [];
        for (var i=0; i<forms.length; i++){
          var f = forms[i];
          var p = f && f.parentNode; if (!p) continue;
          while (f.firstChild){ p.insertBefore(f.firstChild, f); }
          p.removeChild(f);
        }
        try{ promoLog('sanitize_forms', { where: where||'(unknown)', removed: (forms && forms.length) || 0 }); }catch(__){}
        return container;
      }catch(_){ return container; }
    }

    // Дополнительный санитайз вокруг #promo_items_composite:
    // если непосредственно ПЕРЕД/ПОСЛЕ него или он ОБЁРНУТ в <form>,
    // и эта форма относится к промо-товарам (содержит .items_container или cart_items),
    // аккуратно «разворачиваем» форму (переносим детей и удаляем сам тег form).
    function sanitizeFormsNearComposite(){
      try{
        var comp = document.getElementById('promo_items_composite');
        if (!comp) return;
        var removed = 0;

        function isPromoForm(f){
          try{
            if (!f) return false;
            if (f.querySelector && (f.querySelector('.items_container')
              || f.querySelector("input[name='cart_items'], input[name='cart_items[]']"))) return true;
            return false;
          }catch(_){ return false; }
        }

        function unwrapForm(f){
          try{
            var p = f && f.parentNode; if (!p) return false;
            while (f.firstChild){ p.insertBefore(f.firstChild, f); }
            p.removeChild(f);
            return true;
          }catch(_){ return false; }
        }

        // Случай 1: композит сам находится внутри <form>
        var par = comp.parentElement;
        if (par && par.tagName && par.tagName.toLowerCase() === 'form' && isPromoForm(par)){
          if (unwrapForm(par)) removed++;
        }

        // Случай 2: перед/после композита стоит <form>
        var prev = comp.previousElementSibling;
        while (prev && prev.tagName && prev.tagName.toLowerCase() === 'form' && isPromoForm(prev)){
          var toUnwrap = prev; prev = prev.previousElementSibling;
          if (unwrapForm(toUnwrap)) removed++;
        }
        var next = comp.nextElementSibling;
        while (next && next.tagName && next.tagName.toLowerCase() === 'form' && isPromoForm(next)){
          var toUnwrap2 = next; next = next.nextElementSibling;
          if (unwrapForm(toUnwrap2)) removed++;
        }

        try{ promoLog('sanitize_forms_near_composite', { removed: removed }); }catch(__){}
      }catch(__){}
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

    // Частичное обновление плиток ТОЛЬКО внутри попапа «Управление» без вставки формы
    function refreshItemsInPopup(btn){
      try{
        var popup = document.getElementById('promo_popup');
        if (!popup){ promoLog('refresh_items_skip_no_popup', null); return; }

        // Ищем текущий контейнер плиток в попапе
        var currentList = popup.querySelector('.items_container');
        if (!currentList){
          try{
            var anyItem = popup.querySelector("input[name='cart_items[]'], input[name='cart_items']");
            if (anyItem){ currentList = anyItem.closest ? anyItem.closest('.items_container') : null; }
          }catch(__){}
        }
        if (!currentList){ promoLog('refresh_items_skip_no_container', null); return; }

        promoLog('refresh_items_start', { hasContainer: true });
        fetchHtmlRespectingEncoding(location.href)
          .then(function(html){
            try{
              var dp = new DOMParser();
              var doc = dp.parseFromString(html, 'text/html');
              if (!doc){ throw new Error('no document'); }

              // В свежем документе найдём такой же контейнер списка/плиток
              var newList = doc.querySelector('.items_container');
              if (!newList){
                var anyNew = doc.querySelector("input[name='cart_items[]'], input[name='cart_items']");
                if (anyNew){ newList = anyNew.closest ? anyNew.closest('.items_container') : null; }
              }
              if (!newList){ throw new Error('new items container not found'); }

              // Заменяем контейнер плиток в попапе (никаких форм не вставляем)
              var cloned = newList.cloneNode(true);
              // Санитайз: удалим возможные <form> оболочки/вложения
              cloned = stripFormsFromContainer(cloned, 'popup');
              currentList.parentNode.replaceChild(cloned, currentList);

              // Переинициализируем модули (обработчики, очистка onclick и т.д.)
              try{ if (window.Promo && typeof Promo.autoRun === 'function') Promo.autoRun(); }catch(__){}

              // Поддержим актуальную видимость согласно текущему режиму
              try{
                var snap = snapshotViewMode(document.querySelector('.items_container'));
                ensureVisibilityByMode(snap && snap.mode);
                if (window.requestAnimationFrame){ requestAnimationFrame(function(){ try{ ensureVisibilityByMode(snap && snap.mode); }catch(__){} }); }
                setTimeout(function(){ try{ ensureVisibilityByMode(snap && snap.mode); }catch(__){} }, 30);
              }catch(__){}

              promoLog('refresh_items_ok', { replaced: true });
            }catch(ex){
              promoLog('refresh_items_fail', { message: (ex && (ex.message||ex)) || 'unknown' });
            }
          })
          .catch(function(err){ promoLog('refresh_items_err', { message: (err && (err.message||err)) || 'unknown' }); });
      }catch(__){}
    }

    // Обновить список предметов на основной странице (вне попапа) без перезагрузки
    function refreshItemsOnMainPage(){
      try{
        // Пытаемся найти контейнер списка на текущей странице
        var currentMainList = null;
        // Приоритет: контейнер внутри композиции #promo_items_composite, если он есть
        try{
          var composite = document.getElementById('promo_items_composite');
          if (composite){ currentMainList = composite.querySelector('.items_container'); }
        }catch(__){}
        if (!currentMainList){ currentMainList = document.querySelector('.items_container'); }
        if (!currentMainList){
          // Попробуем найти по любому чекбоксу и подняться до контейнера
          try{
            var anyItem = document.querySelector("input[name='cart_items[]'], input[name='cart_items']");
            if (anyItem){ currentMainList = anyItem.closest ? anyItem.closest('.items_container') : null; }
          }catch(__){}
        }
        if (!currentMainList){ promoLog('refresh_main_skip_no_container', null); return; }

        // Снимем снимок режима и селектов до замены
        var viewSnap = snapshotViewMode(currentMainList);
        var selectsSnap = snapshotCharSelects();

        promoLog('refresh_main_start', null);
        fetchHtmlRespectingEncoding(location.href)
          .then(function(html){
            try{
              var dp = new DOMParser();
              var doc = dp.parseFromString(html, 'text/html');
              if (!doc){ throw new Error('no document'); }

              // В свежем документе найдём такой же контейнер списка
              var newMainList = null;
              try{
                var compositeNew = doc.getElementById('promo_items_composite');
                if (compositeNew){ newMainList = compositeNew.querySelector('.items_container'); }
              }catch(__){}
              if (!newMainList){ newMainList = doc.querySelector('.items_container'); }
              if (!newMainList){
                // запасной путь — по любому чекбоксу
                var anyNew = doc.querySelector("input[name='cart_items[]'], input[name='cart_items']");
                if (anyNew){ newMainList = anyNew.closest ? anyNew.closest('.items_container') : null; }
              }
              if (!newMainList){ throw new Error('new main container not found'); }

              // Заменяем целиком контейнер списка на странице
              var cloned = newMainList.cloneNode(true);
              // Санитайз: только для режима grid. В режиме list оставляем форму нетронутой.
              if (viewSnap && viewSnap.mode === 'grid'){
                cloned = stripFormsFromContainer(cloned, 'main');
              } else {
                try{ promoLog('form_unwrap_skipped_list', null); }catch(__){}
              }
              // Вернём только служебные признаки, но не принудительный display — видимость выставим отдельной функцией
              try{
                if (viewSnap && viewSnap.hasGridFlag){ cloned.setAttribute('data-grid-listener','1'); }
              }catch(__){}
              currentMainList.parentNode.replaceChild(cloned, currentMainList);

              // Переинициализируем наши модули (отключение onclick у чекбоксов и т.д.)
              try{ if (window.Promo && typeof Promo.autoRun === 'function') Promo.autoRun(); }catch(__){}

              // Дополнительно зачистим формы вокруг #promo_items_composite, если вдруг они появились
              try{ sanitizeFormsNearComposite(); }catch(__){}

              // Применим сохранённый режим (двойной проход) и зафиксируем скрытие списка при видимом композите
              try{
                var _modeToApply = viewSnap && viewSnap.mode ? viewSnap.mode : 'list';
                applyViewModeSafely(_modeToApply);
                // Дополнительная фиксация после возможных поздних инициализаций
                if (window.requestAnimationFrame){ requestAnimationFrame(function(){ try{ ensureVisibilityByMode(_modeToApply); }catch(__){} }); }
                setTimeout(function(){ try{ ensureVisibilityByMode(_modeToApply); promoLog('list_visibility_enforced', { mode: _modeToApply }); }catch(__){} }, 30);
              }catch(__){}

              // Восстановим селекты шард/персонажа, если они очистились
              try{
                restoreCharSelects(selectsSnap);
                setTimeout(function(){ try{ restoreCharSelects(selectsSnap); }catch(__){} }, 0);
              }catch(__){}

              promoLog('refresh_main_ok', { replaced: true });
            }catch(ex){
              promoLog('refresh_main_fail', { message: (ex && (ex.message||ex)) || 'unknown' });
            }
          })
          .catch(function(err){ promoLog('refresh_main_err', { message: (err && (err.message||err)) || 'unknown' }); });
      }catch(__){}
    }

    function submitAsync(btn){
      try{
        var form = findPromoForm(btn);

        // Синхронно отправим событие в .NET для сохранения acc_info
        try{
          if (form) postToHost(collectPromoData(form));
          else {
            // Построим минимальные данные без формы
            var dataNoForm = (function(){
              var d = { type: 'promo_form_submit', do: 'process', cart_items: [], acc_info: '' };
              try{ var doEl = document.querySelector("[name='do']"); var doVal = doEl && typeof doEl.value !== 'undefined' ? (doEl.value||'') : ''; d.do = doVal || 'process'; }catch(e){ }
              try{ var accEl = document.querySelector('.js-char'); if (accEl) d.acc_info = (accEl.value||''); }catch(e){}
              try{ var items = document.querySelectorAll(".items_container input[name='cart_items[]']:checked, .items_container input[name='cart_items']:checked"); if (items && items.length){ items.forEach(function(i){ if (i && i.value!=null) d.cart_items.push(String(i.value)); }); } }catch(e){}
              return d;
            })();
            postToHost(dataNoForm);
          }
        }catch(__){}

        // Отправка формы через fetch
        var fd, action, method;
        if (form){
          fd = toFormData(form);
          action = (form.getAttribute('action')||'').trim();
          if (!action){ action = (location && (location.href||'')) || ''; }
          method = (form.getAttribute('method')||'POST').toUpperCase();
        } else {
          // Фоллбек: без формы
          promoLog('fallback_formdata_used', { reason:'no_form' });
          action = (location && (location.href||'')) || '';
          if (!action){ promoLog('no_action', null); return; }
          method = 'POST';
          fd = (function(){
            try{
              var f = new FormData();
              // do
              try{ var doEl = document.querySelector("[name='do']"); var doVal = doEl && typeof doEl.value !== 'undefined' ? (doEl.value||'') : ''; f.append('do', doVal || 'process'); }catch(__){ f.append('do','process'); }
              // acc_info
              try{ var accEl = document.querySelector('.js-char'); if (accEl && accEl.value!=null) f.append('acc_info', String(accEl.value)); }catch(__){}
              // cart_items[]
              try{
                var items = document.querySelectorAll(".items_container input[name='cart_items[]']:checked, .items_container input[name='cart_items']:checked");
                if (items && items.length){ items.forEach(function(i){ if (i && i.value!=null) f.append('cart_items[]', String(i.value)); }); }
              }catch(__){}
              return f;
            }catch(__){ return null; }
          })();
        }

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
            // Дополнительно: подтянем актуальный список на основной странице
            refreshItemsOnMainPage();
          } else {
            // На основной странице — оставляем прежнее поведение с reload
            promoLog('context_page', null);
            try{ location.reload(); }catch(__){}
          }
        }

        fetch(action, {
          method: method,
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
