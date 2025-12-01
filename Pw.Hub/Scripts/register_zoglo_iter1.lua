-- Регистрация по рефке Zoglo — итерация 1 (только переходы и выбор способов регистрации)
-- Шаги:
-- 1) Открыть реферальную ссылку на pwonline.ru
-- 2) Нажать на .js-reg (должно открыть account.vkplay.ru)
-- 3) На странице VK ID нажать вторую кнопку из контейнера .ph-form__submit ("Создать новый аккаунт VK ID")
-- 4) На следующей странице нажать кнопку с data-test-id="register"
-- 5) Выбрать способ регистрации: радиокнопка с data-test-id="email-id" ("Почта")

-- Параметры запуска
local REF_URL = 'https://pwonline.ru/static/lp/playnewpw1/?mt_sub1=8356541&mt_click_id=mt-rihxx9-1762538013-2465897769'
-- При необходимости замените прокси на ваш; формат: login:password@ip:port или ip:port
local PROXY = 'user329260:ezmudi@37.221.80.83:4550'

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

-- Обёртки для v2 и утилиты
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

-- Безопасный клик по первому найденному селектору
local function clickSelector(h, sel)
  return aExec(h, [[
    (function(){
      var el = document.querySelector(']]..sel..[[');
      if(!el) return 'not-found';
      el.click();
      return 'ok';
    })();
  ]])
end

-- Безопасный клик по индексу внутри NodeList
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

local co = coroutine.create(function()
  Print('Старт: открываю реферальную ссылку...')
  local handle = aCreate({ Proxy = PROXY, StartUrl = REF_URL })
  if handle == 0 then Print('Не удалось создать браузер'); return end

  -- На всякий случай повторно инициируем переход
  aGo(handle, REF_URL)

  -- Шаг 1: кнопка .js-reg на лендинге pwonline.ru
  Print('Ожидание кнопки .js-reg на pwonline.ru...')
  if not aWaitFor(handle, '.js-reg', 20000) then Print('Не нашли .js-reg'); return end
  local r1 = clickSelector(handle, '.js-reg')
  Print('Клик .js-reg: '..tostring(r1))

  -- Дать время на открытие/переход во "внутреннем" окне WebView2
  aDelay(2000)

  -- Шаг 2: на account.vkplay.ru контейнер .ph-form__submit и в нём вторая кнопка
  Print('Ожидание контейнера .ph-form__submit на account.vkplay.ru...')
  if not aWaitFor(handle, '.ph-form__submit', 30000) then Print('Не нашли .ph-form__submit'); return end
  local r2 = clickNth(handle, '.ph-form__submit button, .ph-form__submit a', 1) -- индекс 1 = вторая кнопка
  Print('Клик по второй кнопке в .ph-form__submit: '..tostring(r2))

  aDelay(1500)

  -- Шаг 3: кнопка data-test-id="register"
  Print('Ожидание кнопки [data-test-id="register"]...')
  if not aWaitFor(handle, '[data-test-id="register"]', 30000) then Print('Не нашли кнопку register'); return end
  local r3 = clickSelector(handle, '[data-test-id="register"]')
  Print('Клик register: '..tostring(r3))

  aDelay(1500)

  -- Шаг 4: выбор способа регистрации — радиокнопка Почта (data-test-id="email-id")
  Print('Ожидание радиокнопки [data-test-id="email-id"] (Почта)...')
  if not aWaitFor(handle, '[data-test-id="email-id"]', 30000) then Print('Не нашли radio email-id'); return end
  local r4 = clickSelector(handle, '[data-test-id="email-id"]')
  Print('Выбор способа "Почта": '..tostring(r4))

  Print('Итерация 1 завершена. Готов к следующему этапу.')

  -- Закрывать браузер сейчас не будем, чтобы вы могли продолжить ручную проверку; при желании раскомментируйте:
  -- aClose(handle)
end)

coroutine.resume(co)
