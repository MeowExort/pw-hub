using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.IO;
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
        private readonly IAiConfigService _cfg;

        public AiDocService()
        {
            _http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
            _cfg = (App.Services?.GetService(typeof(IAiConfigService)) as IAiConfigService) ?? new AiConfigService();
            var eff = _cfg.GetEffective();
            _apiUrl = eff.ApiUrl;
            _apiKey = eff.ApiKey;
            _model = eff.Model;
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
            // Эффективные настройки (перечитываем из окружения при каждом вызове)
            var effectiveUrl = _apiUrl;
            var effectiveKey = _apiKey;
            var effectiveModel = _model;
            try
            {
                var newUrl = Environment.GetEnvironmentVariable("OLLAMA_CHAT_URL")?.Trim();
                var newKey = Environment.GetEnvironmentVariable("OLLAMA_API_KEY")?.Trim();
                var newModel = Environment.GetEnvironmentVariable("OLLAMA_MODEL")?.Trim();
                if (!string.IsNullOrWhiteSpace(newUrl)) effectiveUrl = newUrl;
                if (newKey != null) effectiveKey = newKey;
                if (!string.IsNullOrWhiteSpace(newModel)) effectiveModel = newModel;
            }
            catch { }

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
                model = effectiveModel,
                stream = true,
                messages = new()
                {
                    new ChatMessage { role = "system", content = systemPrompt },
                    new ChatMessage { role = "user", content = userPrompt }
                }
            };

            using var msg = new HttpRequestMessage(HttpMethod.Post, effectiveUrl)
            {
                Content = new StringContent(JsonSerializer.Serialize(req, _json), Encoding.UTF8, "application/json")
            };
            if (!string.IsNullOrEmpty(effectiveKey))
                msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", effectiveKey);

            try
            {
                using var resp = await _http.SendAsync(msg, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
                resp.EnsureSuccessStatusCode();

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
                            var chunk = JsonSerializer.Deserialize<StreamChunk>(line, _json);
                            if (!string.IsNullOrEmpty(chunk?.response)) sb.Append(chunk.response ?? string.Empty);
                            else if (!string.IsNullOrEmpty(chunk?.message?.content)) sb.Append(chunk?.message?.content ?? string.Empty);
                        }
                        catch { }
                    }
                }
                catch { }

                if (sb.Length > 0)
                    return sb.ToString();

                // Fallback: read full JSON
                var json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                try
                {
                    var parsed = JsonSerializer.Deserialize<ChatResponse>(json, _json) ?? new ChatResponse();
                    return parsed.messages?.LastOrDefault()?.content ?? string.Empty;
                }
                catch
                {
                    try
                    {
                        var chunk = JsonSerializer.Deserialize<StreamChunk>(json, _json);
                        return chunk?.response ?? chunk?.message?.content ?? string.Empty;
                    }
                    catch { return json; }
                }
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
        private sealed class StreamChunk { public bool done { get; set; } public ChatMessage message { get; set; } public string response { get; set; } }
    }
}
