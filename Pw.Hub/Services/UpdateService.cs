using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Linq;
using Pw.Hub.Windows;

namespace Pw.Hub.Services;

public class UpdateManifest
{
    public string Version { get; set; } = "1.0.0";
    public string Url { get; set; } = string.Empty; // direct download URL to installer, archive or new exe
    public string ReleaseNotes { get; set; }
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
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
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
                ShowMessage($"Не удалось проверить обновления.\n{ex.Message}", "Обновление", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            return;
        }

        if (manifest == null || string.IsNullOrWhiteSpace(manifest.Version))
        {
            if (showNoUpdatesMessage)
                ShowMessage("Некорректный манифест обновления", "Обновление", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var current = GetCurrentVersion();
        if (!Version.TryParse(NormalizeSemVer(manifest.Version), out var latest))
        {
            if (showNoUpdatesMessage)
                ShowMessage("Не удалось распознать версию в манифесте", "Обновление", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (latest <= current)
        {
            if (showNoUpdatesMessage)
                ShowMessage($"У вас установлена последняя версия ({current}).", "Обновление", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var notes = string.IsNullOrWhiteSpace(manifest.ReleaseNotes) ? string.Empty : $"\n\nИзменения:\n{manifest.ReleaseNotes}";
        var msg = $"Доступна новая версия {latest}. Текущая версия: {current}.{notes}\n\nСкачать и установить сейчас?";
        var res = ShowMessage(msg, "Доступно обновление", manifest.Mandatory ? MessageBoxButton.OKCancel : MessageBoxButton.YesNo,
            MessageBoxImage.Information);
        var proceed = manifest.Mandatory ? res == MessageBoxResult.OK : res == MessageBoxResult.Yes;
        if (!proceed) return;

        if (string.IsNullOrWhiteSpace(manifest.Url))
        {
            ShowMessage("В манифесте не указан URL для загрузки", "Обновление", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Show modal-like progress dialog
        Window owner = null;
        UpdateProgressWindow progress = null;
        Application.Current?.Dispatcher.Invoke(() =>
        {
            owner = Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive && w.IsVisible)
                    ?? Application.Current?.MainWindow;
            progress = new UpdateProgressWindow();
            if (owner != null)
            {
                progress.Owner = owner;
                owner.IsEnabled = false; // emulate modality
            }
            progress.SetTitle("Загрузка обновления...");
            progress.SetStatus("Подключение...");
            progress.Show();
        });

        void CloseProgress()
        {
            try
            {
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    progress?.CloseSafe();
                    if (owner != null) owner.IsEnabled = true;
                });
            }
            catch { }
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

            var contentLength = response.Content.Headers.ContentLength;
            if (contentLength.HasValue && contentLength.Value > 0)
                progress?.SetIndeterminate(false);
            else
                progress?.SetIndeterminate(true);

            await using (var input = await response.Content.ReadAsStreamAsync())
            await using (var fs = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                var buffer = new byte[81920];
                long totalRead = 0;
                var sw = Stopwatch.StartNew();
                int read;
                while ((read = await input.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await fs.WriteAsync(buffer, 0, read);
                    totalRead += read;

                    if (contentLength.HasValue && contentLength.Value > 0)
                    {
                        var percent = (double)totalRead / contentLength.Value * 100.0;
                        progress?.SetProgress(percent);
                        if (sw.ElapsedMilliseconds > 250)
                        {
                            var doneMb = totalRead / 1024d / 1024d;
                            var totalMb = contentLength.Value / 1024d / 1024d;
                            progress?.SetStatus($"Загрузка... {Math.Round(percent)}% ({doneMb:0.#} / {totalMb:0.#} МБ)");
                            sw.Restart();
                        }
                    }
                    else
                    {
                        if (sw.ElapsedMilliseconds > 300)
                        {
                            var doneMb = totalRead / 1024d / 1024d;
                            progress?.SetStatus($"Загрузка... {doneMb:0.#} МБ");
                            sw.Restart();
                        }
                    }
                }
            }

            var ext = Path.GetExtension(target).ToLowerInvariant();
            var appExePath = Process.GetCurrentProcess().MainModule?.FileName ?? Assembly.GetEntryAssembly()?.Location ?? string.Empty;
            var appDir = string.IsNullOrWhiteSpace(appExePath) ? AppContext.BaseDirectory : Path.GetDirectoryName(appExePath)!;

            // If it's a zip archive — extract and prepare copy script
            if (ext == ".zip")
            {
                progress?.SetTitle("Установка обновления...");
                progress?.SetStatus("Распаковка файлов...");
                progress?.SetIndeterminate(true);
                var extractDir = Path.Combine(tempDir, "extracted");
                if (Directory.Exists(extractDir)) Directory.Delete(extractDir, true);
                ZipFile.ExtractToDirectory(target, extractDir);

                progress?.SetStatus("Подготовка к замене файлов...");
                CloseProgress();
                RunUpdaterScriptAndShutdown(extractDir, appDir, appExePath, replaceExeOnly: false);
                return;
            }

            // If it's MSI or typical setup/installer EXE — run it as before
            var lowerName = fileName.ToLowerInvariant();
            var looksLikeInstaller = ext == ".msi" || (ext == ".exe" && (lowerName.Contains("setup") || lowerName.Contains("install") || lowerName.Contains("installer") || lowerName.Contains("setup-")));
            if (looksLikeInstaller)
            {
                progress?.SetTitle("Установка обновления...");
                progress?.SetStatus("Запуск установщика...");
                CloseProgress();
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
            progress?.SetTitle("Установка обновления...");
            progress?.SetStatus("Подготовка установки...");
            CloseProgress();
            RunUpdaterScriptAndShutdown(newPayloadPath: target, appDir: appDir, appExePath: appExePath, replaceExeOnly: true);
        }
        catch (Exception ex)
        {
            CloseProgress();
            ShowMessage($"Не удалось скачать или установить обновление.\n{ex.Message}", "Обновление", MessageBoxButton.OK, MessageBoxImage.Error);
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
            sb.AppendLine("start \"\" /D %APPDIR% %APPEXE%");
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
            ShowMessage($"Не удалось подготовить установку обновления.\n{ex.Message}", "Обновление", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static MessageBoxResult ShowMessage(string text, string caption, MessageBoxButton buttons, MessageBoxImage icon)
    {
        try
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher != null)
            {
                if (dispatcher.CheckAccess())
                    return ShowMessageCore(text, caption, buttons, icon);
                return dispatcher.Invoke(() => ShowMessageCore(text, caption, buttons, icon));
            }
        }
        catch { }
        return MessageBox.Show(text, caption, buttons, icon);
    }

    private static MessageBoxResult ShowMessageCore(string text, string caption, MessageBoxButton buttons, MessageBoxImage icon)
    {
        // Prefer an active/visible window as owner to ensure the message box appears on top
        var owner = Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive && w.IsVisible)
                    ?? Application.Current?.MainWindow;
        if (owner != null && owner.IsVisible)
            return MessageBox.Show(owner, text, caption, buttons, icon);

        // When no visible owner window exists (e.g., during startup), show on default desktop to avoid being suppressed
        return MessageBox.Show(text, caption, buttons, icon, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
    }

    private static string NormalizeSemVer(string version)
    {
        if (string.IsNullOrWhiteSpace(version)) return "0.0.0";
        // Remove any metadata like "+build" or commit sha
        var v = version.Split('+')[0].Trim();
        return v;
    }
}
