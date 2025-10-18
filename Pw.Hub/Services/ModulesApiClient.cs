using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Web;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;

namespace Pw.Hub.Services
{
    public class ModulesApiClient
    {
        private readonly HttpClient _http;
        public string BaseUrl { get; }

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public ModulesApiClient(string? baseUrl = null, HttpMessageHandler? handler = null)
        {
            BaseUrl = baseUrl?.TrimEnd('/') ?? Environment.GetEnvironmentVariable("PW_MODULES_API")?.TrimEnd('/') ?? "http://localhost:5000";
            _http = handler == null ? new HttpClient() : new HttpClient(handler);
            _http.Timeout = TimeSpan.FromSeconds(15);
        }

        public async Task<PagedModulesResponse> SearchAsync(string? q = null, string? tags = null, string? sort = null, string? order = null, int page = 1, int pageSize = 20)
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
            using var resp = await _http.GetAsync(url);
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<PagedModulesResponse>(json, JsonOptions) ?? new PagedModulesResponse();
            return result;
        }

        public async Task<ModuleDto?> InstallAsync(Guid id, string userId)
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

        public async Task<ModuleDto?> UninstallAsync(Guid id, string userId)
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

        public async Task<ModuleDto?> IncrementRunAsync(Guid id)
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
        public string? Description { get; set; }
        public string DescriptionHtml { get; set; } = string.Empty;
        public string Script { get; set; } = string.Empty;
        public InputDefinitionDto[] Inputs { get; set; } = Array.Empty<InputDefinitionDto>();
        public long RunCount { get; set; }
        public int InstallCount { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }

    public class InputDefinitionDto
    {
        public string Name { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public bool Required { get; set; }
    }
}