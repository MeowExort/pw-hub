# pw-hub-docs — сайт документации

Сайт с документацией по Lua API и руководствами пользователя.

## Разработка
```
cd pw-hub-docs
npm install
npm run dev
```

## Сборка
```
npm run build
npm run preview
```

## Структура
- `src/components/*` — компоненты React (карточки функций, поиск, навигация и пр.).
- `src/data/luaApiData.js` — исходные данные по Lua API для автогенерации карточек.
- `public/` — статические файлы.

## Полезные ссылки
- Журнал изменений: [CHANGELOG.md](CHANGELOG.md)
- Корневая документация репозитория: [../README.md](../README.md)
