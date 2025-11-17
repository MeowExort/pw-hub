### Изменения с 0.2.0 до 1.0.0

Дата релиза: 2025-11-17

#### Added — добавлено
- Pw.Hub — библиотека модулей:
  - Окно `ModulesLibraryWindow` и `ModulesLibraryViewModel` для обзора, установки, удаления и обновления модулей.
  - Проверка доступности обновлений для установленных модулей; массовые операции.
  - Отображение локальной/удалённой версии, индикаторы необходимости обновления.
- Редактор модулей API:
  - `ModulesApiEditorWindow`/`ModulesApiEditorViewModel` с предпросмотром, валидацией и автозаполнением полей.
  - Интеграция с AI‑помощником для генерации описания (через `AiDocService`).
- Клиент к Modules API для приложения:
  - `ModulesApiClient` с авторизацией, проверкой версий, установкой/удалением, получением метаданных.
  - `ModulesSyncService` для синхронизации модуля и локального состояния.
  - `UpdatesCheckService` для периодической проверки обновлений.
- Pw.Modules.Api — Minimal API:
  - CRUD и выдача метаданных модулей (версионирование, описание, ссылка на пакет).
  - Swagger UI на корне (`/`).
  - Health Checks (`/healthz`).
  - HTTP‑логирование запросов (`UseHttpLogging`).
  - OpenTelemetry: экспорт метрик/трейсов при наличии `OTEL_EXPORTER_OTLP_ENDPOINT`.
  - Telegram: эндпоинты и сервис отправки сообщений, привязка/отвязка Telegram (бот‑интеграция).
- Docker: многоступенчатая сборка API (см. корневой `Dockerfile`).
- Документация (репозитории `pw-hub-docs`, `pw-hub-landing`) — стартовые версии сайтов и лендинга.

#### Changed — изменено
- Pw.Hub:
  - Улучшен UX модульной библиотеки и редактора: корректные состояния кнопок «Установить/Удалить/Обновить», возврат главного окна на передний план при закрытии дочерних окон.
  - Доработаны проверки версии модулей (семантическое сравнение, значения по умолчанию `"1.0.0"`).
  - Обновлены зависимости: WebView2 `1.0.3537.50`, EF Core `9.0.0`, Markdig `0.37.0`.
- Pw.Modules.Api:
  - Применение миграций БД автоматически при старте (`db.Database.MigrateAsync()` в `Program.cs`).
  - Вертикальные слайсы в `Features/*` для модулей и Telegram.
  - Стабилизация модели данных модулей, метрики (`ModuleMetrics`).

#### Fixed — исправлено
- Стабильность навигации и перерисовки WebView2 при пересоздании контролов (минимизация «белых вспышек»).
- Корректная проверка доступности обновлений модулей и их состояния в UI.
- Исправлены отдельные XAML‑неточности и граничные кейсы включения/отключения кнопок действий.

#### Breaking — важные изменения
- Версия продуктов повышена до `1.0.0` и считается стабильной.
- Pw.Modules.Api требует доступной строки подключения PostgreSQL:
  - Переменная окружения `PW_MODULES_PG` или ключ `ConnectionStrings:Postgres` в конфигурации. Без настройки API не стартует.
- Для Pw.Hub по‑прежнему требуется установленный Microsoft Edge WebView2 Runtime на машине пользователя.

#### Developer/Infra
- Swagger UI публикуется на корне API, Health Checks на `/healthz`.
- Поддержка OpenTelemetry через переменные `OTEL_EXPORTER_OTLP_ENDPOINT`/`OTEL_SERVICE_NAME`.
- Docker‑образ API: `docker build -t pw-modules-api .` и запуск c пробросом `PW_MODULES_PG`.
- В репозитории добавлены рекомендации по тестированию: быстрый рецепт MSTest для локального запуска.

#### Ключевые файлы (неполный список)
- Pw.Hub:
  - `Windows/ModulesLibraryWindow.xaml(.cs)`
  - `ViewModels/ModulesLibraryViewModel.cs`
  - `Windows/ModulesApiEditorWindow.xaml(.cs)`
  - `ViewModels/ModulesApiEditorViewModel.cs`
  - `Services/ModulesApiClient.cs`, `Services/ModulesSyncService.cs`, `Services/UpdatesCheckService.cs`
  - `Models/ModuleDefinition.cs`, `modules.json`
- Pw.Modules.Api:
  - `Program.cs`, `Features/Modules/*`, `Features/Auth/Telegram/*`
  - `Infrastructure/Telegram/*`, `Domain/*`, `Application/*`
  - `Data/ModulesDbContext.cs`, `Migrations/*`
  - `Dockerfile` (в корне репозитория)

#### Известные ограничения
- Pw.Hub: миграции SQLite не применяются автоматически — учитывайте это при изменении схемы.
- WebView2 не позволяет жёстко подменять HTTP `Accept-Language`; имитация выполняется через DOM/JS.
- Anti‑detect не гарантирует абсолютную невидимость, ориентирован на правдоподобие.

#### Версионирование
- `Pw.Hub/Pw.Hub.csproj`: `<Version>1.0.0</Version>` установлена.
- У API и модулей принята семантическая схема версий; значения по умолчанию — `"1.0.0"`.
