### Изменения с 0.1.1 до 0.2.0

Дата релиза: 2025-11-08

#### Added — добавлено
- Anti‑detect для основного браузера при смене аккаунта: генерация нового Fingerprint (UA, navigator, screen, timezone, Canvas/WebGL) и применение до навигации.
- Anti‑detect для Lua API v1: закрепление Fingerprint за аккаунтом и повторное использование при следующих свитчах; хранение в `Fingerprints/<AccountId>.fp.json`.
- Класс `Models/FingerprintProfile` с полями и русскими XML‑комментариями.
- Сервис `Services/FingerprintGenerator` для правдоподобных профилей (Windows/Chromium, RU/EN локали, популярные таймзоны/экраны/GPU).
- Логирование в JSONL: инфраструктура `Infrastructure/Logging/Log.cs` и `Infrastructure/Logging/RuntimeOptions.cs`; глобальные обработчики исключений.
- Окно логов `Windows/LogsWindow` с тёмной темой, фильтрами по уровню/категории, автопрокруткой; горячая клавиша `Ctrl+Shift+L`.
- Декларативная регистрация Lua API через атрибут `Infrastructure/LuaApiFunctionAttribute.cs`; ленивый реестр `Infrastructure/LuaApiRegistry.cs`.
- Автоматическое закрытие браузеров v2 после завершения скрипта: трекер запуска `Infrastructure/RunContextTracker.cs`, интеграция в `LuaExecutionService`, `LuaEditorViewModel` и `LuaIntegration`.
- Трекинг асинхронных операций скрипта редактора: `Infrastructure/RunLifetimeTracker.cs` + «тихий» watchdog в `LuaScriptRunner` для корректного завершения async‑скриптов.
- Индикаторы загрузки страниц для основного браузера (`Pages/AccountPage`) и каждого браузера v2 (`Controls/BrowserView`).

#### Changed — изменено
- `Services/AccountManager`: централизованная смена аккаунта для v1 через перегрузку `ChangeAccountAsync(string, AccountSwitchOptions)` с созданием новой сессии и применением anti‑detect до кук/навигации.
- `Services/WebCoreBrowser`: внедрение скрипта спуфинга на `DocumentCreated`, подмена `UserAgent`, настройка тёмного фона; безопасное пересоздание сессии c предзагрузкой/финализацией контролов.
- `Tools/LuaIntegration`: помечены публичные методы атрибутами `LuaApiFunction`; добавлено явное прокидывание `runId` и регистрация/дерегистрация дескрипторов браузеров v2.
- `Services/LuaExecutionService`: запуск модулей теперь всегда оборачивается в `BeginRun/EndRunCloseAll` для авто‑закрытия браузеров; подключено лог‑окно.
- `Windows/LuaEditorWindow` и `ViewModels/LuaEditorViewModel`: путь запуска из редактора также использует `BeginRun/SetRunId/EndRunCloseAll`; добавлен сбор аргументов перед запуском.
- `Pages/AccountPage`: логика UI/навигации, тёмный фон, отключение белых вспышек, обновление адресной строки, скрытие overlay после первого успешного свитча.

#### Fixed — исправлено
- v2 WebView2 «скукоживание» по высоте: при программной замене контролов выставляется `Grid.Row=1` в `Controls/BrowserView` и при предзагрузке/финализации.
- Горячая клавиша логов: исправлена ошибка XAML `Modifiers` (WPF требует `Control+Shift`, а не `Control, Shift`).
- Невидимый индикатор загрузки — добавлены и корректно управляются свойства `IsLoading` в `AccountPage` и `BrowserView`.
- XAML ошибка ширины колонки в `LogsWindow`: `GridViewColumn.Width` перестал использовать `"*"`, заменён на число (например, `600`).
- Пустой список Lua API до старта скриптов — теперь атрибутный реестр возвращает функции с самого начала.
- Авто‑закрытие браузеров v2 не срабатывало из редактора Lua — добавлено явное завершение и очистка в `LuaEditorViewModel` и `LuaScriptRunner`.

#### Developer/Infra
- Подписки на глобальные исключения (`DispatcherUnhandledException`, `AppDomain.UnhandledException`, `TaskScheduler.UnobservedTaskException`) с логированием.
- Структурированные логи в ключевых точках: `AccountManager`, `BrowserManager`, `WebCoreBrowser`, `RunContextTracker`, `RunLifetimeTracker`.
- Обновления DI/старта приложения (`App.xaml.cs`): парсинг `--debug`, показ окна логов в Debug, трей‑уведомления о завершении модулей.

#### Ключевые файлы (неполный список)
- Abstractions: `IBrowser.cs`, `IAccountManager.cs`.
- Models: `FingerprintProfile.cs`, `Account.cs` (доп. поля/порядок в отряде).
- Services: `AccountManager.cs`, `WebCoreBrowser.cs`, `BrowserManager.cs`, `LuaExecutionService.cs`, `RunModuleCoordinator.cs`.
- Tools: `LuaIntegration.cs`, `LuaScriptRunner.cs`.
- Infrastructure: `Logging/Log.cs`, `Logging/RuntimeOptions.cs`, `LuaApiFunctionAttribute.cs`, `LuaApiRegistry.cs`, `RunContextTracker.cs`, `RunLifetimeTracker.cs`.
- UI: `MainWindow.xaml/.cs`, `Pages/AccountPage.xaml/.cs`, `Controls/BrowserView.xaml/.cs`, `Windows/LogsWindow.xaml/.cs`.

#### Известные ограничения
- Подмена HTTP `Accept-Language` не реализована напрямую в WebView2; имитация выполняется через `navigator.languages`/`language`. Для жёсткой подмены потребуется прокси/перехват ресурсов.
- Anti‑detect ориентирован на правдоподобную вариативность, не гарантирует «абсолютную невидимость».

#### Версионирование
- `Pw.Hub/Pw.Hub.csproj`: `<Version>0.2.0</Version>` установлена.
