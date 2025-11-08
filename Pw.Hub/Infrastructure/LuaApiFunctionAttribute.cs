using System;

namespace Pw.Hub.Infrastructure;

/// <summary>
/// Атрибут для декларативной регистрации функций Lua API.
/// Разметьте им публичный метод-обёртку, доступный из Lua, чтобы он автоматически попал в каталог API
/// (для подсказок/AI) ещё до запуска любых Lua-скриптов.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class LuaApiFunctionAttribute : Attribute
{
    /// <summary>
    /// Имя функции, под которым она доступна из Lua (если не задано, используется имя метода).
    /// Пример: <c>BrowserV2_Create</c>.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Версия API: <c>"v1"</c> или <c>"v2"</c>.
    /// </summary>
    public string Version { get; init; } = "v1";

    /// <summary>
    /// Категория (например: Account, Browser, BrowserV2, Helpers, Net, Telegram).
    /// </summary>
    public string Category { get; init; } = string.Empty;

    /// <summary>
    /// Человекочитаемая сигнатура, как её писать в Lua.
    /// Пример: <c>BrowserV2_Create(options, function(handle) ... end)</c>
    /// </summary>
    public string Signature { get; init; } = string.Empty;

    /// <summary>
    /// Короткое описание на русском.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Сниппет для редактора (может содержать маркер <c>__CURSOR__</c> для позиции курсора).
    /// </summary>
    public string Snippet { get; init; } = string.Empty;
}
