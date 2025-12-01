# Pw.Modules.Api — Minimal API

Сервис для хранения/управления модулями и предоставления статистики для лендинга.

## Быстрый старт (локально)
- Требования: .NET SDK 10.0, PostgreSQL.
- Переменная окружения подключения:
  - `PW_MODULES_PG` — строка подключения к PostgreSQL (либо настройте `ConnectionStrings:Postgres` в конфигурации).
- Запуск:
```
dotnet run -p Pw.Modules.Api
```
- Swagger UI доступен на корневом пути (например, http://localhost:5000/).

## Эндпоинты
- Аутентификация и профиль (`/api/auth`):
  - `POST /register` — регистрация (username, password).
  - `POST /login` — вход (username, password).
  - `GET /me` — текущий пользователь (заголовок `X-Auth-Token`).
  - `POST /username` — изменение имени пользователя (заголовок `X-Auth-Token`).
  - `POST /password` — смена пароля (заголовок `X-Auth-Token`).
- Модули (`/api/modules`) — CRUD и счётчики установок/запусков (см. Swagger).
- Приложение (`/api/app/stats`) — сводная статистика (активные пользователи, модули, запуски).
- `/healthz` — Health Checks.

## CORS
- Для группы `/api/app` применена политика `PwHub` (разрешён домен pw-hub.ru).

## Docker
- В корне репозитория есть `Dockerfile` (многоступенчатая сборка API).
```
docker build -t pw-modules-api .
docker run --rm -p 8080:8080 pw-modules-api
```

## Полезные ссылки
- Журнал изменений: [CHANGELOG.md](CHANGELOG.md)
- Корневая документация репозитория: [../README.md](../README.md)
