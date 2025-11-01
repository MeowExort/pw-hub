using System;
using System.IO;
using System.Text.Json;

namespace Pw.Hub.Services
{
    /// <summary>
    /// Реализация IAiConfigService. Хранит настройки в файле %AppData%/Pw.Hub/ai.config.json
    /// и синхронизирует их с переменными окружения процесса и пользователя.
    /// </summary>
    public sealed class AiConfigService : IAiConfigService
    {
        private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
        private readonly string _configFilePath;

        public AiConfigService()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var dir = Path.Combine(appData, "Pw.Hub");
            try { if (!Directory.Exists(dir)) Directory.CreateDirectory(dir); } catch { }
            _configFilePath = Path.Combine(dir, "ai.config.json");
        }

        public AiConfig Load()
        {
            try
            {
                if (!File.Exists(_configFilePath))
                {
                    // Вернём значения из окружения/дефолты
                    return GetFromEnvOrDefault();
                }
                var json = File.ReadAllText(_configFilePath);
                var cfg = JsonSerializer.Deserialize<AiConfig>(json, _json) ?? new AiConfig();
                return Sanitize(cfg);
            }
            catch
            {
                return GetFromEnvOrDefault();
            }
        }

        public void Save(AiConfig cfg)
        {
            if (cfg == null) cfg = new AiConfig();
            cfg = Sanitize(cfg);
            try
            {
                var json = JsonSerializer.Serialize(cfg, _json);
                File.WriteAllText(_configFilePath, json);
            }
            catch { }

            // Синхронизируем переменные окружения, чтобы текущий процесс и новые процессы видели настройки
            try
            {
                if (!string.IsNullOrWhiteSpace(cfg.ApiUrl))
                {
                    Environment.SetEnvironmentVariable("OLLAMA_CHAT_URL", cfg.ApiUrl, EnvironmentVariableTarget.User);
                    Environment.SetEnvironmentVariable("OLLAMA_CHAT_URL", cfg.ApiUrl, EnvironmentVariableTarget.Process);
                }
                if (!string.IsNullOrWhiteSpace(cfg.Model))
                {
                    Environment.SetEnvironmentVariable("OLLAMA_MODEL", cfg.Model, EnvironmentVariableTarget.User);
                    Environment.SetEnvironmentVariable("OLLAMA_MODEL", cfg.Model, EnvironmentVariableTarget.Process);
                }
                // API Key допускаем пустым (очистка)
                Environment.SetEnvironmentVariable("OLLAMA_API_KEY", cfg.ApiKey ?? string.Empty, EnvironmentVariableTarget.User);
                Environment.SetEnvironmentVariable("OLLAMA_API_KEY", cfg.ApiKey ?? string.Empty, EnvironmentVariableTarget.Process);
            }
            catch { }
        }

        public AiConfig GetEffective()
        {
            // Приоритет: файл → окружение → дефолты
            var fileCfg = LoadFromFileOrNull();
            if (fileCfg != null) return fileCfg;
            return GetFromEnvOrDefault();
        }

        private AiConfig? LoadFromFileOrNull()
        {
            try
            {
                if (!File.Exists(_configFilePath)) return null;
                var json = File.ReadAllText(_configFilePath);
                var cfg = JsonSerializer.Deserialize<AiConfig>(json, _json);
                return cfg == null ? null : Sanitize(cfg);
            }
            catch { return null; }
        }

        private static AiConfig GetFromEnvOrDefault()
        {
            var url = (Environment.GetEnvironmentVariable("OLLAMA_CHAT_URL") ?? string.Empty).Trim();
            var model = (Environment.GetEnvironmentVariable("OLLAMA_MODEL") ?? string.Empty).Trim();
            var key = (Environment.GetEnvironmentVariable("OLLAMA_API_KEY") ?? string.Empty).Trim();
            return new AiConfig
            {
                ApiUrl = string.IsNullOrWhiteSpace(url) ? "https://ollama.com/api/chat" : url,
                Model = string.IsNullOrWhiteSpace(model) ? "llama3.1" : model,
                ApiKey = key ?? string.Empty
            };
        }

        private static AiConfig Sanitize(AiConfig cfg)
        {
            cfg.ApiUrl = (cfg.ApiUrl ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(cfg.ApiUrl)) cfg.ApiUrl = "https://ollama.com/api/chat";
            cfg.Model = (cfg.Model ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(cfg.Model)) cfg.Model = "llama3.1";
            cfg.ApiKey = (cfg.ApiKey ?? string.Empty).Trim();
            return cfg;
        }
    }
}
