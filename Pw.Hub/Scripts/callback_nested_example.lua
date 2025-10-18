-- Пример: вызов другой функции внутри callback (вложенные callback'и)
-- Сценарий: переходим на страницу → читаем title через JS → перезагружаем страницу

Print('Starting nested-callback example...')

Browser_NavigateCb('https://pwonline.ru/promo_items.php', function(navigateOk)
  if not navigateOk then
    Print('Navigation failed')
    return
  end
  Print('Navigation requested, executing JS...')

  -- Внутри этого колбэка вызываем другую функцию с колбэком
  Browser_ExecuteScriptCb('document.title', function(title)
    Print('document.title = ' .. tostring(title))

    -- Ещё один вызов функции внутри текущего callback
    Browser_ReloadCb(function(reloaded)
      Print('Reload requested: ' .. tostring(reloaded))
      Print('Nested-callback example finished')
    end)
  end)
end)

-- Альтернативный мини-пример с аккаунтами:
-- Account_GetAccountsCb(function(accounts)
--   Print('Accounts in DB: ' .. tostring(#accounts))
--   -- Здесь внутри callback вызываем другую API-функцию
--   Account_GetAccountCb(function(currentName)
--     Print('Current account: ' .. tostring(currentName))
--   end)
-- end)
