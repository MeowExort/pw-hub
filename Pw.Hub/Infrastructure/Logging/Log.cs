using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Pw.Hub.Infrastructure.Logging;

public enum LogLevel
{
    Trace = 0,
    Debug = 1,
    Information = 2,
    Warning = 3,
    Error = 4,
    Critical = 5,
}

public sealed class LogEntry
{
    public DateTime TimestampUtc { get; set; }
    public LogLevel Level { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public int? EventId { get; set; }
    public Dictionary<string, object?> Context { get; set; } = new();
    public string? Exception { get; set; }
    public int ThreadId { get; set; }
}

public sealed class LoggerConfig
{
    public string LogsDirectory { get; set; } = Path.Combine(AppContext.BaseDirectory, "logs");
    public LogLevel MinimumLevel { get; set; } = LogLevel.Information;
    public int MaxQueueSize { get; set; } = 5000;
}

public interface ILogger
{
    void Log(LogLevel level, string message, int? eventId = null, Exception? ex = null, IDictionary<string, object?>? ctx = null);
    void Trace(string message, IDictionary<string, object?>? ctx = null);
    void Debug(string message, IDictionary<string, object?>? ctx = null);
    void Info(string message, IDictionary<string, object?>? ctx = null);
    void Warn(string message, IDictionary<string, object?>? ctx = null);
    void Error(string message, Exception? ex = null, IDictionary<string, object?>? ctx = null);
    void Critical(string message, Exception? ex = null, IDictionary<string, object?>? ctx = null);
}

public static class Log
{
    private static readonly object _initLock = new();
    private static volatile bool _initialized;
    private static LoggerConfig _config = new();
    private static readonly ConcurrentQueue<LogEntry> _queue = new();
    private static int _queueCount;
    private static CancellationTokenSource? _cts;
    private static Task? _worker;
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    private static string _currentFilePath = string.Empty;
    private static DateOnly _currentDate;
    private static readonly object _fileLock = new();

    public static event Action<LogEntry>? EntryPublished; // for live UI feed

    public static void Initialize(LoggerConfig? config)
    {
        if (_initialized) return;
        lock (_initLock)
        {
            if (_initialized) return;
            _config = config ?? new LoggerConfig();
            try { Directory.CreateDirectory(_config.LogsDirectory); } catch { }
            _cts = new CancellationTokenSource();
            _worker = Task.Run(() => WorkerAsync(_cts.Token));
            _initialized = true;
        }
    }

    public static void Shutdown()
    {
        try
        {
            _cts?.Cancel();
            _worker?.Wait(1000);
        }
        catch { }
    }

    public static ILogger For<T>() => new CategoryLogger(typeof(T).FullName ?? typeof(T).Name);
    public static ILogger For(string category) => new CategoryLogger(category);

    private static void Enqueue(LogEntry entry)
    {
        if (!_initialized) Initialize(null);
        // Drop if queue too large (avoid blocking)
        var count = Interlocked.Increment(ref _queueCount);
        if (count > _config.MaxQueueSize)
        {
            Interlocked.Decrement(ref _queueCount);
            return;
        }
        _queue.Enqueue(entry);
        EntryPublished?.Invoke(entry);
    }

    private static async Task WorkerAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (!_queue.TryDequeue(out var entry))
                {
                    await Task.Delay(20, ct);
                    continue;
                }
                Interlocked.Decrement(ref _queueCount);
                WriteEntry(entry);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                // ignore
            }
        }

        // drain on shutdown (best effort)
        while (_queue.TryDequeue(out var entry2))
        {
            try { WriteEntry(entry2); } catch { }
        }
    }

    private static void WriteEntry(LogEntry e)
    {
        // daily rolling
        var date = DateOnly.FromDateTime(DateTime.UtcNow);
        if (date != _currentDate || string.IsNullOrEmpty(_currentFilePath))
        {
            lock (_fileLock)
            {
                _currentDate = date;
                _currentFilePath = Path.Combine(_config.LogsDirectory, $"{date:yyyy-MM-dd}.jsonl");
            }
        }

        var json = JsonSerializer.Serialize(e, _json) + "\n";
        lock (_fileLock)
        {
            File.AppendAllText(_currentFilePath, json);
        }
    }

    private sealed class CategoryLogger(string category) : ILogger
    {
        private readonly string _category = category;
        public void Log(LogLevel level, string message, int? eventId = null, Exception? ex = null, IDictionary<string, object?>? ctx = null)
        {
            if (level < _config.MinimumLevel) return;
            var entry = new LogEntry
            {
                TimestampUtc = DateTime.UtcNow,
                Level = level,
                Category = _category,
                Message = message,
                EventId = eventId,
                Exception = ex?.ToString(),
                ThreadId = Environment.CurrentManagedThreadId,
                Context = ctx != null ? new Dictionary<string, object?>(ctx) : new Dictionary<string, object?>()
            };
            Enqueue(entry);
        }

        public void Trace(string message, IDictionary<string, object?>? ctx = null) => Log(LogLevel.Trace, message, ctx: ctx);
        public void Debug(string message, IDictionary<string, object?>? ctx = null) => Log(LogLevel.Debug, message, ctx: ctx);
        public void Info(string message, IDictionary<string, object?>? ctx = null) => Log(LogLevel.Information, message, ctx: ctx);
        public void Warn(string message, IDictionary<string, object?>? ctx = null) => Log(LogLevel.Warning, message, ctx: ctx);
        public void Error(string message, Exception? ex = null, IDictionary<string, object?>? ctx = null) => Log(LogLevel.Error, message, ex: ex, ctx: ctx);
        public void Critical(string message, Exception? ex = null, IDictionary<string, object?>? ctx = null) => Log(LogLevel.Critical, message, ex: ex, ctx: ctx);
    }
}
