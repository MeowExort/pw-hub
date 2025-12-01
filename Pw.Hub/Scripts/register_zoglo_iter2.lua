-- Регистрация по рефке Zoglo — итерация 2
-- Шаги (сценарий самодостаточный: повторяет путь итерации 1 до формы):
-- 1) Открыть реферальную ссылку на pwonline.ru
-- 2) Нажать на .js-reg (переход на account.vkplay.ru)
-- 3) Нажать вторую кнопку в .ph-form__submit ("Создать новый аккаунт VK ID")
-- 4) Нажать кнопку [data-test-id="register"]
-- 5) Выбрать способ регистрации [data-test-id="email-id"]
-- 6) Ввести почту в input[name="login"] = agit0.o+test1@yandex.ru
-- 7) Нажать кнопку type="submit"
-- 8) Если появилась капча — кликнуть #not-robot-captcha-checkbox
-- 9) Если капчи нет — пользователь ВРУЧНУЮ вводит код из почты (скрипт ждёт и оставляет окно открытым)

-- Параметры запуска (замените при необходимости)
local REF_URL = 'https://pwonline.ru/static/lp/playnewpw1/?mt_sub1=8356541&mt_click_id=mt-rihxx9-1762538013-2465897769'
local PROXY   = 'user329260:ezmudi@37.221.80.83:4550'
local EMAIL   = 'agit0.o+test1@yandex.ru'

-- Настройки и селекторы для капчи во всплывающем iframe (VK ID)
-- При необходимости подстройте список селекторов iframe без правки логики ниже
local CAPTCHA_IFRAME_SELECTORS = {
  'iframe',
  '[data-test-id="captcha-widget"] iframe',
  'iframe[src*="not_robot_captcha"]',
}
local CAPTCHA_CHECKBOX_SELECTOR = '#not-robot-captcha-checkbox'

-- Простая реализация await на корутинах
local await
await = function(starter)
  local co = coroutine.running()
  if not co then error('await must be used inside a coroutine') end
  local resumed = false
  starter(function(...)
    if not resumed then
      resumed = true
      local ok, err = coroutine.resume(co, ...)
      if not ok then Print('resume error: '..tostring(err)) end
    end
  end)
  return coroutine.yield()
end

-- Обёртки v2 и утилиты
local aDelay = function(ms)
  return await(function(k) DelayCb(ms, function() k(true) end) end)
end

local aCreate = function(options)
  return await(function(k) BrowserV2_Create(options or {}, function(h) k(h) end) end)
end

local aClose = function(h)
  return await(function(k) BrowserV2_Close(h, function(ok) k(ok) end) end)
end

local aGo = function(h, url)
  return await(function(k) BrowserV2_Navigate(h, url, function(ok) k(ok) end) end)
end

local aWaitFor = function(h, sel, timeout)
  return await(function(k) BrowserV2_WaitForElement(h, sel, timeout or 20000, function(v) k(v) end) end)
end

local aExec = function(h, js)
  return await(function(k) BrowserV2_ExecuteScript(h, js, function(v) k(v) end) end)
end

local function clickSelector(h, sel)
  return aExec(h, [[
    (function(){
      var el = document.querySelector(']]..sel..[[');
      if(!el) return 'not-found';
      if (el.tagName === 'BUTTON' || el.tagName === 'A' || typeof el.click === 'function') el.click();
      else el.dispatchEvent(new MouseEvent('click', { bubbles:true }));
      return 'ok';
    })();
  ]])
end

-- Дождаться наличия iframe по любому из селекторов
local function waitForIframe(h, selectors, timeout)
  timeout = timeout or 20000
  local started = os.clock()
  while (os.clock() - started) * 1000 < timeout do
    local found = aExec(h, [[
      (function(){
        var sels = ]]..string.format('%q', table.concat(CAPTCHA_IFRAME_SELECTORS, '\n'))..[[.split('\n');
        for (var i=0;i<sels.length;i++){
          var f = document.querySelector(sels[i]);
          if (f) return true;
        }
        return false;
      })();
    ]])
    if tostring(found) == 'true' then return true end
    aDelay(200)
  end
  return false
end

-- Выполнить JS внутри iframe (если same‑origin). Возвращает значение выражения или строку ошибки.
local function aExecInIframe(h, iframeSelector, js)
  local wrapped = [[
    (function(){
      var fr = document.querySelector(']]..iframeSelector..[[');
      if(!fr) return '__NO_IFRAME__';
      try{
        var win = fr.contentWindow; if(!win) return '__NO_WIN__';
        var doc = win.document; if(!doc) return '__NO_DOC__';
        // Выполняем переданный код в контексте фрейма
        return (function(){ ]]..js..[[ })();
      }catch(e){ return '__IFRAME_ERR__:'+ (e && e.message ? e.message : String(e)); }
    })();
  ]]
  return aExec(h, wrapped)
end

-- Дождаться элемента внутри iframe
local function waitForInIframe(h, iframeSel, innerSel, timeout)
  timeout = timeout or 15000
  local started = os.clock()
  while (os.clock() - started) * 1000 < timeout do
    local res = aExecInIframe(h, iframeSel, [[
      var el = document.querySelector(']]..innerSel..[[');
      return !!el;
    ]])
    if tostring(res) == 'true' then return true end
    aDelay(200)
  end
  return false
end

-- Клик по селектору внутри первого найденного iframe из списка
local function clickCaptchaCheckboxInAnyIframe(h)
  for i = 1, #CAPTCHA_IFRAME_SELECTORS do
    local sel = CAPTCHA_IFRAME_SELECTORS[i]
    -- Проверим, что фрейм присутствует
    local hasFrame = aExec(h, [[(function(){return !!document.querySelector(']]..sel..[[');})()]]);
    if tostring(hasFrame) == 'true' then
      -- Дождёмся чекбокс внутри этого фрейма
      local ok = waitForInIframe(h, sel, CAPTCHA_CHECKBOX_SELECTOR, 10000)
      if ok then
        -- Скролл к элементу и клик с ретраями
        for attempt=1,4 do
          local clickRes = aExecInIframe(h, sel, [[
            var el = document.querySelector(']]..CAPTCHA_CHECKBOX_SELECTOR..[[');
            if(!el) return 'not-found';
            try{ el.scrollIntoView({block:'center', inline:'nearest'}); }catch(e){}
            try{
              if (typeof el.click === 'function') el.click();
              else el.dispatchEvent(new MouseEvent('click', {bubbles:true}));
            }catch(e){ return 'err:'+(e && e.message ? e.message : String(e)); }
            return 'ok';
          ]])
          if tostring(clickRes) == 'ok' then
            return true, sel
          end
          aDelay(500)
        end
      end
    end
  end
  return false, nil
end

local function clickNth(h, sel, idx)
  return aExec(h, [[
    (function(){
      var els = document.querySelectorAll(']]..sel..[[');
      var i = ]]..tostring(idx)..[[;
      if(!els || els.length<=i) return 'not-found';
      var target = els[i];
      if(!target) return 'not-found';
      if (target.tagName === 'BUTTON' || target.tagName === 'A' || typeof target.click === 'function') target.click();
      else target.dispatchEvent(new MouseEvent('click', { bubbles:true }));
      return 'ok';
    })();
  ]])
end

-- Ввод текста, совместимый с React/контролируемыми инпутами: посимвольная эмуляция
local function typeInto(h, sel, text)
  text = tostring(text)
  return aExec(h, [[
    (function(){
      var el = document.querySelector(']]..sel..[[');
      if(!el) return 'not-found';
      try { el.scrollIntoView({block:'center', inline:'nearest'}); } catch(e){}
      // Сфокусируем элемент
      try { el.focus(); } catch(e){}

      // Получим нативный setter значения для HTMLInputElement
      var desc = Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, 'value');
      var setter = desc && desc.set ? desc.set : null;
      if (!setter) { el.value = ''; }

      // Очистим текущее значение через нативный setter, чтобы React/Vue поняли изменение
      try { setter && setter.call(el, ''); } catch(e){ try { el.value=''; } catch(_){} }
      try { el.dispatchEvent(new Event('input', {bubbles:true})); } catch(e){}

      // Посимвольно вставляем, генерируя keydown/beforeinput/input/keyup
      var text = ']]..text:gsub("'", "\\'")..[[';
      for (var i=0; i<text.length; i++){
        var ch = text[i];
        try { el.dispatchEvent(new KeyboardEvent('keydown', {key: ch, bubbles:true})); } catch(e){}
        try { el.dispatchEvent(new InputEvent('beforeinput', {inputType:'insertText', data: ch, bubbles:true, cancelable:true})); } catch(e){}
        try {
          if (setter) setter.call(el, (el.value || '') + ch); else el.value = (el.value || '') + ch;
        } catch(e){ try { el.value = (el.value || '') + ch; } catch(_){} }
        try { el.dispatchEvent(new Event('input', {bubbles:true})); } catch(e){}
        try { el.dispatchEvent(new KeyboardEvent('keyup', {key: ch, bubbles:true})); } catch(e){}
      }

      // Сообщим об изменении
      try { el.dispatchEvent(new Event('change', {bubbles:true})); } catch(e){}

      // Вернём фактическое значение, чтобы можно было проверить в логе
      return (el && typeof el.value === 'string') ? ('ok:' + el.value) : 'ok';
    })();
  ]])
end

local co = coroutine.create(function()
  Print('Итерация 2: старт навигации к форме регистрации...')
  local handle = aCreate({ Proxy = PROXY, StartUrl = REF_URL })
  if handle == 0 then Print('Не удалось создать браузер'); return end
  
  aDelay(1500)
  aGo(handle, REF_URL)

  -- Повтор пути из итерации 1
  Print('Ожидаю кнопку .js-reg ...')
  if not aWaitFor(handle, '.js-reg', 20000) then Print('Не нашли .js-reg'); return end
  Print('Клик .js-reg: '..tostring(clickSelector(handle, '.js-reg')))
  aDelay(1500)

  Print('Ожидаю .ph-form__submit ...')
  if not aWaitFor(handle, '.ph-form__submit', 30000) then Print('Не нашли .ph-form__submit'); return end
  Print('Клик второй кнопки в .ph-form__submit: '..tostring(clickNth(handle, '.ph-form__submit button, .ph-form__submit a', 1)))
  aDelay(1200)

  Print('Ожидаю кнопку [data-test-id="register"] ...')
  if not aWaitFor(handle, '[data-test-id="register"]', 30000) then Print('Не нашли кнопку register'); return end
  Print('Клик register: '..tostring(clickSelector(handle, '[data-test-id="register"]')))
  aDelay(1200)

  Print('Ожидаю радиокнопку [data-test-id="email-id"] ...')
  if not aWaitFor(handle, '[data-test-id="email-id"]', 30000) then Print('Не нашли radio email-id'); return end
  Print('Выбор способа "Почта": '..tostring(clickSelector(handle, '[data-test-id="email-id"]')))

  aDelay(500)
  -- Шаг 6: вводим почту
  Print('Ожидаю поле input[name="login"] ...')
  if not aWaitFor(handle, 'input[name="login"]', 30000) then Print('Поле логина не найдено'); return end
  local t1 = typeInto(handle, 'input[name="login"]', EMAIL)
  Print('Ввод почты: '..tostring(t1))
  aDelay(700)

  -- Шаг 7: отправляем форму
  Print('Отправляю форму (submit)...')
  local submitRes = aExec(handle, [[
    (function(){
      // Попробуем кликнуть по активной кнопке сабмита
      var btn = document.querySelector('button[type="submit"]:not([disabled]):not([aria-disabled="true"]), input[type="submit"]:not([disabled]):not([aria-disabled="true"])');
      if (btn) { btn.click(); return 'clicked-btn'; }
      // Фолбэк: отправка ближайшей формы от поля логина
      var el = document.querySelector('input[name="login"]');
      var form = el && (el.form || el.closest && el.closest('form'));
      if (form) {
        if (typeof form.requestSubmit === 'function') { form.requestSubmit(); return 'requestSubmit'; }
        form.submit && form.submit();
        return 'submit';
      }
      return 'no-submit';
    })();
  ]])
  Print('Сабмит формы: '..tostring(submitRes))

  -- Шаг 8: проверка капчи (во всплывающем overlay-iframe)
  Print('[CAPTCHA] Проверяю, появилась ли капча (overlay iframe)...')
  -- Небольшая пауза на появление оверлея
  aDelay(1000)
  local overlayReady = waitForIframe(handle, CAPTCHA_IFRAME_SELECTORS, 12000)
  if overlayReady then
    Print('[CAPTCHA] Найден overlay. Пытаюсь кликнуть чекбокс внутри iframe...')
    local ok, usedFrameSel = clickCaptchaCheckboxInAnyIframe(handle)
    if ok then
      Print('[CAPTCHA] Чекбокс нажат (iframe: '..tostring(usedFrameSel)..'). Жду обработки...')
      -- Ждём исчезновения overlay или появления следующего шага
      local startT = os.clock()
      while (os.clock() - startT) * 1000 < 15000 do
        local stillThere = aExec(handle, [[
          (function(){
            var sels = ]]..string.format('%q', table.concat(CAPTCHA_IFRAME_SELECTORS, '\n'))..[[.split('\n');
            for (var i=0;i<sels.length;i++){ if (document.querySelector(sels[i])) return true; }
            return false;
          })();
        ]])
        if tostring(stillThere) ~= 'true' then break end
        aDelay(400)
      end
    else
      Print('[CAPTCHA] Не удалось нажать чекбокс в iframe. Оставляю окно для ручного решения.')
      aDelay(120000)
    end
  else
    Print('[CAPTCHA] Overlay не обнаружен. Возможно, капчи нет. Ожидается код подтверждения на почту: '..EMAIL)
    Print('[CAPTCHA] Введите код вручную в открытом окне. Скрипт подождёт 2 минуты...')
    aDelay(120000)
  end

  Print('Итерация 2 завершена. Браузер оставлен открытым для дальнейших действий.')
  -- При необходимости можно закрыть автоматически:
  -- aClose(handle)
end)

coroutine.resume(co)
