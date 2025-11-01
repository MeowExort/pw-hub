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
        /// </summary>
        Task<AiAssistantResponse> SendAsync(string prompt, string currentCode, CancellationToken ct = default);

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
        /// Извлечённый из ответа блок кода Lua (если был обнаружен в ```lua ... ```), иначе пустая строка.
        /// </summary>
        public string ExtractedCode { get; set; } = string.Empty;
        /// <summary>
        /// Diff‑строки для предпросмотра изменений.
        /// </summary>
        public IList<string> DiffLines { get; set; } = new List<string>();
    }
}
