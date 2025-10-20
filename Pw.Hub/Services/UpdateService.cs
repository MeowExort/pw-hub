using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Windows;

namespace Pw.Hub.Services;

public class UpdateManifest
{
    public string Version { get; set; } = "1.0.0";
    public string Url { get; set; } = string.Empty; // direct download URL to installer, archive or new exe
    public string? ReleaseNotes { get; set; }
    public bool Mandatory { get; set; }
}

public class UpdateService
{
    private readonly string _manifestUrl;

    private readonly HttpClient _http = new();

    public UpdateService()
    {
        var baseUrl = Environment.GetEnvironmentVariable("PW_MODULES_API")?.TrimEnd('/') ?? "https://api.pw-hub.ru";
        _manifestUrl = baseUrl + "/api/app/manifest";
    }

    public Version GetCurrentVersion()
    {
        // Try to read informational or file version; fallback to assembly version
        var asm = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var infoAttr = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(infoAttr) && Version.TryParse(NormalizeSemVer(infoAttr), out var iv))
            return iv;

        var fvi = FileVersionInfo.GetVersionInfo(asm.Location);
        if (Version.TryParse(NormalizeSemVer(fvi.ProductVersion ?? fvi.FileVersion), out var fv))
            return fv;

        return asm.GetName().Version ?? new Version(1,0,0,0);
    }

    public async Task CheckForUpdates(bool showNoUpdatesMessage = false)
    {
        UpdateManifest manifest;
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            using var resp = await _http.GetAsync(_manifestUrl, cts.Token);
            resp.EnsureSuccessStatusCode();
            await using var stream = await resp.Content.ReadAsStreamAsync(cts.Token);
            manifest = await JsonSerializer.DeserializeAsync<UpdateManifest>(stream, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }, cts.Token);
        }
        catch (Exception ex)
        {
            if (showNoUpdatesMessage)
            {
                MessageBox.Show($"Не удалось проверить обновления.\n{ex.Message}", "Обновление", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            return;
        }

        if (manifest == null || string.IsNullOrWhiteSpace(manifest.Version))
        {
            if (showNoUpdatesMessage)
                MessageBox.Show("Некорректный манифест обновления", "Обновление", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var current = GetCurrentVersion();
        if (!Version.TryParse(NormalizeSemVer(manifest.Version), out var latest))
        {
            if (showNoUpdatesMessage)
                MessageBox.Show("Не удалось распознать версию в манифесте", "Обновление", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (latest <= current)
        {
            if (showNoUpdatesMessage)
                MessageBox.Show($"У вас установлена последняя версия ({current}).", "Обновление", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var notes = string.IsNullOrWhiteSpace(manifest.ReleaseNotes) ? string.Empty : $"\n\nИзменения:\n{manifest.ReleaseNotes}";
        var msg = $"Доступна новая версия {latest}. Текущая версия: {current}.{notes}\n\nСкачать и установить сейчас?";
        var res = MessageBox.Show(msg, "Доступно обновление", manifest.Mandatory ? MessageBoxButton.OKCancel : MessageBoxButton.YesNo,
            MessageBoxImage.Information);
        var proceed = manifest.Mandatory ? res == MessageBoxResult.OK : res == MessageBoxResult.Yes;
        if (!proceed) return;

        if (string.IsNullOrWhiteSpace(manifest.Url))
        {
            MessageBox.Show("В манифесте не указан URL для загрузки", "Обновление", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "PwHubUpdate");
            Directory.CreateDirectory(tempDir);
            var fileName = Path.GetFileName(new Uri(manifest.Url).LocalPath);
            if (string.IsNullOrWhiteSpace(fileName)) fileName = $"PwHubSetup_{latest}.exe";
            var target = Path.Combine(tempDir, fileName);

            using var response = await _http.GetAsync(manifest.Url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            await using (var fs = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await response.Content.CopyToAsync(fs);
            }

            var ext = Path.GetExtension(target).ToLowerInvariant();
            var appExePath = Process.GetCurrentProcess().MainModule?.FileName ?? Assembly.GetEntryAssembly()?.Location ?? string.Empty;
            var appDir = string.IsNullOrWhiteSpace(appExePath) ? AppContext.BaseDirectory : Path.GetDirectoryName(appExePath)!;

            // If it's a zip archive — extract and prepare copy script
            if (ext == ".zip")
            {
                var extractDir = Path.Combine(tempDir, "extracted");
                if (Directory.Exists(extractDir)) Directory.Delete(extractDir, true);
                ZipFile.ExtractToDirectory(target, extractDir);

                RunUpdaterScriptAndShutdown(extractDir, appDir, appExePath, replaceExeOnly: false);
                return;
            }

            // If it's MSI or typical setup/installer EXE — run it as before
            var lowerName = fileName.ToLowerInvariant();
            var looksLikeInstaller = ext == ".msi" || (ext == ".exe" && (lowerName.Contains("setup") || lowerName.Contains("install") || lowerName.Contains("installer") || lowerName.Contains("setup-")));
            if (looksLikeInstaller)
            {
                var psi = new ProcessStartInfo
                {
                    FileName = target,
                    UseShellExecute = true,
                    Verb = "open"
                };
                Process.Start(psi);
                Application.Current.Dispatcher.Invoke(() => Application.Current.Shutdown());
                return;
            }

            // Otherwise, assume it's a portable EXE update: replace current exe after exit
            RunUpdaterScriptAndShutdown(newPayloadPath: target, appDir: appDir, appExePath: appExePath, replaceExeOnly: true);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Не удалось скачать или установить обновление.\n{ex.Message}", "Обновление", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static void RunUpdaterScriptAndShutdown(string newPayloadPath, string appDir, string appExePath, bool replaceExeOnly)
    {
        try
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "PwHubUpdate");
            Directory.CreateDirectory(tempDir);
            var scriptPath = Path.Combine(tempDir, "update.cmd");
            var procId = Process.GetCurrentProcess().Id;

            var sb = new StringBuilder();
            sb.AppendLine("@echo off");
            sb.AppendLine("setlocal enabledelayedexpansion");
            sb.AppendLine($"set APPDIR=\"{appDir}\"");
            sb.AppendLine($"set APPEXE=\"{appExePath}\"");
            if (replaceExeOnly)
            {
                sb.AppendLine($"set NEWEXE=\"{newPayloadPath}\"");
            }
            else
            {
                sb.AppendLine($"set SRC=\"{newPayloadPath}\"");
            }
            sb.AppendLine("echo Ожидание завершения работы приложения...");
            sb.AppendLine($"powershell -NoProfile -Command \"try {{ $p = Get-Process -Id {procId} -ErrorAction SilentlyContinue; while($p) {{ Start-Sleep -Milliseconds 200; $p = Get-Process -Id {procId} -ErrorAction SilentlyContinue }} }} catch {{}}\"");

            if (replaceExeOnly)
            {
                // Replace only exe
                sb.AppendLine("echo Замена исполняемого файла...");
                sb.AppendLine("copy /Y %NEWEXE% %APPEXE% >nul");
            }
            else
            {
                // Copy all files from extracted folder into app folder
                sb.AppendLine("echo Копирование новых файлов...");
                // Use robocopy to preserve structure and overwrite files
                sb.AppendLine("robocopy %SRC% %APPDIR% /E /R:2 /W:1 /NP /NFL /NDL >nul");
                sb.AppendLine("if %ERRORLEVEL% GEQ 8 ( echo Robocopy failed with code %ERRORLEVEL% & exit /b %ERRORLEVEL% )");
            }

            sb.AppendLine("echo Запуск приложения...");
            sb.AppendLine("start \"\" %APPEXE%");
            sb.AppendLine("exit /b 0");

            File.WriteAllText(scriptPath, sb.ToString(), Encoding.UTF8);

            // Start the updater script and shutdown the app
            var psi = new ProcessStartInfo
            {
                FileName = scriptPath,
                UseShellExecute = true,
                Verb = "open",
                WorkingDirectory = tempDir
            };
            Process.Start(psi);
            Application.Current.Dispatcher.Invoke(() => Application.Current.Shutdown());
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Не удалось подготовить установку обновления.\n{ex.Message}", "Обновление", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static string NormalizeSemVer(string? version)
    {
        if (string.IsNullOrWhiteSpace(version)) return "0.0.0";
        // Remove any metadata like "+build" or commit sha
        var v = version.Split('+')[0].Trim();
        return v;
    }
}
