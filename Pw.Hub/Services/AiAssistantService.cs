using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Pw.Hub.Services
{
    /// <summary>
    /// Реализация IAiAssistantService поверх Ollama Cloud API (простая чат-сессия без стриминга).
    /// Выполняет извлечение кода из ответа и строит diff к текущему тексту редактора.
    /// </summary>
    public class AiAssistantService : IAiAssistantService, IDisposable
    {
        private readonly IDiffPreviewService _diff;
        private readonly HttpClient _http;
        private readonly List<AiMessage> _messages = new();
        private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

        private string _apiUrl;
        private string _apiKey;
        private string _model;

        public AiAssistantService(IDiffPreviewService diff)
        {
            _diff = diff;
            _http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
            // Конфигурация через переменные окружения с дефолтами
            _apiUrl = Environment.GetEnvironmentVariable("OLLAMA_CHAT_URL")?.Trim() ?? "https://ollama.com/api/chat";
            _apiKey = Environment.GetEnvironmentVariable("OLLAMA_API_KEY")?.Trim() ?? string.Empty;
            _model = Environment.GetEnvironmentVariable("OLLAMA_MODEL")?.Trim() ?? "llama3.1";
        }

        public void NewSession()
        {
            _messages.Clear();
        }

        public async Task<AiAssistantResponse> SendAsync(string prompt, string currentCode, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(prompt))
                return new AiAssistantResponse { AssistantText = "Пустой запрос" };

            EnsureSystemPriming();
            _messages.Add(new AiMessage { role = "user", content = prompt });

            var req = new AiChatRequest
            {
                model = _model,
                messages = _messages.ToList(),
                stream = false
            };

            using var msg = new HttpRequestMessage(HttpMethod.Post, _apiUrl)
            {
                Content = new StringContent(JsonSerializer.Serialize(req, _jsonOptions), Encoding.UTF8, "application/json")
            };

            if (!string.IsNullOrEmpty(_apiKey))
            {
                msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            }

            string assistantText;
            try
            {
                using var resp = await _http.SendAsync(msg, ct).ConfigureAwait(false);
                resp.EnsureSuccessStatusCode();
                var json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                var parsed = JsonSerializer.Deserialize<AiChatResponse>(json, _jsonOptions) ?? new AiChatResponse { messages = new List<AiMessage>() };
                assistantText = parsed.messages?.LastOrDefault()?.content ?? string.Empty;
            }
            catch (Exception ex)
            {
                assistantText = "Ошибка запроса к AI: " + ex.Message;
            }

            if (string.IsNullOrEmpty(assistantText)) assistantText = "(пустой ответ)";
            var code = ExtractLuaCodeBlock(assistantText);

            IList<string> diffLines = Array.Empty<string>();
            if (!string.IsNullOrEmpty(code))
            {
                try { diffLines = _diff.BuildUnifiedDiffGit(currentCode?.Replace("\r\n", "\n"), code.Replace("\r\n", "\n")); }
                catch { diffLines = new List<string> { "Не удалось построить diff" }; }
            }

            // Обновляем историю: добавляем ответ ассистента
            _messages.Add(new AiMessage { role = "assistant", content = assistantText });

            return new AiAssistantResponse
            {
                AssistantText = assistantText,
                ExtractedCode = code ?? string.Empty,
                DiffLines = diffLines
            };
        }

        private void EnsureSystemPriming()
        {
            if (_messages.Count > 0 && _messages[0].role == "system") return;
            _messages.Insert(0, new AiMessage
            {
                role = "system",
                content = @"Ты - эксперт по Lua скриптам для автоматизации игровых процессов. 
Сгенерируй чистый, рабочий код на Lua.
Отвечай ТОЛЬКО кодом в одном блоке ```lua ...``` без пояснений.
Если в запросе просят правки, верни весь итоговый файл со внесёнными изменениями. 

ДОСТУПНОЕ API (все функции асинхронные с callback):

=== РАБОТА С АККАУНТАМИ ===
Account_GetAccountCb(cb) - получить текущий аккаунт
Account_GetAccountsCb(cb) - получить все аккаунты (возвращает таблицу с полями: Id, Name, OrderIndex, Squad, Servers)
Account_IsAuthorizedCb(cb) - проверка авторизации (возвращает boolean)
Account_ChangeAccountCb(accountId, cb) - сменить аккаунт

=== РАБОТА С БРАУЗЕРОМ ===
Browser_NavigateCb(url, cb) - перейти по URL
Browser_ReloadCb(cb) - перезагрузить страницу
Browser_ExecuteScriptCb(jsCode, cb) - выполнить JavaScript
Browser_ElementExistsCb(selector, cb) - проверить наличие элемента
Browser_WaitForElementCb(selector, timeoutMs, cb) - ждать появление элемента

=== ВСПОМОГАТЕЛЬНЫЕ ФУНКЦИИ ===
Print(text) - вывести текст в лог
DelayCb(ms, cb) - асинхронная задержка
ReportProgress(percent) - отчет о прогрессе
ReportProgressMsg(percent, message) - отчет с сообщением
Complete(result) - завершить выполнение модуля (если скрипт запущен как модуль)
Net_PostJsonCb(url, jsonBody, contentType, cb) - отправить POST запрос (возвращает таблицу с полями: Success, ResponseBody, Error)
Telegram_SendMessageCb(textMessage, contentType, cb) - отправить сообщение пользователю

=== СТРУКТУРА ДАННЫХ ===
Аккаунт: {Id, Name, OrderIndex, Squad, Servers[]}
Отряд: {Id, Name, OrderIndex}
Сервер: {Id, Name, OptionId, DefaultCharacterOptionId, Characters[]}
Персонаж: {Id, Name, OptionId}

=== АРГУМЕНТЫ ЗАПУСКА (глобальная таблица args) ===
При запуске/отладке из редактора перед показом диалога запуска появляется окно ввода параметров. 
Если у модуля заданы входные параметры, в среде Lua доступна глобальная таблица args с введёнными значениями:
- Доступ: args.param или args[""param""]; Если параметр не введён — значение будет nil.
- Возможные типы значений по типу ввода:
  • string/password — строка
  • number — число (если введено корректно), иначе строка; проверяй через type(...) или tonumber
  • bool — boolean (true/false)
  • squad — таблица Отряд {Id, Name, OrderIndex, Accounts?}
  • squads — массив таблиц Отряд
  • account — таблица Аккаунт {Id, Name, OrderIndex, Squad, Servers[]}
  • accounts — массив таблиц Аккаунт
Пример использования:
```lua
-- Безопасно читаем строковый параметр username
local username = (args and args.username) or ""guest""
Print(""Пользователь:"" .. username)

-- Перебор выбранных аккаунтов (если есть)
if args and args.accounts then
    for i, acc in ipairs(args.accounts) do
        Print(string.format(""[%d] %s (%s)"", i, acc.Name or """" , acc.Id or """"))
    end
end
```

ВАЖНЫЕ ПРАВИЛА:
1. ВСЕГДА используй асинхронные версии функций (оканчиваются на Cb)
2. Добавляй комментарии на русском языке для основных блоков
3. Обрабатывай возможные ошибки и пограничные случаи
4. Используй понятные именования переменных
5. Логируй ключевые этапы через Print()
6. Если функция Complete доступна - вызывай ее в конце
7. Для работы с таблицами используй ipairs и # для размера
8. Всегда проверяй существование данных перед использованием
9. Функции не могут использовать другие функции, объявленные после них. Поэтому в самом начале объяви все функции для взаимных вызовов, а потом присвой им значение.
10. JavaScript для выполнения должен быть объявлен в однострочной переменной.
11. Выполненный JavaScript может вернуть только строку.
12. Если результатом выполнения нужен массив, то в JavaScript нужно соединить результат в одну строку с разделителем, а в lua потом разбить строку на массив через разделитель.
13. Переходить можно только по ссылкам с доменом pwonline.ru
14. Перед обращением к элементу нужно ждать, пока он появится (1000 МС достаточно)."
            });
        }

        private static string ExtractLuaCodeBlock(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            var m = Regex.Match(text, "```lua\\s*(.*?)```", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            if (m.Success) return m.Groups[1].Value.Trim();
            // fallback: generic fenced code
            m = Regex.Match(text, "```\\s*(.*?)```", RegexOptions.Singleline);
            if (m.Success) return m.Groups[1].Value.Trim();
            return string.Empty;
        }

        public void Dispose()
        {
            _http.Dispose();
        }

        // DTOs matching Ollama API
        private sealed class AiMessage { public string role { get; set; } = string.Empty; public string content { get; set; } = string.Empty; }
        private sealed class AiChatRequest { public string model { get; set; } = string.Empty; public List<AiMessage> messages { get; set; } = new(); public bool stream { get; set; } }
        private sealed class AiChatResponse { public List<AiMessage> messages { get; set; } = new(); }
    }
}
