using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Pw.Hub.Services;

public class JsonAppConfigService : IAppConfigService
{
    private readonly string _filePath;
    private readonly object _lock = new();
    private Dictionary<string, string> _store = new();
    private int _dirty;

    public JsonAppConfigService()
    {
        try
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var dir = Path.Combine(appData, "PwHub");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            _filePath = Path.Combine(dir, "config.json");
            Load();
        }
        catch
        {
            _filePath = Path.GetFullPath("config.json");
            _store = new();
        }
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_filePath)) { _store = new(); return; }
            var json = File.ReadAllText(_filePath);
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    // prop.Value хранит уже JSON-значение; сохраним raw
                    dict[prop.Name] = prop.Value.GetRawText();
                }
                _store = dict;
            }
            else
            {
                _store = new();
            }
        }
        catch
        {
            // Бэкап повреждённого файла
            try
            {
                var bak = _filePath + ".bak";
                File.Copy(_filePath, bak, overwrite: true);
            }
            catch { }
            _store = new();
        }
    }

    public bool TryGetString(string key, out string? value)
    {
        lock (_lock)
        {
            if (_store.TryGetValue(key, out var v)) { value = v; return true; }
        }
        value = null; return false;
    }

    public void SetString(string key, string? value)
    {
        lock (_lock)
        {
            if (value == null)
            {
                _store[key] = "null";
            }
            else
            {
                _store[key] = value;
            }
            Interlocked.Exchange(ref _dirty, 1);
        }
    }

    public async Task SaveAsync()
    {
        if (Interlocked.Exchange(ref _dirty, 0) == 0) return;
        Dictionary<string, string> snapshot;
        lock (_lock) snapshot = new Dictionary<string, string>(_store);
        try
        {
            // Сериализуем как объект, где значения уже являются JSON-строками -> вставим raw
            // Сконструируем вручную
            using var sw = new StreamWriter(_filePath, false);
            await sw.WriteAsync("{");
            var first = true;
            foreach (var kv in snapshot)
            {
                if (!first) await sw.WriteAsync(",");
                first = false;
                await sw.WriteAsync(JsonSerializer.Serialize(kv.Key));
                await sw.WriteAsync(":");
                await sw.WriteAsync(string.IsNullOrWhiteSpace(kv.Value) ? "null" : kv.Value);
            }
            await sw.WriteAsync("}");
        }
        catch
        {
            // ignore IO errors
        }
    }
}
