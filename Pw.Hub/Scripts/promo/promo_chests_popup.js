// promo_chests_popup.js — открытие сундуков во всплывающем окне вместо перехода
(function(){
  try{
    var qs = (location.search||'').toLowerCase();
    // На странице активации (do=activate) поведение не трогаем
    if (qs.indexOf('do=activate') !== -1) return;

    // Глобальные хелперы для внешнего окна (родитель promo_items.php), чтобы iframe мог просить закрыть попап и обновить страницу
    function ensurePopupGlobals(){
      try{
        if (!window.__promoChestAfterSubmit){
          window.__promoChestAfterSubmit = function(){
            try{ hidePopup(); }catch(_){ }
            try{ window.location.reload(); }catch(_){ }
          };
        }
        // Глобальный тултип в РОДИТЕЛЕ (над iframe), чтобы исключить любые проблемы видимости внутри iframe
        if (!window.__promoChestTooltip){
          var gTip, gHost;
          function ensureGlobalTip(){
            try{
              // Определяем хост для тултипа: ВЕРХ уровня — overlay, чтобы рисовать НАД попапом
              // При его отсутствии используем body; при появлении overlay — перепривяжем
              var host = document.getElementById('promo_chest_popup_overlay')
                        || document.body;
              if (!host) return null;
              gHost = host;

              if (!gTip){
                gTip = document.createElement('div');
                gTip.id = 'promo_chest_global_tip';
                gTip.style = [
                  // Позиционирование внутри overlay (над попапом)
                  'position:absolute',
                  'z-index:2147483647',
                  'pointer-events:none',
                  'display:none',
                  'visibility:hidden',
                  'opacity:0',
                  'max-width:360px',
                  'min-width:120px',
                  'background:#ffffff',
                  'color:#222',
                  'border:1px solid #E2D8C9',
                  'box-shadow:0 8px 24px rgba(0,0,0,0.25)',
                  'border-radius:8px',
                  'padding:8px 10px',
                  'font-family:Arial,sans-serif',
                  'font-size:13px',
                  'line-height:1.35'
                ].join(';');
              }
              // Убедимся, что тултип прикреплён именно к overlay
              if (gTip.parentNode !== gHost){
                try{ gTip.parentNode && gTip.parentNode.removeChild(gTip); }catch(__){}
                gHost.appendChild(gTip);
              }
              return gTip;
            }catch(__){}
            return null;
          }
          window.__promoChestTooltip = {
            // Статичная подсказка: игнорируем координаты, центрируем НАД попапом (а не внутри него)
            // show(html)
            show: function(html){
              try{
                var t = ensureGlobalTip(); if (!t) return;
                t.innerHTML = html || '';
                if (!html) { this.hide(); return; }
                t.style.display = 'block';
                t.style.visibility = 'visible';
                t.style.opacity = '1';
                // Размещаем тултип над попапом: берём rect попапа, проецируем в координаты overlay
                var overlay = document.getElementById('promo_chest_popup_overlay') || gHost || document.body;
                var popup = document.getElementById('promo_chest_popup');
                var roHost = overlay.getBoundingClientRect();
                var roPopup = popup ? popup.getBoundingClientRect() : roHost;

                // Ограничение ширины тултипа относительно ширины попапа/overlay
                var maxW = Math.max(240, Math.min(360, roPopup.width - 16));
                t.style.maxWidth = maxW + 'px';

                // Нужно измерить размеры тултипа после вставки контента
                var tipW = t.offsetWidth || maxW;
                var tipH = t.offsetHeight || 60;

                // Центрируем по горизонтали над попапом
                var left = roPopup.left + (roPopup.width - tipW)/2 - roHost.left;
                // На 8px выше верхней кромки попапа
                var top = roPopup.top - tipH - 8 - roHost.top;

                // Клампим в пределах overlay
                if (left < 8) left = 8;
                var hostW = roHost.width || (window.innerWidth||800);
                if (left + tipW > hostW - 8) left = Math.max(8, hostW - tipW - 8);
                if (top < 8) top = 8; // если слишком высоко, прижмём к верхней границе overlay

                t.style.left = left + 'px';
                t.style.top = top + 'px';
              }catch(__){}
            },
            // move игнорируется — тултип статичен
            move: function(){
              try{
                return; // no-op
              }catch(__){}
            },
            hide: function(){
              try{
                if (!gTip) return;
                gTip.style.opacity = '0';
                gTip.style.visibility = 'hidden';
                gTip.style.display = 'none';
              }catch(__){}
            }
          };
        }
      }catch(_){ }
    }

    // Создаём (один раз) оверлей и контейнер попапа
    function ensurePopupHost(){
      try{
        if (document.getElementById('promo_chest_popup_overlay')) return;

        var overlay = document.createElement('div');
        overlay.id = 'promo_chest_popup_overlay';
        overlay.style = [
          'position:fixed',
          'z-index:2147483647',
          'left:0',
          'top:0',
          'right:0',
          'bottom:0',
          'background:rgba(0,0,0,0.45)',
          'display:none',
          'align-items:center',
          'justify-content:center'
        ].join(';');

        var popup = document.createElement('div');
        popup.id = 'promo_chest_popup';
        popup.style = [
          // Делаем контекстом позиционирования для статичного тултипа
          'position:relative',
          'background:#f7f3e9',
          'border-radius:10px',
          'box-shadow:0 12px 32px rgba(0,0,0,0.35)',
          'border:1px solid #d8cbb4',
          'min-width:360px',
          'max-width:440px',
          'width:auto',
          'max-height:80vh',
          'display:flex',
          'flex-direction:column',
          'overflow:hidden'
        ].join(';');

        var header = document.createElement('div');
        header.id = 'promo_chest_popup_header';
        header.style = [
          'display:flex',
          'align-items:center',
          'justify-content:space-between',
          'padding:8px 10px',
          'background:#e3d5bf',
          'border-bottom:1px solid #d3c4aa',
          'font-family:Arial,sans-serif',
          'font-size:13px',
          'font-weight:700',
          'color:#2c4a8d'
        ].join(';');
        var title = document.createElement('div');
        title.id = 'promo_chest_popup_title';
        title.textContent = 'Активация сундука';
        var closeBtn = document.createElement('button');
        closeBtn.textContent = '×';
        closeBtn.title = 'Закрыть';
        closeBtn.style = [
          'border:none',
          'background:transparent',
          'cursor:pointer',
          'font-size:18px',
          'line-height:1',
          'padding:0 4px',
          'color:#333'
        ].join(';');
        closeBtn.addEventListener('click', function(){ hidePopup(); });
        header.appendChild(title);
        header.appendChild(closeBtn);

        var body = document.createElement('div');
        body.id = 'promo_chest_popup_body';
        body.style = [
          'position:relative',
          'flex:1 1 auto',
          'background:#fff',
          'overflow:hidden'
        ].join(';');

        var iframe = document.createElement('iframe');
        iframe.id = 'promo_chest_popup_iframe';
        iframe.style = [
          'border:none',
          'width:100%',
          'height:100%'
        ].join(';');
        // Важно: allow-same-origin, чтобы можно было читать/переписывать DOM внутри iframe
        iframe.setAttribute('sandbox', 'allow-same-origin allow-scripts allow-forms');
        body.appendChild(iframe);

        popup.appendChild(header);
        popup.appendChild(body);
        overlay.appendChild(popup);
        document.body.appendChild(overlay);

        // Гарантируем наличие глобального обработчика после сабмита формы в iframe
        ensurePopupGlobals();
      }catch(_){ }
    }

    // Автоподбор размеров попапа под содержимое iframe, чтобы минимизировать внутренние скроллбары
    function autosizePopupToIframe(iframe){
      try{
        if (!iframe) return;
        var popup = document.getElementById('promo_chest_popup');
        var overlay = document.getElementById('promo_chest_popup_overlay');
        if (!popup || !overlay) return;

        var innerDoc = iframe.contentDocument;
        if (!innerDoc || !innerDoc.body) return;

        // Корень содержимого формы внутри iframe
        var formRoot = innerDoc.body.firstElementChild || innerDoc.body;
        if (!formRoot) return;

        // Убеждаемся, что элемент в документе и имеет размеры
        var rect = formRoot.getBoundingClientRect();
        if (!rect || !rect.width || !rect.height) return;

        // Немного запасных отступов по краям, но не шире ~440px вместе с рамками
        var desiredWidth = Math.ceil(rect.width) + 32;
        if (desiredWidth > 440) desiredWidth = 440;
        // Высоту берём почти вплотную к форме, без лишнего «хвоста» снизу
        var desiredHeight = Math.ceil(rect.height);

        var headerEl = document.getElementById('promo_chest_popup_header');
        var headerH = headerEl ? headerEl.offsetHeight : 0;

        var totalHeight = desiredHeight + headerH;

        // Не вылезать за пределы окна браузера
        var maxW = Math.max(360, (window.innerWidth || document.documentElement.clientWidth || 800) - 40);
        var maxH = Math.max(240, (window.innerHeight || document.documentElement.clientHeight || 600) - 40);

        var finalWidth = Math.min(desiredWidth, maxW, 440);
        var finalHeight = Math.min(totalHeight, maxH);

        // Применяем размеры к попапу
        popup.style.width = finalWidth + 'px';
        popup.style.height = finalHeight + 'px';
        popup.style.maxWidth = maxW + 'px';
        popup.style.maxHeight = maxH + 'px';

        // Высота тела попапа = общая высота минус шапка
        var bodyWrap = document.getElementById('promo_chest_popup_body');
        if (bodyWrap){
          var bodyH = finalHeight - headerH;
          if (bodyH < 100) bodyH = Math.max(100, finalHeight - headerH);
          bodyWrap.style.height = bodyH + 'px';
        }

        // iframe растягиваем по высоте тела
        iframe.style.height = '100%';
      }catch(_){ }
    }

    // Попытаться внутри iframe оставить только форму активации из .promo_container .promo_chest_block_spisok
    function tryIsolateFormInIframe(iframe){
      try{
        if (!iframe || !iframe.contentWindow || !iframe.contentDocument) return;
        var doc = iframe.contentDocument;
        if (!doc) return;

        // Проверка домена: работаем только с тем же origin, что и родитель
        try{
          var frameLoc = doc.location || iframe.contentWindow.location;
          var parentLoc = window.location;
          if (!frameLoc || frameLoc.origin !== parentLoc.origin) return;
        }catch(_){ return; }

        // Полностью вычищаем стили внутри iframe: убираем style/link[rel=stylesheet] из <head>
        try{
          var head = doc.head;
          if (head){
            var styleNodes = head.querySelectorAll('style,link[rel="stylesheet"],link[rel="preload"][as="style"]');
            for (var i = 0; i < styleNodes.length; i++){
              try{ styleNodes[i].parentNode.removeChild(styleNodes[i]); }catch(__){}
            }
          }
        }catch(__){}

        var container = doc.querySelector('.promo_container .promo_chest_block_spisok');
        if (!container) return;

        // Оборачиваем нужный блок во временный root и чистим body
        var body = doc.body;
        if (!body) return;

        // Если уже изолировали раньше — не дублируем
        if (body.getAttribute('data-promo-chest-iso') === '1') return;
        body.setAttribute('data-promo-chest-iso', '1');

        // Сбрасываем базовые стили body: убираем паддинги/маргины/фон
        try{
          body.style.margin = '0';
          body.style.padding = '0';
          body.style.paddingBottom = '0';
          body.style.background = 'transparent';
        }catch(_){ }

        // Создаём новый body-контейнер с минимальными стилями (фон/шрифт как у «Плиток»)
        var wrapper = doc.createElement('div');
        wrapper.style = [
          'margin:0',
          'min-height:100%',
          'padding:16px 20px',
          'background:#f7f3e9',
          'font-family:Arial,sans-serif',
          'color:#333',
          'display:flex',
          'align-items:flex-start',
          'justify-content:center'
        ].join(';');

        // Клонируем исходный блок, но удаляем все inline-стили, чтобы не осталось стилей сайта
        var cloned = container.cloneNode(true);
        try{
          var allNodes = cloned.querySelectorAll('*');
          for (var j = 0; j < allNodes.length; j++){
            try{ allNodes[j].removeAttribute('style'); }catch(__){}
          }
        }catch(__){}

        // Ищем форму активации внутри клона
        var form = cloned.querySelector('form') || cloned;

        // Стилизуем форму-обёртку: карточка с плитками предметов + кнопка сабмита
        try{
          form.style = [
            'margin:0 auto',
            'padding:16px 20px',
            // Ограничиваем ширину самой формы ~400px для более аккуратного вида
            'max-width:400px',
            'width:100%',
            'background:#ffffff',
            'border-radius:10px',
            'box-shadow:0 12px 32px rgba(0,0,0,0.2)',
            'border:1px solid #d8cbb4',
            'box-sizing:border-box',
            'display:flex',
            'flex-direction:column',
            'gap:12px',
            'font-size:13px',
            // Центрируем внутренний контент формы по горизонтали
            'align-items:center'
          ].join(';');

          // Заголовок формы (если есть)
          try{
            var titles = form.querySelectorAll('h1,h2,h3,.title,.promo_chest_title');
            for (var k = 0; k < titles.length; k++){
              var t = titles[k];
              t.style.margin = '0 0 8px 0';
              t.style.fontSize = '14px';
              t.style.fontWeight = '700';
              t.style.color = '#2c4a8d';
            }
          }catch(__){}

          // Построение плиток вместо таблицы: показываем только иконку, а описание — в тултипе
          try{
            var table = form.querySelector('table.promo_items');
            if (table && !form.getAttribute('data-promo-items-grid')){
              form.setAttribute('data-promo-items-grid','1');

              // Оставляем реальные input'ы формы, но прячем их визуально
              try{
                var realInputs = table.querySelectorAll('input[type="radio"], input[type="checkbox"]');
                for (var ri = 0; ri < realInputs.length; ri++){
                  var rInp = realInputs[ri];
                  // display:none, чтобы не занимали место, но продолжали участвовать в сабмите
                  rInp.style.display = 'none';
                }
              }catch(__){}

              // Подготовка ссылок на тултип в родителе; создаём локальный как запасной вариант
              var parentTip = null;
              try{ parentTip = window.parent && window.parent.__promoChestTooltip ? window.parent.__promoChestTooltip : null; }catch(__){}
              var tip = null;
              if (!parentTip){
                tip = doc.getElementById('promo_chest_items_tooltip');
                if (!tip){
                  tip = doc.createElement('div');
                  tip.id = 'promo_chest_items_tooltip';
                  tip.style = [
                    'position:fixed',
                    'z-index:2147483647',
                    'pointer-events:none',
                    'display:none',
                    'visibility:hidden',
                    'opacity:0',
                    'max-width:360px',
                    'min-width:120px',
                    'background:#ffffff',
                    'color:#222',
                    'border:1px solid #E2D8C9',
                    'box-shadow:0 8px 24px rgba(0,0,0,0.25)',
                    'border-radius:8px',
                    'padding:8px 10px',
                    'font-family:Arial,sans-serif',
                    'font-size:13px',
                    'line-height:1.35'
                  ].join(';');
                  doc.body.appendChild(tip);
                }
              }

              // Контейнер плиток
              var tilesHost = doc.createElement('div');
              tilesHost.style = [
                'display:flex',
                'flex-wrap:wrap',
                'gap:8px',
                'align-content:flex-start',
                // Центруем плитки по горизонтали
                'justify-content:center',
                'margin:4px 0 0 0'
              ].join(';');

              // Собираем строки таблицы в структуру
              var itemRows = table.querySelectorAll('tr');
              var tileItems = [];
              var itemSize = 56; var cropSize = 30; var cropX = 45, cropY = 25;

              for (var r = 0; r < itemRows.length; r++){
                try{
                  var tr = itemRows[r];
                  var imgCell = tr.querySelector('.img_item_cell');
                  var inputCell = tr.querySelector('.item_input_block');
                  if (!imgCell || !inputCell) continue;

                  var img = imgCell.querySelector('img');
                  var descSpan = imgCell.querySelector('span');
                  var label = inputCell.querySelector('label');
                  var input = inputCell.querySelector('input[type="radio"], input[type="checkbox"]');
                  if (!img || !input) continue;

                  var nameText = label ? (label.innerText || label.textContent || '').trim() : '';
                  var descHtml = '';
                  try{ descHtml = descSpan ? (descSpan.innerHTML || descSpan.innerText || '') : ''; }catch(__){ descHtml = ''; }

                  var src = img.getAttribute('src') || '';
                  try{ if (src && src.indexOf('//') === 0){ src = doc.location.protocol + src; } }catch(__){}

                  // Плитка
                  var block = doc.createElement('div');
                  block.className = 'promo_chest_item_tile';
                  block.style = [
                    'width:'+itemSize+'px',
                    'height:'+itemSize+'px',
                    'border-radius:10px',
                    'box-shadow:inset 0 0 0 1px #E2D8C9',
                    'background:#ffffffCC',
                    'display:flex',
                    'align-items:center',
                    'justify-content:center',
                    'cursor:pointer',
                    'position:relative'
                  ].join(';');
                  // Добавим нативный title как резервный тултип, если кастомный не сработает
                  try{ if (nameText) block.setAttribute('title', nameText); }catch(__){}

                  var crop = doc.createElement('div');
                  crop.style = [
                    'width:'+cropSize+'px',
                    'height:'+cropSize+'px',
                    'overflow:hidden',
                    'border-radius:6px',
                    'box-shadow:inset 0 0 0 1px #E2D8C9',
                    'background:#fff',
                    'position:relative'
                  ].join(';');
                  var img2 = doc.createElement('img');
                  img2.src = src;
                  img2.alt = nameText;
                  img2.title = '';
                  img2.style = [
                    'position:absolute',
                    'left:-'+cropX+'px',
                    'top:-'+cropY+'px',
                    'width:auto',
                    'height:auto',
                    'max-width:none',
                    'max-height:none',
                    'image-rendering:auto'
                  ].join(';');
                  crop.appendChild(img2);
                  block.appendChild(crop);

                  var isRadio = (input.getAttribute('type') || '').toLowerCase() === 'radio';

                  // Тултип по наведению
                  (function(blockRef, nameRef, descRef){
                    function getXY(e){
                      try{
                        if (!e) return null;
                        if (typeof e.pageX === 'number' && typeof e.pageY === 'number') return { x:e.pageX, y:e.pageY };
                        var sl = (doc.documentElement.scrollLeft||0) + (doc.body.scrollLeft||0);
                        var st = (doc.documentElement.scrollTop||0) + (doc.body.scrollTop||0);
                        if (typeof e.clientX === 'number' && typeof e.clientY === 'number') return { x:e.clientX + sl, y:e.clientY + st };
                      }catch(__){}
                      return null;
                    }
                    function positionLocalTipAt(x, y){
                      if (!tip) return;
                      var w = tip.offsetWidth || 320;
                      var h = tip.offsetHeight || 60;
                      var vw = window.innerWidth || doc.documentElement.clientWidth || 800;
                      var vh = window.innerHeight || doc.documentElement.clientHeight || 600;
                      var nx = x + 14;
                      var ny = y + 14;
                      if (nx + w > vw - 8) nx = x - w - 10;
                      if (ny + h > vh - 8) ny = y - h - 10;
                      if (nx < 8) nx = 8;
                      if (ny < 8) ny = 8;
                      tip.style.left = nx + 'px';
                      tip.style.top = ny + 'px';
                    }

                    function showTip(e){
                      var html = '';
                      if (nameRef){ html += '<div style="font-weight:700; margin-bottom:4px;">'+nameRef+'</div>'; }
                      if (descRef){ html += descRef.toString(); }
                      if (parentTip){
                        // Статичный тултип: просто показываем текст над попапом
                        try{ window.parent.__promoChestTooltip.show(html); }catch(__){}
                      } else if (tip){
                        tip.innerHTML = html;
                        if (html){
                          tip.style.display = 'block';
                          tip.style.visibility = 'visible';
                          tip.style.opacity = '1';
                          // Позиционируем рядом с плиткой (внутри iframe)
                          var rectb = blockRef.getBoundingClientRect();
                          var sl = (doc.documentElement.scrollLeft||0) + (doc.body.scrollLeft||0);
                          var st = (doc.documentElement.scrollTop||0) + (doc.body.scrollTop||0);
                          positionLocalTipAt(rectb.right + sl + 8, rectb.top + st);
                          // Тоже сделаем пару повторных подстроек после возможного автосайза
                          var tries2 = 0;
                          (function rePos2(){
                            try{
                              tries2++;
                              var r2 = blockRef.getBoundingClientRect();
                              var sl2 = (doc.documentElement.scrollLeft||0) + (doc.body.scrollLeft||0);
                              var st2 = (doc.documentElement.scrollTop||0) + (doc.body.scrollTop||0);
                              positionLocalTipAt(r2.right + sl2 + 8, r2.top + st2);
                              if (tries2 < 6) setTimeout(rePos2, 50);
                            }catch(__){}
                          })();
                        } else {
                          hideTip();
                        }
                      }
                    }

                    function moveTip(e){
                      if (parentTip){
                        var fe = null; try{ fe = window.frameElement || null; }catch(__){}
                        if (e && typeof e.clientX === 'number'){
                          window.parent.__promoChestTooltip.move(e.clientX, e.clientY, fe, 'client');
                        } else {
                          var xy = getXY(e);
                          if (xy){
                            var sl = (doc.documentElement.scrollLeft||0) + (doc.body.scrollLeft||0);
                            var st = (doc.documentElement.scrollTop||0) + (doc.body.scrollTop||0);
                            window.parent.__promoChestTooltip.move(xy.x - sl, xy.y - st, fe, 'client');
                          }
                        }
                      } else if (tip){
                        if (tip.style.display === 'none') return;
                        var xy = getXY(e);
                        if (xy) positionLocalTipAt(xy.x, xy.y);
                      }
                    }

                    function hideTip(){
                      try{
                        if (parentTip){
                          window.parent.__promoChestTooltip.hide();
                        } else if (tip){
                          tip.style.opacity = '0';
                          tip.style.visibility = 'hidden';
                          tip.style.display = 'none';
                        }
                      }catch(__){}
                    }

                    // Наведение мыши
                    blockRef.addEventListener('mouseenter', function(e){ try{ showTip(e); }catch(__){} });
                    blockRef.addEventListener('mouseover', function(e){ try{ showTip(e); }catch(__){} });
                    // Больше не двигаем за курсором — тултип «прикреплён» к плитке
                    // blockRef.addEventListener('mousemove', function(e){ try{ moveTip(e); }catch(__){} });
                    blockRef.addEventListener('mouseleave', function(){ try{ hideTip(); }catch(__){} });
                    // Фокус клавиатурой
                    try{ blockRef.setAttribute('tabindex','0'); }catch(__){}
                    blockRef.addEventListener('focus', function(e){ try{ showTip(e); }catch(__){} });
                    blockRef.addEventListener('blur', function(){ try{ hideTip(); }catch(__){} });
                    // Тач-устройства
                    blockRef.addEventListener('touchstart', function(e){ try{ showTip(e.touches && e.touches[0] ? e.touches[0] : e); }catch(__){} }, {passive:true});
                    blockRef.addEventListener('touchend', function(){ try{ hideTip(); }catch(__){} });
                  })(block, nameText, descHtml);

                  tileItems.push({
                    input: input,
                    block: block,
                    isRadio: isRadio
                  });

                  tilesHost.appendChild(block);
                }catch(__){}
              }

              // Убираем таблицу из визуального потока, но не трогаем её структуру (на всякий случай)
              try{ table.style.display = 'none'; }catch(__){}

              // Синхронизация рамки выбора с состоянием input'ов
              function syncSelection(){
                try{
                  for (var i = 0; i < tileItems.length; i++){
                    var it = tileItems[i];
                    if (!it || !it.block || !it.input) continue;
                    if (it.input.checked){
                      it.block.style.outline = '2px solid #2c4a8d';
                      it.block.style.outlineOffset = '-2px';
                    } else {
                      it.block.style.outline = '2px solid transparent';
                      it.block.style.outlineOffset = '-2px';
                    }
                  }
                }catch(__){}
              }

              // Клик по плитке — меняем состояние исходного radio/checkbox
              (function(){
                for (var i = 0; i < tileItems.length; i++){
                  (function(it){
                    if (!it || !it.block || !it.input) return;
                    it.block.addEventListener('click', function(){
                      try{
                        if (it.isRadio){
                          // Радио: ставим выбранный и снимаем остальные в этой группе
                          var name = it.input.getAttribute('name');
                          if (name){
                            var group = form.querySelectorAll('input[type="radio"][name="'+name.replace(/"/g,'\\"')+'"]');
                            for (var g = 0; g < group.length; g++){
                              group[g].checked = false;
                            }
                          }
                          it.input.checked = true;
                        } else {
                          it.input.checked = !it.input.checked;
                        }
                        try{ it.input.dispatchEvent(new Event('change', { bubbles:true })); }catch(__){}
                        syncSelection();
                      }catch(__){}
                    });
                  })(tileItems[i]);
                }
              })();

              // На случай изменения состояния изнутри формы (клавиатура и т.п.)
              try{
                form.addEventListener('change', function(e){
                  try{
                    var t = e && e.target;
                    if (!t || (t.type !== 'radio' && t.type !== 'checkbox')) return;
                    syncSelection();
                  }catch(__){}
                });
              }catch(__){}

              // Начальная подсветка
              syncSelection();

              // Вставляем контейнер плиток перед кнопкой сабмита (или в конец формы)
              var inserted = false;
              try{
                var submitBtnHost = form.querySelector('input[type="submit"], button[type="submit"], .submit, .btn_submit');
                if (submitBtnHost && submitBtnHost.parentNode){
                  submitBtnHost.parentNode.insertBefore(tilesHost, submitBtnHost);
                  inserted = true;
                }
              }catch(__){}
              if (!inserted){
                form.appendChild(tilesHost);
              }
            }
          }catch(__){}

          // Кнопка сабмита — сохраняем и отдельно выделяем визуально
          try{
            var submitBtn = form.querySelector('input[type="submit"], button[type="submit"], .submit, .btn_submit');
            if (submitBtn){
              var btnStyles = [
                'margin-top:10px',
                // Центрируем кнопку сабмита под плитками
                'align-self:center',
                'padding:6px 14px',
                'background:#2c4a8d',
                'color:#fff',
                'border:none',
                'border-radius:6px',
                'cursor:pointer',
                'font-weight:700',
                'font-size:13px'
              ];
              submitBtn.style = btnStyles.join(';');

              // Обновляем текст кнопки: явно ставим «Активировать»,
              // если в оригинале текст пустой или состоит только из пробелов
              try{
                var txt = '';
                if (submitBtn.tagName === 'INPUT'){
                  txt = submitBtn.value || '';
                  if (!txt || !txt.trim()){
                    submitBtn.value = 'Активировать';
                  }
                } else {
                  txt = (submitBtn.textContent || '').trim();
                  if (!txt){
                    submitBtn.textContent = 'Активировать';
                  }
                }
              }catch(__){}
            }
          }catch(__){}

          // После сабмита формы: закрыть попап и обновить список предметов (перезагрузить родительскую promo-страницу).
          // Сабмит формы не блокируем: запрос уйдёт как обычно, а перезагрузка произойдёт уже в родительском окне.
          try{
            if (!form.getAttribute('data-promo-chest-submit-hook')){
              form.setAttribute('data-promo-chest-submit-hook','1');
              form.addEventListener('submit', function(){
                try{
                  var parentWin = null;
                  try{ parentWin = window.parent || window.top || null; }catch(__){ parentWin = null; }
                  if (parentWin && parentWin.__promoChestAfterSubmit){
                    // Небольшая задержка, чтобы успел стартовать сабмит/редирект внутри iframe
                    setTimeout(function(){
                      try{ parentWin.__promoChestAfterSubmit(); }catch(__){}
                    }, 150);
                  }
                }catch(__){}
              });
            }
          }catch(__){}
        }catch(__){}

        // Вставляем форму в наш wrapper
        wrapper.appendChild(form);

        // Чистим существующий body и вставляем только нашу обёртку
        while (body.firstChild) body.removeChild(body.firstChild);
        body.appendChild(wrapper);

        // После изоляции формы — подобрать размер попапа под содержимое
        try{ autosizePopupToIframe(iframe); }catch(_){ }
      }catch(_){ }
    }

    function showPopup(titleText, href){
      try{
        ensurePopupHost();
        var overlay = document.getElementById('promo_chest_popup_overlay');
        var popupEl = document.getElementById('promo_chest_popup');
        var title = document.getElementById('promo_chest_popup_title');
        var iframe = document.getElementById('promo_chest_popup_iframe');
        if (!overlay || !iframe) return;
        if (title && titleText) title.textContent = titleText;

        // Снимаем предыдущие обработчики, чтобы избежать накопления
        try{ iframe.onload = null; }catch(_){ }

        // После загрузки страницы в iframe — изолируем форму
        try{
          iframe.onload = function(){
            try{
              // Немного подождём на случай отложенной отрисовки
              var attempts = 0;
              function tick(){
                try{
                  attempts++;
                  tryIsolateFormInIframe(iframe);
                  if (attempts < 10){
                    // до ~500 мс максимум (10 * 50)
                    setTimeout(tick, 50);
                  }
                }catch(_){ }
              }
              tick();
            }catch(_){ }
          };
        }catch(_){ }

        iframe.src = href || '';
        overlay.style.display = 'flex';

        // Добавляем закрытие по клику вне области окна и по ESC
        try{
          // Хендлер клика по оверлею: закрывать, если клик не внутри popup
          if (!overlay.__overlayClickHandler){
            overlay.__overlayClickHandler = function(e){
              try{
                var pop = document.getElementById('promo_chest_popup') || popupEl;
                var target = e && (e.target || e.srcElement);
                var inside = false;
                if (pop && target){
                  // Если цель клика не внутри попапа — закрываем
                  inside = !!pop.contains(target);
                }
                if (!inside){
                  hidePopup();
                }
              }catch(__){}
            };
          }
          // На всякий случай перед повторным добавлением удалим возможный старый обработчик
          try{ overlay.removeEventListener('click', overlay.__overlayClickHandler); }catch(__){}
          overlay.addEventListener('click', overlay.__overlayClickHandler);

          // Хендлер ESC на всём документе (пока открыт попап)
          if (!overlay.__escHandler){
            overlay.__escHandler = function(e){
              try{
                var key = e && (e.key || e.code);
                var keyCode = e && e.keyCode;
                if ((key === 'Escape' || key === 'Esc' || keyCode === 27)){
                  if (e && e.preventDefault) e.preventDefault();
                  hidePopup();
                }
              }catch(__){}
            };
          }
          try{ document.removeEventListener('keydown', overlay.__escHandler); }catch(__){}
          document.addEventListener('keydown', overlay.__escHandler);
        }catch(__){}
      }catch(_){ }
    }

    function hidePopup(){
      try{
        var overlay = document.getElementById('promo_chest_popup_overlay');
        var iframe = document.getElementById('promo_chest_popup_iframe');
        if (overlay) overlay.style.display = 'none';
        if (iframe) iframe.removeAttribute('src');

        // Снимаем установленные обработчики, чтобы не накапливались
        try{
          if (overlay && overlay.__overlayClickHandler){
            overlay.removeEventListener('click', overlay.__overlayClickHandler);
          }
          if (overlay && overlay.__escHandler){
            document.removeEventListener('keydown', overlay.__escHandler);
          }
        }catch(__){}
      }catch(_){ }
    }

    // Делегированный обработчик кликов по сундукам.
    // Работает и для уже существующих, и для динамически добавленных элементов.
    function installDelegatedHandler(){
      try{
        if (document.documentElement.getAttribute('data-promo-chest-popup-click-handler') === '1') return;
        document.documentElement.setAttribute('data-promo-chest-popup-click-handler', '1');

        ensurePopupHost();

        document.addEventListener('click', function(e){
          try{
            var target = e.target || e.srcElement;
            if (!target) return;

            // Ищем ближайший тег <a> с классом promo_chest_item
            var a = target.closest ? target.closest('a.promo_chest_item') : null;
            if (!a) return;

            // На всякий случай не вмешиваемся на страницах активации
            var qsInner = (location.search||'').toLowerCase();
            if (qsInner.indexOf('do=activate') !== -1) return;

            if (e && e.preventDefault) e.preventDefault();
            if (e && e.stopPropagation) e.stopPropagation();

            var href = a.getAttribute('href') || a.href || '';
            if (!href) return;

            var label = '';
            try{
              var title = a.getAttribute('title');
              if (title) label = title;
            }catch(_){ }

            showPopup(label || 'Активация сундука', href);
          }catch(_){ }
        }, true); // capture=true, чтобы успеть перехватить до нативной навигации
      }catch(_){ }
    }

    // Устанавливаем делегированный обработчик один раз
    installDelegatedHandler();
  }catch(e){ }
})();
