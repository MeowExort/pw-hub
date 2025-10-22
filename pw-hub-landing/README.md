# pw-hub-landing — лендинг (Vite + vite-plugin-ssr)

Пререндеримый лендинг, обслуживаемый как статический сайт (Nginx в Docker) или через `vite preview` в dev.

## Разработка
```
cd pw-hub-landing
npm install
npm run dev
```
Открыть http://localhost:5173

## Сборка и предпросмотр
```
npm run build
npm run preview
```

## Docker
```
docker build -t pw-landing -f pw-hub-landing/Dockerfile pw-hub-landing
docker run --rm -p 8080:80 pw-landing
```

## Интеграция с API
- Блок преимуществ запрашивает `/api/app/stats`. В продакшене настройте реверс‑прокси для маршрута `/api` на Pw.Modules.Api.
- При раздельных доменах — убедитесь, что CORS на API настроен (домен `pw-helper.ru` уже разрешён для `/api/app`).

## Полезные ссылки
- Журнал изменений: [CHANGELOG.md](CHANGELOG.md)
- Корневая документация репозитория: [../README.md](../README.md)
