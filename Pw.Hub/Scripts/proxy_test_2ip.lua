-- Тест прокси через 2ip.ru для Lua API v2
-- Действия:
-- 1) Создать браузер с прокси user329260:ezmudi@37.221.80.83:4550
-- 2) Перейти на https://2ip.ru
-- 3) Подождать 30 секунд
-- 4) Закрыть браузер

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

-- Обёртки v2 и задержки
local aDelay = function(ms)
  return await(function(k)
    DelayCb(ms, function() k(true) end)
  end)
end

local aCreate = function(options)
  return await(function(k)
    BrowserV2_Create(options or {}, function(h) k(h) end)
  end)
end

local aClose = function(h)
  return await(function(k)
    BrowserV2_Close(h, function(ok) k(ok) end)
  end)
end

local aGo = function(h, url)
  return await(function(k)
    BrowserV2_Navigate(h, url, function(ok) k(ok) end)
  end)
end

-- Параметры теста
local PROXY = 'user329260:ezmudi@37.221.80.83:4550'
local URL = 'https://2ip.ru'

local co = coroutine.create(function()
  Print('Создаю браузер с прокси: '..PROXY)
  local handle = aCreate({ Proxy = PROXY, StartUrl = URL })
  if handle == 0 then
    Print('Не удалось создать браузер')
    return
  end

  -- Дополнительно инициируем навигацию (на случай, если StartUrl не сработал)
  aGo(handle, URL)

  Print('Ожидание 30 секунд для проверки IP на 2ip.ru...')
  aDelay(30000)

  Print('Закрываю браузер...')
  aClose(handle)
  Print('Готово.')
end)

coroutine.resume(co)
