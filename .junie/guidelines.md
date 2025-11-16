### Руководство по разработке для pw-hub (проекты Pw.Hub и Pw.Modules.Api)

Ниже собраны практические детали именно этого репозитория: как собирать и конфигурировать проекты, как запускать и писать тесты, а также нюансы, которые часто важны при отладке.

#### Состав репозитория (релевантно этому гайду)
- Pw.Hub — WPF‑приложение (.NET 8, C# 12) с встраиваемым браузером WebView2 и локальной БД SQLite (EF Core).
- Pw.Modules.Api — Minimal API (.NET 8) с PostgreSQL, Swagger UI на корне, Health Checks, HTTP‑логированием и OpenTelemetry.

---

### Сборка и конфигурация

#### Общие требования
- Windows 10+; установлен .NET SDK 8.x (локально замечен 8.0.416).
- Для фронтенд‑частей (не покрыто в этом гайде) нужен Node.js LTS, но это не требуется для сборки указанных .NET проектов.

#### Pw.Hub (WPF)
- Открыть решение `Pw.Hub.sln` в Rider/Visual Studio и собрать конфигурацию Debug/Release.
- Зависимости и особенности:
  - WebView2: требуется установленный Microsoft Edge WebView2 Runtime на машине пользователя.
  - EF Core + SQLite: файл базы `pwhub.db` создаётся автоматически; конфигурация контекста — `Pw.Hub\Infrastructure\AppDbContext.cs` с `UseSqlite("Data Source=pwhub.db")`.
  - Миграции для Pw.Hub присутствуют в `Pw.Hub\Migrations` и применяются через обычный EF‑пайплайн (на стороне приложения они не авто‑применяются, учитывайте это при миграциях).
- UI/поведение:
  - В проекте много внимания уделено подавлению «белых вспышек» WebView2 (см. `Controls/BrowserView.xaml(.cs)`) и корректной блокировке взаимодействия через `IsHitTestVisible` вместо `IsEnabled` (см. `CHANGELOG.md`).

#### Pw.Modules.Api (Minimal API)
- Быстрый запуск локально:
  - Требуется PostgreSQL и строка подключения. Используется приоритет: переменная окружения `PW_MODULES_PG`, затем `ConnectionStrings:Postgres` в конфигурации.
  - Команда запуска:
    ```
    dotnet run -p Pw.Modules.Api
    ```
- Поведение на старте:
  - Автоприменение миграций: в `Program.cs` выполняется `db.Database.MigrateAsync()` при старте, поэтому при корректной строке подключения схема БД будет актуализирована автоматически.
  - Swagger UI публикуется на корне (RoutePrefix = ""): откройте http://localhost:5000/ (или порт из переменных окружения ASP.NET).
  - Health Checks: `/healthz`.
  - CORS: есть политика `PwHelper` (разрешён домен pw-helper.ru); дефолтная политика — более permissive.
  - HTTP‑логирование включено (`UseHttpLogging`).
  - OpenTelemetry поддерживается: чтение `OTEL_EXPORTER_OTLP_ENDPOINT`/`OTEL_SERVICE_NAME` и экспорт через OTLP при наличии настроек.
- Docker (многоступенчатая сборка в корневом `Dockerfile`):
  ```
  docker build -t pw-modules-api .
  docker run --rm -p 8080:8080 -e PW_MODULES_PG="Host=...;Database=...;Username=...;Password=..." pw-modules-api
  ```

---

### Тестирование

Ниже описан подход, протестированный в рамках этой сессии, и проверенные команды. Мы сознательно не добавляли постоянные тестовые проекты в решение; демонстрация проведена на временном проекте, который был удалён после проверки. Это даёт вам повторяемый рецепт.

#### Как быстро создать и прогнать юнит‑тесты (MSTest)
1) Создайте временный тестовый проект (из корня репозитория):
```
dotnet new mstest -n Tests.Demo -o Tests.Demo
```
2) Убедитесь, что TargetFramework — `net8.0` (при необходимости выставьте в `Tests.Demo.csproj`).
3) Добавьте простой тест (пример класса):
```
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.Demo;

[TestClass]
public class SmokeMSTest
{
    [TestMethod]
    public void Always_true()
    {
        Assert.IsTrue(true);
    }
}
```
4) Запустите тесты:
```
dotnet test Tests.Demo\Tests.Demo.csproj
```
Ожидаемый результат: Passed 1 tests.

5) По завершении — удалите временную папку:
```
Remove-Item -Recurse -Force .\Tests.Demo
```

В этой сессии мы выполнили шаги 1–4 (тест прошёл), после чего удалили временный каталог.

#### Рекомендации по добавлению постоянных тестов в этот репозиторий
- Выберите фреймворк: в репозитории нет предустановленного тестового набора; проще всего начать с MSTest или xUnit. Для консистентности с примером — используйте MSTest.
- Создайте отдельный проект(ы) в решении, но НЕ смешивайте UI‑зависимые тесты WPF с unit‑тестами доменной логики:
  - Для `Pw.Hub`: выносите тестируемую бизнес‑логику из UI‑кода (ViewModels/Services) и покрывайте её unit‑тестами. UI‑интеракции WebView2 интеграционно тестировать сложно без специальных хостов и стаба браузера.
  - Для `Pw.Modules.Api`: интеграционные тесты удобно строить на базе `Microsoft.AspNetCore.Mvc.Testing` и `WebApplicationFactory<TEntryPoint>`, c использованием in‑memory PostgreSQL (например, Testcontainers с `postgres`) или подмены `DbContextOptions` на тестовую БД. Минимальный smoke‑тест можно сделать против `/healthz`.
- Примеры зависимостей для API‑тестов:
  ```
  <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="8.*" />
  <PackageReference Include="Respawn" Version="6.*" />
  <PackageReference Include="Testcontainers.PostgreSql" Version="3.*" />
  ```
- Рекомендации по прогону:
  - Локально: `dotnet test` по решению или конкретному проекту.
  - В CI: используйте кэш пакетов NuGet и отдельную стадию для интеграционных тестов API (с подъёмом PostgreSQL контейнера).

---

### Дополнительные сведения для разработки

#### Код‑стайл и архитектурные заметки
- Язык/платформа: C# 12, .NET 8. Сохраняйте стиль кода в духе существующих файлов (именование, `var`/явные типы, выражения‑члены там, где уже используются, русскоязычные комментарии).
- Pw.Hub:
  - Слоение: `ViewModels`, `Controls`, `Pages`, `Services`, `Infrastructure`.
  - WebView2: контрол `BrowserView` реализует `IWebViewHost`, есть механика быстрой замены экземпляра для InPrivate‑сессий и устранения белых вспышек. При правках учитывайте обработчики навигации и инициализации (`NavigationStarting`, `CoreWebView2InitializationCompleted`, и т.п.).
  - Работа с EF Core локально: контекст `AppDbContext` в одном месте жёстко указывает строку подключения к SQLite; если понадобится переключаемая конфигурация — вынесите в настройки/DI.
- Pw.Modules.Api:
  - Вертикальные слайсы в `Features/*` (endpoints как статические классы с методами `Handle`). Это облегчает локализацию изменений и тестируемость отдельных фич.
  - Логирование/метрики: включено `UseHttpLogging`; OpenTelemetry метрики/трейсы настраиваются через переменные окружения. Не забудьте указывать `OTEL_EXPORTER_OTLP_ENDPOINT` в средах, где нужен экспорт.
  - Миграции применяются автоматически при старте — это упрощает деплой и локальные стенды, но требует аккуратности при небезопасных миграциях.

#### Диагностика и «подводные камни»
- WebView2: если не установлен Runtime, WPF‑приложение может падать или показывать пустой экран. Проверяйте наличие WebView2 Runtime на машине.
- «Белые вспышки» при инициализации WebView2 минимизированы в коде; не ломайте последовательность инициализации/замены контролов.
- Блокировка UI в дереве WPF: проект сознательно использует `IsHitTestVisible=false` вместо `IsEnabled=false` в ряде мест — это влияет на доступность и визуальные состояния; сохраняйте семантику.
- API: при отсутствии `PW_MODULES_PG` и ConnectionString приложение упадёт при попытке применить миграции. В контейнере обязательно прокидывайте строку подключения.

#### Полезные команды
- Сборка решения: `dotnet build Pw.Hub.sln -c Release`
- Запуск API: `dotnet run -p Pw.Modules.Api`
- Юнит‑тесты (локально для конкретного проекта): `dotnet test path\to\Your.Tests.csproj`
- Очистка NuGet‑кэша при подозрениях на артефакты: `dotnet nuget locals all --clear`

---

### Lua API (v1 и v2)

В `Pw.Hub` встроен Lua‑движок (NLua) и экспорт API для автоматизации действий в приложении и во встроенном браузере WebView2. Каталог Lua API формируется декларативно через атрибуты и используется в редакторе/AI‑подсказках.

Ключевые файлы (источник истины):
- `Pw.Hub\Tools\LuaIntegration.cs` — реализация и экспорт функций Lua API (методы помечены атрибутом `LuaApiFunction`).
- `Pw.Hub\Infrastructure\LuaApiFunctionAttribute.cs` — атрибут для декларативной разметки API.
- `Pw.Hub\Infrastructure\LuaApiRegistry.cs` — реестр описаний API (поддерживает «шпаргалку» через `ToCheatSheetText()`).

Общие принципы
- Все вызовы неблокирующие: результат возвращается в переданный Lua‑колбэк `function(...) ... end`.
- Версия v1 опирается на «глобальный» браузер UI; версия v2 вводит «дескрипторы» (int `handle`) и позволяет управлять несколькими браузерами параллельно.
- Основные категории: `Helpers`, `Account`, `Browser` (v1), `BrowserV2` (v2), `Telegram`, `Net`.

Различия версий
- v1: функции обычно имеют суффикс `..._Cb` и работают с глобальным контекстом браузера. Подходят для простых последовательных сценариев.
- v2: функции начинаются с `BrowserV2_*` и принимают `handle` — идентификатор конкретного экземпляра браузера. Подходят для параллельных/изолированных сценариев.

Справочник функций (сформировано из атрибутов в коде)
- Helpers (v1):
  - `Print(value)` — вывести текст в лог редактора.
  - `DelayCb(ms, function() ... end)` — неблокирующая задержка и вызов колбэка.
  - `ReportProgress(percent)` — обновить прогресс выполнения (0–100).
  - `ReportProgressMsg(percent, message)` — обновить прогресс с сообщением.
- Account (v1):
  - `Account_GetAccountCb(function(acc) ... end)` — получить текущий аккаунт (Lua‑таблица с вложенными серверами/персонажами; у аккаунта присутствует укороченная информация о скваде).
  - `Account_IsAuthorizedCb(function(isAuth) ... end)` — проверить авторизацию текущего аккаунта.
  - `Account_GetAccountsCb(function(accounts) ... end)` — получить список аккаунтов (Lua‑таблица объектов‑таблиц).
  - `Account_ChangeAccountCb(accountId, function(ok) ... end)` — переключить текущий аккаунт.
- Browser (v1):
  - `Browser_NavigateCb(url, function(ok) ... end)` — открыть URL в глобальном браузере.
  - `Browser_ReloadCb(function(ok) ... end)` — перезагрузить страницу.
  - `Browser_ExecuteScriptCb(jsCode, function(result) ... end)` — выполнить JS; результат — строка.
  - `Browser_ElementExistsCb(selector, function(exists) ... end)` — проверить наличие элемента.
  - `Browser_WaitForElementCb(selector, timeoutMs, function(found) ... end)` — ждать появления элемента до таймаута (мс).
- Telegram (v1):
  - `Telegram_SendMessageCb(text, function(ok) ... end)` — отправить сообщение пользователю через Modules API.
- Net (v1):
  - `Net_PostJsonCb(url, jsonBody, contentType, function(response) ... end)` — HTTP POST JSON и вернуть строковый ответ. Примечание: имя функции доступно как `Net_PostJsonCb`.

- BrowserV2 (v2):
  - `BrowserV2_Create(options, function(handle) ... end)` — создать новый браузер и вернуть дескриптор. Пример `options`: `{ StartUrl = "https://pwonline.ru/" }`.
  - `BrowserV2_Close(handle, function(ok) ... end)` — закрыть созданный браузер.
  - `BrowserV2_Navigate(handle, url, function(ok) ... end)` — открыть URL в указанном браузере.
  - `BrowserV2_Reload(handle, function(ok) ... end)` — перезагрузить страницу.
  - `BrowserV2_ExecuteScript(handle, jsCode, function(result) ... end)` — выполнить JS и вернуть строковый результат.
  - `BrowserV2_ElementExists(handle, selector, function(exists) ... end)` — проверить наличие элемента.
  - `BrowserV2_WaitForElement(handle, selector, timeoutMs, function(found) ... end)` — ждать появления элемента.
  - `BrowserV2_ChangeAccount(handle, accountId, function(ok) ... end)` — переключить аккаунт в контексте указанного браузера.
  - `BrowserV2_GetCurrentAccount(handle, function(acc) ... end)` — получить текущий аккаунт для браузера (Lua‑таблица, как в v1).

Примеры
- v1 (глобальный браузер):
  ```lua
  Print('Старт')
  Browser_NavigateCb('https://example.org', function(ok)
      if not ok then Print('Навигация не удалась') return end
      Browser_ExecuteScriptCb("return document.title", function(title)
          Print('Заголовок: ' .. tostring(title))
      end)
  end)
  ```

- v2 (несколько браузеров через дескрипторы):
  ```lua
  BrowserV2_Create({ StartUrl = 'https://example.org' }, function(handle)
      if handle == 0 then Print('Не удалось создать браузер') return end
      BrowserV2_Navigate(handle, 'https://example.org/catalog', function(ok)
          if not ok then Print('Навигация не удалась') return end
          BrowserV2_ExecuteScript(handle, "return document.title", function(title)
              Print('Заголовок: ' .. tostring(title))
              BrowserV2_Close(handle, function(closed) end)
          end)
      end)
  end)
  ```

Заметки и подводные камни
- Вызовы, связанные с браузером, учитывают жизненный цикл запуска скрипта через `RunLifetimeTracker`; при остановке скрипта висящие операции корректно завершаются.
- В v2 `handle` сопоставляется с контекстом запуска через `RunContextTracker`, что важно для корректного закрытия браузеров при остановке.
- Каталог API формируется автоматически по атрибутам при первом обращении; редактор/AI берут данные из `LuaApiRegistry` — ручная синхронизация списка не требуется.

#### Синхроноподобные обёртки для Lua (await поверх колбэков)

Во встроенном Lua‑скриптинге все API изначально неблокирующие и используют колбэки (`...Cb`). Чтобы существенно повысить читаемость сценариев, рекомендуем оборачивать такие вызовы в «await» на корутинах и получать результат как обычное возвращаемое значение.

Базовая функция `await` (использовать только внутри корутины):

```lua
-- Объявление функции
local await

-- Присвоение значения
await = function(starter)
  local co = coroutine.running()
  if not co then error('await must be used inside a coroutine') end
  local resumed = false
  starter(function(...)
    if not resumed then
      resumed = true
      local ok, err = coroutine.resume(co, ...)
      if not ok then Pfx('resume error:', tostring(err)) end
    end
  end)
  return coroutine.yield()
end
```

Пример обёртки для задержки (`DelayCb`) и её использование:

```lua
-- Объявление функции
local aDelay

-- Присвоение значения
aDelay = function(timeMs)
  return await(function(k)
    DelayCb(timeMs, function() k(true) end)
  end)
end

-- Запуск внутри корутины
local co = coroutine.create(function()
  Print('Старт')
  local ok = aDelay(500)
  Print('Задержка ОК: ' .. tostring(ok))
end)
coroutine.resume(co)
```

Рекомендация: для всех асинхронных функций, возвращающих результат через колбэк, добавляйте парные «await‑обёртки» с префиксом `a*`. Ниже типовые шаблоны:

```lua
-- Browser v1
local aNavigate = function(url)
  return await(function(k)
    Browser_NavigateCb(url, function(ok) k(ok) end)
  end)
end

local aExecJs = function(js)
  return await(function(k)
    Browser_ExecuteScriptCb(js, function(result) k(result) end)
  end)
end

-- Account v1
local aGetAccount = function()
  return await(function(k)
    Account_GetAccountCb(function(acc) k(acc) end)
  end)
end

-- Browser v2
local aV2Navigate = function(handle, url)
  return await(function(k)
    BrowserV2_Navigate(handle, url, function(ok) k(ok) end)
  end)
end

local aV2WaitFor = function(handle, selector, timeoutMs)
  return await(function(k)
    BrowserV2_WaitForElement(handle, selector, timeoutMs, function(found) k(found) end)
  end)
end
```

Композитный пример (v2):

```lua
local co = coroutine.create(function()
  local handle = await(function(k)
    BrowserV2_Create({ StartUrl = 'https://example.org' }, function(h) k(h) end)
  end)
  if handle == 0 then Print('Не удалось создать браузер') return end

  local ok = aV2Navigate(handle, 'https://example.org/catalog')
  if not ok then Print('Навигация не удалась') return end

  local title = await(function(k)
    BrowserV2_ExecuteScript(handle, "return document.title", function(v) k(v) end)
  end)
  Print('Заголовок: ' .. tostring(title))

  await(function(k)
    BrowserV2_Close(handle, function(closed) k(closed) end)
  end)
end)
coroutine.resume(co)
```

Важно
- `await` должен вызываться строго внутри корутины (`coroutine.running()`), иначе будет ошибка.
- Следите за корректной передачей всех возвращаемых значений из колбэка в `k(...)` — они попадут в `return` `await`.
- Для долгих операций имеет смысл добавлять таймауты в обёртки или использовать существующие `...WaitFor...Cb` функции.
- Соглашение имён: колбэк‑функции заканчиваются на `...Cb`, синхроноподобные обёртки — `a*` (например, `aDelay`, `aGetAccount`, `aExecJs`, `aV2Navigate`).

Ссылка: раздел «Lua API (v1 и v2)» выше описывает исходные неблокирующие функции, которые вы можете оборачивать указанным способом.

### Резюме по демонстрации тестов в этой сессии
- Был создан временный проект `Tests.Demo` (MSTest, net8.0) с одним smoke‑тестом.
- Команда запуска: `dotnet test Tests.Demo\Tests.Demo.csproj` — прошёл 1 тест.
- После проверки временные файлы удалены. Для повторения процесса используйте раздел «Как быстро создать и прогнать юнит‑тесты (MSTest)» выше.
