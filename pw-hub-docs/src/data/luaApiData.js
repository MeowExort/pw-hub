export const luaApiData = {
    account: [
        {
            name: 'Account_GetAccountCb',
            category: 'Account',
            page: 'account',
            signature: 'Account_GetAccountCb(callback)',
            description: 'Асинхронно получает логин текущего авторизованного аккаунта. Функция выполняется в фоновом режиме и возвращает результат через callback.',
            parameters: [
                {
                    name: 'callback',
                    type: 'function',
                    description: 'Функция обратного вызова, которая получит логин аккаунта в виде строки'
                }
            ],
            returns: 'nil',
            example: `-- Получить текущий аккаунт
Account_GetAccountCb(function(accountName)
  if accountName and accountName ~= "" then
    Print("Текущий аккаунт: " .. accountName)
  else
    Print("Аккаунт не авторизован")
  end
end)`,
            notes: 'Функция работает асинхронно и не блокирует выполнение скрипта. Всегда проверяйте результат на пустоту.'
        },
        {
            name: 'Account_IsAuthorizedCb',
            category: 'Account',
            page: 'account',
            signature: 'Account_IsAuthorizedCb(callback)',
            description: 'Проверяет, авторизован ли текущий пользователь на сайте Perfect World.',
            parameters: [
                {
                    name: 'callback',
                    type: 'function',
                    description: 'Функция обратного вызова, которая получит boolean значение (true/false)'
                }
            ],
            returns: 'nil',
            example: `-- Проверить авторизацию
Account_IsAuthorizedCb(function(isAuthorized)
  if isAuthorized then
    Print("✅ Пользователь авторизован")
    -- Можно выполнять действия для авторизованного пользователя
    Account_GetAccountCb(function(account)
      Print("Добро пожаловать, " .. account)
    end)
  else
    Print("❌ Требуется авторизация")
    -- Здесь можно выполнить переход на страницу логина
  end
end)`
        },
        {
            name: 'Account_GetAccountsCb',
            category: 'Account',
            page: 'account',
            signature: 'Account_GetAccountsCb(callback)',
            description: 'Получает список всех аккаунтов, зарегистрированных в системе. Возвращает таблицу с объектами аккаунтов.',
            parameters: [
                {
                    name: 'callback',
                    type: 'function',
                    description: 'Функция обратного вызова, которая получит таблицу с аккаунтами'
                }
            ],
            returns: 'nil',
            example: `-- Получить все аккаунты
Account_GetAccountsCb(function(accounts)
  local totalAccounts = #accounts
  Print("Найдено аккаунтов: " .. totalAccounts)
  
  -- Перебор всех аккаунтов
  for i, account in ipairs(accounts) do
    Print(string.format("Аккаунт %d: %s (ID: %s)", i, account.Name, account.Id))
    
    -- Можно также получить информацию о серверах и персонажах
    if account.Servers then
      for j, server in ipairs(account.Servers) do
        Print("  Сервер: " .. server.Name)
        if server.Characters then
          for k, character in ipairs(server.Characters) do
            Print("    Персонаж: " .. character.Name .. " Уровень: " .. character.Level)
          end
        end
      end
    end
  end
end)`,
            notes: 'Каждый объект аккаунта содержит поля: Id, Name, Servers, Characters и другие.'
        },
        {
            name: 'Account_ChangeAccountCb',
            category: 'Account',
            page: 'account',
            signature: 'Account_ChangeAccountCb(accountId, callback)',
            description: 'Переключает текущий активный аккаунт на указанный. Функция загружает cookies и выполняет перезагрузку страницы.',
            parameters: [
                {
                    name: 'accountId',
                    type: 'string',
                    description: 'UUID аккаунта в формате строки'
                },
                {
                    name: 'callback',
                    type: 'function',
                    description: 'Функция обратного вызова, которая получит boolean результат операции'
                }
            ],
            returns: 'nil',
            example: `-- Переключиться на другой аккаунт
local targetAccountId = "3fa85f64-5717-4562-b3fc-2c963f66afa6"

Print("Начинаем переключение аккаунта...")
Account_ChangeAccountCb(targetAccountId, function(success)
  if success then
    Print("✅ Аккаунт успешно переключен")
    
    -- Проверим авторизацию после переключения
    Account_IsAuthorizedCb(function(isAuth)
      if isAuth then
        Print("✅ Авторизация подтверждена")
        -- Продолжаем выполнение скрипта
      else
        Print("❌ Ошибка авторизации после переключения")
      end
    end)
  else
    Print("❌ Ошибка переключения аккаунта")
    -- Можно попробовать повторить или выбрать другой аккаунт
  end
end)`,
            notes: 'Перед переключением сохраняет cookies текущего аккаунта. Рекомендуется проверять авторизацию после переключения.'
        }
    ],
    browser: [
        {
            name: 'Browser_NavigateCb',
            category: 'Browser',
            page: 'browser',
            signature: 'Browser_NavigateCb(url, callback)',
            description: 'Переходит по указанному URL в веб-браузере. Поддерживает только домен pwonline.ru.',
            parameters: [
                {
                    name: 'url',
                    type: 'string',
                    description: 'Полный URL для перехода (должен содержать pwonline.ru)'
                },
                {
                    name: 'callback',
                    type: 'function',
                    description: 'Функция обратного вызова, которая получит boolean результат'
                }
            ],
            returns: 'nil',
            example: `-- Перейти на страницу промо-предметов
local targetUrl = "https://pwonline.ru/promo_items.php"

Print("Переходим по URL: " .. targetUrl)
Browser_NavigateCb(targetUrl, function(success)
  if success then
    Print("✅ Успешно перешли на страницу")
    
    -- Ждем загрузки контента страницы
    Browser_WaitForElementCb(".main_menu", 5000, function(elementFound)
      if elementFound then
        Print("✅ Страница полностью загружена")
        -- Можно продолжать выполнение скрипта
      else
        Print("⚠️ Основные элементы страницы не найдены")
      end
    end)
  else
    Print("❌ Ошибка навигации")
  end
end)`
        },
        {
            name: 'Browser_ExecuteScriptCb',
            category: 'Browser',
            page: 'browser',
            signature: 'Browser_ExecuteScriptCb(script, callback)',
            description: 'Выполняет JavaScript код в контексте текущей страницы и возвращает результат.',
            parameters: [
                {
                    name: 'script',
                    type: 'string',
                    description: 'JavaScript код для выполнения'
                },
                {
                    name: 'callback',
                    type: 'function',
                    description: 'Функция обратного вызова, которая получит результат выполнения скрипта'
                }
            ],
            returns: 'nil',
            example: `-- Получить информацию о пользователе с помощью JavaScript
local getUserInfoJS = [[
  (function() {
    try {
      var userElement = document.querySelector('.auth_h > h2 > a > strong');
      if (userElement) {
        return {
          username: userElement.innerText.trim(),
          success: true
        };
      } else {
        return {
          error: 'Элемент пользователя не найден',
          success: false
        };
      }
    } catch (e) {
      return {
        error: e.message,
        success: false
      };
    }
  })()
]]

Browser_ExecuteScriptCb(getUserInfoJS, function(result)
  -- result будет строкой, нужно распарсить JSON
  local userInfo = JSON.parse(result)
  
  if userInfo.success then
    Print("👤 Имя пользователя: " .. userInfo.username)
  else
    Print("❌ Ошибка: " .. (userInfo.error or "неизвестная ошибка"))
  end
end)`,
            notes: 'Для работы с JSON результатами используйте функцию JSON.parse(). Всегда обрабатывайте возможные ошибки выполнения JavaScript.'
        },
        {
            name: 'Browser_WaitForElementCb',
            category: 'Browser',
            page: 'browser',
            signature: 'Browser_WaitForElementCb(selector, timeoutMs, callback)',
            description: 'Ожидает появления элемента на странице в течение указанного времени.',
            parameters: [
                {
                    name: 'selector',
                    type: 'string',
                    description: 'CSS селектор элемента'
                },
                {
                    name: 'timeoutMs',
                    type: 'number',
                    description: 'Время ожидания в миллисекундах'
                },
                {
                    name: 'callback',
                    type: 'function',
                    description: 'Функция обратного вызова, которая получит boolean результат'
                }
            ],
            returns: 'nil',
            example: `-- Ожидать появления главного меню 10 секунд
local selector = ".main_menu"
local timeout = 10000

Print("⏳ Ожидаем появления элемента: " .. selector)
Browser_WaitForElementCb(selector, timeout, function(found)
  if found then
    Print("✅ Элемент найден, продолжаем выполнение")
    
    -- Элемент появился, можно выполнять дальнейшие действия
    Browser_ExecuteScriptCb("document.querySelector('.main_menu').style.border = '2px solid green'", function()
      Print("✅ Элемент выделен зеленой рамкой")
    end)
  else
    Print("❌ Элемент не найден за " .. timeout .. "ms")
    -- Можно предпринять альтернативные действия
  end
end)`,
            notes: 'Рекомендуется использовать разумные таймауты (5000-15000ms) в зависимости от скорости загрузки страницы.'
        },
        {
            name: 'Browser_ElementExistsCb',
            category: 'Browser',
            page: 'browser',
            signature: 'Browser_ElementExistsCb(selector, callback)',
            description: 'Проверяет существование элемента на странице без ожидания.',
            parameters: [
                {
                    name: 'selector',
                    type: 'string',
                    description: 'CSS селектор элемента'
                },
                {
                    name: 'callback',
                    type: 'function',
                    description: 'Функция обратного вызова, которая получит boolean результат'
                }
            ],
            returns: 'nil',
            example: `-- Быстрая проверка существования элемента
local checkElement = ".user_profile"

Browser_ElementExistsCb(checkElement, function(exists)
  if exists then
    Print("✅ Элемент профиля пользователя найден")
    -- Можно выполнять действия с этим элементом
  else
    Print("⚠️ Элемент профиля не найден, пользователь не авторизован")
    -- Можно выполнить переход на страницу авторизации
  end
end)`,
            notes: 'В отличие от Browser_WaitForElementCb, эта функция проверяет элемент мгновенно без ожидания.'
        }
    ],
    utilities: [
        {
            name: 'Print',
            category: 'Utilities',
            page: 'utilities',
            signature: 'Print(message)',
            description: 'Выводит сообщение в консоль выполнения скрипта. Полезно для отладки и логирования.',
            parameters: [
                {
                    name: 'message',
                    type: 'string',
                    description: 'Текст сообщения для вывода'
                }
            ],
            returns: 'nil',
            example: `-- Простые сообщения
Print("🚀 Скрипт начал выполнение")
Print("==========================")

-- Сообщения с переменными
local accountCount = 5
local itemCount = 42
Print("Обработано аккаунтов: " .. accountCount)
Print("Найдено предметов: " .. itemCount)

-- Отладочная информация
local currentTime = os.date("%H:%M:%S")
Print("Текущее время: " .. currentTime)

-- Разные уровни логирования
Print("✅ Операция завершена успешно")
Print("⚠️ Предупреждение: медленное соединение")
Print("❌ Ошибка: элемент не найден")`,
            notes: 'Используйте эмодзи и форматирование для лучшей читаемости логов.'
        },
        {
            name: 'DelayCb',
            category: 'Utilities',
            page: 'utilities',
            signature: 'DelayCb(delayMs, callback)',
            description: 'Выполняет задержку выполнения без блокировки интерфейса. Асинхронный аналог sleep().',
            parameters: [
                {
                    name: 'delayMs',
                    type: 'number',
                    description: 'Время задержки в миллисекундах'
                },
                {
                    name: 'callback',
                    type: 'function',
                    description: 'Функция, которая будет вызвана после задержки'
                }
            ],
            returns: 'nil',
            example: `-- Простая задержка
Print("Начало выполнения")
DelayCb(2000, function()
  Print("Выполнено после 2 секунд задержки")
end)

-- Последовательные задержки
Print("Этап 1")
DelayCb(1000, function()
  Print("Этап 2")
  DelayCb(1000, function()
    Print("Этап 3")
    DelayCb(1000, function()
      Print("Все этапы завершены")
    end)
  end)
end)

-- Задержка между операциями с аккаунтами
Account_GetAccountsCb(function(accounts)
  for i, account in ipairs(accounts) do
    Print("Обрабатываем аккаунт: " .. account.Name)
    
    -- Выполняем операции с аккаунтом...
    
    -- Задержка перед следующим аккаунтом
    DelayCb(i * 2000, function()
      Print("Переходим к следующему аккаунту...")
    end)
  end
end)`,
            notes: 'Используйте задержки для имитации человеческого поведения и избежания блокировок.'
        },
        {
            name: 'ReportProgress',
            category: 'Utilities',
            page: 'utilities',
            signature: 'ReportProgress(percent)',
            description: 'Обновляет индикатор прогресса выполнения скрипта.',
            parameters: [
                {
                    name: 'percent',
                    type: 'number',
                    description: 'Процент выполнения от 0 до 100'
                }
            ],
            returns: 'nil',
            example: `-- Обновление прогресса в цикле
local totalItems = 10

Print("Начинаем обработку " .. totalItems .. " элементов")
for i = 1, totalItems do
  local progress = math.floor((i / totalItems) * 100)
  
  ReportProgress(progress)
  Print("Обработан элемент " .. i .. " из " .. totalItems .. " (" .. progress .. "%)")
  
  -- Имитация обработки
  DelayCb(500, function() end)
end

ReportProgress(100)
Print("✅ Обработка завершена")

-- Прогресс с этапами
ReportProgress(0)
Print("Этап 1: Загрузка данных...")
DelayCb(1000, function()
  ReportProgress(25)
  Print("Этап 2: Обработка аккаунтов...")
  DelayCb(1000, function()
    ReportProgress(50)
    Print("Этап 3: Активация промокодов...")
    DelayCb(1000, function()
      ReportProgress(75)
      Print("Этап 4: Сохранение результатов...")
      DelayCb(1000, function()
        ReportProgress(100)
        Print("✅ Все этапы завершены")
      end)
    end)
  end)
end)`,
            notes: 'Используйте для длительных операций, чтобы пользователь видел прогресс выполнения.'
        },
        {
            name: 'ReportProgressMsg',
            category: 'Utilities',
            page: 'utilities',
            signature: 'ReportProgressMsg(percent, message)',
            description: 'Обновляет индикатор прогресса с текстовым сообщением.',
            parameters: [
                {
                    name: 'percent',
                    type: 'number',
                    description: 'Процент выполнения от 0 до 100'
                },
                {
                    name: 'message',
                    type: 'string',
                    description: 'Текстовое сообщение для отображения'
                }
            ],
            returns: 'nil',
            example: `-- Детальный отчет о прогрессе
local accounts = {"Аккаунт1", "Аккаунт2", "Аккаунт3", "Аккаунт4"}

for i, accountName in ipairs(accounts) do
  local progress = math.floor((i / #accounts) * 100)
  local message = "Обработка " .. accountName .. " (" .. i .. "/" .. #accounts .. ")"
  
  ReportProgressMsg(progress, message)
  Print("📝 " .. message)
  
  -- Имитация работы с аккаунтом
  DelayCb(1500, function()
    Print("✅ " .. accountName .. " обработан")
  end)
end

ReportProgressMsg(100, "Все аккаунты обработаны")
Print("🎉 Задача завершена")`,
            notes: 'Сообщения помогают пользователю понимать, что именно происходит в текущий момент.'
        }
    ]
}

// Вспомогательная функция для получения всех функций с информацией о странице
export const getAllFunctions = () => {
    const allFunctions = []

    Object.keys(luaApiData).forEach(page => {
        luaApiData[page].forEach(func => {
            allFunctions.push({
                ...func,
                page: page // Добавляем информацию о странице
            })
        })
    })

    return allFunctions
}