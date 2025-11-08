using System;
using System.Collections.Concurrent;
using System.Threading;
using Pw.Hub.Infrastructure.Logging;

namespace Pw.Hub.Infrastructure;

/// <summary>
/// Tracks outstanding asynchronous operations for a running Lua script (identified by runId).
/// Used to determine when an "async" script became idle so the editor can auto-complete the run.
/// </summary>
public static class RunLifetimeTracker
{
    private static readonly ILogger _log = Log.For("RunLifetimeTracker");

    private static readonly ConcurrentDictionary<Guid, int> _counts = new();
    private static readonly AsyncLocal<Guid?> _current = new();

    public static void SetActive(Guid runId) { _current.Value = runId; }
    public static void ClearActive() { _current.Value = null; }

    public static void BeginOp(Guid? runId = null)
    {
        var id = runId ?? _current.Value;
        if (id == null) return;
        _counts.AddOrUpdate(id.Value, 1, (_, old) => Math.Max(0, old) + 1);
        try { _log.Debug("BeginOp", new System.Collections.Generic.Dictionary<string, object?> { { "runId", id }, { "count", GetCount(id.Value) } }); } catch { }
    }

    public static void EndOp(Guid? runId = null)
    {
        var id = runId ?? _current.Value;
        if (id == null) return;
        _counts.AddOrUpdate(id.Value, 0, (_, old) => old > 0 ? old - 1 : 0);
        try { _log.Debug("EndOp", new System.Collections.Generic.Dictionary<string, object?> { { "runId", id }, { "count", GetCount(id.Value) } }); } catch { }
    }

    public static int GetCount(Guid runId)
    {
        return _counts.TryGetValue(runId, out var c) ? (c < 0 ? 0 : c) : 0;
    }

    public static void Reset(Guid runId)
    {
        _counts.TryRemove(runId, out _);
    }
}
