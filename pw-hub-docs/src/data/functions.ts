export type Param = { name: string; type: string; description: string }
export type Example = { title: string; description?: string; code: string }
export type FunctionDoc = {
  key: string
  name: string
  summary: string
  signature: string
  params?: Param[]
  returns?: string
  notes?: string[]
  examples?: Example[]
}
export type Category = {
  key: string
  title: string
  description: string
  functions: FunctionDoc[]
  examples?: Example[]
}

// Общие вспомогательные функции
const common: Category = {
  key: 'common',
  title: 'Общие функции',
  description:
    'Базовые утилиты для вывода сообщений, задержек и отчёта о прогрессе выполнения сценариев.',
  functions: [
    {
      key: 'Print',
      name: 'Print(text)',
      summary: 'Выводит текстовое сообщение из Lua в интерфейс PW Hub.',
      signature: 'Print(text: string): void',
      params: [
        { name: 'text', type: 'string', description: 'Сообщение для вывода.' },
      ],
      returns: 'Ничего не возвращает.',
      notes: [
        'Если установлен обработчик вывода, сообщение отобразится в окне редактора/лога; иначе будет показано системное окно.',
      ],
      examples: [
        {
          title: 'Простой вывод',
          code: `Print('Привет из Lua!')`,
        },
      ],
    },
    {
      key: 'DelayCb',
      name: 'DelayCb(ms, callback)',
      summary:
        'Неблокирующая задержка. По истечении времени вызывает колбэк на UI-потоке.',
      signature: 'DelayCb(ms: number, callback: function): void',
      params: [
        {
          name: 'ms',
          type: 'number',
          description: 'Длительность задержки в миллисекундах.',
        },
        {
          name: 'callback',
          type: 'function',
          description: 'Функция без аргументов, вызываемая после задержки.',
        },
      ],
      returns: 'Ничего не возвращает.',
      examples: [
        {
          title: 'Задержка и лог',
          code: `Print('Ждём 1 секунду...')\nDelayCb(1000, function()\n  Print('Прошла 1 секунда')\nend)`,
        },
      ],
    },
    {
      key: 'ReportProgress',
      name: 'ReportProgress(percent)',
      summary: 'Сообщает о прогрессе выполнения без текста.',
      signature: 'ReportProgress(percent: number): void',
      params: [
        {
          name: 'percent',
          type: 'number (0..100)',
          description: 'Проценты выполнения операции.',
        },
      ],
      returns: 'Ничего не возвращает.',
      examples: [
        {
          title: 'Отчёт без сообщения',
          code: `ReportProgress(50) -- 50%`,
        },
      ],
    },
    {
      key: 'ReportProgressMsg',
      name: 'ReportProgressMsg(percent, message)',
      summary: 'Сообщает о прогрессе выполнения с текстовым сообщением.',
      signature: 'ReportProgressMsg(percent: number, message: string): void',
      params: [
        {
          name: 'percent',
          type: 'number (0..100)',
          description: 'Проценты выполнения операции.',
        },
        {
          name: 'message',
          type: 'string',
          description: 'Произвольное сообщение статуса.',
        },
      ],
      returns: 'Ничего не возвращает.',
      examples: [
        {
          title: 'Ступенчатый прогресс',
          code: `ReportProgressMsg(10, 'Старт')\nDelayCb(500, function()\n  ReportProgressMsg(60, 'Середина')\n  DelayCb(500, function()\n    ReportProgressMsg(100, 'Готово')\n  end)\nend)`,
        },
      ],
    },
  ],
  examples: [
    {
      title: 'Комплекс: лог + прогресс + задержка',
      description:
        'Демонстрация совместной работы функций Print, ReportProgressMsg и DelayCb.',
      code: `Print('Начинаем работу')\nReportProgressMsg(0, 'Инициализация')\nDelayCb(800, function()\n  ReportProgressMsg(50, 'Половина пути')\n  DelayCb(800, function()\n    ReportProgressMsg(100, 'Готово')\n    Print('Завершено')\n  end)\nend)`,
    },
  ],
}

// Работа с аккаунтом
const account: Category = {
  key: 'account',
  title: 'Работа с аккаунтом',
  description:
    'Получение текущего аккаунта, списка аккаунтов и смена активного аккаунта. Доступны колбэк-версии для неблокирующей работы.',
  functions: [
    {
      key: 'Account_GetAccountCb',
      name: 'Account_GetAccountCb(callback)',
      summary: 'Асинхронно возвращает идентификатор текущего аккаунта в колбэк.',
      signature: 'Account_GetAccountCb(callback: function(accountId: string)): void',
      params: [
        { name: 'callback', type: 'function', description: 'Функция (accountId: string).' },
      ],
      returns: 'Ничего не возвращает (результат приходит в колбэк).',
      examples: [
        {
          title: 'Вывести активный аккаунт',
          code: `Account_GetAccountCb(function(id)\n  Print('Текущий аккаунт: ' .. tostring(id))\nend)`,
        },
      ],
    },
    {
      key: 'Account_IsAuthorizedCb',
      name: 'Account_IsAuthorizedCb(callback)',
      summary: 'Асинхронно сообщает, авторизован ли аккаунт.',
      signature: 'Account_IsAuthorizedCb(callback: function(isOk: boolean)): void',
      params: [
        { name: 'callback', type: 'function', description: 'Функция (isOk: boolean).' },
      ],
      returns: 'Ничего не возвращает.',
      examples: [
        {
          title: 'Проверка авторизации',
          code: `Account_IsAuthorizedCb(function(ok)\n  if ok then\n    Print('Авторизован')\n  else\n    Print('Не авторизован')\n  end\nend)`,
        },
      ],
    },
    {
      key: 'Account_GetAccountsJsonCb',
      name: 'Account_GetAccountsJsonCb(callback)',
      summary: 'Асинхронно возвращает JSON со списком аккаунтов.',
      signature: 'Account_GetAccountsJsonCb(callback: function(json: string)): void',
      params: [
        { name: 'callback', type: 'function', description: 'Функция (json: string).' },
      ],
      returns: 'Ничего не возвращает.',
      examples: [
        {
          title: 'Разбор JSON',
          code: `Account_GetAccountsJsonCb(function(json)\n  Print('JSON: ' .. json)\n  -- при необходимости распарсить JSON в таблицу средствами Lua
end)`,
        },
      ],
    },
    {
      key: 'Account_GetAccountsCb',
      name: 'Account_GetAccountsCb(callback)',
      summary: 'Асинхронно возвращает массив аккаунтов (как таблицу Lua).',
      signature: 'Account_GetAccountsCb(callback: function(accounts: table)): void',
      params: [
        { name: 'callback', type: 'function', description: 'Функция (accounts: table).' },
      ],
      returns: 'Ничего не возвращает.',
      examples: [
        {
          title: 'Перебор аккаунтов',
          code: `Account_GetAccountsCb(function(accounts)\n  for i, acc in ipairs(accounts) do\n    -- acc имеет поля в соответствии с моделью Account (C#)\n    Print('Аккаунт #' .. i)\n  end\nend)`,
        },
      ],
    },
    {
      key: 'Account_ChangeAccountCb',
      name: 'Account_ChangeAccountCb(accountId, callback)',
      summary: 'Асинхронно меняет активный аккаунт и вызывает колбэк после завершения.',
      signature: 'Account_ChangeAccountCb(accountId: string, callback: function): void',
      params: [
        { name: 'accountId', type: 'string (GUID)', description: 'Идентификатор аккаунта.' },
        { name: 'callback', type: 'function', description: 'Функция без параметров.' },
      ],
      returns: 'Ничего не возвращает.',
      notes: ['При неверном формате GUID операция не выполняется.'],
      examples: [
        {
          title: 'Смена аккаунта',
          code: `local id = '00000000-0000-0000-0000-000000000000'\nAccount_ChangeAccountCb(id, function()\n  Print('Аккаунт сменён')\nend)`,
        },
      ],
    },

    // Синхронные (могут быть недоступны, если не зарегистрированы в среде)
    {
      key: 'Account_GetAccount',
      name: 'Account_GetAccount()',
      summary: 'Возвращает идентификатор текущего аккаунта (синхронно).',
      signature: 'Account_GetAccount(): string',
      returns: 'Строка с идентификатором аккаунта.',
      notes: ['Синхронные версии требуют регистрации в Lua-среде; при отсутствии — используйте *Cb функции.'],
      examples: [
        { title: 'Пример', code: `local id = Account_GetAccount()\nPrint(id)` },
      ],
    },
    {
      key: 'Account_IsAuthorized',
      name: 'Account_IsAuthorized()',
      summary: 'Сообщает, авторизован ли аккаунт (синхронно).',
      signature: 'Account_IsAuthorized(): boolean',
      returns: 'true/false.',
    },
    {
      key: 'Account_GetAccountsJson',
      name: 'Account_GetAccountsJson()',
      summary: 'Возвращает JSON со списком аккаунтов (синхронно).',
      signature: 'Account_GetAccountsJson(): string',
      returns: 'Строка JSON.',
    },
    {
      key: 'Account_GetAccounts',
      name: 'Account_GetAccounts()',
      summary: 'Возвращает массив аккаунтов (синхронно).',
      signature: 'Account_GetAccounts(): table',
      returns: 'Таблица Lua с аккаунтами.',
    },
    {
      key: 'Account_ChangeAccount',
      name: 'Account_ChangeAccount(accountId)',
      summary: 'Меняет активный аккаунт (синхронно).',
      signature: 'Account_ChangeAccount(accountId: string): void',
      params: [
        { name: 'accountId', type: 'string (GUID)', description: 'Идентификатор аккаунта.' },
      ],
      returns: 'Ничего не возвращает.',
    },
  ],
  examples: [
    {
      title: 'Проверить и сменить аккаунт',
      description:
        'Проверяем авторизацию, при необходимости меняем аккаунт и выводим итог.',
      code: `Account_IsAuthorizedCb(function(ok)\n  if not ok then\n    local id = '00000000-0000-0000-0000-000000000000'\n    Account_ChangeAccountCb(id, function()\n      Print('Сменили аккаунт. Повторная проверка...')\n      Account_IsAuthorizedCb(function(ok2)\n        Print('Авторизация: ' .. tostring(ok2))\n      end)\n    end)\n  else\n    Print('Уже авторизованы')\n  end\nend)`,
    },
  ],
}

// Управление встроенным браузером
const browser: Category = {
  key: 'browser',
  title: 'Встроенный браузер',
  description:
    'Навигация, перезагрузка, выполнение JavaScript, ожидание элементов и работа с куки.',
  functions: [
    // Колбэк-версии (регистрируются в Lua)
    {
      key: 'Browser_NavigateCb',
      name: 'Browser_NavigateCb(url, callback)',
      summary: 'Переходит по указанному URL. Колбэк вызывается по завершению навигации.',
      signature: 'Browser_NavigateCb(url: string, callback: function): void',
      params: [
        { name: 'url', type: 'string', description: 'Адрес страницы.' },
        { name: 'callback', type: 'function', description: 'Функция без параметров.' },
      ],
      returns: 'Ничего не возвращает.',
      examples: [
        { title: 'Переход на сайт', code: `Browser_NavigateCb('https://example.com', function()\n  Print('Страница загружена')\nend)` },
      ],
    },
    {
      key: 'Browser_ReloadCb',
      name: 'Browser_ReloadCb(callback)',
      summary: 'Перезагружает текущую страницу.',
      signature: 'Browser_ReloadCb(callback: function): void',
      params: [ { name: 'callback', type: 'function', description: 'Функция без параметров.' } ],
      returns: 'Ничего не возвращает.',
    },
    {
      key: 'Browser_ExecuteScriptCb',
      name: 'Browser_ExecuteScriptCb(script, callback)',
      summary: 'Выполняет JavaScript на странице. Результат передаётся в колбэк как строка.',
      signature: 'Browser_ExecuteScriptCb(script: string, callback: function(result: string)): void',
      params: [
        { name: 'script', type: 'string', description: 'JS-код для выполнения.' },
        { name: 'callback', type: 'function', description: 'Функция (result: string).' },
      ],
      returns: 'Ничего не возвращает.',
      examples: [
        {
          title: 'Получить заголовок страницы',
          code: `Browser_ExecuteScriptCb('return document.title', function(result)\n  Print('Заголовок: ' .. result)\nend)`,
        },
      ],
    },
    {
      key: 'Browser_ElementExistsCb',
      name: 'Browser_ElementExistsCb(selector, callback)',
      summary: 'Проверяет наличие элемента по CSS-селектору.',
      signature: 'Browser_ElementExistsCb(selector: string, callback: function(exists: boolean)): void',
      params: [
        { name: 'selector', type: 'string', description: 'CSS-селектор элемента.' },
        { name: 'callback', type: 'function', description: 'Функция (exists: boolean).' },
      ],
      returns: 'Ничего не возвращает.',
    },
    {
      key: 'Browser_WaitForElementCb',
      name: 'Browser_WaitForElementCb(selector, timeoutMs, callback)',
      summary: 'Ожидает появления элемента на странице в течение заданного таймаута.',
      signature:
        'Browser_WaitForElementCb(selector: string, timeoutMs: number, callback: function(exists: boolean)): void',
      params: [
        { name: 'selector', type: 'string', description: 'CSS-селектор.' },
        { name: 'timeoutMs', type: 'number', description: 'Таймаут ожидания в миллисекундах.' },
        { name: 'callback', type: 'function', description: 'Функция (exists: boolean).' },
      ],
      returns: 'Ничего не возвращает.',
    },
    // Работа с куки (JSON)
    {
      key: 'Browser_GetCookiesJsonCb',
      name: 'Browser_GetCookiesJsonCb(callback)',
      summary: 'Возвращает JSON с куки браузера.',
      signature: 'Browser_GetCookiesJsonCb(callback: function(json: string)): void',
      params: [ { name: 'callback', type: 'function', description: 'Функция (json: string).' } ],
      returns: 'Ничего не возвращает.',
    },
    {
      key: 'Browser_SetCookiesJsonCb',
      name: 'Browser_SetCookiesJsonCb(json, callback)',
      summary: 'Устанавливает куки в браузере из JSON.',
      signature: 'Browser_SetCookiesJsonCb(json: string, callback: function): void',
      params: [
        { name: 'json', type: 'string', description: 'JSON-массив объектов Cookie.' },
        { name: 'callback', type: 'function', description: 'Функция без параметров.' },
      ],
      returns: 'Ничего не возвращает.',
    },

    // Синхронные версии (могут отсутствовать в среде)
    { key: 'Browser_Navigate', name: 'Browser_Navigate(url)', summary: 'Навигация (синхронно).', signature: 'Browser_Navigate(url: string): void', params: [ { name: 'url', type: 'string', description: 'Адрес страницы.' } ] },
    { key: 'Browser_Reload', name: 'Browser_Reload()', summary: 'Перезагрузка (синхронно).', signature: 'Browser_Reload(): void' },
    { key: 'Browser_ExecuteScript', name: 'Browser_ExecuteScript(script)', summary: 'Выполнение JS (синхронно).', signature: 'Browser_ExecuteScript(script: string): string' },
    { key: 'Browser_ElementExists', name: 'Browser_ElementExists(selector)', summary: 'Проверка наличия элемента (синхронно).', signature: 'Browser_ElementExists(selector: string): boolean' },
    { key: 'Browser_WaitForElement', name: 'Browser_WaitForElement(selector, timeoutMs)', summary: 'Ожидание элемента (синхронно).', signature: 'Browser_WaitForElement(selector: string, timeoutMs: number): boolean' },
    { key: 'Browser_GetCookiesJson', name: 'Browser_GetCookiesJson()', summary: 'Получить куки JSON (синхронно).', signature: 'Browser_GetCookiesJson(): string' },
    { key: 'Browser_SetCookiesJson', name: 'Browser_SetCookiesJson(json)', summary: 'Установить куки JSON (синхронно).', signature: 'Browser_SetCookiesJson(json: string): void' },
  ],
  examples: [
    {
      title: 'Логин на сайте: ожидание и выполнение JS',
      description:
        'Навигация, ожидание формы логина, ввод через JS и отправка формы. Затем чтение заголовка и отчёт о прогрессе.',
      code: `Browser_NavigateCb('https://example.com/login', function()\n  Browser_WaitForElementCb('#login', 10000, function(exists)\n    if not exists then\n      Print('Форма логина не появилась')\n      return\n    end\n    Browser_ExecuteScriptCb("document.querySelector('#login').value='user'", function()\n      Browser_ExecuteScriptCb("document.querySelector('#password').value='pass'", function()\n        Browser_ExecuteScriptCb("document.querySelector('form').submit()", function()\n          ReportProgressMsg(70, 'Отправили форму')\n          Browser_ExecuteScriptCb('return document.title', function(t)\n            Print('Текущая страница: ' .. t)\n            ReportProgressMsg(100, 'Готово')\n          end)\n        end)\n      end)\n    end)\n  end)\nend)`,
    },
  ],
}

// Прочие асинхронные вспомогательные функции (исторические)
const callbacks: Category = {
  key: 'callbacks',
  title: 'Доп. колбэки',
  description:
    'Исторические/вспомогательные функции обратного вызова для обратной совместимости.',
  functions: [
    {
      key: 'GetAccountAsyncCallback',
      name: 'GetAccountAsyncCallback(callback)',
      summary: 'Возвращает id аккаунта через Action<string> (служебная функция).',
      signature: 'GetAccountAsyncCallback(callback: function(accountId: string)): void',
      params: [ { name: 'callback', type: 'function', description: 'Функция (accountId: string).' } ],
      returns: 'Ничего не возвращает.',
    },
  ],
}

export const categories: Category[] = [common, account, browser, callbacks]

// Упрощённый индекс функций по ключу
export const allFunctionsIndex: Record<string, FunctionDoc> = Object.fromEntries(
  categories.flatMap(c => c.functions).map(fn => [fn.key, fn])
)
