using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Pw.Hub.Services
{
    /// <summary>
    /// Генерация Markdown-описаний модулей через тот же Ollama Chat API, что и AiAssistantService.
    /// </summary>
    public sealed class AiDocService : IAiDocService, IDisposable
    {
        private readonly HttpClient _http;
        private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);
        private readonly string _apiUrl;
        private readonly string _apiKey;
        private readonly string _model;

        public AiDocService()
        {
            _http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
            _apiUrl = Environment.GetEnvironmentVariable("OLLAMA_CHAT_URL")?.Trim() ?? "https://ollama.com/api/chat";
            _apiKey = Environment.GetEnvironmentVariable("OLLAMA_API_KEY")?.Trim() ?? string.Empty;
            _model = Environment.GetEnvironmentVariable("OLLAMA_MODEL")?.Trim() ?? "llama3.1";
        }

        public async Task<string> GenerateDescriptionAsync(
            string name,
            string version,
            IReadOnlyCollection<(string Name, string Type, string Label)> inputs,
            string scriptFragment,
            string? previousDescription = null,
            string? previousScriptFragment = null,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "Введите название модуля";

            var inputsBlock = (inputs == null || inputs.Count == 0)
                ? "(параметров нет)"
                : string.Join("\n", inputs.Select(i => $"- {i.Name} ({i.Type}) — {i.Label}"));

            var scriptInfo = string.IsNullOrWhiteSpace(scriptFragment)
                ? "(скрипт ещё не задан)"
                : scriptFragment;

            var prevDescInfo = string.IsNullOrWhiteSpace(previousDescription) ? "(нет)" : previousDescription;
            var prevScriptInfo = string.IsNullOrWhiteSpace(previousScriptFragment) ? "(нет)" : previousScriptFragment;

            var systemPrompt =
                "Ты помощник по документации. Пиши кратко и по делу на русском языке. Используй Markdown. Не придумывай функционал, которого нет. Если дан контекст предыдущей версии, добавь раздел 'История изменений' по сравнению с текущей версией.";

            var userPrompt = $@"Сгенерируй краткое описание модуля для каталога. Структура: 1) Краткое резюме (1-2 предложения), 2) Параметры, 3) Пример запуска (если уместно), 4) Предупреждения/требования, 5) История изменений (если есть предыдущая версия).

Название: {name}
Версия: {version}
Параметры:
{inputsBlock}

Текущий скрипт (фрагмент):
{scriptInfo}

Предыдущее описание:
{prevDescInfo}

Предыдущий скрипт (фрагмент):
{prevScriptInfo}

Требования к ответу:
- Формат — Markdown, без лишних преамбул вроде ""Вот описание"".
- Если параметров нет — явно напиши, что модуль не требует ввода.
- Раздел 'История изменений' должен отражать отличия между предыдущей и текущей версиями (кратко: что улучшено, добавлено, удалено).
- Не используй HTML.
";

            var req = new ChatRequest
            {
                model = _model,
                stream = false,
                messages = new()
                {
                    new ChatMessage { role = "system", content = systemPrompt },
                    new ChatMessage { role = "user", content = userPrompt }
                }
            };

            using var msg = new HttpRequestMessage(HttpMethod.Post, _apiUrl)
            {
                Content = new StringContent(JsonSerializer.Serialize(req, _json), Encoding.UTF8, "application/json")
            };
            if (!string.IsNullOrEmpty(_apiKey))
                msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

            try
            {
                using var resp = await _http.SendAsync(msg, ct).ConfigureAwait(false);
                resp.EnsureSuccessStatusCode();
                var json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                var parsed = JsonSerializer.Deserialize<ChatResponse>(json, _json) ?? new ChatResponse();
                return parsed.messages?.LastOrDefault()?.content ?? string.Empty;
            }
            catch (Exception ex)
            {
                return "Ошибка AI: " + ex.Message;
            }
        }

        public void Dispose() => _http.Dispose();

        private sealed class ChatMessage { public string role { get; set; } = string.Empty; public string content { get; set; } = string.Empty; }
        private sealed class ChatRequest { public string model { get; set; } = string.Empty; public List<ChatMessage> messages { get; set; } = new(); public bool stream { get; set; } }
        private sealed class ChatResponse { public List<ChatMessage> messages { get; set; } = new(); }
    }
}
