using System;

namespace Pw.Hub.Services
{
    /// <summary>
    /// Сервис конфигурации AI. Отвечает за чтение/запись настроек (URL, модель, API ключ)
    /// в конфигурационный файл пользователя и предоставляет «эффективные» значения
    /// (приоритет: файл → переменные окружения → значения по умолчанию).
    /// </summary>
    public interface IAiConfigService
    {
        AiConfig Load();
        void Save(AiConfig cfg);
        AiConfig GetEffective();
    }

    public sealed class AiConfig
    {
        public string ApiUrl { get; set; } = "https://ollama.com/api/chat";
        public string Model { get; set; } = "llama3.1";
        public string ApiKey { get; set; } = string.Empty;
    }
}
