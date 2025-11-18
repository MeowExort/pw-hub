// promo_character_select.js — модуль Promo.CharacterSelect
(function(){
  try{
    // Диагностический логгер JS → .NET (WebView2)
    function promoLog(eventName, payload){
      try{
        var msg = { type: 'promo_log', event: 'charSel_' + String(eventName || ''), data: payload || null, ts: Date.now() };
        if (window.chrome && window.chrome.webview && window.chrome.webview.postMessage){
          window.chrome.webview.postMessage(JSON.stringify(msg));
        }
      }catch(_){ }
    }
    try{ promoLog('loaded', { hasPromo: !!(window.Promo && window.Promo.register) }); }catch(_){ }
    if (!window.Promo || !Promo.register){
      // fallback к прежнему поведению
      try{
        // Готовность DOM без зависимости от jQuery
        var onReady = function(cb){
          try{
            if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', function(){ try{ cb(); }catch(__){} }, { once: true });
            else { try{ cb(); }catch(__){} }
          }catch(__){}
        };
        onReady(function(){
          try{ promoLog('fallback_ready', { readyState: document.readyState }); }catch(_){ }
          // Ждём появления глобального объекта shards и корневых контейнеров (их может быть несколько: основной и в попапе «Управление»)
          // Больше не используем одиночный таймер ожидания: ставим периодический тик и MutationObserver
          // перенесённый оригинальный код
          function createCharacterSelect(data, hostEl, root) {
              try{
                // Подключаем стиль один раз на страницу
                var styleId = 'pw_char_select_style';
                if (!document.getElementById(styleId)){
                  var style = document.createElement('style');
                  style.id = styleId;
                  style.textContent = "\n                    .character-select-container {\n                        font-family: Arial, sans-serif;\n                        margin: 10px 0px;\n                    }\n                    .character-select {\n                        width: 100%;\n                        padding: 10px;\n                        font-size: 14px;\n                        border-radius: 5px;\n                        background: #F0E8DC;\n                        color: #333;\n                        overflow: hidden;\n                        overflow-y: visible;\n                        max-height: none !important;\n                        height: auto !important;\n                    }\n                    .character-select:focus {\n                        outline: none;\n                        border-color: #2c4a8d;\n                        box-shadow: 0 0 5px rgba(74, 107, 175, 0.5);\n                    }\n                    .server-group {\n                        font-weight: bold;\n                        color: #2c4a8d;\n                        background: #F6F1E7;\n                        padding: 5px;\n                    }\n                    .character-option {\n                        padding: 8px 15px;\n                        border-bottom: 1px solid #f0f0f0;\n                    }\n                    .character-name {\n                        font-weight: bold;\n                        color: #333;\n                    }\n                    .character-info {\n                        font-size: 12px;\n                        color: #666;\n                        margin-left: 10px;\n                    }\n                    .character-level {\n                        float: right;\n                        color: #4a6baf;\n                        font-weight: bold;\n                    }\n                    option {\n                        padding: 8px;\n                        border-bottom: 1px solid #f0f0f0;\n                    }\n                    option:checked {\n                        background: #C6B9A3;\n                        color: white;\n                    }\n                  ";
                  (document.head || document.documentElement).appendChild(style);
                }
                var container = document.createElement('div');
                container.className = 'character-select-container';
                var totalOptions = 0;
                for (var sid in data){
                  var server = data[sid];
                  if (!server) continue;
                  totalOptions += 1;
                  var accs = server.accounts || {};
                  for (var aid in accs){
                    var account = accs[aid];
                    if (account && Array.isArray(account.chars)) totalOptions += account.chars.length;
                  }
                }
                var select = document.createElement('select');
                select.className = 'character-select';
                if (totalOptions > 0) select.setAttribute('size', String(totalOptions));
                for (var sid2 in data){
                  var server2 = data[sid2];
                  if (!server2) continue;
                  var sep = document.createElement('option');
                  sep.disabled = true;
                  sep.className = 'server-group';
                  sep.textContent = '─── ' + (server2.name || '') + ' ───';
                  select.appendChild(sep);
                  var accs2 = server2.accounts || {};
                  for (var aid2 in accs2){
                    var account2 = accs2[aid2];
                    if (!account2 || !Array.isArray(account2.chars)) continue;
                    for (var i=0;i<account2.chars.length;i++){
                      var ch = account2.chars[i];
                      if (!ch) continue;
                      var opt = document.createElement('option');
                      opt.value = ch.id;
                      opt.textContent = (ch.name || '') + ' - ' + (ch.occupation || '') + ' (' + (ch.level||'') + ' ур.)';
                      select.appendChild(opt);
                    }
                  }
                }
                select.addEventListener('change', function(){
                  try{
                    var selectedId = select.value;
                    if (!selectedId) return;
                    var selectedAccountId = '';
                    var selectedCharacterId = '';
                    var selectedServerId = '';
                    for (var sid3 in data){
                      var server3 = data[sid3];
                      if (!server3) continue;
                      var accs3 = server3.accounts || {};
                      for (var aid3 in accs3){
                        var account3 = accs3[aid3];
                        if (!account3 || !Array.isArray(account3.chars)) continue;
                        for (var j=0;j<account3.chars.length;j++){
                          var ch3 = account3.chars[j];
                          if (ch3 && String(ch3.id) == String(selectedId)){
                            selectedAccountId = aid3;
                            selectedCharacterId = selectedId;
                            selectedServerId = server3.id;
                            break;
                          }
                        }
                        if (selectedCharacterId) break;
                      }
                      if (selectedCharacterId) break;
                    }
                    if (selectedCharacterId){
                      var scope = null;
                      try{ scope = (hostEl && hostEl.closest) ? hostEl.closest('#promo_container') : null; }catch(__){}
                      var sel = (scope && scope.querySelector('.js-shard')) || document.querySelector('.js-shard');
                      if (sel){
                        sel.value = selectedServerId;
                        var e = document.createEvent('HTMLEvents');
                        e.initEvent('change', true, false);
                        sel.dispatchEvent(e);
                      }
                      var sel2 = (scope && scope.querySelector('.js-char')) || document.querySelector('.js-char');
                      if (sel2){
                        sel2.value = selectedAccountId + '_' + selectedServerId + '_' + selectedCharacterId;
                        var e2 = document.createEvent('HTMLEvents');
                        e2.initEvent('change', true, false);
                        sel2.dispatchEvent(e2);
                      }
                    }
                  }catch(__){}
                });
                container.appendChild(select);
                try{ hostEl.appendChild(container); }catch(__){}
              }catch(__){}
          }
          // Fallback: построение данных из нативных селектов, если window.shards отсутствует
          function extractDataFromNative(root){
            try{
              var scope = root || document;
              var shardSel = (scope && scope.querySelector) ? scope.querySelector('.js-shard') : null;
              var charSel = (scope && scope.querySelector) ? scope.querySelector('.js-char') : null;
              if (!charSel) charSel = document.querySelector('.js-char');
              if (!shardSel) shardSel = document.querySelector('.js-shard');
              if (!charSel) return null;
              var servers = {};
              var shardName = function(id){
                try{
                  if (!shardSel) return id;
                  for (var i=0;i<shardSel.options.length;i++){
                    var o = shardSel.options[i];
                    if (String(o.value) === String(id)) return o.text || id;
                  }
                }catch(__){}
                return id;
              };
              for (var i=0;i<charSel.options.length;i++){
                var opt = charSel.options[i];
                if (!opt || !opt.value) continue;
                var parts = String(opt.value).split('_');
                if (parts.length < 3) continue;
                var accId = parts[0];
                var srvId = parts[1];
                var chId = parts[2];
                if (!servers[srvId]) servers[srvId] = { id: srvId, name: shardName(srvId), accounts: {} };
                var srv = servers[srvId];
                if (!srv.accounts[accId]) srv.accounts[accId] = { id: accId, chars: [] };
                var text = opt.text || (''+chId);
                // Попробуем грубо извлечь класс и уровень из текста, если есть
                var name = text;
                var occupation = '';
                var level = '';
                try{
                  var m = text.match(/^(.*?)\s*-\s*(.*?)\s*\((\d+)\s*ур\.?\)/i);
                  if (m){ name = m[1]; occupation = m[2]; level = m[3]; }
                }catch(__){}
                srv.accounts[accId].chars.push({ id: chId, name: name, occupation: occupation, level: level });
              }
              return servers;
            }catch(__){ return null; }
          }
          // Пер-контейнерный билд: строим для каждого #promo_container ровно один раз
          function buildForContainer(root){
            try{
              if (!root) return;
              if (root.__pwCharSelBuilt) { try{ promoLog('fallback_build_skip_already', null); }catch(_){ } return; } // уже построено в этом контейнере
              try{ promoLog('fallback_build_enter', null); }catch(_){ }
              var data = null;
              if (window.shards && (typeof window.shards === 'object' || Array.isArray(window.shards))){
                data = window.shards;
              try{ promoLog('fallback_data_source', { source: 'shards', hasData: !!data }); }catch(_){ }
              } else {
                data = extractDataFromNative(root);
              try{ promoLog('fallback_data_source', { source: 'native', hasData: !!data }); }catch(_){ }
              }
              if (!data) { try{ promoLog('fallback_build_no_data', null); }catch(_){ } return; }
              root.__pwCharSelBuilt = true;
              var characterContainer = document.createElement('div');
              characterContainer.className = 'characterContainer';
              root.appendChild(characterContainer);
              try { createCharacterSelect(data, characterContainer, root); } catch(e) {}
              // Добавим кнопку отправки ТОЛЬКО внутри окна «Управление» (popup)
              // На основной странице (режим «Список») кнопку не создаём и не меняем
              try{
                var insidePopup = !!(root.closest && root.closest('#promo_popup'));
                if (insidePopup && !root.querySelector('.js-transfer-go')){
                  var submitButton = document.createElement('div');
                  // В попапе не используем класс go_items, чтобы не получать принудительную высоту 32px
                  submitButton.className = 'js-transfer-go';
                  root.appendChild(submitButton);
                  try{ promoLog('fallback_submit_added', { insidePopup: !!insidePopup }); }catch(_){ }
                }
              }catch(__){}
              try{ promoLog('fallback_build_ok', null); }catch(_){ }
            }catch(_){ }
          }
          function ensureBuiltForAll(){
            try{
              var roots = document.querySelectorAll('#promo_container');
              if (!roots || !roots.length) return;
              for (var i=0;i<roots.length;i++) buildForContainer(roots[i]);
            }catch(__){}
          }
          // Немедленный запуск и долгий периодический тик: дождёмся как появления контейнеров, так и поздней инициализации window.shards
          try{ ensureBuiltForAll(); }catch(__){}
          (function(){
            var tries = 0;
            var h = setInterval(function(){
              try{ ensureBuiltForAll(); }catch(__){}
              if (++tries > 240) clearInterval(h); // ~60 секунд по 250мс
            }, 250);
          })();
          // Долгоживущий наблюдатель: ловим добавление новых контейнеров в любой момент (для попапа, открытого позже)
          try{
            if (!window.__pwCharSelObserver){
              var obs = new MutationObserver(function(muts){
                try{
                  for (var mi=0; mi<muts.length; mi++){
                    var m = muts[mi];
                    for (var ni=0; ni<m.addedNodes.length; ni++){
                      var n = m.addedNodes[ni];
                      if (!(n instanceof Element)) continue;
                      if (n.id === 'promo_container') { buildForContainer(n); continue; }
                      try{
                        var found = n.querySelectorAll ? n.querySelectorAll('#promo_container') : [];
                        if (found && found.length){
                          for (var fi=0; fi<found.length; fi++) buildForContainer(found[fi]);
                        }
                      }catch(__){}
                    }
                  }
                }catch(__){}
              });
              obs.observe(document.body || document.documentElement, { childList: true, subtree: true });
              window.__pwCharSelObserver = obs;
              try{ promoLog('fallback_observer_created', null); }catch(_){ }
            }
          }catch(__){}
          try {
              (function(){
                  var last = window.__pwLastAccInfo;
                  if (!last || typeof last !== 'string') return;
                  var parts = last.split('_');
                  if (parts.length < 3) return;
                  var accountId = parts[0];
                  var serverId = parts[1];
                  var characterId = parts[2];
                  function setNative(){
                      try {
                          var shard = document.querySelector('.js-shard');
                          if (shard) {
                              shard.value = serverId;
                              var ev = document.createEvent('HTMLEvents'); ev.initEvent('change', true, false); shard.dispatchEvent(ev);
                          }
                      } catch(e){}
                      try {
                          var jchar = document.querySelector('.js-char');
                          if (jchar) {
                              jchar.value = accountId + '_' + serverId + '_' + characterId;
                              var ev2 = document.createEvent('HTMLEvents'); ev2.initEvent('change', true, false); jchar.dispatchEvent(ev2);
                          }
                      } catch(e){}
                  }
                  function setCustom(){
                      try {
                          var sels = document.querySelectorAll('.character-select');
                          if (!sels || !sels.length) return;
                          for (var i=0;i<sels.length;i++){
                              var sel = sels[i];
                              if (sel && sel.querySelector("option[value='"+characterId+"']")) {
                                  sel.value = characterId;
                                  var ev3 = document.createEvent('HTMLEvents'); ev3.initEvent('change', true, false); sel.dispatchEvent(ev3);
                              }
                          }
                      } catch(e){}
                  }
                  function tryAll(attempt){
                      attempt = attempt || 0;
                      setNative();
                      setCustom();
                      try{ if (attempt === 0 || attempt === 10 || attempt === 20) promoLog('fallback_preselect_attempt', { attempt: attempt }); }catch(_){ }
                      if (attempt < 20) setTimeout(function(){ tryAll(attempt+1); }, 100);
                  }
                  tryAll(0);
              })();
          } catch(e){}
        });
      }catch(_){ }
      return;
    }

    // Ветвь с модульной системой
    Promo.register('CharacterSelect', {
      run: function(){
        Promo.ready(function(){
          try{ promoLog('module_ready', { readyState: document.readyState }); }catch(_){ }
          try{
            // Чистая реализация без jQuery
            function createCharacterSelect(data, hostEl, root) {
                try{
                  var styleId = 'pw_char_select_style';
                  if (!document.getElementById(styleId)){
                    var style = document.createElement('style');
                    style.id = styleId;
                    style.textContent = "\n                      .character-select-container { font-family: Arial, sans-serif; margin: 10px 0px; }\n                      .character-select { width: 100%; padding: 10px; font-size: 14px; border-radius: 5px; background: #F0E8DC; color: #333; overflow: hidden; overflow-y: visible; max-height: none !important; height: auto !important; }\n                      .character-select:focus { outline: none; border-color: #2c4a8d; box-shadow: 0 0 5px rgba(74, 107, 175, 0.5); }\n                      .server-group { font-weight: bold; color: #2c4a8d; background: #F6F1E7; padding: 5px; }\n                      .character-option { padding: 8px 15px; border-bottom: 1px solid #f0f0f0; }\n                      .character-name { font-weight: bold; color: #333; }\n                      .character-info { font-size: 12px; color: #666; margin-left: 10px; }\n                      .character-level { float: right; color: #4a6baf; font-weight: bold; }\n                      option { padding: 8px; border-bottom: 1px solid #f0f0f0; }\n                      option:checked { background: #C6B9A3; color: white; }\n                    ";
                    (document.head || document.documentElement).appendChild(style);
                    try{ promoLog('module_style_injected', null); }catch(_){ }
                  }
                  var container = document.createElement('div');
                  container.className = 'character-select-container';
                  var totalOptions = 0;
                  for (var sid in data){
                    var server = data[sid];
                    if (!server) continue;
                    totalOptions += 1;
                    var accs = server.accounts || {};
                    for (var aid in accs){
                      var account = accs[aid];
                      if (account && Array.isArray(account.chars)) totalOptions += account.chars.length;
                    }
                  }
                  var select = document.createElement('select');
                  select.className = 'character-select';
                  if (totalOptions > 0) select.setAttribute('size', String(totalOptions));
                  for (var sid2 in data){
                    var server2 = data[sid2];
                    if (!server2) continue;
                    var sep = document.createElement('option');
                    sep.disabled = true;
                    sep.className = 'server-group';
                    sep.textContent = '─── ' + (server2.name || '') + ' ───';
                    select.appendChild(sep);
                    var accs2 = server2.accounts || {};
                    for (var aid2 in accs2){
                      var account2 = accs2[aid2];
                      if (!account2 || !Array.isArray(account2.chars)) continue;
                      for (var i=0;i<account2.chars.length;i++){
                        var ch = account2.chars[i];
                        if (!ch) continue;
                        var opt = document.createElement('option');
                        opt.value = ch.id;
                        opt.textContent = (ch.name || '') + ' - ' + (ch.occupation || '') + ' (' + (ch.level||'') + ' ур.)';
                        select.appendChild(opt);
                      }
                    }
                  }
                  select.addEventListener('change', function(){
                    try{
                      var selectedId = select.value;
                      if (!selectedId) return;
                      var selectedAccountId = '';
                      var selectedCharacterId = '';
                      var selectedServerId = '';
                      for (var sid3 in data){
                        var server3 = data[sid3];
                        if (!server3) continue;
                        var accs3 = server3.accounts || {};
                        for (var aid3 in accs3){
                          var account3 = accs3[aid3];
                          if (!account3 || !Array.isArray(account3.chars)) continue;
                          for (var j=0;j<account3.chars.length;j++){
                            var ch3 = account3.chars[j];
                            if (ch3 && String(ch3.id) == String(selectedId)){
                              selectedAccountId = aid3;
                              selectedCharacterId = selectedId;
                              selectedServerId = server3.id;
                              break;
                            }
                          }
                          if (selectedCharacterId) break;
                        }
                        if (selectedCharacterId) break;
                      }
                      try{ promoLog('module_on_change', { selectedId: selectedId }); }catch(_){ }
                      if (selectedCharacterId){
                        var scope = null;
                        try{ scope = (hostEl && hostEl.closest) ? hostEl.closest('#promo_container') : null; }catch(__){}
                        var sel = (scope && scope.querySelector('.js-shard')) || document.querySelector('.js-shard');
                        if (sel){
                          sel.value = selectedServerId;
                          var e = document.createEvent('HTMLEvents');
                          e.initEvent('change', true, false);
                          sel.dispatchEvent(e);
                        }
                        var sel2 = (scope && scope.querySelector('.js-char')) || document.querySelector('.js-char');
                        if (sel2){
                          sel2.value = selectedAccountId + '_' + selectedServerId + '_' + selectedCharacterId;
                          var e2 = document.createEvent('HTMLEvents');
                          e2.initEvent('change', true, false);
                          sel2.dispatchEvent(e2);
                        }
                      }
                    }catch(__){}
                  });
                  container.appendChild(select);
                  try{ hostEl.appendChild(container); }catch(__){}
                }catch(__){}
            }

            // Fallback: построение данных из нативных селектов, если window.shards отсутствует (модульная ветка)
            function extractDataFromNative(root){
              try{
                var scope = root || document;
                var shardSel = (scope && scope.querySelector) ? scope.querySelector('.js-shard') : null;
                var charSel = (scope && scope.querySelector) ? scope.querySelector('.js-char') : null;
                if (!charSel) charSel = document.querySelector('.js-char');
                if (!shardSel) shardSel = document.querySelector('.js-shard');
                if (!charSel) return null;
                var servers = {};
                var shardName = function(id){
                  try{
                    if (!shardSel) return id;
                    for (var i=0;i<shardSel.options.length;i++){
                      var o = shardSel.options[i];
                      if (String(o.value) === String(id)) return o.text || id;
                    }
                  }catch(__){}
                  return id;
                };
                for (var i=0;i<charSel.options.length;i++){
                  var opt = charSel.options[i];
                  if (!opt || !opt.value) continue;
                  var parts = String(opt.value).split('_');
                  if (parts.length < 3) continue;
                  var accId = parts[0];
                  var srvId = parts[1];
                  var chId = parts[2];
                  if (!servers[srvId]) servers[srvId] = { id: srvId, name: shardName(srvId), accounts: {} };
                  var srv = servers[srvId];
                  if (!srv.accounts[accId]) srv.accounts[accId] = { id: accId, chars: [] };
                  var text = opt.text || (''+chId);
                  var name = text;
                  var occupation = '';
                  var level = '';
                  try{
                    var m = text.match(/^(.*?)\s*-\s*(.*?)\s*\((\d+)\s*ур\.?\)/i);
                    if (m){ name = m[1]; occupation = m[2]; level = m[3]; }
                  }catch(__){}
                  srv.accounts[accId].chars.push({ id: chId, name: name, occupation: occupation, level: level });
                }
                return servers;
              }catch(__){ return null; }
            }

            function buildForContainer(root){
              try{
                if (!root) return;
                if (root.__pwCharSelBuilt) { try{ promoLog('module_build_skip_already', null); }catch(_){ } return; }
                try{ promoLog('module_build_enter', null); }catch(_){ }
                var data = null;
                if (window.shards && (typeof window.shards === 'object' || Array.isArray(window.shards))){
                  data = window.shards;
                  try{ promoLog('module_data_source', { source: 'shards', hasData: !!data }); }catch(_){ }
                } else {
                  data = extractDataFromNative(root);
                  try{ promoLog('module_data_source', { source: 'native', hasData: !!data }); }catch(_){ }
                }
                if (!data) { try{ promoLog('module_build_no_data', null); }catch(_){ } return; }
                root.__pwCharSelBuilt = true;
                var characterContainer = document.createElement('div');
                characterContainer.className = 'characterContainer';
                root.appendChild(characterContainer);
                try { createCharacterSelect(data, characterContainer, root); } catch(e) {}
                try{
                  var insidePopup = !!(root.closest && root.closest('#promo_popup'));
                  if (insidePopup && !root.querySelector('.js-transfer-go')){
                    var submitButton = document.createElement('div');
                    // Не добавляем класс go_items, чтобы не навешивалась фиксированная высота 32px от сайта
                    submitButton.className = 'js-transfer-go';
                    root.appendChild(submitButton);
                    try{ promoLog('module_submit_added', { insidePopup: true }); }catch(_){ }
                  } else {
                    try{ promoLog('module_submit_skip', { insidePopup: !!insidePopup }); }catch(_){ }
                  }
                }catch(__){}
                try{ promoLog('module_build_ok', null); }catch(_){ }
              }catch(_){ }
            }
            function ensureBuiltForAll(){
              try{
                var roots = document.querySelectorAll('#promo_container');
                if (!roots || !roots.length) return;
                for (var i=0;i<roots.length;i++) buildForContainer(roots[i]);
              }catch(__){}
            }
            // Немедленный запуск и длительный тик + наблюдатель (без ожидания), чтобы не пропустить позднюю инициализацию данных
            try{ ensureBuiltForAll(); }catch(__){}
            (function(){
              var tries = 0;
              var h = setInterval(function(){
                try{ ensureBuiltForAll(); }catch(__){}
                try{ if (tries === 0 || tries === 10 || tries === 60 || tries === 120 || tries === 240) promoLog('module_tick', { tries: tries, hasShards: !!window.shards }); }catch(_){ }
                if (++tries > 240) clearInterval(h);
              }, 250);
            })();
            try{
              if (!window.__pwCharSelObserver){
                var obs = new MutationObserver(function(muts){
                  try{
                    for (var mi=0; mi<muts.length; mi++){
                      var m = muts[mi];
                      for (var ni=0; ni<m.addedNodes.length; ni++){
                        var n = m.addedNodes[ni];
                        if (!(n instanceof Element)) continue;
                        if (n.id === 'promo_container') { buildForContainer(n); continue; }
                        try{
                          var found = n.querySelectorAll ? n.querySelectorAll('#promo_container') : [];
                          if (found && found.length){
                            for (var fi=0; fi<found.length; fi++) buildForContainer(found[fi]);
                          }
                        }catch(__){}
                      }
                    }
                  }catch(__){}
                });
                obs.observe(document.body || document.documentElement, { childList: true, subtree: true });
                window.__pwCharSelObserver = obs;
                try{ promoLog('module_observer_created', null); }catch(_){ }
              }
            }catch(__){}

            try {
              (function(){
                  var last = window.__pwLastAccInfo;
                  if (!last || typeof last !== 'string') return;
                  var parts = last.split('_');
                  if (parts.length < 3) return;
                  var accountId = parts[0];
                  var serverId = parts[1];
                  var characterId = parts[2];
                  function setNative(){
                      try {
                          var shard = document.querySelector('.js-shard');
                          if (shard) {
                              shard.value = serverId;
                              var ev = document.createEvent('HTMLEvents'); ev.initEvent('change', true, false); shard.dispatchEvent(ev);
                          }
                      } catch(e){}
                      try {
                          var jchar = document.querySelector('.js-char');
                          if (jchar) {
                              jchar.value = accountId + '_' + serverId + '_' + characterId;
                              var ev2 = document.createEvent('HTMLEvents'); ev2.initEvent('change', true, false); jchar.dispatchEvent(ev2);
                          }
                      } catch(e){}
                  }
                  function setCustom(){
                      try {
                          var sels = document.querySelectorAll('.character-select');
                          if (!sels || !sels.length) return;
                          for (var i=0;i<sels.length;i++){
                              var sel = sels[i];
                              if (sel && sel.querySelector("option[value='"+characterId+"']")) {
                                  sel.value = characterId;
                                  var ev3 = document.createEvent('HTMLEvents'); ev3.initEvent('change', true, false); sel.dispatchEvent(ev3);
                              }
                          }
                      } catch(e){}
                  }
                  function tryAll(attempt){
                      attempt = attempt || 0;
                      setNative();
                      setCustom();
                      try{ if (attempt === 0 || attempt === 10 || attempt === 20) promoLog('module_preselect_attempt', { attempt: attempt }); }catch(_){ }
                      if (attempt < 20) setTimeout(function(){ tryAll(attempt+1); }, 100);
                  }
                  tryAll(0);
              })();
            } catch(e){}
          }catch(_){ }
        });
      }
    });

  }catch(_){ }
})();