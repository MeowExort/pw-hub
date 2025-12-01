-- =============================================================
-- ДЕКЛАРАЦИИ (для взаимных вызовов)
-- =============================================================
local Pfx
local safeLen, toAccountId, normalizeAccounts
local await, aCreate, aClose, aNav, aWait, aExists, aExec, aChange
local ensureAuthorized, processAccount, groupMain, startGroup
-- Имена
local nameById, seedNamesFromArgs, nameOf
-- Прогресс
local progress, reportInit, report
-- ETA/ограничение темпа
local fmtDuration
-- Завершение
local _reported, TryFinish
-- Распознавание текстов ошибок
local isAlreadyActivated

-- Входные данные (из args)
local accounts_raw = (args and args.Accounts) or {}
local windows = tonumber(args and args.Windows or 1) or 1
if windows < 1 then windows = 1 end
local promo = tostring(args and args.PromoCode or '')

local MAX_RETRY_ATTEMPTS = 10                   -- максимум попыток для контролируемых ошибок
local RETRY_DELAY_MIN_MS = 1000                 -- минимальная задержка между повторами (мс)
local RETRY_DELAY_MAX_MS = 3000                 -- максимальная задержка между повторами (мс)
local RECREATE_EVERY_ACCOUNTS = 5               -- пересоздание браузера каждые N аккаунтов
local RECREATE_AFTER_503_WAIT_MS = 30000        -- ожидание перед пересозданием браузера после 503 (мс)
local PACE_SEC_PER_ACCOUNT = 20                 -- лимит темпа на корутину: 3 аккаунта/мин (каждые 20 сек)

-- Константы/JS
local JS_IS_503, JS_WAIT_READY, JS_CLICK_PIN_PRIMARY, JS_READ_ERROR_TEXT, JS_READ_ERRORS_AGG
-- Вспомогательные JS/утилиты для поля #pin
local jsEscape, setPinValue, readErrorText
-- Нормализация строк
local _normText

-- Глобальные результаты и счётчик активных групп
local results = {}
local pending = 0

-- =============================================================
-- РЕАЛИЗАЦИИ (ПРИСВАИВАНИЯ)
-- =============================================================
Pfx = function(...) local b={} for i=1,select('#', ...) do b[i]=tostring(select(i,...)) end Print(table.concat(b, ' ')) end

-- Инициализация ГПСЧ (для джиттера ожиданий)
do
  if not _RANDOMSEEDED then
    _RANDOMSEEDED = true
    local seed = os.time() + tonumber(tostring({}):gsub("[^0-9]", ""))
    math.randomseed(seed)
    -- отбросить первые значения для лучшей рандомизации
    math.random(); math.random(); math.random()
  end
end

-- JS утилиты
JS_IS_503 = [[(function(){try{var t=(document.title||'')+' '+(document.body&&document.body.innerText||'');t=t.toLowerCase();return t.indexOf('503')>=0;}catch(e){}return false;})()]]
JS_WAIT_READY = [[(function(){return document.readyState;})()]]
JS_CLICK_PIN_PRIMARY = [[(function(){try{var b=document.querySelector("button[type='submit'], input[type='submit']");if(b){b.click();return 'submit_clicked';}}catch(e){}return 'submit_not_found';})()]]
JS_READ_ERROR_TEXT = [[(function(){try{var el=document.querySelector('.m_error');return el?(el.innerText||''):'';}catch(e){return '';} })()]]
-- Аггрегированный сбор текста ошибок из нескольких возможных селекторов
JS_READ_ERRORS_AGG = [[(function(){
  try{
    var sels = [
      '.m_error',
      '.alert-danger',
      '.alert.error',
      '.alert',
      '.error',
      '.validation-summary-errors',
      '.pin_code_input .error',
      '.message.error',
      '.modal .m_error',
      '.modal .alert',
      '#error_message',
      '.pin_error',
      '.pin-result .error'
    ];
    var texts = [];
    for (var i=0;i<sels.length;i++){
      var list = document.querySelectorAll(sels[i]);
      if(!list) continue;
      for (var j=0;j<list.length;j++){
        var t = (list[j].innerText||'').trim();
        if (t) texts.push(t);
      }
    }
    return texts.join(' | ');
  }catch(e){return ''}
})()]]

-- Нормализация русскоязычных текстов ошибок для устойчивого сравнения
_normText = function(s)
  s = tostring(s or ''):lower()
  -- удалить символы-плейсхолдеры/мусор: unicode replacement char и т.п.
  s = s:gsub('�+', '')            -- U+FFFD (replacement character) может сыпаться из-за кодировки
  -- удалить управляющие ascii-символы (CR/LF/TAB и пр.), оставить пробелы схлопыванием ниже
  s = s:gsub('[%z\1-\31\127]', '')
  -- нормализовать тире: разные виды -> обычный дефис
  s = s:gsub('[–—−]', '-')
  -- убрать артефакты вида "�-" или "-�" возникшие при битых символах рядом с дефисом
  s = s:gsub('�%-', '')
  s = s:gsub('%-�', '')
  -- унифицировать букву "ё" -> "е" (иногда попадается разнобой)
  s = s:gsub('ё', 'е')
  -- схлопнуть пробелы/переводы строк до одного пробела
  s = s:gsub('%s+', ' ')
  -- обрезать края
  s = s:match('^%s*(.-)%s*$') or s
  return s
end

-- Проверка: сообщение об уже активированном пин-коде считаем успехом
isAlreadyActivated = function(text)
  local s = _normText(text)
  local phrases = {
    'вы уже активировали этот пин-код',
    'вы уже активировали пин-код',
    'этот пин-код уже активирован',
    'пин-код уже активирован',
    -- варианты без дефиса
    'вы уже активировали этот пин код',
    'этот пин код уже активирован',
    'пин код уже активирован',
    -- укороченный общий маркер
    'уже активирован'
  }
  for _,p in ipairs(phrases) do
    if string.find(s, p, 1, true) then return true end
  end
  -- Дополнительная эвристика на случай битых символов: ищем ключевые основы слов
  -- Требуем одновременно признак "уже актив" и наличие упоминания пин-кода
  if (string.find(s, 'уже актив', 1, true) or string.find(s, 'уже активир', 1, true)) then
    if string.find(s, 'пин-код', 1, true) or (string.find(s, 'пин', 1, true) and string.find(s, 'код', 1, true)) then
      return true
    end
  end
  return false
end

-- Экранирование строки для встраивания в JS одинарных кавычек и обратных слешей
jsEscape = function(s)
  s = tostring(s or '')
  s = s:gsub('\\', '\\\\')
  s = s:gsub("'", "\\'")
  return s
end

-- Установить значение в поле ввода промокода (#pin) и сгенерировать события input/change
setPinValue = function(handle, value)
  local v = jsEscape(value)
  local js = [[(function(){try{
var el=document.querySelector('#pin');
if(!el){var cand=document.querySelector('.pin_code_input input, input[name="pin"]'); if(cand) el=cand;}
if(!el) return 'pin_not_found';
el.focus(); el.value='__VAL__';
el.dispatchEvent(new Event('input',{bubbles:true}));
el.dispatchEvent(new Event('change',{bubbles:true}));
return 'pin_set';}catch(e){return 'pin_exc';}})()]]
  js = js:gsub('__VAL__', v)
  return tostring(aExec(handle, js) or '')
end

-- Прочитать текст ошибки с экрана (аггрегировано из разных мест)
readErrorText = function(handle)
  local txt = tostring(aExec(handle, JS_READ_ERRORS_AGG) or '')
  if not txt or txt == '' then
    txt = tostring(aExec(handle, JS_READ_ERROR_TEXT) or '')
  end
  return txt or ''
end

-- Безопасная длина массива
safeLen = function(t) if type(t)~='table' then return 0 end local n=0 for k,_ in pairs(t) do if type(k)=='number' and k>0 and k%1==0 and k>n then n=k end end return n end

-- Имена
nameById = {}
seedNamesFromArgs = function(list)
  if type(list)~='table' then return end
  for _,v in pairs(list) do
    if type(v)=='table' then
      local id = (type(v.Id)=='string' and v.Id) or nil
      local nm = (v.Name and tostring(v.Name)) or ''
      if id and id~='' and nm~='' then nameById[id]=nm end
    end
  end
end
nameOf = function(id) local nm=nameById[id]; return (nm and nm~='' and nm) or id end

-- Нормализация аккаунтов -> массив Id
toAccountId = function(x) if type(x)=='string' then return x end if type(x)=='table' and x.Id~=nil then return tostring(x.Id) end return nil end
normalizeAccounts = function(list) local out,i={},1 if type(list)~='table' then return out end for _,v in pairs(list) do local id=toAccountId(v); if id and id~='' then out[i]=id; i=i+1 end end return out end

-- Прогресс
progress = { total = 0, done = 0, last = -1 }
reportInit = function(total, win)
  progress.total = total or 0
  progress.done = 0
  progress.last = -1
  -- Используем общий репорт, чтобы включить «примерно осталось …»
  report('Старт: аккаунтов '..tostring(progress.total)..', окон '..tostring(win or 1), true)
end
-- Форматирование длительности в компактный вид (часы/мин/сек)
fmtDuration = function(sec)
  sec = tonumber(sec) or 0
  if sec < 0 then sec = 0 end
  local s = math.floor(sec % 60)
  local m = math.floor((sec / 60) % 60)
  local h = math.floor(sec / 3600)
  if h > 0 then return string.format('%dч %dм', h, m) end
  if m > 0 then return string.format('%dм %dс', m, s) end
  return string.format('%dс', s)
end

report = function(message, force)
  local pct = 0
  if progress.total > 0 then pct = math.floor((progress.done / progress.total) * 100) end
  -- Примерная оценка оставшегося времени: равномерный темп 20с/аккаунт на корутину
  local windowsCount = tonumber(windows or 1) or 1
  if windowsCount < 1 then windowsCount = 1 end
  local remaining = math.max(0, (progress.total - progress.done))
  local approxRemainSec = math.ceil(remaining / windowsCount) * PACE_SEC_PER_ACCOUNT
  local suffix = ''
  if remaining > 0 then
    suffix = ' (примерно осталось '..fmtDuration(approxRemainSec)..')'
  else
    suffix = ''
  end
  local msg = (message or '')..suffix
  if force or pct ~= progress.last then
    ReportProgressMsg(pct, msg)
    progress.last = pct
  else
  -- если нужно писать каждое сообщение, раскомментируйте
  -- ReportProgressMsg(pct, msg)
  end
end

-- await-«обёртки»
await = function(starter) local co=coroutine.running(); if not co then error('await must be used inside a coroutine') end local resumed=false starter(function(...) if not resumed then resumed=true; local ok,err=coroutine.resume(co, ...); if not ok then Pfx('resume error:', tostring(err)) end end end) return coroutine.yield() end
aCreate=function(options) return await(function(k) BrowserV2_Create(options or {}, function(h) k(h) end) end) end
aClose=function(handle) return await(function(k) BrowserV2_Close(handle, function(ok) k(ok==true) end) end) end
aNav=function(handle,url) return await(function(k) BrowserV2_Navigate(handle,url,function(ok) k(ok==true) end) end) end
aWait=function(handle,sel,ms) return await(function(k) BrowserV2_WaitForElement(handle,sel,ms or 5000,function(f) k(f==true) end) end) end
aExists=function(handle,sel) return await(function(k) BrowserV2_ElementExists(handle,sel,function(e) k(e==true) end) end) end
aExec=function(handle,js) return await(function(k) BrowserV2_ExecuteScript(handle,js,function(res) k(res) end) end) end
aChange=function(handle,accId) return await(function(k) BrowserV2_ChangeAccount(handle,accId,function(ok) k(ok==true) end) end) end
aDelay=function(timeMs) return await(function(k) DelayCb(timeMs,function() k(true) end) end) end

-- Завершение (одноразовое)
_reported = false
TryFinish = function() if _reported then return end _reported = true DelayCb(0, function() Complete('ok') end) end

-- Проверка авторизации
ensureAuthorized = function(handle) aNav(handle,'https://pwonline.ru/'); return aWait(handle,'.main_menu',30000) end

-- Обработка одного аккаунта
processAccount = function(handle, accountId, promoCode)
  if not aChange(handle, accountId) then return { status='ошибка', error='не удалось сменить аккаунт' } end
  if not ensureAuthorized(handle) then aChange(handle, accountId); if not ensureAuthorized(handle) then return { status='ошибка', error='не авторизован (таймаут)' } end end
  local url='https://pwonline.ru/pin/'..promoCode
  aNav(handle, url)
  if not aWait(handle, '.pin_code_input', 30000) then return { status='ошибка', error='кнопка .pin_code_input не найдена (таймаут)' } end

  -- Предварительная проверка: если сообщение уже отображается (например, осталось от прошлого сабмита)
  do
    local preErr = readErrorText(handle)
    if preErr and preErr ~= '' and isAlreadyActivated(preErr) then
      return { status='успешно', comment='уже активирован ранее' }
    end
  end

  local lastError = 'не удалось определить результат'
  for attempt=1,MAX_RETRY_ATTEMPTS do
    -- Ранняя проверка перед новой попыткой: если на странице уже есть сообщение «уже активирован» — не кликаем, выходим
    local errPre = readErrorText(handle)
    if errPre and errPre ~= '' and isAlreadyActivated(errPre) then
      return { status='успешно', comment='уже активирован ранее' }
    end
    -- ВАЖНО: после ошибки (и вообще перед каждой попыткой) вбиваем промокод в поле #pin
    local setRes = setPinValue(handle, promoCode)
    if setRes ~= 'pin_set' then
      -- если поле не найдено, подождём чуть-чуть (возможен ререндер) и попробуем ещё раз один раз локально
      aDelay(150)
      setRes = setPinValue(handle, promoCode)
    end

    local r1=tostring(aExec(handle, JS_CLICK_PIN_PRIMARY) or '')
    -- Исправление сравнения: ожидаем 'submit_clicked'
    -- повторный клик пробуем на каждой попытке
    if r1 ~= 'submit_clicked' then
      -- кнопка не найдена/не нажата — это тоже поводовторить
    end

    aExec(handle, JS_WAIT_READY)
    aDelay(700)

    local is503=tostring(aExec(handle, JS_IS_503) or ''):lower()
    if is503=='true' then
      return { status='ошибка 503' }
    end

    if aExists(handle, '.pin_bonuses_list') then
      return { status='успешно' }
    end

    local errTxt = readErrorText(handle)

    local errLower = (errTxt or ''):lower()
    if errLower ~= '' then
      -- Особый случай: уже активирован — считаем успехом
      if isAlreadyActivated(errTxt) then
        return { status='успешно', comment='уже активирован ранее' }
      end
      -- Контролируемая ошибка — пробуем ещё раз до лимита
      lastError = errTxt
    else
      -- Иногда UI обновляется с задержкой
      aDelay(1000)
      if aExists(handle, '.pin_bonuses_list') then return { status='успешно' } end
      if aExists(handle, '.m_error') or true then
        errTxt = readErrorText(handle)
        errLower = (errTxt or ''):lower()
        if isAlreadyActivated(errTxt) then
          return { status='успешно', comment='уже активирован ранее' }
        end
        lastError = errTxt ~= '' and errTxt or lastError
      end
    end

    -- Если это не была 503 и не успех, ждём случайный интервал 1–3 сек и повторяем, пока не исчерпаны попытки
    if attempt < MAX_RETRY_ATTEMPTS then
      local minMs = tonumber(RETRY_DELAY_MIN_MS) or 1000
      local maxMs = tonumber(RETRY_DELAY_MAX_MS) or 3000
      if maxMs < minMs then maxMs = minMs end
      local jitter = minMs
      local span = maxMs - minMs
      if span > 0 then jitter = minMs + math.random(0, span) end
      aDelay(jitter)
    end
  end

  -- Лимит исчерпан — возвращаем последнюю ошибку
  return { status='ошибка', error = lastError }
end

-- Тело корутины для одной группы
groupMain = function(groupIndex, accountIds)
  local handle = aCreate({})
  aDelay(1000)
  if (not handle) or tonumber(handle)==0 then
    Pfx('[Группа #',groupIndex,'] не удалось создать браузер')
    for _,accId in ipairs(accountIds) do results[accId]={ status='ошибка', error='браузер не создан' } end
    return
  end
  Pfx('[Группа #',groupIndex,'] handle =', handle)
  Pfx('[Группа #',groupIndex,'] лимит темпа: 3 аккаунта/мин (шаг ', PACE_SEC_PER_ACCOUNT, 'с)')
  local groupStartTs = os.time()
  report('Группа #'..tostring(groupIndex)..' стартовала ('..tostring(#accountIds)..' акк.)', false)
  for i=1,#accountIds do
    -- Равномерное распределение: старт обработки i-го аккаунта не раньше расписания (каждые 20 секунд)
    do
      local targetTs = groupStartTs + (i-1) * PACE_SEC_PER_ACCOUNT
      local now = os.time()
      if now < targetTs then
        local waitMs = (targetTs - now) * 1000
        if waitMs > 0 then aDelay(waitMs) end
      end
    end
    -- Пересоздаём окно браузера каждые RECREATE_EVERY_ACCOUNTS аккаунтов
    if i > 1 and ((i-1) % RECREATE_EVERY_ACCOUNTS) == 0 then
      local oldHandle = handle
      Pfx('[Группа #',groupIndex,'] достигнут порог ', RECREATE_EVERY_ACCOUNTS, ' аккаунтов, пересоздаю браузер...')
      local newHandle = aCreate({})
      aDelay(700)
      if newHandle and tonumber(newHandle) ~= 0 then
        handle = newHandle
        Pfx('[Группа #',groupIndex,'] новый handle =', handle, '; закрываю старый =', oldHandle)
        pcall(aClose, oldHandle)
      else
        Pfx('[Группа #',groupIndex,'] не удалось пересоздать браузер — продолжаю со старым окном')
      end
    end

    local accId = tostring(accountIds[i])
    local who = nameOf(accId)
    Pfx('[Группа #',groupIndex,'] аккаунт:', who, '(', i, '/', #accountIds, ')')

    -- Цикл повтора обработки одного аккаунта, используется при 503 (пересоздаём браузер и пробуем снова)
    local processed = false
    while not processed do
      local ok,res = pcall(processAccount, handle, accId, promo)
      if not ok then res = { status='ошибка', error=('lua error: '..tostring(res)) } end

      if res and res.status=='ошибка 503' then
        -- 503: подождать 30 секунд и пересоздать браузер, затем повторить ЭТОТ ЖЕ аккаунт в новом окне
        Pfx('[Группа #',groupIndex,'] получена ошибка 503 — ждём ', math.floor((RECREATE_AFTER_503_WAIT_MS or 30000)/1000), ' сек и пересоздаём браузер (аккаунт: ', who, ')')
        aDelay(RECREATE_AFTER_503_WAIT_MS or 30000)
        local oldHandle = handle
        local newHandle = aCreate({})
        aDelay(1000)
        if newHandle and tonumber(newHandle) ~= 0 then
          handle = newHandle
          pcall(aClose, oldHandle)
        else
          Pfx('[Группа #',groupIndex,'] не удалось создать новый браузер для повтора после 503 — продолжаю со старым')
        end
        -- повторяем цикл без увеличения прогресса
      else
        -- Не 503 — фиксируем результат и выходим из цикла для этого аккаунта
        results[accId] = res or { status='ошибка', error='пустой результат' }
        -- ПРОГРЕСС: шаг готов
        progress.done = math.min(progress.done + 1, progress.total)
        local msg
        if res and res.status then
          if res.status=='успешно' then
            if res.comment and tostring(res.comment)~='' then
              msg = who..': успешно — '..tostring(res.comment)
            else
              msg = who..': успешно'
            end
          elseif res.status=='ошибка' then msg = who..': ошибка — '..(res.error or '')
          else msg = who..': '..tostring(res.status) end
        else
          msg = who..': нет результата'
        end
        report(msg, false)
        -- лёгкая пауза между аккаунтами
        aDelay(300)
        processed = true
      end
    end
  end
  Pfx('[Группа #',groupIndex,'] завершилась, закрываю браузер')
  aClose(handle)
end

-- Запуск корутины одной группы и финализация отчёта
startGroup = function(groupIndex, accountIds)
  local co = coroutine.create(function()
    groupMain(groupIndex, accountIds)
    -- Группа завершилась
    pending = pending - 1
    if pending <= 0 then
    -- Допечатаем финальный отчёт и завершим прогресс (100%)
      local accounts = normalizeAccounts(accounts_raw)
      local okCount,e503,errCount = 0,0,0
      for _, accId in ipairs(accounts) do
        local r = results[accId]
        if r then
          if r.status=='успешно' then okCount=okCount+1
          elseif r.status=='ошибка 503' then e503=e503+1
          elseif r.status=='ошибка' then errCount=errCount+1 end
        end
      end
      -- гарантированно 100%
      progress.done = progress.total
      report('Готово: успешно='..okCount..', 503='..e503..', ошибок='..errCount, true)

      Pfx('===== Отчёт по вводу промокода =====')
      Pfx('Всего аккаунтов:', #accounts)
      Pfx('Успешно:', okCount)
      Pfx('Ошибка 503:', e503)
      Pfx('Ошибок:', errCount)
      for _, accId in ipairs(accounts) do
        local r = results[accId]
        local who = nameOf(accId)
        if r then
          if r.status=='успешно' then
            if r.comment and tostring(r.comment)~='' then
              Pfx(who, ': успешно — ', r.comment)
            else
              Pfx(who, ': успешно')
            end
          elseif r.status=='ошибка 503' then Pfx(who, ': ошибка 503')
          elseif r.status=='ошибка' then Pfx(who, ': ошибка — ', r.error or '')
          else Pfx(who, ': нет результата') end
        else
          Pfx(who, ': нет результата')
        end
      end
      TryFinish()
    end
  end)
  local ok,err = coroutine.resume(co); if not ok then Pfx('Coroutine start error:', tostring(err)) end
end

-- ===============================
-- ОСНОВНОЙ ПОТОК ЗАПУСКА
-- ===============================
seedNamesFromArgs(accounts_raw)
local accounts = normalizeAccounts(accounts_raw)
Pfx('accounts normalized:', #accounts)
Pfx('windows:', windows)
Pfx('promo:', promo)
if #accounts==0 then Pfx('Нет аккаунтов.'); ReportProgressMsg(0, 'Нет аккаунтов'); TryFinish(); return end
if promo=='' then Pfx('Промокод пуст.'); ReportProgressMsg(0, 'Пустой промокод'); TryFinish(); return end

-- Разброс аккаунтов по окнам (ровно N групп)
local groups = {}; for i=1,windows do groups[i]={} end
for i=1,#accounts do local g=((i-1)%windows)+1; table.insert(groups[g], accounts[i]) end

-- Инициализация прогресса
progress.total = #accounts
reportInit(progress.total, windows)

-- Примерное общее время активации (учитывая параллелизм окон и лимит 3 акк/мин на корутину)
do
  local win = tonumber(windows or 1) or 1
  if win < 1 then win = 1 end
  local batches = math.ceil(#accounts / win)
  local estimateSec = batches * PACE_SEC_PER_ACCOUNT
  Pfx('Примерное время активации: ~', fmtDuration(estimateSec))
end

-- Подсчёт активных групп и запуск корутин
pending = 0; for gi=1,#groups do if #groups[gi]>0 then pending=pending+1 end end
Pfx('groups:', #groups, 'pending:', pending)
if pending==0 then Pfx('Пустые группы.'); ReportProgressMsg(100, 'Готово (пусто)'); TryFinish(); return end
for gi=1,#groups do local grp = groups[gi]; if #grp>0 then startGroup(gi, grp) end end