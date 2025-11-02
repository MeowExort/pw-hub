using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Pw.Hub.Services
{
    /// <summary>
    /// Сервис взаимодействия с AI-помощником для редактора Lua.
    /// Инкапсулирует сетевые вызовы и построение diff к текущему коду.
    /// </summary>
    public interface IAiAssistantService
    {
        /// <summary>
        /// Отправляет запрос пользователю и возвращает ответ ассистента, включая извлечённый код и diff.
        /// onStreamDelta (если задан) вызывается при поступлении каждого фрагмента текста от ассистента (stream=true).
        /// </summary>
        /// <param name="prompt">Запрос пользователя к AI</param>
        /// <param name="currentCode">Текущий код в редакторе</param>
        /// <param name="manualChangesDiff">Diff ручных правок с момента последнего обращения к AI (null если нет изменений)</param>
        /// <param name="ct">Токен отмены</param>
        /// <param name="onStreamDelta">Callback для получения потоковых фрагментов ответа</param>
        Task<AiAssistantResponse> SendAsync(string prompt, string currentCode, string? manualChangesDiff = null, CancellationToken ct = default, Action<string>? onStreamDelta = null);

        /// <summary>
        /// Начать новую сессию (очистить историю сообщений на стороне сервиса, если требуется).
        /// </summary>
        void NewSession();
    }

    public sealed class AiAssistantResponse
    {
        /// <summary>
        /// Полный текст ответа ассистента (включая пояснения и, возможно, блок кода).
        /// </summary>
        public string AssistantText { get; set; } = string.Empty;
        /// <summary>
        /// Краткое резюме внесённых изменений (для показа отдельным сообщением). Если изменений нет — пояснение.
        /// </summary>
        public string Summary { get; set; } = string.Empty;
        /// <summary>
        /// Извлечённый из ответа блок кода Lua (если был обнаружен в ```lua ... ```), иначе пустая строка.
        /// </summary>
        public string ExtractedCode { get; set; } = string.Empty;
        /// <summary>
        /// Diff‑строки для предпросмотра изменений.
        /// </summary>
        public IList<string> DiffLines { get; set; } = new List<string>();
    }
}
