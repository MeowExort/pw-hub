using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Pw.Hub.Services
{
    /// <summary>
    /// Сервис генерации текстовых описаний (Markdown) для модулей через AI.
    /// Инкапсулирует сетевой вызов и формирование промптов.
    /// </summary>
    public interface IAiDocService
    {
        /// <summary>
        /// Сгенерировать описание модуля на основе полей и скрипта.
        /// Возвращает Markdown-текст.
        /// </summary>
        Task<string> GenerateDescriptionAsync(
            string name,
            string version,
            IReadOnlyCollection<(string Name, string Type, string Label)> inputs,
            string scriptFragment,
            string? previousDescription = null,
            string? previousScriptFragment = null,
            CancellationToken ct = default);
    }
}
