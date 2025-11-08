using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Pw.Hub.Infrastructure.Logging;
using Pw.Hub.Services;

namespace Pw.Hub.Infrastructure;

/// <summary>
/// Отслеживание ресурсов, созданных во время одного запуска Lua-скрипта (ранду).
/// Сейчас используется для автоматического закрытия браузеров v2, созданных скриптом,
/// если скрипт их не закрыл сам.
/// </summary>
public static class RunContextTracker
{
    private static readonly ILogger _log = Log.For("RunContextTracker");

    // Флаг поведения: можно будет вынести в настройки.
    public static bool AutoCloseV2OnFinish { get; set; } = true;

    // Текущий runId в асинхронном контексте
    private static readonly AsyncLocal<Guid?> _currentRun = new();

    // Карта: runId -> множество дескрипторов браузеров v2
    private static readonly ConcurrentDictionary<Guid, ConcurrentDictionary<int, byte>> _byRun = new();

    /// <summary>
    /// Начать новый запуск и сделать его активным в текущем асинхронном контексте.
    /// </summary>
    public static Guid BeginRun()
    {
        var id = Guid.NewGuid();
        _currentRun.Value = id;
        _byRun.TryAdd(id, new ConcurrentDictionary<int, byte>());
        try { _log.Debug("BeginRun", new Dictionary<string, object?> { { "runId", id } }); } catch { }
        return id;
    }

    /// <summary>
    /// Установить активный runId в текущем асинхронном контексте (если требуется вручную).
    /// </summary>
    public static void SetActive(Guid runId)
    {
        _currentRun.Value = runId;
    }

    /// <summary>
    /// Очистить активный runId из текущего контекста.
    /// </summary>
    public static void ClearActive()
    {
        _currentRun.Value = null;
    }

    /// <summary>
    /// Зарегистрировать дескриптор браузера v2 в текущем запуске (implicit AsyncLocal path).
    /// Если активный запуск не установлен, метод безопасно игнорирует вызов.
    /// </summary>
    public static void RegisterHandle(int handle)
    {
        var id = _currentRun.Value;
        if (id == null) return;
        if (_byRun.TryGetValue(id.Value, out var set))
        {
            set.TryAdd(handle, 0);
            try { _log.Debug("RegisterHandle (implicit)", new Dictionary<string, object?> { { "runId", id }, { "handle", handle } }); } catch { }
        }
    }

    /// <summary>
    /// Зарегистрировать дескриптор браузера v2 в указанном запуске (explicit runId path).
    /// </summary>
    public static void RegisterHandle(int handle, Guid runId)
    {
        var set = _byRun.GetOrAdd(runId, _ => new ConcurrentDictionary<int, byte>());
        set.TryAdd(handle, 0);
        try { _log.Debug("RegisterHandle (explicit)", new Dictionary<string, object?> { { "runId", runId }, { "handle", handle } }); } catch { }
    }

    /// <summary>
    /// Удалить дескриптор браузера v2 из текущего запуска (например, если он закрыт вручную).
    /// </summary>
    public static void UnregisterHandle(int handle)
    {
        var id = _currentRun.Value;
        if (id == null) return;
        if (_byRun.TryGetValue(id.Value, out var set))
        {
            set.TryRemove(handle, out _);
            try { _log.Debug("UnregisterHandle (implicit)", new Dictionary<string, object?> { { "runId", id }, { "handle", handle } }); } catch { }
        }
    }

    /// <summary>
    /// Удалить дескриптор браузера v2 из указанного запуска (explicit runId path).
    /// </summary>
    public static void UnregisterHandle(int handle, Guid runId)
    {
        if (_byRun.TryGetValue(runId, out var set))
        {
            set.TryRemove(handle, out _);
            try { _log.Debug("UnregisterHandle (explicit)", new Dictionary<string, object?> { { "runId", runId }, { "handle", handle } }); } catch { }
        }
    }

    /// <summary>
    /// Снимок дескрипторов для указанного runId.
    /// </summary>
    public static int[] Snapshot(Guid runId)
    {
        if (_byRun.TryGetValue(runId, out var set))
        {
            return set.Keys.ToArray();
        }
        return Array.Empty<int>();
    }

    /// <summary>
    /// Завершение запуска: попытаться закрыть все оставшиеся браузеры v2, зарегистрированные этим запуском.
    /// После выполнения список очищается.
    /// </summary>
    public static async Task EndRunCloseAll(BrowserManager? mgr, ILogger? log, Guid runId)
    {
        try { log ??= _log; } catch { }
        if (!AutoCloseV2OnFinish)
        {
            try { log?.Info("EndRun: auto-close disabled"); } catch { }
            _byRun.TryRemove(runId, out _);
            return;
        }
        var handles = Snapshot(runId);
        int closed = 0;
        try { log?.Info("EndRun: cleanup start", new Dictionary<string, object?> { { "runId", runId }, { "handles", handles } }); } catch { }
        if (mgr != null)
        {
            foreach (var h in handles)
            {
                try
                {
                    if (mgr.Close(h)) closed++;
                }
                catch
                {
                    // ignore per-handle errors
                }
            }
        }
        _byRun.TryRemove(runId, out _);
        try { log?.Info("EndRun: cleanup completed", new Dictionary<string, object?> { { "runId", runId }, { "closed", closed }, { "total", handles.Length } }); } catch { }
        await Task.CompletedTask;
    }
}
