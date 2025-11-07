using System;
using System.IO;
using System.Threading.Tasks;

namespace Pw.Hub.Services;

/// <summary>
/// Фоновая очистка "хвостов" профилей WebView2, которые могли остаться после аварийного завершения
/// или из-за удерживаемых дескрипторов. Удаляет папки в %LOCALAPPDATA%/PwHub/WebViewProfiles,
/// которые старше порога возраста. Выполняется best-effort, без исключений наружу.
/// </summary>
public static class ProfileCleanupService
{
    /// <summary>
    /// Запускает асинхронную очистку в фоне. Папки старше <paramref name="olderThan"/> будут удалены.
    /// </summary>
    public static Task CleanupLeftoversAsync(TimeSpan? olderThan = null)
    {
        return Task.Run(async () =>
        {
            try
            {
                var age = olderThan ?? TimeSpan.FromHours(12);
                var root = GetProfilesRoot();
                if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) return;

                foreach (var dir in Directory.EnumerateDirectories(root))
                {
                    try
                    {
                        var info = new DirectoryInfo(dir);
                        var lastWrite = info.LastWriteTimeUtc;
                        var ageNow = DateTime.UtcNow - lastWrite;
                        if (ageNow < age) continue;

                        await TryDeleteDirWithRetries(dir);
                    }
                    catch { }
                }
            }
            catch { }
        });
    }

    private static string GetProfilesRoot()
    {
        try
        {
            var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var dir = Path.Combine(baseDir, "PwHub", "WebViewProfiles");
            Directory.CreateDirectory(dir);
            return dir;
        }
        catch
        {
            try
            {
                var fallback = Path.Combine(Path.GetTempPath(), "PwHub", "WebViewProfiles");
                Directory.CreateDirectory(fallback);
                return fallback;
            }
            catch
            {
                return string.Empty;
            }
        }
    }

    private static async Task<bool> TryDeleteDirWithRetries(string path, int attempts = 10, int delayMs = 250)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        for (int i = 0; i < attempts; i++)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    try
                    {
                        foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                        {
                            try { File.SetAttributes(file, FileAttributes.Normal); } catch { }
                        }
                    }
                    catch { }
                    Directory.Delete(path, true);
                }
                return true;
            }
            catch
            {
                try { await Task.Delay(delayMs); } catch { }
            }
        }
        return false;
    }
}
