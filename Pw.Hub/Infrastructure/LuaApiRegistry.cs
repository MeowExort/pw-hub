using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Pw.Hub.Infrastructure;

/// <summary>
/// Единый реестр описаний Lua API, используемый автодополнением и AI‑панелью.
/// Источники данных:
/// 1) Атрибутная разметка методов <see cref="LuaApiFunctionAttribute"/> (сканируется лениво при первом обращении);
/// 2) Явная замена через <see cref="ReplaceAll"/> (для совместимости со старой регистрацией в NLua).
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
    private static bool _initializedFromAttributes;

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
            // Считаем, что данные предоставлены явно и готовы
            _initializedFromAttributes = true;
        }
    }

    private static void EnsureInitialized()
    {
        lock (_lock)
        {
            if (_initializedFromAttributes && _entries.Count > 0)
                return;

            // Сканируем атрибуты только если текущий список пуст
            if (_entries.Count == 0)
            {
                try
                {
                    var list = new List<Entry>();
                    var assemblies = AppDomain.CurrentDomain.GetAssemblies()
                        .Where(a => !a.IsDynamic)
                        .Where(a => a.GetName().Name?.StartsWith("Pw.") == true)
                        .ToArray();

                    foreach (var asm in assemblies)
                    {
                        Type? attrType = typeof(LuaApiFunctionAttribute);
                        foreach (var type in asm.GetTypes())
                        {
                            foreach (var m in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
                            {
                                var attr = m.GetCustomAttribute<LuaApiFunctionAttribute>(inherit: false);
                                if (attr == null) continue;
                                var name = string.IsNullOrWhiteSpace(attr.Name) ? m.Name : attr.Name!;
                                list.Add(new Entry
                                {
                                    Name = name,
                                    Version = attr.Version ?? "v1",
                                    Category = attr.Category ?? string.Empty,
                                    Signature = attr.Signature ?? string.Empty,
                                    Description = attr.Description ?? string.Empty,
                                    Snippet = attr.Snippet ?? string.Empty,
                                });
                            }
                        }
                    }

                    _entries = list
                        .Where(e => !string.IsNullOrWhiteSpace(e.Name))
                        .GroupBy(e => e.Name)
                        .Select(g => g.First())
                        .OrderBy(e => e.Category)
                        .ThenBy(e => e.Name)
                        .ToList();
                }
                catch
                {
                    // ignore scanning errors — оставим список пустым, будет сообщение ниже
                }
            }

            _initializedFromAttributes = true;
        }
    }

    /// <summary>
    /// Возвращает копию текущего каталога API.
    /// </summary>
    public static IReadOnlyList<Entry> GetAll()
    {
        EnsureInitialized();
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
        EnsureInitialized();
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
