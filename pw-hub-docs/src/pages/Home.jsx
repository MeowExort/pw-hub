import { Link } from 'react-router-dom'
import SearchBox from '../components/SearchBox'
import LuaCodeBlock from '../components/LuaCodeBlock'

export default function Home() {
    const handleSearch = (results, query) => {
        console.log(`Найдено ${results.length} результатов для "${query}"`)
    }

    const simpleScriptExample = `-- Пример простого скрипта
Print("Скрипт запущен")

-- Проверить авторизацию
Account_IsAuthorizedCb(function(isAuth)
  if isAuth then
    Print("Пользователь авторизован")
    
    -- Получить текущий аккаунт
    Account_GetAccountCb(function(account)
      Print("Текущий аккаунт: " .. account)
    end)
  else
    Print("Требуется авторизация")
  end
end)`

    const promoCodeExample = `local PROMO_CODE = "SUMMER2024"

-- Получить все аккаунты
Account_GetAccountsCb(function(accounts)
  for i, acc in ipairs(accounts) do
    Print("Обрабатываем аккаунт: " .. acc.Name)
    
    -- Переключиться на аккаунт
    Account_ChangeAccountCb(acc.Id, function(success)
      if success then
        -- Перейти на страницу активации
        Browser_NavigateCb("https://pwonline.ru/pin.php", function()
          -- Ввести и активировать промокод
          local js = [[
            var input = document.querySelector('.pin_input > input');
            if (input) {
              input.value = ']] .. PROMO_CODE .. [[';
              var form = input.closest('form');
              if (form) form.submit();
            }
          ]]
          Browser_ExecuteScriptCb(js, function()
            Print("Промокод активирован для " .. acc.Name)
          end)
        end)
      else
        Print("Ошибка переключения на аккаунт: " .. acc.Name)
      end
    end)
    
    -- Задержка между аккаунтами
    DelayCb(2000, function() end)
  end
end)`

    return (
        <div>
            <section style={{ marginBottom: '3rem' }}>
                <h1>📚 Документация Lua API</h1>
                <p style={{ fontSize: '1.2rem', color: 'var(--text-secondary)', marginBottom: '2rem' }}>
                    Полное руководство по автоматизации Perfect World с помощью Lua скриптов
                </p>

                {/* Поиск на главной странице */}
                <div style={{ marginBottom: '2rem' }}>
                    <SearchBox onSearch={handleSearch} />
                </div>

                <div style={{
                    background: 'linear-gradient(135deg, rgba(255,179,0,0.1) 0%, rgba(255,143,0,0.1) 100%)',
                    border: '1px solid var(--accent)',
                    borderRadius: '12px',
                    padding: '2rem',
                    marginBottom: '2rem'
                }}>
                    <h2 style={{ color: 'var(--accent)', marginBottom: '1rem' }}>🚀 Быстрый старт</h2>
                    <p style={{ marginBottom: '1.5rem' }}>
                        PW Hub предоставляет мощный Lua API для автоматизации рутинных задач в Perfect World.
                        Создавайте скрипты для массовой активации промокодов, сбора наград и управления аккаунтами.
                    </p>

                    <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(250px, 1fr))', gap: '1rem' }}>
                        <div style={{ padding: '1rem', background: 'rgba(255,255,255,0.05)', borderRadius: '8px' }}>
                            <h4 style={{ color: 'var(--accent)' }}>👥 Управление аккаунтами</h4>
                            <p style={{ fontSize: '0.9rem' }}>Автоматическое переключение между аккаунтами, проверка авторизации</p>
                        </div>
                        <div style={{ padding: '1rem', background: 'rgba(255,255,255,0.05)', borderRadius: '8px' }}>
                            <h4 style={{ color: 'var(--accent)' }}>🌐 Веб-автоматизация</h4>
                            <p style={{ fontSize: '0.9rem' }}>Навигация по сайту, выполнение JavaScript, ожидание элементов</p>
                        </div>
                        <div style={{ padding: '1rem', background: 'rgba(255,255,255,0.05)', borderRadius: '8px' }}>
                            <h4 style={{ color: 'var(--accent)' }}>⚙️ Утилиты</h4>
                            <p style={{ fontSize: '0.9rem' }}>Логирование, задержки, отчеты о прогрессе</p>
                        </div>
                    </div>
                </div>
            </section>

            <section id="getting-started" style={{ marginBottom: '3rem', scrollMarginTop: '2rem' }}>
                <h2>🎯 Начало работы</h2>

                <div className="function-card">
                    <h3>Структура Lua скрипта</h3>
                    <p>Базовый скрипт состоит из последовательности вызовов API функций. Все функции используют callback-подход для асинхронного выполнения.</p>

                    <LuaCodeBlock code={simpleScriptExample} />

                    <div style={{
                        marginTop: '1rem',
                        padding: '1rem',
                        background: 'rgba(255, 179, 0, 0.1)',
                        border: '1px solid var(--accent)',
                        borderRadius: '8px'
                    }}>
                        <strong>💡 Основные принципы:</strong>
                        <ul style={{ marginTop: '0.5rem', paddingLeft: '1.5rem' }}>
                            <li>Все функции асинхронные и используют callback</li>
                            <li>Используйте <code>Print()</code> для отладки</li>
                            <li>Обрабатывайте ошибки в callback функциях</li>
                            <li>Используйте <code>DelayCb()</code> для задержек между операциями</li>
                        </ul>
                    </div>
                </div>
            </section>

            <section id="examples" style={{ marginBottom: '3rem', scrollMarginTop: '2rem' }}>
                <h2>📚 Примеры скриптов</h2>

                <div style={{ display: 'grid', gap: '1.5rem' }}>
                    <div className="function-card">
                        <h3>Активация промокода на всех аккаунтах</h3>
                        <p>Этот скрипт автоматически активирует указанный промокод на всех ваших аккаунтах:</p>

                        <LuaCodeBlock code={promoCodeExample} />

                        <div style={{
                            marginTop: '1rem',
                            padding: '1rem',
                            background: 'rgba(0, 150, 255, 0.1)',
                            border: '1px solid #0096ff',
                            borderRadius: '8px'
                        }}>
                            <strong>🔧 Как это работает:</strong>
                            <ol style={{ marginTop: '0.5rem', paddingLeft: '1.5rem' }}>
                                <li>Получает список всех аккаунтов</li>
                                <li>Для каждого аккаунта переключается и авторизуется</li>
                                <li>Переходит на страницу активации промокодов</li>
                                <li>Вводит промокод и отправляет форму</li>
                                <li>Делает паузу между аккаунтами для избежания блокировки</li>
                            </ol>
                        </div>
                    </div>

                    <div className="function-card">
                        <h3>Сбор сундуков караванщика</h3>
                        <p>Автоматический сбор всех доступных сундуков караванщика:</p>

                        <LuaCodeBlock code={`-- Константы
local PROMO_ITEMS_URL = "https://pwonline.ru/promo_items.php"

-- Функция активации сундука
local function activateChest(chestId)
  local activateUrl = PROMO_ITEMS_URL .. "?do=activate&cart_id=" .. chestId
  Browser_NavigateCb(activateUrl, function()
    Browser_WaitForElementCb("body", 1000, function()
      -- Отмечаем чекбоксы и нажимаем кнопку активации
      local js = [[
        var checkboxes = document.querySelectorAll('input[type="checkbox"]');
        checkboxes.forEach(function(cb){ cb.checked = true; });
        
        var btn = document.querySelector('button[type="submit"]') || 
                  document.querySelector('input[type="submit"]');
        if(btn) {
          btn.click();
          return 'OK';
        }
        return 'ERROR';
      ]]
      Browser_ExecuteScriptCb(js, function(result)
        if result == "OK" then
          Print("Сундук активирован: " .. chestId)
        else
          Print("Ошибка активации сундука: " .. chestId)
        end
      end)
    end)
  end)
end

-- Основная логика
Account_GetAccountsCb(function(accounts)
  for i, acc in ipairs(accounts) do
    Print("Обрабатываем аккаунт: " .. acc.Name)
    
    Account_ChangeAccountCb(acc.Id, function(success)
      if success then
        -- Поиск сундуков на странице
        Browser_NavigateCb(PROMO_ITEMS_URL, function()
          DelayCb(1500, function()
            local findChestsJS = [[
              var chests = [];
              var links = document.querySelectorAll('a.chest_activate_red');
              links.forEach(function(link) {
                var label = link.parentElement.querySelector('label');
                if (label && label.textContent.includes('Сундук караванщика')) {
                  var match = link.getAttribute('href').match(/cart_id=(\\d+)/);
                  if (match) chests.push(match[1]);
                }
              });
              chests.join(',');
            ]]
            Browser_ExecuteScriptCb(findChestsJS, function(chestsStr)
              if chestsStr and chestsStr ~= "" then
                local chests = {}
                for chestId in chestsStr:gmatch("([^,]+)") do
                  table.insert(chests, chestId)
                end
                Print("Найдено сундуков: " .. #chests)
                
                -- Активируем все сундуки
                for j, chestId in ipairs(chests) do
                  DelayCb(j * 1000, function()
                    activateChest(chestId)
                  end)
                end
              else
                Print("Сундуки не найдены")
              end
            end)
          end)
        end)
      end
    end)
    
    -- Пауза между аккаунтами
    DelayCb(i * 3000, function() end)
  end
end)`} />
                    </div>
                </div>
            </section>

            <section id="best-practices" style={{ scrollMarginTop: '2rem' }}>
                <h2>💡 Лучшие практики</h2>

                <div style={{ display: 'grid', gap: '1rem' }}>
                    <div style={{ padding: '1rem', background: 'rgba(255,255,255,0.05)', borderRadius: '8px' }}>
                        <h4 style={{ color: 'var(--accent)' }}>🛡️ Безопасность</h4>
                        <ul style={{ paddingLeft: '1.5rem', color: 'var(--text-secondary)' }}>
                            <li>Не храните чувствительные данные в скриптах</li>
                            <li>Используйте официальные API методы</li>
                            <li>Проверяйте скрипты перед запуском</li>
                            <li>Используйте задержки между операциями для избежания блокировки</li>
                        </ul>
                    </div>

                    <div style={{ padding: '1rem', background: 'rgba(255,255,255,0.05)', borderRadius: '8px' }}>
                        <h4 style={{ color: 'var(--accent)' }}>⚡ Производительность</h4>
                        <ul style={{ paddingLeft: '1.5rem', color: 'var(--text-secondary)' }}>
                            <li>Используйте задержки между операциями (1000-3000ms)</li>
                            <li>Обрабатывайте ошибки в callback функциях</li>
                            <li>Используйте ReportProgress для длительных операций</li>
                            <li>Логируйте ключевые этапы выполнения с помощью Print()</li>
                        </ul>
                    </div>

                    <div style={{ padding: '1rem', background: 'rgba(255,255,255,0.05)', borderRadius: '8px' }}>
                        <h4 style={{ color: 'var(--accent)' }}>📝 Стиль кода</h4>
                        <ul style={{ paddingLeft: '1.5rem', color: 'var(--text-secondary)' }}>
                            <li>Используйте понятные имена переменных и функций</li>
                            <li>Добавляйте комментарии к сложным участкам кода</li>
                            <li>Разбивайте большие скрипты на логические функции</li>
                            <li>Используйте отступы и форматирование для читаемости</li>
                        </ul>
                    </div>
                </div>
            </section>
        </div>
    )
}