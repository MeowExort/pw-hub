using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Windows;
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
            var scriptPath = Path.Combine(tempDir, "update.ps1");
            var procId = Process.GetCurrentProcess().Id;

            // PowerShell script content (UTF-8 with BOM) to properly handle Cyrillic paths and UTF-8 output
            var sb = new StringBuilder();
            sb.AppendLine("Param(\n  [Parameter(Mandatory=$true)][string]$NewPayloadPath,\n  [Parameter(Mandatory=$true)][string]$AppDir,\n  [Parameter(Mandatory=$true)][string]$AppExePath,\n  [Parameter(Mandatory=$true)][int]$ProcId,\n  [switch]$ReplaceExeOnly\n)");
            sb.AppendLine("$ErrorActionPreference = 'Stop'");
            sb.AppendLine("try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8; $OutputEncoding = [System.Text.UTF8Encoding]::new($false) } catch {} ");
            sb.AppendLine("$LogDir = Join-Path -Path ([System.IO.Path]::GetTempPath()) -ChildPath 'PwHubUpdate'");
            sb.AppendLine("$LogPath = Join-Path -Path $LogDir -ChildPath 'update.log'");
            sb.AppendLine("New-Item -ItemType Directory -Force -Path $LogDir | Out-Null");
            // Write the very first bootstrap line before any Transcript starts (ensures the file exists)
            sb.AppendLine("try { $ts = Get-Date -Format 'yyyy-MM-dd HH:mm:ss.fff'; Add-Content -LiteralPath $LogPath -Value \"[$ts] Bootstrap: скрипт запущен\" -Encoding UTF8 } catch {} ");
            sb.AppendLine("function Write-Log([string]$msg){ try { $ts = Get-Date -Format 'yyyy-MM-dd HH:mm:ss.fff'; $line = \"[$ts] $msg\"; Write-Host $line; Add-Content -LiteralPath $LogPath -Value $line -Encoding UTF8 } catch {} }");
            sb.AppendLine("try { Start-Transcript -Path $LogPath -Append -Force | Out-Null } catch {} ");
            sb.AppendLine("Write-Log '== Старт обновления =='");
            sb.AppendLine("Write-Log \"Параметры: NewPayloadPath=[$NewPayloadPath], AppDir=[$AppDir], AppExePath=[$AppExePath], ProcId=[$ProcId], ReplaceExeOnly=[$ReplaceExeOnly]\"");
            sb.AppendLine("function Ensure-Elevation {\n  try {\n    $testFile = Join-Path -Path $AppDir -ChildPath ('write_test_' + [guid]::NewGuid().ToString() + '.tmp')\n    Set-Content -LiteralPath $testFile -Value 'test' -Encoding UTF8\n    Remove-Item -LiteralPath $testFile -Force -ErrorAction SilentlyContinue\n    return $true\n  } catch {\n    return $false\n  }\n}");
            sb.AppendLine("if (-not (Ensure-Elevation)) {\n  Write-Log 'Требуются права администратора. Запрашиваем повышение привилегий...'\n  $args = @('-NoProfile','-ExecutionPolicy','Bypass','-File', $PSCommandPath, '-NewPayloadPath', $NewPayloadPath, '-AppDir', $AppDir, '-AppExePath', $AppExePath, '-ProcId', $ProcId.ToString())\n  if ($ReplaceExeOnly) { $args += '-ReplaceExeOnly' }\n  Start-Process -FilePath 'powershell.exe' -Verb RunAs -ArgumentList $args\n  exit 0\n}");
            sb.AppendLine("Write-Log 'Ожидание завершения работы приложения...'");
            sb.AppendLine("try { $p = Get-Process -Id $ProcId -ErrorAction SilentlyContinue; while ($p) { Start-Sleep -Milliseconds 200; $p = Get-Process -Id $ProcId -ErrorAction SilentlyContinue } } catch { Write-Log ('Ошибка ожидания процесса: ' + $_) }");
            sb.AppendLine("Start-Sleep -Milliseconds 200");
            sb.AppendLine("try {");
            sb.AppendLine("  if ($ReplaceExeOnly) {");
            sb.AppendLine("    Write-Log 'Замена исполняемого файла...' ");
            sb.AppendLine("    $attempts = 0; while ($true) { try { Copy-Item -LiteralPath $NewPayloadPath -Destination $AppExePath -Force; Write-Log 'EXE заменен.'; break } catch { $attempts++; Write-Log ('Попытка копирования неудачна: ' + $_.Exception.Message); if ($attempts -ge 10) { throw }; Start-Sleep -Milliseconds 200 } } ");
            sb.AppendLine("  } else {");
            sb.AppendLine("    Write-Log 'Копирование новых файлов...' ");
            sb.AppendLine("    $items = Get-ChildItem -LiteralPath $NewPayloadPath -Force ");
            sb.AppendLine("    foreach ($it in $items) { try { Write-Log (\"Копируем: \" + $it.FullName); Copy-Item -LiteralPath $it.FullName -Destination $AppDir -Recurse -Force } catch { Write-Log ('Ошибка копирования ' + $it.FullName + ': ' + $_.Exception.Message); throw } }");
            sb.AppendLine("  }");
            sb.AppendLine("  try { Unblock-File -LiteralPath $AppExePath } catch { Write-Log ('Unblock-File ошибка: ' + $_.Exception.Message) } ");
            sb.AppendLine("  Write-Log 'Запуск приложения...'");
            sb.AppendLine("  Start-Process -FilePath $AppExePath -WorkingDirectory $AppDir | Out-Null");
            sb.AppendLine("  Write-Log 'Готово. Выход.'");
            sb.AppendLine("  try { Stop-Transcript | Out-Null } catch {} ");
            sb.AppendLine("  exit 0");
            sb.AppendLine("} catch { Write-Log ('ФАТАЛЬНАЯ ОШИБКА: ' + $_.Exception.ToString()); try { Stop-Transcript | Out-Null } catch {}; Write-Host 'Нажмите Enter, чтобы закрыть окно...'; Read-Host | Out-Null; exit 1 }");

            // Write script with BOM to ensure PowerShell reads as UTF-8
            File.WriteAllText(scriptPath, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

            // Pre-launch diagnostic log
            try
            {
                var prelaunch = Path.Combine(tempDir, "prelauncher.log");
                File.AppendAllText(prelaunch, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] Preparing to start PowerShell.\r\nScriptPath={scriptPath}\r\nNewPayloadPath={newPayloadPath}\r\nAppDir={appDir}\r\nAppExePath={appExePath}\r\nReplaceExeOnly={replaceExeOnly}\r\n", Encoding.UTF8);
            }
            catch { }

            // Try launch strategy A: direct powershell.exe with visible window
            string psFullPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32\\WindowsPowerShell\\v1.0\\powershell.exe");
            if (!File.Exists(psFullPath)) psFullPath = "powershell.exe"; // fallback to PATH

            bool started = false;
            Exception lastStartEx = null;
            try
            {
                var psiA = new ProcessStartInfo
                {
                    FileName = psFullPath,
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Normal,
                    WorkingDirectory = tempDir,
                    Verb = "open",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\" -NewPayloadPath \"{newPayloadPath}\" -AppDir \"{appDir}\" -AppExePath \"{appExePath}\" -ProcId {procId}{(replaceExeOnly ? " -ReplaceExeOnly" : string.Empty)}"
                };
                started = Process.Start(psiA) != null;
            }
            catch (Exception ex)
            {
                lastStartEx = ex;
            }

            // Fallback B: run via cmd.exe /k to force a visible console that stays open
            if (!started)
            {
                try
                {
                    var psiB = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        UseShellExecute = true,
                        WindowStyle = ProcessWindowStyle.Normal,
                        WorkingDirectory = tempDir,
                        Arguments = $"/c \"\"{psFullPath}\" -NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\" -NewPayloadPath \"{newPayloadPath}\" -AppDir \"{appDir}\" -AppExePath \"{appExePath}\" -ProcId {procId}{(replaceExeOnly ? " -ReplaceExeOnly" : string.Empty)}\""
                    };
                    started = Process.Start(psiB) != null;
                }
                catch (Exception ex)
                {
                    lastStartEx = ex;
                }
            }

            if (!started)
            {
                ShowMessage($"Не удалось запустить окно обновления PowerShell.\\n{lastStartEx?.Message}", "Обновление", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Shutdown the app to allow files to be replaced
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
