using System.Collections.Generic;

namespace Pw.Hub.Services
{
    /// <summary>
    /// Сервис построения строк unified diff-превью между двумя текстами.
    /// Отвечает только за вычисление строк превью, без визуального рендеринга.
    /// </summary>
    public interface IDiffPreviewService
    {
        /// <summary>
        /// Формирует git‑подобный unified diff в виде списка строк с контекстом.
        /// </summary>
        /// <param name="oldText">Исходный текст (текущий код).</param>
        /// <param name="newText">Предлагаемый текст (например, от AI).</param>
        /// <param name="context">Число контекстных строк вокруг изменений.</param>
        /// <returns>Список строк diff‑превью.</returns>
        IList<string> BuildUnifiedDiffGit(string oldText, string newText, int context = 3);
    }
}
