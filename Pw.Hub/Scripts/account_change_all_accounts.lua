-- Пример: пройти по всем аккаунтам и по очереди переключиться на каждый
-- Используются callback-версии API, чтобы не блокировать UI.

local function changeToAccountById(accountId, onDone)
  Account_ChangeAccountCb(tostring(accountId), function(ok)
    if not ok then
      Print('Не удалось переключиться на аккаунт: ' .. tostring(accountId))
      if onDone ~= nil then onDone(false) end
      return
    end
    -- Подтверждаем, что теперь активен нужный аккаунт
    Account_GetAccountCb(function(name)
      Print('Сейчас активен аккаунт: ' .. tostring(name))
      if onDone ~= nil then onDone(true) end
    end)
  end)
end

-- Последовательно обрабатываем элементы таблицы accounts
local function processAccounts(accounts, index)
  index = index or 1
  local acc = accounts[index]
  if acc == nil then
    Print('Проход по аккаунтам завершён. Всего: ' .. tostring(index - 1))
    return
  end

  local id = tostring(acc.Id)
  local name = tostring(acc.Name)
  Print(string.format('(%d) Переключаюсь на: %s [%s]', index, name, id))
  changeToAccountById(id, function(_)
    -- Переходим к следующему аккаунту после завершения
    processAccounts(accounts, index + 1)
  end)
end

-- Старт: сначала покажем текущий аккаунт, затем получим весь список и начнём обход
Account_GetAccountCb(function(current)
  if current == nil or current == '' then
    Print('Текущий аккаунт не определён (возможно, не авторизованы)')
  else
    Print('Текущий аккаунт перед стартом: ' .. tostring(current))
  end

  Account_GetAccountsCb(function(accounts)
    if accounts == nil or #accounts == 0 then
      Print('Список аккаунтов пуст')
      return
    end

    Print('Найдено аккаунтов: ' .. tostring(#accounts))
    processAccounts(accounts, 1)
  end)
end)
