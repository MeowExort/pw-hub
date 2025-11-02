using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.IO;
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
        private readonly IAiConfigService _cfg;

        public AiAssistantService(IDiffPreviewService diff)
        {
            _diff = diff;
            _http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
            // Читаем конфигурацию через IAiConfigService (файл → ENV → дефолты)
            _cfg = (App.Services?.GetService(typeof(IAiConfigService)) as IAiConfigService) ?? new AiConfigService();
            var effective = _cfg.GetEffective();
            _apiUrl = effective.ApiUrl;
            _apiKey = effective.ApiKey;
            _model = effective.Model;
        }

        public void NewSession()
        {
            _messages.Clear();
        }

        public async Task<AiAssistantResponse> SendAsync(string prompt, string currentCode, string? manualChangesDiff = null, CancellationToken ct = default, Action<string>? onStreamDelta = null)
        {
            if (string.IsNullOrWhiteSpace(prompt))
                return new AiAssistantResponse { AssistantText = "Пустой запрос" };

            // Перечитываем конфиг: приоритет файл → ENV → дефолты
            try
            {
                var eff = _cfg.GetEffective();
                _apiUrl = eff.ApiUrl;
                _model = eff.Model;
                _apiKey = eff.ApiKey;
            }
            catch { }

            EnsureSystemPriming();
            
            // Формируем расширенный запрос с контекстом текущего кода и ручных правок
            var userPrompt = new StringBuilder();
            userPrompt.AppendLine("=== ТЕКУЩИЙ КОД ===");
            userPrompt.AppendLine("```lua");
            userPrompt.AppendLine(currentCode ?? string.Empty);
            userPrompt.AppendLine("```");
            userPrompt.AppendLine();
            
            if (!string.IsNullOrWhiteSpace(manualChangesDiff))
            {
                userPrompt.AppendLine("=== РУЧНЫЕ ПРАВКИ (с момента последнего обращения к AI) ===");
                userPrompt.AppendLine(manualChangesDiff);
                userPrompt.AppendLine();
            }
            
            userPrompt.AppendLine("=== ЗАПРОС ===");
            userPrompt.AppendLine(prompt);
            
            _messages.Add(new AiMessage { role = "user", content = userPrompt.ToString() });

            var req = new AiChatRequest
            {
                model = _model,
                messages = _messages.ToList(),
                stream = true
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
                using var resp = await _http.SendAsync(msg, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
                resp.EnsureSuccessStatusCode();

                // Попытка потокового чтения (stream=true): NDJSON/JSONL
                var sb = new StringBuilder();
                try
                {
                    await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
                    using var reader = new StreamReader(stream, Encoding.UTF8);
                    while (!reader.EndOfStream)
                    {
                        ct.ThrowIfCancellationRequested();
                        var line = await reader.ReadLineAsync().ConfigureAwait(false);
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        try
                        {
                            var chunk = JsonSerializer.Deserialize<AiStreamChunk>(line, _jsonOptions);
                            string piece = null;
                            if (!string.IsNullOrEmpty(chunk?.message?.content))
                                piece = chunk.message.content;
                            else if (!string.IsNullOrEmpty(chunk?.response))
                                piece = chunk.response;

                            if (!string.IsNullOrEmpty(piece))
                            {
                                sb.Append(piece);
                                try { onStreamDelta?.Invoke(piece); } catch { }
                            }
                        }
                        catch
                        {
                            // Игнорируем парсинг отдельных строк
                        }
                    }
                }
                catch
                {
                    // Если потоковое чтение не удалось, попробуем обычный JSON целиком
                }

                if (sb.Length == 0)
                {
                    var full = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                    try
                    {
                        var parsed = JsonSerializer.Deserialize<AiChatResponse>(full, _jsonOptions) ?? new AiChatResponse { messages = new List<AiMessage>() };
                        assistantText = parsed.messages?.LastOrDefault()?.content ?? string.Empty;
                    }
                    catch
                    {
                        // Попытка распарсить как generate-stream (response)
                        try
                        {
                            var ch = JsonSerializer.Deserialize<AiStreamChunk>(full, _jsonOptions);
                            assistantText = ch?.response ?? string.Empty;
                        }
                        catch
                        {
                            assistantText = full;
                        }
                    }
                }
                else
                {
                    assistantText = sb.ToString();
                }
            }
            catch (Exception ex)
            {
                assistantText = "Ошибка запроса к AI: " + ex.Message;
            }

            if (string.IsNullOrEmpty(assistantText)) assistantText = "(пустой ответ)";
            
            // Извлекаем полный код из ответа AI
            var code = ExtractLuaCodeBlock(assistantText);

            // Извлекаем краткое резюме из сообщения ассистента (до блока кода)
            var aiSummary = ExtractSummaryFromAssistant(assistantText);

            IList<string> diffLines = Array.Empty<string>();
            if (!string.IsNullOrEmpty(code))
            {
                try { diffLines = _diff.BuildUnifiedDiffGit(currentCode?.Replace("\r\n", "\n"), code.Replace("\r\n", "\n")); }
                catch { diffLines = new List<string> { "Не удалось построить diff" }; }
            }

            // Fallback: если AI не прислал резюме, построим краткую сводку из diff
            var summary = string.IsNullOrWhiteSpace(aiSummary) ? BuildSummary(diffLines) : aiSummary;

            // Обновляем историю: добавляем ответ ассистента
            _messages.Add(new AiMessage { role = "assistant", content = assistantText });

            return new AiAssistantResponse
            {
                AssistantText = assistantText,
                Summary = summary,
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

ФОРМАТ ОТВЕТА:
1) Краткое резюме изменений (1–2 предложения)
2) Пустая строка
3) Весь итоговый код в блоке ```lua ... ```

Никаких дополнительных разделов после кода.

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
Telegram_SendMessageCb(textMessage, cb) - отправить сообщение пользователю

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
10. JavaScript для выполнения должен быть объявлен в однострочной переменной.
11. Выполненный JavaScript может вернуть только строку.
12. Если результатом выполнения нужен массив, то в JavaScript нужно соединить результат в одну строку с разделителем, а в lua потом разбить строку на массив через разделитель.
13. Переходить можно только по ссылкам с доменом pwonline.ru
14. Перед обращением к элементу нужно ждать, пока он появится (1000 МС достаточно).
15. Все фукнции должны быть объявлены в начале скрипта для взаимных вызовов.
16. Все константы и настройки должны быть объявлены в начале скрипта.
17. Сообщение пользователю для телеграмм должно быть красивым и информативным, используй смайлы и улучшай читаемость."
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


        private static string ExtractSummaryFromAssistant(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            try
            {
                // Берём всё до первого блока кода
                var idx = text.IndexOf("```", StringComparison.Ordinal);
                var head = idx >= 0 ? text.Substring(0, idx) : text;
                head = head.Replace("\r\n", "\n").Trim();
                if (string.IsNullOrWhiteSpace(head)) return string.Empty;
                // Первый абзац (до двойного перевода строки)
                var paraEnd = head.IndexOf("\n\n", StringComparison.Ordinal);
                var firstPara = paraEnd >= 0 ? head.Substring(0, paraEnd) : head;
                // Уберём маркеры списков/лишние пробелы
                firstPara = Regex.Replace(firstPara, @"^[#>*\s-]+", string.Empty, RegexOptions.Multiline).Trim();
                // Однострочное резюме
                var oneLine = Regex.Replace(firstPara, "\n+", " ").Trim();
                // Ограничим длину
                if (oneLine.Length > 300) oneLine = oneLine.Substring(0, 297) + "...";
                return oneLine;
            }
            catch { return string.Empty; }
        }

        public void Dispose()
        {
            _http.Dispose();
        }

        private static string BuildSummary(IList<string> diffLines)
        {
            if (diffLines == null || diffLines.Count == 0)
                return "Изменений нет. Код идентичен текущему.";
            int add = 0, del = 0, hunks = 0;
            foreach (var l in diffLines)
            {
                if (string.IsNullOrEmpty(l)) continue;
                if (l.StartsWith("@@")) hunks++;
                else if (l.StartsWith("+") && !l.StartsWith("+++")) add++;
                else if (l.StartsWith("-") && !l.StartsWith("---")) del++;
            }
            if (add == 0 && del == 0)
                return "Изменений нет. Код идентичен текущему.";
            return $"Изменения: +{add} / -{del} строк в {hunks} блок(ах). Нажмите ‘Показать полные изменения’, чтобы ознакомиться с diff.";
        }

        // DTOs matching Ollama API
        private sealed class AiMessage { public string role { get; set; } = string.Empty; public string content { get; set; } = string.Empty; }
        private sealed class AiChatRequest { public string model { get; set; } = string.Empty; public List<AiMessage> messages { get; set; } = new(); public bool stream { get; set; } }
        private sealed class AiChatResponse { public List<AiMessage> messages { get; set; } = new(); }
        private sealed class AiStreamChunk { public bool done { get; set; } public AiMessage message { get; set; } public string response { get; set; } }
    }
}
