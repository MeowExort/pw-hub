# PW Hub — монорепозиторий

Русскоязычная документация по проектам репозитория и быстрые ссылки.

## Проекты
- Pw.Hub — WPF‑приложение для работы с аккаунтами и модулями. [Документация](Pw.Hub/README.md) · [Changelog](Pw.Hub/CHANGELOG.md)
- Pw.Modules.Api — ASP.NET Core Minimal API для модуля‑маркетплейса и статистики. [Документация](Pw.Modules.Api/README.md) · [Changelog](Pw.Modules.Api/CHANGELOG.md)
- pw-hub-landing — лендинг (Vite + vite-plugin-ssr). [Документация](pw-hub-landing/README.md) · [Changelog](pw-hub-landing/CHANGELOG.md)
- pw-hub-docs — документация (сайт) по Lua API и руководствам. [Документация](pw-hub-docs/README.md) · [Changelog](pw-hub-docs/CHANGELOG.md)

## Начало работы
- Требования: 
  - Windows 10+, .NET 10 (SDK) для разработки, Node.js LTS для фронтенда.
  - Для API — PostgreSQL, переменная окружения `PW_MODULES_PG` или ConnectionString в appsettings.
- Сборка/запуск:
  - Pw.Hub: откройте `Pw.Hub.sln` в Rider/VS и соберите. БД SQLite `pwhub.db` создаётся автоматически EF Core миграциями.
  - Pw.Modules.Api: `dotnet run -p Pw.Modules.Api` или Docker (см. README проекта).
  - pw-hub-landing и pw-hub-docs: `npm install && npm run dev|build` в соответствующих каталогах.

## Содействие
- Прежде чем открывать PR, ознакомьтесь с [CONTRIBUTING.md](CONTRIBUTING.md).
- Для багов и фич используйте шаблоны задач из `.github/ISSUE_TEMPLATE`.
