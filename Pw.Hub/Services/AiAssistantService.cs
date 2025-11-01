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
                content = "You are a helpful assistant that writes Lua code snippets inside fenced blocks like ```lua ... ``` without extra commentary unless asked."
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
