-- Модуль ввода промокода на https://pwonline.ru/pin.php (callback version)
-- Требуется один входной параметр: args["промокод"]

local function trim(s)
  if s == nil then return "" end
  return (tostring(s):gsub('^%s*(.-)%s*$', '%1'))
end

local promocode = trim(args and args["промокод"]) or ""
if promocode == "" then
  if Complete ~= nil then Complete("Не задан промокод") end
  return
end

local total = 0
local success = 0
local failed = 0
local messages = {}
local total_accounts = 0

local function buildAndFinish()
  local summary = string.format("Промокод: %s\nВсего: %d, Успешно: %d, Ошибки: %d", promocode, total, success, failed)
  local result = summary
  if #messages > 0 then
    result = result .. "\n" .. table.concat(messages, "\n")
  end
  if ReportProgress ~= nil or ReportProgressMsg ~= nil then
    if ReportProgress ~= nil then ReportProgress(100) end
    if ReportProgressMsg ~= nil then ReportProgressMsg(100, "Готово") end
  end
  if Complete ~= nil then Complete(result) end
end

local function processAccountAt(accounts, index)
  if index > #accounts then
    buildAndFinish()
    return
  end

  local acc = accounts[index]
  if acc == nil or acc.Id == nil then
    processAccountAt(accounts, index + 1)
    return
  end

  total = total + 1
  local accName = trim(acc.Name)
  local idStr = tostring(acc.Id)
  local id = idStr:gsub(":(.*)%s.*$","%1")
  if accName == "" then accName = tostring(id) end
  if Print ~= nil then Print(string.format("[%d/%d] %s", index, total_accounts > 0 and total_accounts or #accounts, accName)) end

  -- 1) Переключаемся на аккаунт
  Account_ChangeAccountCb(tostring(id), function(_)
    -- 2) Открываем страницу пинов
    Browser_NavigateCb("https://pwonline.ru/pin.php", function(_)
      -- 3) Ждем поле ввода
      Browser_WaitForElementCb(".pin_input > input", 2000, function(ok)
        if not ok then
          local msg = accName .. ": не найдена форма ввода"
          table.insert(messages, msg)
          if Print ~= nil then Print(msg) end
          failed = failed + 1
          local done = index
          local totalc = total_accounts > 0 and total_accounts or #accounts
          local percent = math.floor((done / math.max(totalc,1)) * 100)
          if ReportProgressMsg ~= nil then ReportProgressMsg(percent, string.format("%d/%d: %s", done, totalc, msg)) elseif ReportProgress ~= nil then ReportProgress(percent) end
          DelayCb(1000, function()
            processAccountAt(accounts, index + 1)
          end)
          return
        end

        -- 4) Заполняем значение и отправляем форму
        local js = "" ..
          "(function(){" ..
          "var input=document.querySelector('.pin_input > input');" ..
          "if(!input){return 'no_input';}" ..
          "input.value='" .. promocode:gsub("'", "\\'") .. "';" ..
          "var form = input.closest('form');" ..
          "var btn = document.querySelector('button[type=submit],input[type=submit]');" ..
          "if(btn){btn.click(); return 'clicked';}" ..
          "if(form){form.submit(); return 'submitted';}" ..
          "return 'no_submit';" ..
          "})();"

        Browser_ExecuteScriptCb(js, function(_)
          -- 5) Ждем появления сообщения
          Browser_WaitForElementCb(".m_error", 2000, function(_)
            Browser_ExecuteScriptCb([[ (function(){ var el=document.querySelector('.m_error'); return el? (el.textContent||'').trim() : ''; })(); ]], function(raw)
              local msg = trim(raw)
              local tolog = nil
              if msg == "" then
                tolog = accName .. ": отправлено"
                table.insert(messages, tolog)
                success = success + 1
              else
                tolog = accName .. ": " .. msg
                table.insert(messages, tolog)
                local low = string.lower(msg)
                if string.find(low, "успеш") then
                  success = success + 1
                else
                  failed = failed + 1
                end
              end
              if Print ~= nil then Print(tolog) end
              local done = index
              local totalc = total_accounts > 0 and total_accounts or #accounts
              local percent = math.floor((done / math.max(totalc,1)) * 100)
              if ReportProgressMsg ~= nil then ReportProgressMsg(percent, string.format("%d/%d: %s", done, totalc, tolog)) elseif ReportProgress ~= nil then ReportProgress(percent) end
              DelayCb(1000, function()
                processAccountAt(accounts, index + 1)
              end)
            end)
          end)
        end)
      end)
    end)
  end)
end

-- Старт: получаем список аккаунтов через callback API
Account_GetAccountsCb(function(accounts)
  -- accounts приходит в виде Lua-таблицы (1..n)
  processAccountAt(accounts, 1)
end)
