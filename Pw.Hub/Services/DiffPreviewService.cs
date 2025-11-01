using System;
using System.Collections.Generic;
using System.Text;

namespace Pw.Hub.Services
{
    /// <summary>
    /// Реализация сервиса формирования git‑подобного unified diff между двумя текстами.
    /// Возвращает список строк, готовых к отображению в превью.
    /// </summary>
    public sealed class DiffPreviewService : IDiffPreviewService
    {
        /// <inheritdoc />
        public IList<string> BuildUnifiedDiffGit(string oldText, string newText, int context = 3)
        {
            var a = (oldText ?? string.Empty).Replace("\r\n", "\n").Split('\n');
            var b = (newText ?? string.Empty).Replace("\r\n", "\n").Split('\n');
            var lines = new List<string>(a.Length + b.Length + 8)
            {
                "--- Текущий файл",
                "+++ Предложение AI"
            };

            // Построение LCS-таблицы (динамическое программирование)
            int n = a.Length, m = b.Length;
            var dp = new int[n + 1, m + 1];
            for (int i = n - 1; i >= 0; i--)
            {
                for (int j = m - 1; j >= 0; j--)
                {
                    if (a[i] == b[j]) dp[i, j] = dp[i + 1, j + 1] + 1;
                    else dp[i, j] = Math.Max(dp[i + 1, j], dp[i, j + 1]);
                }
            }

            // Восстановление сценария правок
            var edits = new List<(char tag, string text, int ia, int jb)>();
            int ia = 0, jb = 0;
            while (ia < n && jb < m)
            {
                if (a[ia] == b[jb])
                {
                    edits.Add((' ', a[ia], ia, jb));
                    ia++;
                    jb++;
                }
                else if (dp[ia + 1, jb] >= dp[ia, jb + 1])
                {
                    edits.Add(('-', a[ia], ia, jb));
                    ia++;
                }
                else
                {
                    edits.Add(('+', b[jb], ia, jb));
                    jb++;
                }
            }
            while (ia < n) { edits.Add(('-', a[ia], ia, jb)); ia++; }
            while (jb < m) { edits.Add(('+', b[jb], ia, jb)); jb++; }

            // Разбиение на блоки (hunks) с контекстом
            int idx = 0;
            while (idx < edits.Count)
            {
                while (idx < edits.Count && edits[idx].tag == ' ') idx++;
                if (idx >= edits.Count) break;
                int hunkStart = Math.Max(0, idx - context);
                int i2 = idx;
                int lastChange = idx;
                while (i2 < edits.Count)
                {
                    if (edits[i2].tag != ' ') lastChange = i2;
                    if (edits[i2].tag == ' ' && i2 - lastChange > context) break;
                    i2++;
                }
                int hunkEnd = Math.Min(edits.Count, lastChange + context + 1);

                // Подсчёт диапазонов для заголовка
                int oldStart = 0, newStart = 0, oldCount = 0, newCount = 0;
                int oldLine = 1, newLine = 1;
                for (int k = 0; k < hunkStart; k++)
                {
                    if (edits[k].tag != '+') oldLine++;
                    if (edits[k].tag != '-') newLine++;
                }
                oldStart = oldLine;
                newStart = newLine;
                for (int k = hunkStart; k < hunkEnd; k++)
                {
                    if (edits[k].tag != '+') oldCount++;
                    if (edits[k].tag != '-') newCount++;
                }

                lines.Add($"@@ -{oldStart},{oldCount} +{newStart},{newCount} @@");
                for (int k = hunkStart; k < hunkEnd; k++)
                {
                    char tag = edits[k].tag;
                    string t = edits[k].text;
                    if (tag == ' ') lines.Add(" " + t);
                    else if (tag == '+') lines.Add("+" + t);
                    else if (tag == '-') lines.Add("-" + t);
                }
                idx = hunkEnd;
            }

            if (lines.Count <= 2)
            {
                lines.Add("Изменений нет (код идентичен текущему).");
            }
            return lines;
        }
    }
}
