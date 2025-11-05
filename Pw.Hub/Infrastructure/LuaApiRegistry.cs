using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Pw.Hub.Infrastructure;

/// <summary>
/// Единый реестр описаний Lua API, используемый автодополнением и AI‑панелью.
/// Пополняется при регистрации функций в Lua (см. LuaIntegration.Register).
/// </summary>
public static class LuaApiRegistry
{
    /// <summary>
    /// Описание одной экспортируемой функции Lua API.
    /// </summary>
    public sealed class Entry
    {
        /// <summary>Отображаемое имя функции в Lua.</summary>
        public string Name { get; init; } = string.Empty;
        /// <summary>Версия API: "v1" или "v2".</summary>
        public string Version { get; init; } = "v1";
        /// <summary>Категория (Account, Browser, BrowserV2, Helpers, Net, Telegram и т.п.).</summary>
        public string Category { get; init; } = string.Empty;
        /// <summary>Сигнатура для человека.</summary>
        public string Signature { get; init; } = string.Empty;
        /// <summary>Короткое описание на русском.</summary>
        public string Description { get; init; } = string.Empty;
        /// <summary>Сниппет для редактора (может содержать __CURSOR__).</summary>
        public string Snippet { get; init; } = string.Empty;
    }

    private static readonly object _lock = new();
    private static List<Entry> _entries = new();

    /// <summary>
    /// Полностью заменяет реестр новым списком, собираемым при регистрации API.
    /// </summary>
    public static void ReplaceAll(IEnumerable<Entry> entries)
    {
        if (entries == null) return;
        lock (_lock)
        {
            // Фильтруем дубликаты по Name (на случай двойной регистрации)
            _entries = entries
                .Where(e => !string.IsNullOrWhiteSpace(e?.Name))
                .GroupBy(e => e.Name)
                .Select(g => g.First())
                .OrderBy(e => e.Category)
                .ThenBy(e => e.Name)
                .ToList();
        }
    }

    /// <summary>
    /// Возвращает копию текущего каталога API.
    /// </summary>
    public static IReadOnlyList<Entry> GetAll()
    {
        lock (_lock)
        {
            return _entries.ToList();
        }
    }

    /// <summary>
    /// Возвращает краткую «шпаргалку» по API для системного промпта AI.
    /// </summary>
    public static string ToCheatSheetText(int examplesLimitPerCategory = 3)
    {
        List<Entry> list;
        lock (_lock)
        {
            list = _entries.ToList();
        }
        if (list.Count == 0)
            return "Доступные функции Lua API отсутствуют (каталог пуст).";

        var sb = new StringBuilder();
        sb.AppendLine("Доступные функции Lua API (v1 и v2):");
        foreach (var grp in list.GroupBy(e => e.Category))
        {
            sb.AppendLine($"\n[{grp.Key}]");
            foreach (var f in grp)
            {
                var ver = string.IsNullOrWhiteSpace(f.Version) ? "" : $" ({f.Version})";
                sb.AppendLine($"- {f.Name}{ver} — {f.Signature}. {f.Description}");
            }
            // Примеры (не более N на категорию)
            var examples = grp.Take(examplesLimitPerCategory).ToList();
            if (examples.Count > 0)
            {
                sb.AppendLine("Примеры:");
                foreach (var ex in examples)
                {
                    if (!string.IsNullOrWhiteSpace(ex.Snippet))
                    {
                        sb.AppendLine();
                        sb.AppendLine(ex.Snippet);
                    }
                }
            }
        }
        return sb.ToString();
    }
}
