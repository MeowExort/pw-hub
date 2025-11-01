using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Web;

namespace Pw.Hub.Services
{
    public class ModulesApiClient
    {
        private readonly HttpClient _http;
        public string BaseUrl { get; }
        public string Token { get; private set; }
        public UserDto CurrentUser { get; private set; }

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public ModulesApiClient(string baseUrl = null, HttpMessageHandler handler = null)
        {
            BaseUrl = baseUrl?.TrimEnd('/') ?? Environment.GetEnvironmentVariable("PW_MODULES_API")?.TrimEnd('/') ?? "https://api.pw-hub.ru";
            _http = handler == null ? new HttpClient() : new HttpClient(handler);
            _http.Timeout = TimeSpan.FromSeconds(15);
            try
            {
                AuthState.Load();
                if (!string.IsNullOrWhiteSpace(AuthState.Token))
                {
                    Token = AuthState.Token;
                    ApplyAuthHeader();
                    // Do not restore user from disk; it must be fetched via /me
                }
            }
            catch { }
        }

        private void ApplyAuthHeader()
        {
            _http.DefaultRequestHeaders.Remove("X-Auth-Token");
            if (!string.IsNullOrEmpty(Token))
                _http.DefaultRequestHeaders.Add("X-Auth-Token", Token);
        }

        public async Task<AuthResponse> RegisterAsync(string username, string password)
        {
            var url = $"{BaseUrl}/api/auth/register";
            var resp = await _http.PostAsync(url, new StringContent(JsonSerializer.Serialize(new { username, password }, JsonOptions), Encoding.UTF8, "application/json"));
            if (!resp.IsSuccessStatusCode) return null;
            var json = await resp.Content.ReadAsStringAsync();
            var ar = JsonSerializer.Deserialize<AuthResponse>(json, JsonOptions);
            if (ar != null)
            {
                Token = ar.Token;
                ApplyAuthHeader();
                CurrentUser = new UserDto { UserId = ar.UserId, Username = ar.Username, Developer = ar.Developer };
            }
            return ar;
        }

        public async Task<AuthResponse> LoginAsync(string username, string password)
        {
            var url = $"{BaseUrl}/api/auth/login";
            var resp = await _http.PostAsync(url, new StringContent(JsonSerializer.Serialize(new { username, password }, JsonOptions), Encoding.UTF8, "application/json"));
            if (!resp.IsSuccessStatusCode) return null;
            var json = await resp.Content.ReadAsStringAsync();
            var ar = JsonSerializer.Deserialize<AuthResponse>(json, JsonOptions);
            if (ar != null)
            {
                Token = ar.Token;
                ApplyAuthHeader();
                CurrentUser = new UserDto { UserId = ar.UserId, Username = ar.Username, Developer = ar.Developer };
            }
            return ar;
        }

        public async Task<UserDto> MeAsync()
        {
            ApplyAuthHeader();
            var resp = await _http.GetAsync($"{BaseUrl}/api/auth/me");
            if (!resp.IsSuccessStatusCode) return null;
            var json = await resp.Content.ReadAsStringAsync();
            var me = JsonSerializer.Deserialize<UserDto>(json, JsonOptions);
            if (me != null)
            {
                CurrentUser = me;
            }
            return me;
        }

        public async Task<UserDto> UpdateUsernameAsync(string username)
        {
            ApplyAuthHeader();
            var url = $"{BaseUrl}/api/auth/username";
            var payload = new { username };
            var resp = await _http.PostAsync(url, new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json"));
            if (!resp.IsSuccessStatusCode) return null;
            var json = await resp.Content.ReadAsStringAsync();
            var user = JsonSerializer.Deserialize<UserDto>(json, JsonOptions);
            if (user != null)
                CurrentUser = user;
            return user;
        }

        public async Task<bool> ChangePasswordAsync(string currentPassword, string newPassword)
        {
            ApplyAuthHeader();
            var url = $"{BaseUrl}/api/auth/password";
            var payload = new { currentPassword, newPassword };
            var resp = await _http.PostAsync(url, new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json"));
            return resp.IsSuccessStatusCode;
        }

        public sealed class TelegramLinkInfo
        {
            public string Link { get; set; }
            public string State { get; set; }
            public string BotUsername { get; set; }
            public DateTimeOffset ExpiresAt { get; set; }
        }

        public async Task<TelegramLinkInfo> GenerateTelegramLinkAsync()
        {
            ApplyAuthHeader();
            var url = $"{BaseUrl}/api/auth/telegram/link";
            var resp = await _http.PostAsync(url, new StringContent(string.Empty, Encoding.UTF8, "application/json"));
            if (!resp.IsSuccessStatusCode) return null;
            var json = await resp.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<TelegramLinkInfo>(json, JsonOptions);
        }

        public async Task<UserDto> UnlinkTelegramAsync()
        {
            ApplyAuthHeader();
            var url = $"{BaseUrl}/api/auth/telegram/unlink";
            var resp = await _http.PostAsync(url, new StringContent(string.Empty, Encoding.UTF8, "application/json"));
            if (!resp.IsSuccessStatusCode) return null;
            var json = await resp.Content.ReadAsStringAsync();
            var me = JsonSerializer.Deserialize<UserDto>(json, JsonOptions);
            if (me != null) CurrentUser = me;
            return me;
        }

        /// <summary>
        /// Отправляет сообщение текущему пользователю в Telegram (если привязан).
        /// </summary>
        public async Task<bool> SendTelegramMessageAsync(string message)
        {
            ApplyAuthHeader();
            var url = $"{BaseUrl}/api/auth/telegram/send";
            var payload = new { message };
            var content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");
            using var resp = await _http.PostAsync(url, content);
            return resp.IsSuccessStatusCode;
        }

        public async Task<PagedModulesResponse> SearchAsync(string q = null, string tags = null, string sort = null, string order = null, int page = 1, int pageSize = 20)
        {
            var ub = new UriBuilder(BaseUrl + "/api/modules");
            var query = HttpUtility.ParseQueryString(ub.Query);
            if (!string.IsNullOrWhiteSpace(q)) query["q"] = q;
            if (!string.IsNullOrWhiteSpace(tags)) query["tags"] = tags;
            if (!string.IsNullOrWhiteSpace(sort)) query["sort"] = sort;
            if (!string.IsNullOrWhiteSpace(order)) query["order"] = order;
            if (page > 1) query["page"] = page.ToString();
            if (pageSize != 20) query["pageSize"] = pageSize.ToString();
            ub.Query = query.ToString();
            var url = ub.ToString();
            ApplyAuthHeader();
            using var resp = await _http.GetAsync(url);
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<PagedModulesResponse>(json, JsonOptions) ?? new PagedModulesResponse();
            return result;
        }

        public async Task<ModuleDto> CreateModuleAsync(CreateOrUpdateModule req)
        {
            ApplyAuthHeader();
            var url = $"{BaseUrl}/api/modules";
            var resp = await _http.PostAsync(url, new StringContent(JsonSerializer.Serialize(req, JsonOptions), Encoding.UTF8, "application/json"));
            if (!resp.IsSuccessStatusCode) return null;
            var json = await resp.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<ModuleDto>(json, JsonOptions);
        }

        public async Task<ModuleDto> UpdateModuleAsync(Guid id, CreateOrUpdateModule req)
        {
            ApplyAuthHeader();
            var url = $"{BaseUrl}/api/modules/{id}";
            var resp = await _http.PutAsync(url, new StringContent(JsonSerializer.Serialize(req, JsonOptions), Encoding.UTF8, "application/json"));
            if (!resp.IsSuccessStatusCode) return null;
            var json = await resp.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<ModuleDto>(json, JsonOptions);
        }

        public async Task<bool> DeleteModuleAsync(Guid id)
        {
            ApplyAuthHeader();
            var url = $"{BaseUrl}/api/modules/{id}";
            var resp = await _http.DeleteAsync(url);
            return resp.IsSuccessStatusCode;
        }

        public async Task<ModuleDto> InstallAsync(Guid id, string userId)
        {
            var url = $"{BaseUrl}/api/modules/{id}/install?userId={Uri.EscapeDataString(userId)}";
            using var resp = await _http.PostAsync(url, new StringContent(string.Empty, Encoding.UTF8, "application/json"));
            if (resp.IsSuccessStatusCode)
            {
                var json = await resp.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<ModuleDto>(json, JsonOptions);
            }
            return null;
        }

        public async Task<ModuleDto> UninstallAsync(Guid id, string userId)
        {
            var url = $"{BaseUrl}/api/modules/{id}/install?userId={Uri.EscapeDataString(userId)}";
            using var resp = await _http.DeleteAsync(url);
            if (resp.IsSuccessStatusCode)
            {
                var json = await resp.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<ModuleDto>(json, JsonOptions);
            }
            return null;
        }

        public async Task<ModuleDto> IncrementRunAsync(Guid id)
        {
            var url = $"{BaseUrl}/api/modules/{id}/run";
            using var resp = await _http.PostAsync(url, new StringContent(string.Empty, Encoding.UTF8, "application/json"));
            if (resp.IsSuccessStatusCode)
            {
                var json = await resp.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<ModuleDto>(json, JsonOptions);
            }
            return null;
        }

        public async Task<List<ModuleDto>> GetInstalledAsync(string userId)
        {
            var url = $"{BaseUrl}/api/modules/installed?userId={Uri.EscapeDataString(userId ?? string.Empty)}";
            ApplyAuthHeader();
            using var resp = await _http.GetAsync(url);
            if (!resp.IsSuccessStatusCode) return new List<ModuleDto>();
            var json = await resp.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<List<ModuleDto>>(json, JsonOptions) ?? new List<ModuleDto>();
        }

        /// <summary>
        /// Возвращает полные данные модуля по идентификатору (включая Script и Inputs).
        /// </summary>
        public async Task<ModuleDto> GetModuleAsync(Guid id)
        {
            ApplyAuthHeader();
            var url = $"{BaseUrl}/api/modules/{id}";
            using var resp = await _http.GetAsync(url);
            if (!resp.IsSuccessStatusCode) return null;
            var json = await resp.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<ModuleDto>(json, JsonOptions);
        }
    }

    public class PagedModulesResponse
    {
        public int Total { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public List<ModuleDto> Items { get; set; } = new();
    }

    public class ModuleDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Version { get; set; } = "1.0.0";
        public string Description { get; set; }
        public string DescriptionHtml { get; set; } = string.Empty;
        public string Script { get; set; } = string.Empty;
        public InputDefinitionDto[] Inputs { get; set; } = Array.Empty<InputDefinitionDto>();
        public long RunCount { get; set; }
        public int InstallCount { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
        public string OwnerUserId { get; set; }
        public string AuthorUsername { get; set; }
    }

    public class InputDefinitionDto
    {
        public string Name { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Default { get; set; } = string.Empty;
        public bool Required { get; set; }
    }

    public class CreateOrUpdateModule
    {
        public string Name { get; set; } = string.Empty;
        public string Version { get; set; } = "1.0.0";
        public string Description { get; set; }
        public string Script { get; set; } = string.Empty;
        public InputDefinitionDto[] Inputs { get; set; } = Array.Empty<InputDefinitionDto>();
    }

    public class AuthResponse
    {
        public string UserId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public bool Developer { get; set; }
        public string Token { get; set; } = string.Empty;
    }

    public class UserDto
    {
        public string UserId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public bool Developer { get; set; }
        public long? TelegramId { get; set; }
        public string? TelegramUsername { get; set; }
        public DateTimeOffset? TelegramLinkedAt { get; set; }
    }
}